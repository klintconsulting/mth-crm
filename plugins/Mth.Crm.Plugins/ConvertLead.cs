using System;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Custom API mth_ConvertLead (global action, synchronous). Converts an open lead to an opportunity owned by the caller.
    /// "First one wins": the lead is first updated with an optimistic-concurrency check (RowVersion), so if two users
    /// convert at the same time, the second gets an error and its transaction is rolled back.
    /// The client (Bygherre) becomes the account: found by VAT number, then exact name, otherwise created.
    /// Project columns are copied 1:1 (ProjectFields.Names). The lead is closed (inactive, status Converted).
    ///
    /// Inputs : LeadId (Guid)
    /// Outputs: OpportunityId (String), Message (String)
    /// </summary>
    public class ConvertLead : IPlugin
    {
        private const int LeadOpen = 273280000;
        private const int LeadConverted = 273280001;
        private const int OppStatusPotential = 273280001;   // "01 Potential project" (option value = base + code)
        private const int OppTypeExternal = 273280001;      // mth_opportunitytype "External"

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.InitiatingUserId); // the caller owns the new records
            var system = factory.CreateOrganizationService(null);

            var leadId = context.InputParameters.Contains("LeadId") ? (Guid)context.InputParameters["LeadId"] : Guid.Empty;
            if (leadId == Guid.Empty) throw new InvalidPluginExecutionException("LeadId mangler.");

            var cols = ProjectFields.Names.Concat(new[] { "mth_name", "mth_leadstatus", "mth_convertedbyid", "mth_convertedon", "mth_convertedopportunityid" }).ToArray();
            var lead = system.Retrieve("mth_lead", leadId, new ColumnSet(cols));
            var status = lead.GetAttributeValue<OptionSetValue>("mth_leadstatus");
            if (status != null && status.Value != LeadOpen) throw new InvalidPluginExecutionException(AlreadyTaken(lead));

            // 1. Claim the lead (first write, optimistic concurrency).
            var claim = new Entity("mth_lead", leadId) { RowVersion = lead.RowVersion };
            claim["mth_leadstatus"] = new OptionSetValue(LeadConverted);
            claim["mth_convertedbyid"] = new EntityReference("systemuser", context.InitiatingUserId);
            claim["mth_convertedon"] = DateTime.UtcNow;
            try
            {
                system.Execute(new UpdateRequest { Target = claim, ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches });
            }
            catch (Exception ex)
            {
                trace.Trace("Concurrency conflict: {0}", ex.Message);
                throw new InvalidPluginExecutionException("En anden har netop konverteret leadet. Opdatér siden.");
            }

            // 2. Account for the client (Bygherre).
            var accountId = FindOrCreateAccount(service, lead);

            // 3. Opportunity with the project columns.
            var opp = new Entity("mth_opportunity");
            foreach (var f in ProjectFields.Names) if (lead.Contains(f)) opp[f] = lead[f];
            opp["mth_name"] = Truncate(lead.GetAttributeValue<string>("mth_name"), 300);
            opp["mth_leadid"] = new EntityReference("mth_lead", leadId);
            opp["mth_status"] = new OptionSetValue(OppStatusPotential);
            opp["mth_opportunitytype"] = new OptionSetValue(OppTypeExternal);
            opp["mth_description"] = Truncate(lead.GetAttributeValue<string>("mth_projecttexts"), 32000);
            var start = lead.GetAttributeValue<DateTime?>("mth_projectstartdate");
            var end = lead.GetAttributeValue<DateTime?>("mth_projectenddate");
            if (start.HasValue) opp["mth_orderstartdate"] = start.Value;
            if (end.HasValue) opp["mth_orderenddate"] = end.Value;
            opp["mth_closedate"] = start.HasValue && start.Value.AddMonths(-2) > DateTime.Today ? start.Value.AddMonths(-2) : DateTime.Today.AddMonths(3);
            var value = lead.GetAttributeValue<decimal?>("mth_projectvalue");
            if (value.HasValue) opp["mth_estimatedamount"] = new Money(value.Value * 1000000m);
            if (accountId.HasValue) opp["mth_accountid"] = new EntityReference("account", accountId.Value);
            opp["mth_leaddataupdatedon"] = DateTime.UtcNow;
            var oppId = service.Create(opp);

            // 4. Close the lead and link it to the opportunity.
            var close = new Entity("mth_lead", leadId);
            close["mth_convertedopportunityid"] = new EntityReference("mth_opportunity", oppId);
            close["statecode"] = new OptionSetValue(1);
            close["statuscode"] = new OptionSetValue(2);
            system.Update(close);

            context.OutputParameters["OpportunityId"] = oppId.ToString();
            context.OutputParameters["Message"] = "Leadet er konverteret til en salgsmulighed.";
        }

        private static string AlreadyTaken(Entity lead)
        {
            var by = lead.GetAttributeValue<EntityReference>("mth_convertedbyid");
            var on = lead.GetAttributeValue<DateTime?>("mth_convertedon");
            if (by == null) return "Leadet er ikke længere åbent.";
            return string.Format("Leadet er allerede konverteret af {0}{1}.", by.Name ?? "en anden bruger",
                on.HasValue ? " den " + on.Value.ToLocalTime().ToString("dd-MM-yyyy HH:mm") : "");
        }

        private static Guid? FindOrCreateAccount(IOrganizationService service, Entity lead)
        {
            var name = lead.GetAttributeValue<string>("mth_clientname");
            var vat = lead.GetAttributeValue<string>("mth_clientvatno");
            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(vat)) return null;

            if (!string.IsNullOrWhiteSpace(vat))
            {
                var byVat = Find(service, "mth_vatno", vat.Trim());
                if (byVat.HasValue) return byVat;
            }
            if (!string.IsNullOrWhiteSpace(name))
            {
                var byName = Find(service, "name", name.Trim());
                if (byName.HasValue) return byName;
            }
            var a = new Entity("account");
            a["name"] = Truncate(string.IsNullOrWhiteSpace(name) ? "Bygherre " + vat : name.Trim(), 160);
            if (!string.IsNullOrWhiteSpace(vat)) a["mth_vatno"] = Truncate(vat.Trim(), 20);
            a["address1_line1"] = Truncate(lead.GetAttributeValue<string>("mth_clientstreet"), 250);
            a["address1_postalcode"] = Truncate(lead.GetAttributeValue<string>("mth_clientpostalcode"), 20);
            a["address1_city"] = Truncate(lead.GetAttributeValue<string>("mth_clientcity"), 80);
            a["address1_country"] = "Danmark";
            return service.Create(a);
        }

        private static Guid? Find(IOrganizationService service, string column, string value)
        {
            var q = new QueryExpression("account") { ColumnSet = new ColumnSet(false), TopCount = 1 };
            q.Criteria.AddCondition(column, ConditionOperator.Equal, value);
            q.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            var r = service.RetrieveMultiple(q).Entities.FirstOrDefault();
            return r?.Id;
        }

        private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max);
    }
}
