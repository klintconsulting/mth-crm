using System.Globalization;
using System.Net;
using System.Text;

namespace Mth.SmartSync;

/// <summary>
/// Maps a SMART project (+ its current tenders and the companies on its roles) to the Dataverse project columns
/// (identical on mth_lead and mth_opportunity). Port of SMART_ProjectSync.applySmartProjectToLead with fixes:
/// numbers stay numbers, no "TO BE DELETED"/"null" texts, values are truncated to the column length instead of
/// making the whole record fail, and the project description is HTML-safe where it is rendered as HTML.
/// </summary>
public static class SmartMapper
{
    public const string ClientRole = "Bygherre";
    public const string SmartProjectUrl = "https://smart.byggefakta.dk/leads/projects/";
    private static readonly CultureInfo Da = new("da-DK");

    /// <summary>Max lengths of the string columns (from schema.json). Longer values are truncated.</summary>
    public static readonly IReadOnlyDictionary<string, int> MaxLength = new Dictionary<string, int>
    {
        ["mth_name"] = 300, ["mth_projectid"] = 50, ["mth_smartid"] = 20, ["mth_smartlink"] = 300, ["mth_projecttitle"] = 1000,
        ["mth_projecttexts"] = 100000, ["mth_clientname"] = 300, ["mth_clientstreet"] = 300, ["mth_clientpostalcode"] = 20,
        ["mth_clientcity"] = 100, ["mth_clientcompanytype"] = 255, ["mth_clientvatno"] = 50, ["mth_clienttdcid"] = 50,
        ["mth_constructionsite"] = 300, ["mth_constructiontown"] = 100, ["mth_constructionzip"] = 20, ["mth_projectstarttext"] = 255,
        ["mth_projectendtext"] = 255, ["mth_projectvaluetext"] = 255, ["mth_smartcontracttype"] = 255, ["mth_smartcontracttypecode"] = 50,
        ["mth_planningstage"] = 255, ["mth_projectstage"] = 255, ["mth_maincategory"] = 255, ["mth_maincategorycode"] = 50,
        ["mth_maindevtype"] = 255, ["mth_maindevtypecode"] = 50, ["mth_projectcategories"] = 4000, ["mth_projectcountry"] = 100,
        ["mth_projectregion"] = 100, ["mth_projectregioncode"] = 50, ["mth_municipality"] = 100, ["mth_municipalitycode"] = 50,
        ["mth_roleshtml"] = 100000, ["mth_tendershtml"] = 100000
    };

    public static Dictionary<string, object?> Map(SmartProject p, IReadOnlyList<SmartTender> tenders, IReadOnlyDictionary<long, SmartCompany> companies, LookupCache lc)
    {
        var f = new Dictionary<string, object?>();
        var name = FirstNonBlank(p.ProjectName, p.Title) ?? "Projektnavn ikke oplyst";
        f["mth_name"] = name;
        f["mth_projectid"] = p.SourceId?.ToString(CultureInfo.InvariantCulture);
        f["mth_smartid"] = p.Id.ToString(CultureInfo.InvariantCulture);
        f["mth_smartlink"] = SmartProjectUrl + p.Id;
        f["mth_projecttitle"] = Blank(p.Title);
        f["mth_projecttexts"] = Blank(p.Description);

        // Client (Bygherre): first role named "Bygherre" (by role card, else lookup)
        var client = p.Roles.FirstOrDefault(r => string.Equals(RoleName(r, lc), ClientRole, StringComparison.OrdinalIgnoreCase));
        f["mth_clientname"] = Blank(client?.CompanyCard?.Name);
        f["mth_clientvatno"] = Blank(client?.CompanyCard?.OrgNr);
        f["mth_clienttdcid"] = Blank(client?.CompanyCard?.ParId);
        SmartCompany? clientCompany = null;
        if (client?.CompanyId is long cid) companies.TryGetValue(cid, out clientCompany);
        var addr = AddressParts(clientCompany);
        f["mth_clientstreet"] = addr.Street;
        f["mth_clientpostalcode"] = addr.Postcode;
        f["mth_clientcity"] = addr.City;
        f["mth_clientcompanytype"] = clientCompany == null ? null : lc.Name("company_types", clientCompany.CompanyTypeId);

        f["mth_constructionsite"] = Blank(JoinSpace(p.Street, p.StreetNumber));
        f["mth_constructiontown"] = Blank(p.City);
        f["mth_constructionzip"] = Blank(p.PostcodeText) ?? p.Postcode?.ToString(CultureInfo.InvariantCulture);

        f["mth_projectstartdate"] = DateOnlyText(p.StartDate, p.StartAccuracy);
        f["mth_projectstarttext"] = Blank(p.StartText);
        f["mth_projectenddate"] = DateOnlyText(p.FinishDate, p.FinishAccuracy);
        f["mth_projectendtext"] = Blank(p.FinishText);
        f["mth_constructionmonths"] = p.Months;

        f["mth_projectvalue"] = p.Value.HasValue ? Math.Round(p.Value.Value / 1_000_000m, 2) : null;
        f["mth_projectvaluetext"] = Blank(p.ValueText) ?? ValueText(p.Value);

        f["mth_smartcontracttype"] = lc.Name("contract_types", p.ContractTypeId, "contract_type_id");
        f["mth_smartcontracttypecode"] = lc.Code("contract_types", p.ContractTypeId);
        f["mth_planningstage"] = lc.Name("detailed_stages", p.DetailedStageId) ?? lc.Name("project_stages", p.StageId, "stage_id");
        f["mth_projectstage"] = lc.Name("project_stages", p.StageId, "stage_id");
        f["mth_maincategory"] = lc.Name("categories", p.CategoryId, "category_id");
        f["mth_maincategorycode"] = lc.Code("categories", p.CategoryId);
        f["mth_maindevtype"] = lc.Name("development_types", p.DevTypeId, "dev_type_id");
        f["mth_maindevtypecode"] = lc.Code("development_types", p.DevTypeId);
        f["mth_projectcategories"] = Blank(Categories(p, lc));

        f["mth_projectcountry"] = lc.Country(p.CountryId);
        f["mth_projectregion"] = lc.Name("regions", p.RegionId, "region_id");
        f["mth_projectregioncode"] = lc.Code("regions", p.RegionId);
        f["mth_municipality"] = lc.Name("districts", p.DistrictId, "district_id");
        f["mth_municipalitycode"] = lc.Code("districts", p.DistrictId);

        f["mth_apartments"] = ToInt(p.Apartments);
        f["mth_grossfloorarea"] = ToInt(p.GrossArea);
        f["mth_netfloorarea"] = ToInt(p.UsageArea);
        f["mth_volume"] = ToInt(p.GrossVolume);
        f["mth_storeys"] = ToInt(p.Floors);
        f["mth_storeysbelowground"] = ToInt(p.UndergroundFloors);

        f["mth_roleshtml"] = Blank(RolesHtml(p, companies, lc));
        f["mth_tendershtml"] = Blank(TendersHtml(p, tenders, lc));
        f["mth_smartupdatedon"] = p.UpdatedAt?.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

        foreach (var (col, max) in MaxLength)
            if (f.TryGetValue(col, out var v) && v is string s && s.Length > max) f[col] = s[..max];
        return f;
    }

    // ---------- tenders (via updates[]) ----------

    /// <summary>
    /// Active tender ids from PROJECT_TENDER_REGISTERED/UNREGISTERED updates: per tender the latest update wins
    /// (ties: the later entry); active if that update is REGISTERED (SMART_ProjectSync.getActiveTenderIdsFromProjectUpdates).
    /// </summary>
    public static List<long> ActiveTenderIds(SmartProject p)
    {
        var latest = new Dictionary<long, SmartUpdate>();
        foreach (var u in p.Updates)
        {
            if (u.TenderId is not long id || u.Key is not ("PROJECT_TENDER_REGISTERED" or "PROJECT_TENDER_UNREGISTERED")) continue;
            if (!latest.TryGetValue(id, out var cur) || (u.UpdatedAt ?? DateTimeOffset.MinValue) >= (cur.UpdatedAt ?? DateTimeOffset.MinValue)) latest[id] = u;
        }
        return latest.Where(kv => kv.Value.Key == "PROJECT_TENDER_REGISTERED").Select(kv => kv.Key).ToList();
    }

    public static string? TendersHtml(SmartProject p, IReadOnlyList<SmartTender> tenders, LookupCache lc)
    {
        var current = tenders.Where(t => t.IsCurrent).GroupBy(t => t.Id).Select(g => g.First()).ToList();
        var sections = new List<string>();
        foreach (var t in current)
        {
            var lines = new List<string>
            {
                "<b>" + H(FirstNonBlank(t.RoleCard?.Name, lc.Name("company_role_tender", t.CompanyRoleId), "Udbud " + t.Id)) + "</b>"
            };
            AddLine(lines, "Udsendelses dato", t.FirstDayMaterials);
            AddLine(lines, "Licitations dato", t.LastDayApplication);
            AddLine(lines, "Deadline", t.PrequalificationDate);
            AddLine(lines, "Licitations type", FirstNonBlank(t.TypeCard?.Name, lc.Name("tender_types", t.TenderTypeId)));
            var content = FirstNonBlank(t.ContentStandard, t.ContentTed, t.Description);
            if (content != null) lines.Add(H(content));
            sections.Add(string.Join("<br>", lines));
        }
        if (sections.Count == 0)
        {
            // fallback: headings from the active registered updates (no tender details available)
            var active = new HashSet<long>(ActiveTenderIds(p));
            foreach (var u in p.Updates.Where(u => u.TenderId is long id && active.Contains(id) && u.Key == "PROJECT_TENDER_REGISTERED").GroupBy(u => u.TenderId).Select(g => g.Last()))
                sections.Add("<b>" + H(FirstNonBlank(u.RoleCard?.Name, "Udbud " + u.TenderId)) + "</b>");
        }
        return sections.Count == 0 ? null : string.Join("<br><br>", sections);
    }

    private static void AddLine(List<string> lines, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) lines.Add(H(label) + ": " + H(value));
    }

    // ---------- roles ----------
    public static string? RolesHtml(SmartProject p, IReadOnlyDictionary<long, SmartCompany> companies, LookupCache lc)
    {
        var sections = new List<string>();
        foreach (var r in p.Roles)
        {
            var lines = new List<string> { "<b>" + H(RoleName(r, lc) ?? "Rolle") + "</b>" };
            if (!string.IsNullOrWhiteSpace(r.CompanyCard?.Name))
            {
                SmartCompany? company = null;
                if (r.CompanyId is long id) companies.TryGetValue(id, out company);
                var a = AddressParts(company);
                var parts = new List<string?> { r.CompanyCard!.Name, a.Street, a.Postcode, a.City };
                var tel = Comm(r.CompanyCard.Communications, 1); var mail = Comm(r.CompanyCard.Communications, 4);
                if (tel != null) parts.Add("tel: " + tel);
                if (mail != null) parts.Add("email: " + mail);
                if (!string.IsNullOrWhiteSpace(r.CompanyCard.OrgNr)) parts.Add("cvr: " + r.CompanyCard.OrgNr);
                lines.Add(H(string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)))));
            }
            foreach (var c in r.Contacts.Where(c => c.Card != null))
            {
                var card = c.Card!;
                var parts = new List<string?> { JoinSpace(card.FirstName, card.LastName) };
                var tel = Comm(card.Communications, 1); var mob = Comm(card.Communications, 2); var mail = Comm(card.Communications, 4);
                if (tel != null) parts.Add("tel: " + tel);
                if (mob != null) parts.Add("mob: " + mob);
                if (mail != null) parts.Add("email: " + mail);
                lines.Add("&nbsp;&nbsp;&nbsp;&nbsp;" + H(string.Join(", ", parts.Where(x => !string.IsNullOrWhiteSpace(x)))));
            }
            sections.Add(string.Join("<br>", lines));
        }
        return sections.Count == 0 ? null : string.Join("<br><br>", sections);
    }

    public static string? RoleName(SmartRole r, LookupCache lc) => FirstNonBlank(r.RoleCard?.Name, lc.Name("company_roles", r.CompanyRoleId));

    /// <summary>First communication of a type: phone_data.number for phones, else value.</summary>
    public static string? Comm(IEnumerable<SmartCommunication> list, int type)
    {
        var c = list.FirstOrDefault(x => x.TypeId == type);
        return c == null ? null : FirstNonBlank(c.Phone?.Number, c.Value);
    }

    // ---------- address (pickMainAddress) ----------
    public static (string? Street, string? Postcode, string? City) AddressParts(SmartCompany? c)
    {
        if (c == null) return (null, null, null);
        var a = c.Addresses.FirstOrDefault(x => x.Main == true) ?? c.Addresses.FirstOrDefault(x => x.TypeId == 1) ?? c.Addresses.FirstOrDefault();
        return a != null
            ? (Blank(JoinSpace(a.Street, a.StreetNumber)), Blank(a.PostcodeText), Blank(a.City))
            : (Blank(JoinSpace(c.Street, c.StreetNumber)), Blank(c.PostcodeText), Blank(c.City));
    }

    // ---------- small helpers ----------
    public static string? Categories(SmartProject p, LookupCache lc) =>
        string.Join(";", p.CatDevTypes.OrderBy(c => c.Priority ?? 999)
            .Select(c => $"{lc.Name("categories", c.CategoryId, "category_id")} - {lc.Name("development_types", c.DevTypeId, "dev_type_id")}"));

    /// <summary>yyyy-MM-dd from the ISO date; month accuracy (2) gives the 1st of the month.</summary>
    public static string? DateOnlyText(string? iso, int? accuracy)
    {
        if (string.IsNullOrWhiteSpace(iso) || iso.Length < 10) return null;
        if (!DateTime.TryParseExact(iso[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return null;
        if (accuracy == 2) d = new DateTime(d.Year, d.Month, 1);
        return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    /// <summary>"97 mio.kr", "1,5 mio.kr" (millions, max 2 decimals, Danish decimal comma).</summary>
    public static string? ValueText(decimal? value)
    {
        if (!value.HasValue) return null;
        var m = Math.Round(value.Value / 1_000_000m, 2);
        return m.ToString("0.##", Da) + " mio.kr";
    }

    private static int? ToInt(decimal? d) => d.HasValue ? (int)Math.Round(d.Value) : null;
    private static string? JoinSpace(string? a, string? b) => string.Join(" ", new[] { a, b }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
    public static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string? FirstNonBlank(params string?[] values) => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
    /// <summary>Minimal HTML encoding (&amp; &lt; &gt; &quot; &#39;) - keeps æ/ø/å readable (WebUtility encodes them as entities).</summary>
    public static string H(string? s) => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
}
