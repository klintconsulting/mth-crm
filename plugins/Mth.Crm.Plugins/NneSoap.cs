using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Security;
using System.Xml.Linq;

namespace Mth.Crm.Plugins
{
    /// <summary>Search criteria for NNE searchTargetGroup (same fields as the Salesforce LWC nneSearch).</summary>
    public class NneQuery
    {
        public string Name, Street, HouseNo, Zip, City, Cvr, TdcId, Phone;
        public bool NameStartsWith, StreetStartsWith, LegalEntitiesOnly, ActiveOnly = true;
        public int MaxHits = 50;
    }

    /// <summary>One company from NNE. Basic fields come from the search, the rest from fetchCompany.</summary>
    [DataContract]
    public class NneCompany
    {
        [DataMember(Name = "tdcId")] public string TdcId;
        [DataMember(Name = "officialName")] public string OfficialName;
        [DataMember(Name = "street")] public string Street;
        [DataMember(Name = "zipCode")] public string ZipCode;
        [DataMember(Name = "district")] public string District;
        [DataMember(Name = "phone")] public string Phone;
        [DataMember(Name = "cvrNo")] public string CvrNo;
        [DataMember(Name = "pNo")] public string PNo;
        [DataMember(Name = "adProtection")] public bool AdProtection;
        [DataMember(Name = "companyType")] public string CompanyType;
        // fetchCompany
        [DataMember(Name = "statusText")] public string StatusText;
        [DataMember(Name = "statusValue")] public int? StatusValue;
        [DataMember(Name = "companyForm")] public string CompanyForm;
        [DataMember(Name = "legalEntity")] public bool? LegalEntity;
        [DataMember(Name = "email")] public string Email;
        [DataMember(Name = "homePage")] public string HomePage;
        [DataMember(Name = "numberOfEmployees")] public int? NumberOfEmployees;
        [DataMember(Name = "foundedYear")] public int? FoundedYear;
        [DataMember(Name = "tdfName")] public string TdfName;
        // Dataverse
        [DataMember(Name = "accountId")] public string AccountId;
    }

    /// <summary>
    /// NNE (Navne og Numre Erhverv, D&amp;B/Bisnode) SOAP 3.1, rpc/encoded. Builds requests and parses responses; no I/O.
    /// Ported from Salesforce NNE_Service.cls. Responses use multi-ref encoding: elements reference each other with href="#IDn".
    /// </summary>
    public static class NneSoap
    {
        public const string DefaultEndpoint = "https://nne-api.bisnode.com/nne-ws/3.1/NNE";
        private const string Head =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<SOAP-ENV:Envelope xmlns:SOAP-ENV=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:mns=\"http://com.stibo.net/nne/3.1/NNE\" " +
            "xmlns:ns2=\"http://com.stibo.net/nne/3.1/Types/NNE\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" +
            "<SOAP-ENV:Body>";
        private const string Tail = "</SOAP-ENV:Body></SOAP-ENV:Envelope>";

        private static string E(string s) => string.IsNullOrWhiteSpace(s) ? "" : SecurityElement.Escape(s.Trim());
        private static string Int(string s) { long n; return long.TryParse((s ?? "").Trim().Replace(" ", ""), out n) ? n.ToString(CultureInfo.InvariantCulture) : "0"; }
        private static string B(bool b) => b ? "true" : "false";

        public static string SearchEnvelope(NneQuery q, string accessKey)
        {
            var hits = Math.Max(1, Math.Min(5000, q.MaxHits));
            return Head +
                "<mns:searchTargetGroup SOAP-ENV:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<QuestionTargetGroup_1 xsi:type=\"ns2:QuestionTargetGroup\">" +
                "<cvrNo xsi:type=\"xsd:int\">" + Int(q.Cvr) + "</cvrNo>" +
                "<district xsi:type=\"xsd:string\">" + E(q.City) + "</district>" +
                "<houseNo xsi:type=\"xsd:string\">" + E(q.HouseNo) + "</houseNo>" +
                "<name xsi:type=\"xsd:string\">" + E(q.Name) + "</name>" +
                "<nameStartsWith xsi:type=\"xsd:boolean\">" + B(q.NameStartsWith) + "</nameStartsWith>" +
                "<phone xsi:type=\"xsd:string\">" + E(q.Phone) + "</phone>" +
                "<street xsi:type=\"xsd:string\">" + E(q.Street) + "</street>" +
                "<streetStartsWith xsi:type=\"xsd:boolean\">" + B(q.StreetStartsWith) + "</streetStartsWith>" +
                "<tdcId xsi:type=\"xsd:int\">" + Int(q.TdcId) + "</tdcId>" +
                "<zipCode xsi:type=\"xsd:int\">" + Int(q.Zip) + "</zipCode>" +
                "<companyStatus xsi:type=\"xsd:string\">" + (q.ActiveOnly ? "0" : "") + "</companyStatus>" +
                "<legalEntitiesOnly xsi:type=\"xsd:boolean\">" + B(q.LegalEntitiesOnly) + "</legalEntitiesOnly>" +
                "</QuestionTargetGroup_1>" +
                "<int_2 xsi:type=\"xsd:int\">" + hits + "</int_2>" +      // hits per page
                "<int_3 xsi:type=\"xsd:int\">1</int_3>" +                 // page
                "<int_4 xsi:type=\"xsd:int\">1</int_4>" +                 // include advertising-protected companies
                "<String_5 xsi:type=\"xsd:string\">" + E(accessKey) + "</String_5>" +
                "</mns:searchTargetGroup>" + Tail;
        }

        public static string FetchCompanyEnvelope(string tdcId, string accessKey) =>
            Head +
            "<mns:fetchCompany SOAP-ENV:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
            "<int_1 xsi:type=\"xsd:int\">" + Int(tdcId) + "</int_1>" +
            "<int_2 xsi:type=\"xsd:int\">0</int_2>" +
            "<String_3 xsi:type=\"xsd:string\"></String_3>" +
            "<String_4 xsi:type=\"xsd:string\">" + E(accessKey) + "</String_4>" +
            "</mns:fetchCompany>" + Tail;

        /// <summary>Parses a searchTargetGroup (or search) response. Throws on a SOAP fault or an NNE errorMessage.</summary>
        public static List<NneCompany> ParseSearch(string xml, out int total)
        {
            total = 0;
            var doc = XDocument.Parse(xml);
            ThrowOnFault(doc);
            var byId = ById(doc);
            var answer = Deref(byId, doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "result"));
            if (answer == null) return new List<NneCompany>();
            var error = Text(answer, "errorMessage");
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("NNE: " + error);
            var window = Deref(byId, Child(answer, "window"));
            int t; if (window != null && int.TryParse(Text(window, "total"), out t)) total = t;
            var array = Deref(byId, Child(answer, "companyBasic"));
            if (array == null) return new List<NneCompany>();
            return array.Elements().Where(i => i.Name.LocalName == "item")
                .Select(i => Deref(byId, i) ?? i)
                .Select(c => new NneCompany
                {
                    TdcId = Text(c, "tdcId"), OfficialName = Text(c, "officialName"), Street = Text(c, "street"),
                    ZipCode = Text(c, "zipCode"), District = Text(c, "district"), Phone = Text(c, "phone"),
                    CvrNo = NonZero(Text(c, "cvrNo")), PNo = NonZero(Text(c, "PNo")), AdProtection = Text(c, "adProtection") == "1",
                    CompanyType = Text(c, "companyType")
                })
                .Where(c => !string.IsNullOrEmpty(c.TdcId))
                .ToList();
        }

        /// <summary>Parses a fetchCompany response (one company with details). Returns null if empty.</summary>
        public static NneCompany ParseCompany(string xml)
        {
            var doc = XDocument.Parse(xml);
            ThrowOnFault(doc);
            var byId = ById(doc);
            var c = Deref(byId, doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "result"));
            if (c == null || string.IsNullOrEmpty(Text(c, "tdcId"))) return null;
            return new NneCompany
            {
                TdcId = Text(c, "tdcId"), OfficialName = Text(c, "officialName"), Street = Text(c, "street"),
                ZipCode = Text(c, "zipCode"), District = Text(c, "district"), Phone = Text(c, "phone"),
                CvrNo = NonZero(Text(c, "cvrNo")), PNo = NonZero(Text(c, "PNo")), AdProtection = Text(c, "adProtection") == "1",
                CompanyType = Text(c, "companyType"),
                StatusText = Text(c, "statusText"), StatusValue = IntOrNull(Text(c, "statusValue")),
                CompanyForm = Text(c, "companyForm"), LegalEntity = Text(c, "legalEntity") == "" ? (bool?)null : Text(c, "legalEntity") == "1",
                Email = Text(c, "email"), HomePage = Text(c, "homePage"),
                NumberOfEmployees = IntOrNull(Text(c, "numberOfEmployees")), FoundedYear = IntOrNull(Text(c, "foundedYear")),
                TdfName = Text(c, "tdfName")
            };
        }

        /// <summary>Copies the fetchCompany details onto a search row.</summary>
        public static void Merge(NneCompany row, NneCompany details)
        {
            if (details == null) return;
            row.StatusText = details.StatusText; row.StatusValue = details.StatusValue; row.CompanyForm = details.CompanyForm;
            row.LegalEntity = details.LegalEntity; row.Email = details.Email; row.HomePage = details.HomePage;
            row.NumberOfEmployees = details.NumberOfEmployees; row.FoundedYear = details.FoundedYear; row.TdfName = details.TdfName;
            if (string.IsNullOrEmpty(row.PNo)) row.PNo = details.PNo;
        }

        private static void ThrowOnFault(XDocument doc)
        {
            var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
            if (fault != null) throw new InvalidOperationException("NNE SOAP fault: " + (Text(fault, "faultstring") ?? fault.Value));
        }
        private static Dictionary<string, XElement> ById(XDocument doc)
        {
            var d = new Dictionary<string, XElement>();
            foreach (var e in doc.Descendants()) { var id = (string)e.Attribute("id"); if (id != null) d[id] = e; }
            return d;
        }
        private static XElement Deref(Dictionary<string, XElement> byId, XElement reference)
        {
            if (reference == null) return null;
            var href = (string)reference.Attribute("href");
            if (href == null) return reference.HasElements ? reference : null;
            XElement target; return byId.TryGetValue(href.TrimStart('#'), out target) ? target : null;
        }
        private static XElement Child(XElement parent, string local) => parent.Elements().FirstOrDefault(e => e.Name.LocalName == local);
        private static string Text(XElement parent, string local)
        {
            var c = Child(parent, local);
            if (c == null) return "";
            var nil = c.Attributes().FirstOrDefault(a => a.Name.LocalName == "nil");
            return nil != null && (nil.Value == "1" || nil.Value == "true") ? "" : c.Value.Trim();
        }
        private static string NonZero(string s) => s == "0" ? "" : s;
        private static int? IntOrNull(string s) { int n; return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : (int?)null; }
    }
}
