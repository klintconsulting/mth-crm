using System.Globalization;
using System.Text.Json.Nodes;

namespace Mth.SmartSync;

public sealed class SyncOptions
{
    public bool DryRun { get; set; }
    /// <summary>Process at most this many projects per page (tenders/companies are fetched per page).</summary>
    public int PageSize { get; set; } = 50;
    public string StateName { get; set; } = "SMART Projects";
    public DateTimeOffset? Now { get; set; }
}

public sealed class SyncResult
{
    public int Projects, Created, Updated, Unchanged, OpportunitiesUpdated, HistoryEntries, Skipped, Failed, LookupsWritten;
    public DateTimeOffset From, To, NewWatermark;
    public List<string> Errors { get; } = new();
    public override string ToString() =>
        $"Window {From:yyyy-MM-dd HH:mm}–{To:yyyy-MM-dd HH:mm} UTC: {Projects} projects, {Created} leads created, {Updated} updated, {Unchanged} unchanged, " +
        $"{OpportunitiesUpdated} opportunities updated ({HistoryEntries} with history), {Skipped} skipped, {Failed} failed, {LookupsWritten} lookups written. " +
        $"Watermark -> {NewWatermark:yyyy-MM-dd HH:mm:ss} UTC." + (Errors.Count > 0 ? " Errors: " + string.Join(" | ", Errors.Take(10)) : "");
}

/// <summary>
/// One SMART sync run (replaces the Salesforce batch SMART_ProjectSync):
/// 1. watermark from mth_integrationstate, 2. dictionaries (written only when changed), 3. projects updated since the
/// watermark, per page tenders + companies, 4. converted project -> update the opportunity with history; open lead -> update
/// changed fields; new project -> create an open lead (the queue plugin then places it in queues), 5. watermark advances to
/// the run start, or to the earliest failed project's updated_at so failures are retried next run.
/// </summary>
public sealed class SyncRunner
{
    private const int LeadOpen = 273280000;
    private const int OppTypeExternal = 273280001;
    private const int OptionBase = 273280000;
    private readonly ISmartSource _smart;
    private readonly DataverseClient _dv;
    private readonly Action<string> _log;

    public SyncRunner(ISmartSource smart, DataverseClient dv, Action<string> log) { _smart = smart; _dv = dv; _log = log; }

    /// <summary>Danish wall-clock time for the history text (the Azure host runs in UTC). IANA id works on Linux and on Windows (.NET 6+).</summary>
    internal static DateTime DanishNow() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Copenhagen"));

    public async Task<SyncResult> RunAsync(SyncOptions o, CancellationToken ct = default)
    {
        var r = new SyncResult();
        var now = (o.Now ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var state = await _dv.GetAsync($"mth_integrationstates(mth_name='{o.StateName}')?$select=mth_lastrunat,mth_lookupversion", ct);
        var from = state?["mth_lastrunat"] is JsonNode lr ? DateTimeOffset.Parse(lr.GetValue<string>(), CultureInfo.InvariantCulture) : now.AddDays(-1);
        r.From = from; r.To = now; r.NewWatermark = now;
        _log($"Window {from:u} – {now:u}{(o.DryRun ? " (dry run)" : "")}");

        // ---- dictionaries ----
        var dict = await _smart.GetLookupsAsync(ct);
        var lookups = LookupCache.Build(dict, await _smart.GetGeographyAsync("countries", ct), await _smart.GetGeographyAsync("regions", ct), await _smart.GetGeographyAsync("districts", ct));
        r.LookupsWritten = await PersistLookupsAsync(lookups, o.DryRun, ct);
        _log($"Lookups: {lookups.Entries.Count} entries, {r.LookupsWritten} written");

        // ---- projects ----
        var projects = (await _smart.GetProjectsAsync(from, now, ct)).Where(p => p.SourceId.HasValue).ToList();
        r.Projects = projects.Count;
        var failedAt = new List<DateTimeOffset>();
        foreach (var page in projects.Chunk(o.PageSize))
        {
            try { await ProcessPageAsync(page, lookups, o, r, failedAt, ct); }
            catch (Exception e)
            {
                r.Failed += page.Length; r.Errors.Add("Page: " + e.Message);
                failedAt.AddRange(page.Select(p => p.UpdatedAt ?? from));
            }
        }
        if (failedAt.Count > 0) r.NewWatermark = failedAt.Min().AddSeconds(-1);
        if (r.NewWatermark < from) r.NewWatermark = from;

        if (!o.DryRun)
        {
            var body = new Dictionary<string, object?>
            {
                ["mth_lastrunat"] = r.NewWatermark.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), ["mth_lookupversion"] = dict.Version.ToString(),
                ["mth_lastresult"] = Trunc(r.ToString(), 20000), ["mth_lastfinishedon"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'")
            };
            await _dv.PatchAsync($"mth_integrationstates(mth_name='{o.StateName}')", body, ct); // upsert on alternate key
        }
        _log(r.ToString());
        return r;
    }

    private async Task ProcessPageAsync(SmartProject[] page, LookupCache lc, SyncOptions o, SyncResult r, List<DateTimeOffset> failedAt, CancellationToken ct)
    {
        // tenders: active ids from updates[], fallback by project id for projects without any
        var activeByProject = page.ToDictionary(p => p.Id, SmartMapper.ActiveTenderIds);
        var tenderIds = activeByProject.Values.SelectMany(x => x).Distinct().ToList();
        var tenders = tenderIds.Count > 0 ? await _smart.GetTendersByIdsAsync(tenderIds, ct) : new List<SmartTender>();
        var noActive = page.Where(p => activeByProject[p.Id].Count == 0).Select(p => p.Id).ToList();
        var fallback = noActive.Count > 0 ? await _smart.GetTendersByProjectIdsAsync(noActive, ct) : new List<SmartTender>();
        var companyIds = page.SelectMany(p => p.Roles).Where(x => x.CompanyId.HasValue).Select(x => x.CompanyId!.Value).Distinct().ToList();
        var companies = (companyIds.Count > 0 ? await _smart.GetCompaniesAsync(companyIds, ct) : new List<SmartCompany>()).GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());

        // existing leads and converted opportunities for this page
        var ids = string.Join(",", page.Select(p => $"'{p.SourceId}'"));
        var select = string.Join(",", SmartMapper.MaxLength.Keys.Concat(OtherProjectColumns).Distinct());
        var leads = (await _dv.ListAsync($"mth_leads?$select={select},mth_leadstatus&$filter=Microsoft.Dynamics.CRM.In(PropertyName='mth_projectid',PropertyValues=[{ids}])", ct))
            .GroupBy(x => x["mth_projectid"]!.GetValue<string>()).ToDictionary(g => g.Key, g => g.First());
        var opps = (await _dv.ListAsync($"mth_opportunities?$select={select},mth_leadhistory,mth_status,mth_orderstartdate,mth_orderenddate,mth_closedate,mth_description,mth_estimatedamount&$filter=Microsoft.Dynamics.CRM.In(PropertyName='mth_projectid',PropertyValues=[{ids}]) and mth_opportunitytype eq {OppTypeExternal} and statecode eq 0", ct))
            .GroupBy(x => x["mth_projectid"]!.GetValue<string>()).ToDictionary(g => g.Key, g => g.First());

        foreach (var p in page)
        {
            var key = p.SourceId!.Value.ToString(CultureInfo.InvariantCulture);
            try
            {
                var projectTenders = tenders.Where(t => activeByProject[p.Id].Contains(t.Id) || t.ProjectId == p.Id).Concat(fallback.Where(t => t.ProjectId == p.Id)).ToList();
                var fields = SmartMapper.Map(p, projectTenders, companies, lc);

                if (opps.TryGetValue(key, out var opp))
                {
                    var current = Plain(opp);
                    var status = current.GetValueOrDefault("mth_status") is decimal s ? (int)s - OptionBase : (int?)null;
                    var h = OpportunityHistory.Build(current, fields, status, DanishNow());
                    if (h.Update.Count == 0) { r.Unchanged++; continue; }
                    if (!o.DryRun) await _dv.PatchAsync($"mth_opportunities({opp["mth_opportunityid"]})", h.Update, ct);
                    r.OpportunitiesUpdated++; if (h.Changes.Count > 0) r.HistoryEntries++;
                    _log($"  opp  {key} {fields["mth_name"]}: {h.Update.Count} column(s), {h.Changes.Count} logged change(s)");
                }
                else if (leads.TryGetValue(key, out var lead))
                {
                    var current = Plain(lead);
                    if (current.GetValueOrDefault("mth_leadstatus") is decimal ls && (int)ls != LeadOpen) { r.Skipped++; continue; } // closed lead without opportunity
                    var changed = fields.Where(kv => !OpportunityHistory.Same(current.GetValueOrDefault(kv.Key), kv.Value, kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);
                    if (changed.Count == 0) { r.Unchanged++; continue; }
                    if (!o.DryRun) await _dv.PatchAsync($"mth_leads({lead["mth_leadid"]})", changed, ct);
                    r.Updated++;
                    _log($"  lead {key} {fields["mth_name"]}: {changed.Count} changed column(s)");
                }
                else
                {
                    fields["mth_leadstatus"] = LeadOpen;
                    if (!o.DryRun) await _dv.CreateAsync("mth_leads", fields, ct);
                    r.Created++;
                    _log($"  new  {key} {fields["mth_name"]} ({fields["mth_projectregion"]}, {fields["mth_projectvalue"]} mio.)");
                }
            }
            catch (Exception e)
            {
                r.Failed++; r.Errors.Add($"{key}: {e.Message}");
                failedAt.Add(p.UpdatedAt ?? r.From);
            }
        }
    }

    /// <summary>Project columns that are not strings (and therefore not in MaxLength).</summary>
    private static readonly string[] OtherProjectColumns =
    {
        "mth_projectstartdate", "mth_projectenddate", "mth_constructionmonths", "mth_projectvalue", "mth_apartments", "mth_grossfloorarea",
        "mth_netfloorarea", "mth_volume", "mth_storeys", "mth_storeysbelowground", "mth_smartupdatedon"
    };

    private async Task<int> PersistLookupsAsync(LookupCache lc, bool dryRun, CancellationToken ct)
    {
        var existing = (await _dv.ListAsync("mth_smartlookups?$select=mth_key,mth_smartname,mth_rsmcode,mth_parentsmartid", ct))
            .ToDictionary(x => x["mth_key"]?.GetValue<string>() ?? "", x => x);
        var written = 0;
        foreach (var e in lc.Entries)
        {
            var name = Trunc(e.Name, 500); var parent = e.ParentId?.ToString(CultureInfo.InvariantCulture);
            if (existing.TryGetValue(e.Key, out var x) && x["mth_smartname"]?.GetValue<string>() == name
                && x["mth_rsmcode"]?.GetValue<string>() == e.RsmCode && x["mth_parentsmartid"]?.GetValue<string>() == parent) continue;
            if (!dryRun)
                await _dv.PatchAsync($"mth_smartlookups(mth_key='{e.Key.Replace("'", "''")}')", new Dictionary<string, object?>
                {
                    ["mth_name"] = e.Key, ["mth_lookup"] = e.Lookup, ["mth_smartitemid"] = e.Id.ToString(CultureInfo.InvariantCulture),
                    ["mth_smartname"] = name, ["mth_rsmcode"] = e.RsmCode, ["mth_parentsmartid"] = parent
                }, ct);
            written++;
        }
        return written;
    }

    /// <summary>JSON object from the Web API -> plain values (string / decimal / bool / null).</summary>
    public static Dictionary<string, object?> Plain(JsonObject o)
    {
        var d = new Dictionary<string, object?>();
        foreach (var (k, v) in o)
        {
            if (k.Contains('@')) continue;
            d[k] = v switch
            {
                null => null,
                JsonValue jv when jv.TryGetValue<decimal>(out var dec) => dec,
                JsonValue jv when jv.TryGetValue<bool>(out var b) => b,
                JsonValue jv when jv.TryGetValue<string>(out var s) => s,
                _ => v.ToJsonString()
            };
        }
        return d;
    }

    private static string? Trunc(string? s, int max) => s == null || s.Length <= max ? s : s[..max];
}
