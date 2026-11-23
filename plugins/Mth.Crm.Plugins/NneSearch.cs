using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Custom API mth_NneSearch (action): searches NNE like the Salesforce LWC nneSearch and returns the hits as JSON,
    /// enriched with fetchCompany details (status, e-mail, homepage, employees ...) and the Dataverse account if the
    /// company already exists (by TDC ID).
    ///
    /// Inputs : Name, Cvr, TdcId, Street, HouseNo, Zip, City, Phone (String, optional),
    ///          NameStartsWith, StreetStartsWith, LegalEntitiesOnly, ActiveOnly (Boolean, optional), MaxHits (Integer, optional, 1-500)
    /// Outputs: ResultJson (String) = { "total": n, "companies": [ NneCompany ... ] }
    /// </summary>
    public class NneSearch : IPlugin
    {
        private const int MaxDetails = 100;   // fetchCompany calls per search (Salesforce: 95)

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var system = factory.CreateOrganizationService(null);
            var user = factory.CreateOrganizationService(context.InitiatingUserId);
            var p = context.InputParameters;

            string S(string n) => p.Contains(n) ? (p[n] as string)?.Trim() : null;
            bool Bo(string n, bool d) => p.Contains(n) && p[n] is bool b ? b : d;
            var q = new NneQuery
            {
                Name = S("Name"), Cvr = S("Cvr"), TdcId = S("TdcId"), Street = S("Street"), HouseNo = S("HouseNo"),
                Zip = S("Zip"), City = S("City"), Phone = S("Phone"),
                NameStartsWith = Bo("NameStartsWith", false), StreetStartsWith = Bo("StreetStartsWith", false),
                LegalEntitiesOnly = Bo("LegalEntitiesOnly", false), ActiveOnly = Bo("ActiveOnly", true),
                MaxHits = p.Contains("MaxHits") && p["MaxHits"] is int m ? Math.Max(1, Math.Min(500, m)) : 50
            };
            if (new[] { q.Name, q.Cvr, q.TdcId, q.Street, q.Zip, q.City, q.Phone }.All(string.IsNullOrWhiteSpace))
                throw new InvalidPluginExecutionException("Udfyld mindst ét søgefelt.");

            var nne = new NneService(system, trace);
            int total;
            var rows = nne.Search(q, out total);
            if (rows.Count > q.MaxHits) rows = rows.Take(q.MaxHits).ToList();   // NNE returns at least 10 per page
            trace.Trace("NNE search: {0} of {1} hits", rows.Count, total);

            // Details in parallel (one fetchCompany per hit), like fetchCompanyStatuses in Salesforce
            var detailRows = rows.Take(MaxDetails).ToList();
            var details = new NneCompany[detailRows.Count];
            Parallel.For(0, detailRows.Count, new ParallelOptions { MaxDegreeOfParallelism = 8 }, i =>
            {
                try { details[i] = nne.FetchCompany(detailRows[i].TdcId); }
                catch (Exception ex) { trace.Trace("fetchCompany {0} failed: {1}", detailRows[i].TdcId, ex.Message); }
            });
            for (var i = 0; i < detailRows.Count; i++) NneSoap.Merge(detailRows[i], details[i]);

            // Existing accounts (as the caller: only accounts the user can see)
            var accounts = NneService.AccountsByTdcId(user, rows.Select(r => r.TdcId));
            foreach (var r in rows) { Guid id; if (accounts.TryGetValue(r.TdcId, out id)) r.AccountId = id.ToString(); }

            context.OutputParameters["ResultJson"] = ToJson(new SearchResult { Total = total, Companies = rows });
        }

        [DataContract]
        private class SearchResult
        {
            [DataMember(Name = "total")] public int Total;
            [DataMember(Name = "companies")] public List<NneCompany> Companies;
        }

        internal static string ToJson<T>(T value)
        {
            using (var ms = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(ms, value);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }
    }
}
