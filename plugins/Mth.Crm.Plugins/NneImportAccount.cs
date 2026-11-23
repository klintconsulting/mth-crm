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

            var a = new Entity("account");
            a["name"] = T(c.OfficialName, 160);
            a["address1_line1"] = T(c.Street, 250);
            a["address1_postalcode"] = T(c.ZipCode, 20);
            a["address1_city"] = T(c.District, 80);
            a["address1_country"] = "Danmark";
            a["telephone1"] = T(c.Phone, 50);
            a["emailaddress1"] = T(c.Email, 100);
            a["websiteurl"] = T(c.HomePage, 200);
            if (c.NumberOfEmployees.HasValue) a["numberofemployees"] = c.NumberOfEmployees.Value;
            a["mth_tdcid"] = T(c.TdcId, 50);
            a["mth_vatno"] = T(c.CvrNo, 20);
            a["mth_pnumber"] = T(c.PNo, 20);
            a["mth_companyform"] = T(c.CompanyForm, 100);
            if (c.LegalEntity.HasValue) a["mth_legalentity"] = c.LegalEntity.Value;
            a["mth_companystatustext"] = T(c.StatusText, 200);
            if (c.FoundedYear.HasValue && c.FoundedYear.Value > 0) a["mth_foundedyear"] = c.FoundedYear.Value;
            a["mth_adprotection"] = c.AdProtection;
            a["mth_directoryname"] = T(c.TdfName, 200);
            a["mth_nneupdatedon"] = DateTime.UtcNow;
            var id = user.Create(a);
            trace.Trace("Account {0} created from NNE {1}", id, tdcId);

            context.OutputParameters["AccountId"] = id.ToString();
            context.OutputParameters["Created"] = true;
            context.OutputParameters["Message"] = c.OfficialName + " er oprettet.";
        }

        private static string T(string s, int max) => string.IsNullOrEmpty(s) ? null : (s.Length <= max ? s : s.Substring(0, max));
    }
}
