using System.Net.Http.Json;
using System.Text.Json;

namespace Mth.SmartSync;

/// <summary>Source of SMART data: the real API or a folder with recorded/synthetic responses in the same JSON format.</summary>
public interface ISmartSource
{
    Task<SmartDictionary> GetLookupsAsync(CancellationToken ct);
    Task<List<SmartLookupItem>> GetGeographyAsync(string kind, CancellationToken ct); // countries | regions | districts
    /// <summary>Projects with updated_at in [from, to] (both inclusive), all pages.</summary>
    Task<List<SmartProject>> GetProjectsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
    Task<List<SmartTender>> GetTendersByIdsAsync(IReadOnlyCollection<long> tenderIds, CancellationToken ct);
    Task<List<SmartTender>> GetTendersByProjectIdsAsync(IReadOnlyCollection<long> projectIds, CancellationToken ct);
    Task<List<SmartCompany>> GetCompaniesAsync(IReadOnlyCollection<long> companyIds, CancellationToken ct);
}

/// <summary>The real SMART API v2.3 (https://api.smart.byggefakta.dk).</summary>
public sealed class HttpSmartSource : ISmartSource
{
    private const int PageSize = 100;
    private const int IdChunk = 75; // same as Salesforce (URL length)
    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private string? _token;

    public HttpSmartSource(HttpClient http, string clientId, string clientSecret)
    {
        _http = http; _clientId = clientId; _clientSecret = clientSecret;
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    private async Task<T> GetAsync<T>(string pathAndQuery, CancellationToken ct)
    {
        _token ??= await AuthenticateAsync(ct);
        using var req = new HttpRequestMessage(HttpMethod.Get, pathAndQuery);
        req.Headers.Authorization = new("Bearer", _token);
        req.Headers.Accept.ParseAdd("application/json");
        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"SMART {pathAndQuery} -> HTTP {(int)res.StatusCode}");
        return (await res.Content.ReadFromJsonAsync<T>(SmartJson.Options, ct))!;
    }

    private async Task<string> AuthenticateAsync(CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("/v2.3/auth", new { client_id = _clientId, client_secret = _clientSecret }, ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        return doc.RootElement.GetProperty("result").GetProperty("opid").GetString() ?? throw new InvalidOperationException("SMART auth: no opid");
    }

    private async Task<List<T>> PagedAsync<T>(string pathWithFilter, CancellationToken ct)
    {
        var all = new List<T>();
        for (var offset = 0; ; offset += PageSize)
        {
            var sep = pathWithFilter.Contains('?') ? "&" : "?";
            var page = await GetAsync<SmartList<T>>($"{pathWithFilter}{sep}offset={offset}&limit={PageSize}", ct);
            var data = page.Result?.Data ?? new List<T>();
            all.AddRange(data);
            if (data.Count == 0 || all.Count >= (page.Result?.Meta?.Total ?? 0)) return all;
        }
    }

    private static string Iso(DateTimeOffset d) => Uri.EscapeDataString(d.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
    private static IEnumerable<List<long>> Chunks(IReadOnlyCollection<long> ids) => ids.Distinct().Chunk(IdChunk).Select(c => c.ToList());

    public async Task<SmartDictionary> GetLookupsAsync(CancellationToken ct) =>
        (await GetAsync<SmartLookups>("/v2.3/lookups?language_id=3", ct)).Result?.Dict ?? new SmartDictionary();

    public Task<List<SmartLookupItem>> GetGeographyAsync(string kind, CancellationToken ct) => PagedAsync<SmartLookupItem>($"/v2.3/{kind}?language_id=3", ct);

    public Task<List<SmartProject>> GetProjectsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        PagedAsync<SmartProject>($"/v2.3/projects?filter=updated_at:gte:{Iso(from)}&filter=updated_at:lte:{Iso(to)}&cards=1&use_original_currency=1", ct);

    public async Task<List<SmartTender>> GetTendersByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var all = new List<SmartTender>();
        foreach (var c in Chunks(ids)) all.AddRange(await PagedAsync<SmartTender>($"/v2.3/tenders?filter=id:in:[{string.Join(",", c)}]&cards=1", ct));
        return all;
    }

    public async Task<List<SmartTender>> GetTendersByProjectIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var all = new List<SmartTender>();
        foreach (var c in Chunks(ids)) all.AddRange(await PagedAsync<SmartTender>($"/v2.3/tenders?filter=project_id:in:[{string.Join(",", c)}]&cards=1", ct));
        return all;
    }

    public async Task<List<SmartCompany>> GetCompaniesAsync(IReadOnlyCollection<long> ids, CancellationToken ct)
    {
        var all = new List<SmartCompany>();
        foreach (var c in Chunks(ids)) all.AddRange(await PagedAsync<SmartCompany>($"/v2.3/companies?filter=id:in:[{string.Join(",", c)}]", ct));
        return all;
    }
}

/// <summary>
/// Test/demo source: reads SMART-format JSON files from a folder (lookups.json, countries.json, regions.json,
/// districts.json, projects.json, tenders.json, companies.json). The updated_at window is ignored, so every
/// project in projects.json is processed on each run (idempotent upserts).
/// </summary>
public sealed class FileSmartSource : ISmartSource
{
    private readonly string _dir;
    public FileSmartSource(string dir) { _dir = dir; }

    private T Read<T>(string file) => JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(_dir, file)), SmartJson.Options)!;
    private List<T> ReadList<T>(string file) => File.Exists(Path.Combine(_dir, file)) ? Read<SmartList<T>>(file).Result?.Data ?? new() : new();

    public Task<SmartDictionary> GetLookupsAsync(CancellationToken ct) => Task.FromResult(Read<SmartLookups>("lookups.json").Result?.Dict ?? new SmartDictionary());
    public Task<List<SmartLookupItem>> GetGeographyAsync(string kind, CancellationToken ct) => Task.FromResult(ReadList<SmartLookupItem>($"{kind}.json"));
    public Task<List<SmartProject>> GetProjectsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct) => Task.FromResult(ReadList<SmartProject>("projects.json"));
    public Task<List<SmartTender>> GetTendersByIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
        Task.FromResult(ReadList<SmartTender>("tenders.json").Where(t => ids.Contains(t.Id)).ToList());
    public Task<List<SmartTender>> GetTendersByProjectIdsAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
        Task.FromResult(ReadList<SmartTender>("tenders.json").Where(t => t.ProjectId.HasValue && ids.Contains(t.ProjectId.Value)).ToList());
    public Task<List<SmartCompany>> GetCompaniesAsync(IReadOnlyCollection<long> ids, CancellationToken ct) =>
        Task.FromResult(ReadList<SmartCompany>("companies.json").Where(c => ids.Contains(c.Id)).ToList());
}
