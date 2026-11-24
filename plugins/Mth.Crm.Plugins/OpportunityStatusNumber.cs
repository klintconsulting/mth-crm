using System;
using Microsoft.Xrm.Sdk;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Sets mth_opportunity.mth_statusnumber to the integer before the first space in the Status label
    /// ("54 Tender - submitted" -> 54), for filtering and sorting (replaces Salesforce Status_Number__c).
    /// The option values are 273280000 + the status code, so the number is computed from the value without reading labels
    /// (all 21 options verified 2026-11-24). Registered PreOperation (20), synchronous, on Create and on Update of mth_status.
    /// </summary>
    public class OpportunityStatusNumber : IPlugin
    {
        public const int OptionBase = 273280000;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            if (!(context.InputParameters["Target"] is Entity target)) return;
            if (!target.Contains("mth_status")) return;
            target["mth_statusnumber"] = StatusNumber(target.GetAttributeValue<OptionSetValue>("mth_status"));
        }

        public static int? StatusNumber(OptionSetValue status)
        {
            if (status == null) return null;
            var n = status.Value - OptionBase;
            return n >= 0 && n <= 99 ? n : (int?)null;
        }
    }
}
