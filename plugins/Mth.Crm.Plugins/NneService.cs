using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Calls the NNE SOAP service. Endpoint and access key come from the environment variables
    /// mth_NneEndpoint (default NneSoap.DefaultEndpoint) and mth_NneAccessKey (required; value set per environment
    /// by scripts/Deploy-Nne.ps1 and never stored in the solution or repo).
    /// </summary>
    public class NneService
    {
        private readonly string _endpoint, _accessKey;
        private readonly ITracingService _trace;
        private readonly int _timeoutMs;

        public NneService(IOrganizationService system, ITracingService trace, int timeoutMs = 30000)
        {
            _trace = trace;
            _timeoutMs = timeoutMs;
            _endpoint = EnvironmentVariable(system, "mth_NneEndpoint") ?? NneSoap.DefaultEndpoint;
            _accessKey = EnvironmentVariable(system, "mth_NneAccessKey");
            if (string.IsNullOrWhiteSpace(_accessKey))
                throw new InvalidPluginExecutionException("NNE er ikke sat op: miljøvariablen mth_NneAccessKey mangler en værdi.");
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        public List<NneCompany> Search(NneQuery q, out int total) => NneSoap.ParseSearch(Post(NneSoap.SearchEnvelope(q, _accessKey)), out total);

        public NneCompany FetchCompany(string tdcId) => NneSoap.ParseCompany(Post(NneSoap.FetchCompanyEnvelope(tdcId, _accessKey)));

        private string Post(string envelope)
        {
            var req = (HttpWebRequest)WebRequest.Create(_endpoint);
            req.Method = "POST";
            req.ContentType = "text/xml; charset=UTF-8";
            req.Headers.Add("SOAPAction", "\"\"");
            req.Timeout = _timeoutMs;
            req.ReadWriteTimeout = _timeoutMs;
            var bytes = Encoding.UTF8.GetBytes(envelope);
            using (var s = req.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            try
            {
                using (var res = (HttpWebResponse)req.GetResponse())
                using (var r = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
                    return r.ReadToEnd();
            }
            catch (WebException ex)
            {
                string body = null;
                if (ex.Response != null) using (var r = new StreamReader(ex.Response.GetResponseStream(), Encoding.UTF8)) body = r.ReadToEnd();
                _trace?.Trace("NNE call failed: {0} {1}", ex.Message, body);
                if (body != null && body.Contains("Fault")) NneSoap.ParseCompany(body); // throws with the fault text
                throw new InvalidPluginExecutionException("Kaldet til NNE fejlede: " + ex.Message);
            }
        }

        /// <summary>
        /// The legal entity (main company, not a production unit) for a CVR number, with full details, or null.
        /// Prefers an active entity; falls back to an inactive one so the account still gets the official data.
        /// </summary>
        public NneCompany FindLegalEntityByCvr(string cvr)
        {
            cvr = new string((cvr ?? "").Where(char.IsDigit).ToArray());
            if (cvr.Length != 8) return null;
            int total;
            var rows = Search(new NneQuery { Cvr = cvr, LegalEntitiesOnly = true, ActiveOnly = true, MaxHits = 10 }, out total);
            if (rows.Count == 0) rows = Search(new NneQuery { Cvr = cvr, LegalEntitiesOnly = true, ActiveOnly = false, MaxHits = 10 }, out total);
            var hit = rows.FirstOrDefault(r => r.CvrNo == cvr);
            return hit == null ? null : FetchCompany(hit.TdcId) ?? hit;
        }

        /// <summary>New account from NNE company data (shared by mth_NneImportAccount and mth_ConvertLead).</summary>
        public static Entity ToAccount(NneCompany c)
        {
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
            return a;
        }

        private static string T(string s, int max) => string.IsNullOrEmpty(s) ? null : (s.Length <= max ? s : s.Substring(0, max));

        /// <summary>Current value of an environment variable (value record, else default value). Null if not set.</summary>
        public static string EnvironmentVariable(IOrganizationService service, string schemaName)
        {
            var q = new QueryExpression("environmentvariabledefinition") { ColumnSet = new ColumnSet("defaultvalue"), TopCount = 1 };
            q.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);
            var v = q.AddLink("environmentvariablevalue", "environmentvariabledefinitionid", "environmentvariabledefinitionid", JoinOperator.LeftOuter);
            v.EntityAlias = "v"; v.Columns = new ColumnSet("value");
            var row = service.RetrieveMultiple(q).Entities.FirstOrDefault();
            if (row == null) return null;
            var value = row.GetAttributeValue<AliasedValue>("v.value")?.Value as string;
            return !string.IsNullOrWhiteSpace(value) ? value.Trim() : row.GetAttributeValue<string>("defaultvalue");
        }

        /// <summary>Active accounts by TDC ID (NNE unique id) -> account id.</summary>
        public static Dictionary<string, Guid> AccountsByTdcId(IOrganizationService service, IEnumerable<string> tdcIds)
        {
            var ids = tdcIds.Where(t => !string.IsNullOrEmpty(t)).Distinct().ToArray();
            var map = new Dictionary<string, Guid>();
            if (ids.Length == 0) return map;
            var q = new QueryExpression("account") { ColumnSet = new ColumnSet("mth_tdcid") };
            q.Criteria.AddCondition("mth_tdcid", ConditionOperator.In, ids.Cast<object>().ToArray());
            q.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            foreach (var a in service.RetrieveMultiple(q).Entities) map[a.GetAttributeValue<string>("mth_tdcid")] = a.Id;
            return map;
        }
    }
}
