using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Sets mth_collaboratingpartner.mth_opportunityid to the opportunity of its building contract, so a collaborating
    /// partner (UE) shows, filters and links to its opportunity directly (UE -> building contract -> opportunity).
    /// Registered PreOperation (20), synchronous, on Create and on Update of mth_buildingcontractid, so the value is
    /// written in the same transaction. The column is derived: it is overwritten whenever the building contract changes.
    /// </summary>
    public class CollabPartnerOpportunity : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var system = factory.CreateOrganizationService(null);

            if (!(context.InputParameters["Target"] is Entity target)) return;
            if (!target.Contains("mth_buildingcontractid")) return; // Update without a building contract change

            var bc = target.GetAttributeValue<EntityReference>("mth_buildingcontractid");
            EntityReference opp = null;
            if (bc != null)
                opp = system.Retrieve("mth_buildingcontract", bc.Id, new ColumnSet("mth_opportunityid"))
                            .GetAttributeValue<EntityReference>("mth_opportunityid");
            target["mth_opportunityid"] = opp;
            trace.Trace("Collaborating partner {0}: opportunity {1}", target.Id, opp?.Id.ToString() ?? "(none)");
        }
    }
}
