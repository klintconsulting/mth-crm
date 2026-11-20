using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Keeps the computed N:N mth_lead_mth_leadqueue up to date.
    /// - Create/Update of mth_lead (async): evaluates the lead against all active queues; members of queues the
    ///   lead newly enters get an in-app notification (if the queue has "Notify members" on).
    /// - Create/Update of mth_leadqueue (async): re-evaluates all open leads against that queue (no notifications).
    /// Only open leads are placed in queues; a converted/disqualified lead keeps its memberships as history but is
    /// filtered out of the queue views by lead status.
    /// </summary>
    public class LeadQueueSync : IPlugin
    {
        public const string Relationship = "mth_lead_mth_leadqueue";
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
            if (!IsOpen(lead)) { trace.Trace("Lead not open - queues unchanged"); return; }
            var facts = ToFacts(lead);
            var queues = ActiveQueues(service);
            var current = new HashSet<Guid>(Memberships(service, "mth_leadid", leadId).Select(m => m.Value));
            var matching = queues.Where(q => QueueMatcher.Matches(facts, ToCriteria(q), today)).ToList();

            var add = matching.Where(q => !current.Contains(q.Id)).ToList();
            var remove = current.Where(qid => !matching.Any(q => q.Id == qid)).ToList();
            if (add.Count > 0) SafeLink(service, true, "mth_lead", leadId, add.Select(q => q.ToEntityReference()).ToList());
            if (remove.Count > 0) SafeLink(service, false, "mth_lead", leadId, remove.Select(q => new EntityReference("mth_leadqueue", q)).ToList());
            trace.Trace("Lead {0}: +{1} -{2} queue(s)", leadId, add.Count, remove.Count);

            foreach (var q in add.Where(q => q.GetAttributeValue<bool>("mth_notifymembers")))
                NotifyMembers(service, q, lead, isNew);
        }

        private static void SyncQueue(IOrganizationService service, ITracingService trace, Guid queueId, DateTime today)
        {
            var queue = service.Retrieve("mth_leadqueue", queueId, new ColumnSet(QueueColumns));
            var active = queue.GetAttributeValue<bool>("mth_isactive");
            var criteria = ToCriteria(queue);
            var current = new HashSet<Guid>(Memberships(service, "mth_leadqueueid", queueId).Select(m => m.Key));

            var add = new List<EntityReference>(); var remove = new List<EntityReference>();
            foreach (var lead in OpenLeads(service))
            {
                var match = active && QueueMatcher.Matches(ToFacts(lead), criteria, today);
                if (match && !current.Contains(lead.Id)) add.Add(lead.ToEntityReference());
                if (!match && current.Contains(lead.Id)) remove.Add(lead.ToEntityReference());
            }
            foreach (var chunk in Chunks(add, 200)) SafeLink(service, true, "mth_leadqueue", queueId, chunk);
            foreach (var chunk in Chunks(remove, 200)) SafeLink(service, false, "mth_leadqueue", queueId, chunk);
            trace.Trace("Queue {0}: +{1} -{2} lead(s)", queueId, add.Count, remove.Count);
        }

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

        /// <summary>Pairs (leadId, queueId) from the intersect table filtered by one side.</summary>
        private static List<KeyValuePair<Guid, Guid>> Memberships(IOrganizationService service, string column, Guid id)
        {
            var q = new QueryExpression(Relationship) { ColumnSet = new ColumnSet("mth_leadid", "mth_leadqueueid") };
            q.Criteria.AddCondition(column, ConditionOperator.Equal, id);
            return service.RetrieveMultiple(q).Entities
                .Select(e => new KeyValuePair<Guid, Guid>(e.GetAttributeValue<Guid>("mth_leadid"), e.GetAttributeValue<Guid>("mth_leadqueueid"))).ToList();
        }

        /// <summary>
        /// Associate/disassociate that tolerates races: the lead job and the queue job can run at the same time and
        /// link the same pair. On a duplicate (or already-removed) link the batch is retried one by one, ignoring those.
        /// </summary>
        private static void SafeLink(IOrganizationService service, bool associate, string entity, Guid id, List<EntityReference> targets)
        {
            var rel = new Relationship(Relationship);
            try
            {
                if (associate) service.Associate(entity, id, rel, new EntityReferenceCollection(targets));
                else service.Disassociate(entity, id, rel, new EntityReferenceCollection(targets));
            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault>) when (targets.Count > 1)
            {
                foreach (var t in targets) SafeLink(service, associate, entity, id, new List<EntityReference> { t });
            }
            catch (System.ServiceModel.FaultException<OrganizationServiceFault> ex) when (IsDuplicateOrMissing(ex))
            {
                // already linked / already unlinked by the concurrent job - nothing to do
            }
        }

        private static bool IsDuplicateOrMissing(System.ServiceModel.FaultException<OrganizationServiceFault> ex) =>
            ex.Message.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 || ex.Detail?.ErrorCode == -2147220937 /* already exists */
            || ex.Message.IndexOf("does not exist", StringComparison.OrdinalIgnoreCase) >= 0;

        private static IEnumerable<List<EntityReference>> Chunks(List<EntityReference> list, int size)
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
