using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Keeps the lead assignments (mth_leadassignment: one row per lead and queue, with the queue's own status) up to date.
    /// - Create/Update of mth_lead (async): evaluates the lead against all active queues. A matching queue without an
    ///   assignment gets one with status New, and its members are notified (if the queue has "Notify members" on).
    /// - Create/Update of mth_leadqueue (async): re-evaluates all open leads against that queue (no notifications).
    /// Only assignments with status New are added or removed. Rejected, Converted and Lost are decisions and are never
    /// touched, so a queue that rejected a lead never gets it back. Closed leads are left as they are.
    /// </summary>
    public class LeadQueueSync : IPlugin
    {
        public const string AssignmentTable = "mth_leadassignment";
        public const int StatusNew = 273280000, StatusRejected = 273280001, StatusConverted = 273280002, StatusLost = 273280003;
        private const int LeadStatusOpen = 273280000;
        private const int IconInfo = 100000000;
        private const int ToastTimed = 200000000;

        private static readonly string[] QueueColumns =
        {
            "mth_name", "mth_isactive", "mth_notifymembers", "mth_regions", "mth_minvalue", "mth_maxvalue", "mth_categorykeywords",
            "mth_stagekeywords", "mth_contracttypekeywords", "mth_textkeywords", "mth_excludekeywords", "mth_startwithinmonths", "mth_excludestarted"
        };

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var service = ((IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
            if (context.Depth > 2) return;

            var id = context.PrimaryEntityId;
            var today = DateTime.Today;
            if (context.PrimaryEntityName == "mth_lead") SyncLead(service, trace, id, today, context.MessageName == "Create");
            else if (context.PrimaryEntityName == "mth_leadqueue") SyncQueue(service, trace, id, today);
        }

        private static void SyncLead(IOrganizationService service, ITracingService trace, Guid leadId, DateTime today, bool isNew)
        {
            var lead = service.Retrieve("mth_lead", leadId, new ColumnSet(ProjectFields.QueueMatchColumns));
            if (!IsOpen(lead)) { trace.Trace("Lead not open - assignments unchanged"); return; }
            var facts = ToFacts(lead);
            var matching = ActiveQueues(service).Where(q => QueueMatcher.Matches(facts, ToCriteria(q), today)).ToList();
            var existing = Assignments(service, "mth_leadid", leadId).GroupBy(QueueId).ToDictionary(g => g.Key, g => g.First());

            var added = new List<Entity>();
            foreach (var q in matching.Where(q => !existing.ContainsKey(q.Id)))
            {
                // The lead job and a queue job can run at the same time; the alternate key (lead + queue) rejects the
                // second insert, which is fine: the assignment exists.
                try { service.Create(NewAssignment(leadId, q.Id, q.GetAttributeValue<string>("mth_name"))); added.Add(q); }
                catch (System.ServiceModel.FaultException<OrganizationServiceFault> ex) when (IsDuplicate(ex)) { }
            }
            var removed = existing.Values.Where(a => IsNew(a) && !matching.Any(q => q.Id == QueueId(a))).ToList();
            foreach (var a in removed) service.Delete(AssignmentTable, a.Id);
            trace.Trace("Lead {0}: +{1} -{2} assignment(s)", leadId, added.Count, removed.Count);

            foreach (var q in added.Where(q => q.GetAttributeValue<bool>("mth_notifymembers")))
                NotifyMembers(service, q, lead, isNew);
        }

        private static void SyncQueue(IOrganizationService service, ITracingService trace, Guid queueId, DateTime today)
        {
            var queue = service.Retrieve("mth_leadqueue", queueId, new ColumnSet(QueueColumns));
            var active = queue.GetAttributeValue<bool>("mth_isactive") && queue.GetAttributeValue<OptionSetValue>("statecode")?.Value != 1;
            var criteria = ToCriteria(queue);
            var name = queue.GetAttributeValue<string>("mth_name");
            var existing = Assignments(service, "mth_leadqueueid", queueId).GroupBy(a => a.GetAttributeValue<EntityReference>("mth_leadid").Id).ToDictionary(g => g.Key, g => g.First());

            var requests = new List<OrganizationRequest>();
            int added = 0, removed = 0;
            foreach (var lead in OpenLeads(service))
            {
                var match = active && QueueMatcher.Matches(ToFacts(lead), criteria, today);
                Entity a;
                var has = existing.TryGetValue(lead.Id, out a);
                if (match && !has) { requests.Add(new CreateRequest { Target = NewAssignment(lead.Id, queueId, name) }); added++; }
                if (!match && has && IsNew(a)) { requests.Add(new DeleteRequest { Target = a.ToEntityReference() }); removed++; }
            }
            foreach (var chunk in Chunks(requests, 200))
            {
                var batch = new ExecuteMultipleRequest { Settings = new ExecuteMultipleSettings { ContinueOnError = true, ReturnResponses = false }, Requests = new OrganizationRequestCollection() };
                batch.Requests.AddRange(chunk);
                service.Execute(batch);
            }
            trace.Trace("Queue {0}: +{1} -{2} assignment(s)", queueId, added, removed);
        }

        public static Entity NewAssignment(Guid leadId, Guid queueId, string queueName)
        {
            var a = new Entity(AssignmentTable);
            a["mth_name"] = queueName;
            a["mth_leadid"] = new EntityReference("mth_lead", leadId);
            a["mth_leadqueueid"] = new EntityReference("mth_leadqueue", queueId);
            a["mth_status"] = new OptionSetValue(StatusNew);
            a["mth_statuschangedon"] = DateTime.UtcNow;
            return a;
        }

        /// <summary>Assignments filtered by one side ("mth_leadid" or "mth_leadqueueid"), all statuses.</summary>
        public static List<Entity> Assignments(IOrganizationService service, string column, Guid id)
        {
            var q = new QueryExpression(AssignmentTable) { ColumnSet = new ColumnSet("mth_name", "mth_leadid", "mth_leadqueueid", "mth_status"), PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 } };
            q.Criteria.AddCondition(column, ConditionOperator.Equal, id);
            q.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            return service.RetrieveMultiple(q).Entities.Where(a => a.GetAttributeValue<EntityReference>("mth_leadqueueid") != null).ToList();
        }

        public static Guid QueueId(Entity a) => a.GetAttributeValue<EntityReference>("mth_leadqueueid").Id;

        private static bool IsDuplicate(System.ServiceModel.FaultException<OrganizationServiceFault> ex) =>
            ex.Detail?.ErrorCode == -2147088238 /* duplicate key */ || ex.Message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0;
        public static bool IsNew(Entity a) => a.GetAttributeValue<OptionSetValue>("mth_status")?.Value == StatusNew;

        private static void NotifyMembers(IOrganizationService service, Entity queue, Entity lead, bool isNew)
        {
            var q = new QueryExpression("mth_leadqueue_systemuser") { ColumnSet = new ColumnSet("systemuserid") };
            q.Criteria.AddCondition("mth_leadqueueid", ConditionOperator.Equal, queue.Id);
            var value = lead.GetAttributeValue<decimal?>("mth_projectvalue");
            var body = string.Format("{0}{1}{2}",
                lead.GetAttributeValue<string>("mth_name"),
                string.IsNullOrEmpty(lead.GetAttributeValue<string>("mth_projectregion")) ? "" : " · " + lead.GetAttributeValue<string>("mth_projectregion"),
                value.HasValue ? " · " + value.Value.ToString("0.##") + " mio. kr." : "");
            var data = "{\"actions\":[{\"title\":\"Åbn\",\"data\":{\"url\":\"?pagetype=entityrecord&etn=mth_lead&id=" + lead.Id + "\"}}]}";
            foreach (var m in service.RetrieveMultiple(q).Entities)
            {
                var n = new Entity("appnotification");
                n["title"] = (isNew ? "Nyt lead i køen " : "Lead kom i køen ") + queue.GetAttributeValue<string>("mth_name");
                n["body"] = body;
                n["ownerid"] = new EntityReference("systemuser", m.GetAttributeValue<Guid>("systemuserid"));
                n["icontype"] = new OptionSetValue(IconInfo);
                n["toasttype"] = new OptionSetValue(ToastTimed);
                n["data"] = data;
                service.Create(n);
            }
        }

        // ---------- helpers ----------
        private static bool IsOpen(Entity lead)
        {
            var s = lead.GetAttributeValue<OptionSetValue>("mth_leadstatus");
            return s == null || s.Value == LeadStatusOpen;
        }

        private static List<Entity> ActiveQueues(IOrganizationService service)
        {
            var q = new QueryExpression("mth_leadqueue") { ColumnSet = new ColumnSet(QueueColumns) };
            q.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            q.Criteria.AddCondition("mth_isactive", ConditionOperator.Equal, true);
            return service.RetrieveMultiple(q).Entities.ToList();
        }

        private static IEnumerable<Entity> OpenLeads(IOrganizationService service)
        {
            var q = new QueryExpression("mth_lead") { ColumnSet = new ColumnSet(ProjectFields.QueueMatchColumns), PageInfo = new PagingInfo { Count = 5000, PageNumber = 1 } };
            q.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            var f = new FilterExpression(LogicalOperator.Or);
            f.AddCondition("mth_leadstatus", ConditionOperator.Equal, LeadStatusOpen);
            f.AddCondition("mth_leadstatus", ConditionOperator.Null);
            q.Criteria.AddFilter(f);
            while (true)
            {
                var page = service.RetrieveMultiple(q);
                foreach (var e in page.Entities) yield return e;
                if (!page.MoreRecords) yield break;
                q.PageInfo.PageNumber++;
                q.PageInfo.PagingCookie = page.PagingCookie;
            }
        }

        private static IEnumerable<List<T>> Chunks<T>(List<T> list, int size)
        {
            for (var i = 0; i < list.Count; i += size) yield return list.Skip(i).Take(size).ToList();
        }

        public static LeadFacts ToFacts(Entity e) => new LeadFacts
        {
            Name = e.GetAttributeValue<string>("mth_name"),
            Title = e.GetAttributeValue<string>("mth_projecttitle"),
            Texts = e.GetAttributeValue<string>("mth_projecttexts"),
            Region = e.GetAttributeValue<string>("mth_projectregion"),
            Value = e.GetAttributeValue<decimal?>("mth_projectvalue"),
            Categories = e.GetAttributeValue<string>("mth_projectcategories"),
            MainCategory = e.GetAttributeValue<string>("mth_maincategory"),
            MainDevType = e.GetAttributeValue<string>("mth_maindevtype"),
            PlanningStage = e.GetAttributeValue<string>("mth_planningstage"),
            ProjectStage = e.GetAttributeValue<string>("mth_projectstage"),
            ContractType = e.GetAttributeValue<string>("mth_smartcontracttype"),
            StartDate = e.GetAttributeValue<DateTime?>("mth_projectstartdate")
        };

        public static QueueCriteria ToCriteria(Entity q)
        {
            var regions = new List<string>();
            var multi = q.GetAttributeValue<OptionSetValueCollection>("mth_regions");
            if (multi != null && q.FormattedValues.Contains("mth_regions"))
                regions.AddRange(q.FormattedValues["mth_regions"].Split(';').Select(s => s.Trim()).Where(s => s.Length > 0));
            return new QueueCriteria
            {
                Regions = regions,
                MinValue = q.GetAttributeValue<decimal?>("mth_minvalue"),
                MaxValue = q.GetAttributeValue<decimal?>("mth_maxvalue"),
                CategoryKeywords = q.GetAttributeValue<string>("mth_categorykeywords"),
                StageKeywords = q.GetAttributeValue<string>("mth_stagekeywords"),
                ContractTypeKeywords = q.GetAttributeValue<string>("mth_contracttypekeywords"),
                TextKeywords = q.GetAttributeValue<string>("mth_textkeywords"),
                ExcludeKeywords = q.GetAttributeValue<string>("mth_excludekeywords"),
                StartWithinMonths = q.GetAttributeValue<int?>("mth_startwithinmonths"),
                ExcludeStarted = q.GetAttributeValue<bool>("mth_excludestarted")
            };
        }
    }
}
