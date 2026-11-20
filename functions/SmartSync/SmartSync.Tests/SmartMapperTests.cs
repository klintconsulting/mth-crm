using System.Text.Json;
using Mth.SmartSync;
using Xunit;

// Sample JSON is the same shape and values as Salesforce's SMART_ProjectSync_Test (COWI example).
public class SmartMapperTests
{
    private const string Lookups = """
    {"result":{"dict":{"version":100,"language_id":3,"items":[
      {"lookup":"contract_types","items":[{"id":6,"name":"Hovedentreprise","rsm_code":"6"}]},
      {"lookup":"project_stages","items":[{"id":4,"name":"Udførelse","rsm_code":"4"}]},
      {"lookup":"detailed_stages","items":[{"id":15,"name":"Detailprojekt","rsm_code":"15"}]},
      {"lookup":"categories","items":[{"id":23,"name":"Bolig","rsm_code":"23"}]},
      {"lookup":"development_types","items":[{"id":4,"name":"Renovering","rsm_code":"4"}]},
      {"lookup":"company_roles","items":[{"id":20,"name":"Bygherre","rsm_code":"20"}]},
      {"lookup":"company_role_tender","items":[{"id":46,"name":"Hovedentreprenør","rsm_code":"46"}]},
      {"lookup":"company_types","items":[{"id":2,"name":"Kommune","rsm_code":"2"}]},
      {"lookup":"tender_types","items":[{"id":1,"name":"Offentlig","rsm_code":"1"}]}]}}}
    """;

    private const string Project = """
    {"id":555,"source_id":777,"project_name":"Test Projekt","title":"Test titel","description":"Beskrivelse",
     "apartments":115,"floors":3,"underground_floors":1,"gross_area":11000,"usage_area":5000,"gross_volume":20000,
     "contract_type_id":6,"stage_id":4,"detailed_stage_id":15,"country_id":2,"city":"Kongens Lyngby","region_id":22,"district_id":314,
     "postcode":2800,"postcode_text":"2800","street":"Projektvej","street_number":"5",
     "start_date":"2025-08-15T00:00:00Z","start_text":"August 2025","start_date_accuracy_id":2,
     "finish_date":"2027-09-30T00:00:00Z","finish_text":"September 2027","finish_date_accuracy_id":1,
     "months":26,"category_id":23,"dev_type_id":4,"updated_at":"2026-05-28T05:40:28Z","value":97000000,
     "roles":[{"company_role_id":20,"company_id":1001,
        "contacts":[{"contact_card":{"first_name":"Birgitte","last_name":"Hansen","communications":[
            {"communication_type_id":2,"value":"45 22334455","phone_data":{"number":"22334455","calling_code":"45"}},
            {"communication_type_id":4,"value":"bha@cowi.example.com","phone_data":null}]}}],
        "company_role_card":{"id":20,"name":"Bygherre"},
        "company_card":{"id":1001,"name":"COWI A/S","communications":[
            {"communication_type_id":1,"value":"45 56400000","phone_data":{"number":"56400000","calling_code":"45"}},
            {"communication_type_id":4,"value":"cowi@cowi.example.com","phone_data":null}],
            "org_nr":"44623528","par_id":"200104845"}}],
     "cat_dev_type":[{"category_id":23,"dev_type_id":4,"main":true,"priority":1}],
     "updates":[{"update_key":"PROJECT_TENDER_REGISTERED","tender_id":9001,"updated_at":"2026-01-01T00:00:00Z"}]}
    """;

    private const string Tender = """
    {"id":9001,"project_id":555,"description":"Tender desc","content_standard":"Udbudstekst standard","company_role_id":46,"tender_type_id":1,
     "first_day_materials_text":"01-02-2026","last_day_application_text":"01-03-2026","prequalification_date_text":"15-02-2026",
     "archived":0,"deleted":false,"company_role_card":{"id":46,"name":"Hovedentreprenør"},"tender_type_card":{"id":1,"name":"Offentlig"}}
    """;

    private const string Company = """
    {"id":1001,"name":"COWI A/S","company_type_id":2,"city":"Kongens Lyngby","postcode_text":"2800","street":"Parallelvej","street_number":"2",
     "addresses":[{"address_type_id":1,"main":true,"city":"Kongens Lyngby","street":"Parallelvej","street_number":"2","postcode_text":"2800"}]}
    """;

    private static LookupCache Cache() => LookupCache.Build(
        JsonSerializer.Deserialize<SmartLookups>(Lookups, SmartJson.Options)!.Result!.Dict!,
        new[] { new SmartLookupItem { Id = 2, Name = "Danmark", RsmCode = "DK" } },
        new[] { new SmartLookupItem { Id = 22, Name = "Hovedstaden", RsmCode = "22", CountryId = 2 } },
        new[] { new SmartLookupItem { Id = 314, Name = "Lyngby-Taarbæk", RsmCode = "314", RegionId = 22 } });

    private static Dictionary<string, object?> Mapped()
    {
        var p = JsonSerializer.Deserialize<SmartProject>(Project, SmartJson.Options)!;
        var t = JsonSerializer.Deserialize<SmartTender>(Tender, SmartJson.Options)!;
        var c = JsonSerializer.Deserialize<SmartCompany>(Company, SmartJson.Options)!;
        return SmartMapper.Map(p, new[] { t }, new Dictionary<long, SmartCompany> { [c.Id] = c }, Cache());
    }

    [Fact]
    public void Identity_And_Texts()
    {
        var f = Mapped();
        Assert.Equal("Test Projekt", f["mth_name"]);
        Assert.Equal("777", f["mth_projectid"]);
        Assert.Equal("555", f["mth_smartid"]);
        Assert.Equal("https://smart.byggefakta.dk/leads/projects/555", f["mth_smartlink"]);
        Assert.Equal("Test titel", f["mth_projecttitle"]);
        Assert.Equal("Beskrivelse", f["mth_projecttexts"]);
    }

    [Fact]
    public void Client_FromBygherreRole_AddressFromCompanies()
    {
        var f = Mapped();
        Assert.Equal("COWI A/S", f["mth_clientname"]);
        Assert.Equal("44623528", f["mth_clientvatno"]);
        Assert.Equal("200104845", f["mth_clienttdcid"]);
        Assert.Equal("Parallelvej 2", f["mth_clientstreet"]);
        Assert.Equal("2800", f["mth_clientpostalcode"]);
        Assert.Equal("Kongens Lyngby", f["mth_clientcity"]);
        Assert.Equal("Kommune", f["mth_clientcompanytype"]);
    }

    [Fact]
    public void Site_Dates_Value()
    {
        var f = Mapped();
        Assert.Equal("Projektvej 5", f["mth_constructionsite"]);
        Assert.Equal("2800", f["mth_constructionzip"]);
        Assert.Equal("2025-08-01", f["mth_projectstartdate"]); // month accuracy -> 1st
        Assert.Equal("2027-09-30", f["mth_projectenddate"]);   // day accuracy kept
        Assert.Equal(26, f["mth_constructionmonths"]);
        Assert.Equal(97m, f["mth_projectvalue"]);
        Assert.Equal("97 mio.kr", f["mth_projectvaluetext"]);
    }

    [Fact]
    public void Lookups_Translated()
    {
        var f = Mapped();
        Assert.Equal("Hovedentreprise", f["mth_smartcontracttype"]);
        Assert.Equal("6", f["mth_smartcontracttypecode"]);
        Assert.Equal("Detailprojekt", f["mth_planningstage"]);
        Assert.Equal("Udførelse", f["mth_projectstage"]);
        Assert.Equal("Bolig", f["mth_maincategory"]);
        Assert.Equal("Renovering", f["mth_maindevtype"]);
        Assert.Equal("Bolig - Renovering", f["mth_projectcategories"]);
        Assert.Equal("Danmark", f["mth_projectcountry"]);
        Assert.Equal("Hovedstaden", f["mth_projectregion"]);
        Assert.Equal("Lyngby-Taarbæk", f["mth_municipality"]);
    }

    [Fact]
    public void Numbers_AreNumbers()
    {
        var f = Mapped();
        Assert.Equal(115, f["mth_apartments"]);
        Assert.Equal(11000, f["mth_grossfloorarea"]);
        Assert.Equal(3, f["mth_storeys"]);
        Assert.Equal(1, f["mth_storeysbelowground"]);
    }

    [Fact]
    public void RolesHtml_HasCompanyLineAndContact()
    {
        var html = (string)Mapped()["mth_roleshtml"]!;
        Assert.StartsWith("<b>Bygherre</b><br>COWI A/S, Parallelvej 2, 2800, Kongens Lyngby, tel: 56400000, email: cowi@cowi.example.com, cvr: 44623528", html);
        Assert.Contains("Birgitte Hansen, mob: 22334455, email: bha@cowi.example.com", html);
    }

    [Fact]
    public void TendersHtml_FromActiveTender()
    {
        var html = (string)Mapped()["mth_tendershtml"]!;
        Assert.Equal("<b>Hovedentreprenør</b><br>Udsendelses dato: 01-02-2026<br>Licitations dato: 01-03-2026<br>Deadline: 15-02-2026<br>Licitations type: Offentlig<br>Udbudstekst standard", html);
    }

    [Fact]
    public void ActiveTenders_LatestUpdateWins()
    {
        var p = new SmartProject { Updates = {
            new SmartUpdate { Key = "PROJECT_TENDER_REGISTERED", TenderId = 1, UpdatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z") },
            new SmartUpdate { Key = "PROJECT_TENDER_UNREGISTERED", TenderId = 1, UpdatedAt = DateTimeOffset.Parse("2026-02-01T00:00:00Z") },
            new SmartUpdate { Key = "PROJECT_TENDER_REGISTERED", TenderId = 2, UpdatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z") },
            new SmartUpdate { Key = "COMPANY_REGISTERED_TO_PROJECT", TenderId = 3 } } };
        Assert.Equal(new long[] { 2 }, SmartMapper.ActiveTenderIds(p));
    }

    [Fact]
    public void ArchivedTender_IsIgnored_AndFallbackHeadingUsed()
    {
        var p = JsonSerializer.Deserialize<SmartProject>(Project, SmartJson.Options)!;
        var t = JsonSerializer.Deserialize<SmartTender>(Tender, SmartJson.Options)!; t.Archived = 1;
        Assert.Equal("<b>Udbud 9001</b>", SmartMapper.TendersHtml(p, new[] { t }, Cache()));
    }

    [Fact]
    public void ValueText_Danish()
    {
        Assert.Equal("1,5 mio.kr", SmartMapper.ValueText(1_500_000));
        Assert.Equal("97 mio.kr", SmartMapper.ValueText(97_000_000));
        Assert.Null(SmartMapper.ValueText(null));
    }

    [Fact]
    public void LongValues_AreTruncated_NotRejected()
    {
        var p = JsonSerializer.Deserialize<SmartProject>(Project, SmartJson.Options)!;
        p.City = new string('x', 500);
        var f = SmartMapper.Map(p, Array.Empty<SmartTender>(), new Dictionary<long, SmartCompany>(), Cache());
        Assert.Equal(100, ((string)f["mth_constructiontown"]!).Length);
    }

    [Fact]
    public void MissingLookup_GivesReadableFallback()
    {
        var p = JsonSerializer.Deserialize<SmartProject>(Project, SmartJson.Options)!;
        p.CategoryId = 99;
        var f = SmartMapper.Map(p, Array.Empty<SmartTender>(), new Dictionary<long, SmartCompany>(), Cache());
        Assert.Equal("SMART category_id 99", f["mth_maincategory"]);
    }

    [Fact]
    public void GeneratedTestData_Deserializes()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null && !Directory.Exists(Path.Combine(dir, "testdata"))) dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        var src = new FileSmartSource(Path.Combine(dir!, "testdata"));
        var projects = src.GetProjectsAsync(default, default, default).Result;
        Assert.True(projects.Count >= 10);
        Assert.All(projects, p => Assert.NotNull(p.SourceId));
        Assert.Contains(projects, p => p.Roles.Any(r => r.Contacts.Count > 0));
    }
}

public class OpportunityHistoryTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 9, 30, 0);

    [Fact]
    public void Changes_AreLogged_NewestFirst()
    {
        var current = new Dictionary<string, object?> { ["mth_projectvalue"] = 97m, ["mth_planningstage"] = "Projektforslag", ["mth_leadhistory"] = "<b>old block</b>" };
        var smart = new Dictionary<string, object?> { ["mth_projectvalue"] = 120m, ["mth_planningstage"] = "Hovedprojekt" };
        var r = OpportunityHistory.Build(current, smart, statusCode: 20, Now);
        Assert.Equal(2, r.Changes.Count);
        Assert.Contains("<b>Budget (mill)</b> ændret fra <b>\"97\"</b> til <b>\"120\"</b>", r.Changes);
        var half = OpportunityHistory.Build(new Dictionary<string, object?> { ["mth_projectvalue"] = 42m }, new Dictionary<string, object?> { ["mth_projectvalue"] = 52.5m }, 20, Now);
        Assert.Contains("til <b>\"52,5\"</b>", half.Changes[0]);
        var latest = (string)r.Update["mth_leadhistorylatest"]!;
        Assert.StartsWith("<b>20-11-2026 09:30</b><br>", latest);
        Assert.EndsWith("<br><br><b>old block</b>", (string)r.Update["mth_leadhistory"]!);
    }

    [Fact]
    public void Dates_ShownDanish_AndNullEqualsBlank()
    {
        var r = OpportunityHistory.Build(new Dictionary<string, object?> { ["mth_projectstartdate"] = "2027-03-01", ["mth_storeys"] = null },
                                         new Dictionary<string, object?> { ["mth_projectstartdate"] = "2027-05-01", ["mth_storeys"] = null }, 20, Now);
        Assert.Single(r.Changes);
        Assert.Contains("fra <b>\"01-03-2027\"</b> til <b>\"01-05-2027\"</b>", r.Changes[0]);
    }

    [Fact]
    public void NoChanges_NoUpdate()
    {
        var v = new Dictionary<string, object?> { ["mth_projectvalue"] = 97m, ["mth_projectcountry"] = "DK" };
        var r = OpportunityHistory.Build(v, new Dictionary<string, object?> { ["mth_projectvalue"] = 97.00m, ["mth_projectcountry"] = "Danmark" }, 20, Now);
        Assert.Empty(r.Update);
    }

    [Fact]
    public void EarlyStatus_UpdatesOrderDatesAndAmount()
    {
        var smart = new Dictionary<string, object?> { ["mth_projectstartdate"] = "2027-05-01", ["mth_projectvalue"] = 50m, ["mth_projecttexts"] = "Ny tekst" };
        var early = OpportunityHistory.Build(new Dictionary<string, object?>(), smart, statusCode: 5, Now);
        Assert.Equal("2027-03-01", early.Update["mth_closedate"]);
        Assert.Equal(50_000_000m, early.Update["mth_estimatedamount"]);
        Assert.Equal("Ny tekst", early.Update["mth_description"]);
        var late = OpportunityHistory.Build(new Dictionary<string, object?>(), smart, statusCode: 20, Now);
        Assert.False(late.Update.ContainsKey("mth_closedate"));
    }
}
