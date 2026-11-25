using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Rejects a lead in lead queues ("parallel queues": the lead stays in every other queue, so another department
    /// can still convert it). Sets the lead assignment's status to Rejected; LeadQueueSync never changes a rejected
    /// assignment, so the queue does not get the lead back. When the lead has no assignment with status New left,
    /// it is closed as Disqualified. Only members of a queue can reject in that queue.
    ///
    /// Two Custom APIs use this plugin:
    ///  - mth_RejectLeadAssignment: AssignmentId (Guid), Reason (String, optional) — one specific queue.
    ///  - mth_RejectLead:           LeadId (Guid), Reason (String, optional) — all of the caller's own queues.
    /// Outputs: Message (String), RemainingQueues (Integer)
    /// </summary>
    public class RejectLead : IPlugin
    {
        private const int LeadOpen = 273280000;
        private const int LeadDisqualified = 273280002;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var system = ((IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory))).CreateOrganizationService(null);
            var caller = context.InitiatingUserId;
            var reason = context.InputParameters.Contains("Reason") ? context.InputParameters["Reason"] as string : null;
            var mine = new HashSet<Guid>(CallerQueues(system, caller));

            Guid leadId;
            List<Entity> reject;
            if (context.InputParameters.Contains("AssignmentId"))
            {
                var a = system.Retrieve(LeadQueueSync.AssignmentTable, (Guid)context.InputParameters["AssignmentId"], new ColumnSet("mth_name", "mth_leadid", "mth_leadqueueid", "mth_status"));
                leadId = a.GetAttributeValue<EntityReference>("mth_leadid").Id;
                if (!LeadQueueSync.IsNew(a)) throw new InvalidPluginExecutionException("Leadet er allerede " + Status(a) + " i køen " + a.GetAttributeValue<string>("mth_name") + ".");
                if (a.GetAttributeValue<EntityReference>("mth_leadqueueid") == null || !mine.Contains(LeadQueueSync.QueueId(a)))
                    throw new InvalidPluginExecutionException("Du er ikke medlem af køen " + a.GetAttributeValue<string>("mth_name") + ". Kun køens medlemmer kan afvise leadet i den.");
                reject = new List<Entity> { a };
            }
            else
            {
                leadId = context.InputParameters.Contains("LeadId") ? (Guid)context.InputParameters["LeadId"] : Guid.Empty;
                if (leadId == Guid.Empty) throw new InvalidPluginExecutionException("LeadId mangler.");
                reject = LeadQueueSync.Assignments(system, "mth_leadid", leadId).Where(a => LeadQueueSync.IsNew(a) && mine.Contains(LeadQueueSync.QueueId(a))).ToList();
                if (reject.Count == 0)
                    throw new InvalidPluginExecutionException("Leadet ligger ikke i nogen af dine køer. Kun medlemmer af en kø kan afvise leadet i den kø.");
            }

            var lead = system.Retrieve("mth_lead", leadId, new ColumnSet("mth_leadstatus"));
            var status = lead.GetAttributeValue<OptionSetValue>("mth_leadstatus");
            if (status != null && status.Value != LeadOpen) throw new InvalidPluginExecutionException("Leadet er ikke længere åbent.");

            foreach (var a in reject)
            {
                var u = new Entity(LeadQueueSync.AssignmentTable, a.Id);
                u["mth_status"] = new OptionSetValue(LeadQueueSync.StatusRejected);
                u["mth_statuschangedon"] = DateTime.UtcNow;
                u["mth_statuschangedbyid"] = new EntityReference("systemuser", caller);
                if (!string.IsNullOrWhiteSpace(reason)) u["mth_reason"] = reason.Length > 2000 ? reason.Substring(0, 2000) : reason;
                system.Update(u);
            }

            var remaining = LeadQueueSync.Assignments(system, "mth_leadid", leadId).Count(a => LeadQueueSync.IsNew(a) && !reject.Any(r => r.Id == a.Id));
            var names = string.Join(", ", reject.Select(a => a.GetAttributeValue<string>("mth_name"))).TrimEnd('.');   // queue names can end in "mio."
            string message;
            if (remaining == 0)
            {
                var close = new Entity("mth_lead", leadId);
                close["mth_leadstatus"] = new OptionSetValue(LeadDisqualified);
                close["statecode"] = new OptionSetValue(1);
                close["statuscode"] = new OptionSetValue(2);
                system.Update(close);
                message = "Leadet er afvist i " + names + ". Ingen andre køer har det, så det er lukket som afvist.";
            }
            else message = "Leadet er afvist i " + names + ". Det ligger stadig i " + remaining + (remaining == 1 ? " anden kø." : " andre køer.");
            trace.Trace("Lead {0} rejected by {1} in {2} queue(s), {3} remaining", leadId, caller, reject.Count, remaining);

            context.OutputParameters["Message"] = message;
            context.OutputParameters["RemainingQueues"] = remaining;
        }

        private static string Status(Entity a)
        {
            switch (a.GetAttributeValue<OptionSetValue>("mth_status")?.Value)
            {
                case LeadQueueSync.StatusRejected: return "afvist";
                case LeadQueueSync.StatusConverted: return "konverteret";
                case LeadQueueSync.StatusLost: return "tabt til en anden kø";
                default: return "behandlet";
            }
        }

        private static IEnumerable<Guid> CallerQueues(IOrganizationService service, Guid userId)
        {
            var q = new QueryExpression("mth_leadqueue_systemuser") { ColumnSet = new ColumnSet("mth_leadqueueid") };
            q.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            return service.RetrieveMultiple(q).Entities.Select(e => e.GetAttributeValue<Guid>("mth_leadqueueid"));
        }
    }
}
