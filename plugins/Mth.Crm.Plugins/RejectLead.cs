using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Custom API mth_RejectLead (action): the caller rejects an open lead in THEIR OWN lead queues only
    /// (the queues the lead is in where the caller is a member). The lead stays in all other queues, so another
    /// department can still convert it ("parallel queues"). For each rejecting queue a mth_leadrejection row is
    /// written (owned by the caller) and the queue link is removed; LeadQueueSync never puts the lead back in a queue
    /// that rejected it. When no queue is left, the lead is closed as Disqualified.
    ///
    /// Inputs : LeadId (Guid), Reason (String, optional)
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

            var leadId = context.InputParameters.Contains("LeadId") ? (Guid)context.InputParameters["LeadId"] : Guid.Empty;
            if (leadId == Guid.Empty) throw new InvalidPluginExecutionException("LeadId mangler.");
            var reason = context.InputParameters.Contains("Reason") ? context.InputParameters["Reason"] as string : null;
            var caller = context.InitiatingUserId;

            var lead = system.Retrieve("mth_lead", leadId, new ColumnSet("mth_name", "mth_leadstatus"));
            var status = lead.GetAttributeValue<OptionSetValue>("mth_leadstatus");
            if (status != null && status.Value != LeadOpen) throw new InvalidPluginExecutionException("Leadet er ikke længere åbent.");

            var leadQueues = LeadQueues(system, leadId);
            var mine = new HashSet<Guid>(CallerQueues(system, caller));
            var reject = leadQueues.Where(q => mine.Contains(q.Id)).ToList();
            if (reject.Count == 0)
                throw new InvalidPluginExecutionException("Leadet ligger ikke i nogen af dine køer. Kun medlemmer af en kø kan afvise leadet i den kø.");

            foreach (var q in reject)
            {
                var r = new Entity("mth_leadrejection");
                r["mth_name"] = q.GetAttributeValue<string>("mth_name");
                r["mth_leadid"] = new EntityReference("mth_lead", leadId);
                r["mth_leadqueueid"] = q.ToEntityReference();
                if (!string.IsNullOrWhiteSpace(reason)) r["mth_reason"] = reason.Length > 2000 ? reason.Substring(0, 2000) : reason;
                r["ownerid"] = new EntityReference("systemuser", caller);
                system.Create(r);
            }
            system.Disassociate("mth_lead", leadId, new Relationship(LeadQueueSync.Relationship),
                new EntityReferenceCollection(reject.Select(q => q.ToEntityReference()).ToList()));

            var remaining = leadQueues.Count - reject.Count;
            var names = string.Join(", ", reject.Select(q => q.GetAttributeValue<string>("mth_name")));
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
            else message = "Leadet er afvist i " + names + ". Det ligger stadig i " + remaining + " anden/andre kø(er).";
            trace.Trace("Lead {0} rejected by {1} in {2} queue(s), {3} remaining", leadId, caller, reject.Count, remaining);

            context.OutputParameters["Message"] = message;
            context.OutputParameters["RemainingQueues"] = remaining;
        }

        private static List<Entity> LeadQueues(IOrganizationService service, Guid leadId)
        {
            var q = new QueryExpression("mth_leadqueue") { ColumnSet = new ColumnSet("mth_name") };
            var link = q.AddLink(LeadQueueSync.Relationship, "mth_leadqueueid", "mth_leadqueueid");
            link.LinkCriteria.AddCondition("mth_leadid", ConditionOperator.Equal, leadId);
            return service.RetrieveMultiple(q).Entities.ToList();
        }

        private static IEnumerable<Guid> CallerQueues(IOrganizationService service, Guid userId)
        {
            var q = new QueryExpression("mth_leadqueue_systemuser") { ColumnSet = new ColumnSet("mth_leadqueueid") };
            q.Criteria.AddCondition("systemuserid", ConditionOperator.Equal, userId);
            return service.RetrieveMultiple(q).Entities.Select(e => e.GetAttributeValue<Guid>("mth_leadqueueid"));
        }
    }
}
