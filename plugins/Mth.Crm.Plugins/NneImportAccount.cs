using System;
using Microsoft.Xrm.Sdk;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Custom API mth_NneImportAccount (action): creates an account from NNE, owned by the caller
    /// (replaces Salesforce NNE_AccountImporter). The data is fetched server-side with fetchCompany, so the client
    /// only sends the TDC ID. If an active account with the TDC ID already exists, that one is returned instead.
    ///
    /// Inputs : TdcId (String)
    /// Outputs: AccountId (String), Created (Boolean), Message (String)
    /// </summary>
    public class NneImportAccount : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var system = factory.CreateOrganizationService(null);
            var user = factory.CreateOrganizationService(context.InitiatingUserId);

            var tdcId = (context.InputParameters.Contains("TdcId") ? context.InputParameters["TdcId"] as string : null)?.Trim();
            if (string.IsNullOrEmpty(tdcId)) throw new InvalidPluginExecutionException("TdcId mangler.");

            // Duplicate check across all active accounts (system context, so also accounts the user cannot see)
            Guid existing;
            if (NneService.AccountsByTdcId(system, new[] { tdcId }).TryGetValue(tdcId, out existing))
            {
                context.OutputParameters["AccountId"] = existing.ToString();
                context.OutputParameters["Created"] = false;
                context.OutputParameters["Message"] = "Virksomheden findes allerede som Account.";
                return;
            }

            var c = new NneService(system, trace).FetchCompany(tdcId);
            if (c == null) throw new InvalidPluginExecutionException("Virksomheden med TDC ID " + tdcId + " blev ikke fundet i NNE.");

            var id = user.Create(NneService.ToAccount(c));
            trace.Trace("Account {0} created from NNE {1}", id, tdcId);

            context.OutputParameters["AccountId"] = id.ToString();
            context.OutputParameters["Created"] = true;
            context.OutputParameters["Message"] = c.OfficialName + " er oprettet.";
        }
    }
}
