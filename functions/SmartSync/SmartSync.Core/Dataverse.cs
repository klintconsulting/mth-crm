using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Mth.SmartSync;

public interface ITokenProvider { Task<string> GetTokenAsync(CancellationToken ct); }

/// <summary>Azure: app registration (client credentials) for the environment's application user.</summary>
public sealed class ClientSecretTokenProvider : ITokenProvider
{
    private readonly HttpClient _http = new();
    private readonly string _tenantId, _clientId, _secret, _scope;
    private string? _token; private DateTimeOffset _expires;
    public ClientSecretTokenProvider(string tenantId, string clientId, string secret, string environmentUrl)
    { _tenantId = tenantId; _clientId = clientId; _secret = secret; _scope = environmentUrl.TrimEnd('/') + "/.default"; }

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token != null && DateTimeOffset.UtcNow < _expires) return _token;
        using var res = await _http.PostAsync($"https://login.microsoftonline.com/{_tenantId}/oauth2/v2.0/token", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["grant_type"] = "client_credentials", ["client_id"] = _clientId, ["client_secret"] = _secret, ["scope"] = _scope }), ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        _token = doc.RootElement.GetProperty("access_token").GetString();
        _expires = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 120);
        return _token!;
    }
}

/// <summary>
/// Local development: reuses the device-code login of scripts/Dataverse.psm1 (token cache in %LOCALAPPDATA%\mth-crm)
/// and refreshes it with the refresh token. Never used in Azure.
/// </summary>
public sealed class DevTokenProvider : ITokenProvider
{
    private const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";
    private readonly HttpClient _http = new();
    private readonly string _url, _cacheFile;
    private string? _token; private DateTimeOffset _expires;

    public DevTokenProvider(string environmentUrl)
    {
        _url = environmentUrl.TrimEnd('/');
        var name = System.Text.RegularExpressions.Regex.Replace(new Uri(_url).Host, "[^a-z0-9]", "_");
        _cacheFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mth-crm", $"dv-token-{name}.json");
    }

    public async Task<string> GetTokenAsync(CancellationToken ct)
    {
        if (_token != null && DateTimeOffset.UtcNow < _expires) return _token;
        if (!File.Exists(_cacheFile)) throw new InvalidOperationException($"No token cache {_cacheFile}. Run Connect-Dataverse (scripts/Dataverse.psm1) first.");
        var cache = JsonNode.Parse(await File.ReadAllTextAsync(_cacheFile, ct))!;
        using var res = await _http.PostAsync("https://login.microsoftonline.com/organizations/oauth2/v2.0/token", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["grant_type"] = "refresh_token", ["client_id"] = ClientId, ["refresh_token"] = cache["refresh_token"]!.GetValue<string>(), ["scope"] = $"{_url}/user_impersonation offline_access" }), ct);
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
        _token = doc.RootElement.GetProperty("access_token").GetString();
        _expires = DateTimeOffset.UtcNow.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32() - 120);
        var newCache = new JsonObject { ["access_token"] = _token, ["refresh_token"] = doc.RootElement.GetProperty("refresh_token").GetString(), ["expires"] = _expires.LocalDateTime.ToString("s") };
        await File.WriteAllTextAsync(_cacheFile, newCache.ToJsonString(), ct);
        return _token!;
    }
}

/// <summary>Thin Dataverse Web API client (JSON) used by the sync.</summary>
public sealed class DataverseClient
{
    private readonly HttpClient _http;
    private readonly ITokenProvider _tokens;

    public DataverseClient(string environmentUrl, ITokenProvider tokens)
    {
        _http = new HttpClient { BaseAddress = new Uri(environmentUrl.TrimEnd('/') + "/api/data/v9.2/"), Timeout = TimeSpan.FromMinutes(2) };
        _tokens = tokens;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct, IDictionary<string, string>? headers = null)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var req = new HttpRequestMessage(method, path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await _tokens.GetTokenAsync(ct));
            req.Headers.Add("OData-MaxVersion", "4.0"); req.Headers.Add("OData-Version", "4.0");
            req.Headers.Accept.ParseAdd("application/json");
            if (headers != null) foreach (var h in headers) req.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (body != null) req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var res = await _http.SendAsync(req, ct);
            // Service protection limits / transient errors: retry with Retry-After
            if (((int)res.StatusCode == 429 || (int)res.StatusCode >= 502) && attempt < 5)
            {
                var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5 * attempt);
                res.Dispose(); await Task.Delay(wait, ct); continue;
            }
            if (!res.IsSuccessStatusCode)
            {
                var text = await res.Content.ReadAsStringAsync(ct);
                res.Dispose();
                throw new InvalidOperationException($"Dataverse {method} {path} -> {(int)res.StatusCode}: {text}");
            }
            return res;
        }
    }

    public async Task<List<JsonObject>> ListAsync(string path, CancellationToken ct)
    {
        var all = new List<JsonObject>();
        string? next = path;
        while (next != null)
        {
            using var res = await SendAsync(HttpMethod.Get, next, null, ct, new Dictionary<string, string> { ["Prefer"] = "odata.maxpagesize=5000" });
            var json = JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!.AsObject();
            foreach (var v in json["value"]!.AsArray()) all.Add(v!.AsObject());
            next = json["@odata.nextLink"]?.GetValue<string>();
        }
        return all;
    }

    public async Task<JsonObject?> GetAsync(string path, CancellationToken ct)
    {
        try { using var res = await SendAsync(HttpMethod.Get, path, null, ct); return JsonNode.Parse(await res.Content.ReadAsStringAsync(ct))!.AsObject(); }
        catch (InvalidOperationException e) when (e.Message.Contains("-> 404")) { return null; }
    }

    public async Task<Guid> CreateAsync(string entitySet, object body, CancellationToken ct)
    {
        using var res = await SendAsync(HttpMethod.Post, entitySet, body, ct);
        var id = res.Headers.TryGetValues("OData-EntityId", out var v) ? v.First() : "";
        var m = System.Text.RegularExpressions.Regex.Match(id, @"\(([0-9a-fA-F-]{36})\)");
        return m.Success ? Guid.Parse(m.Groups[1].Value) : Guid.Empty;
    }

    public async Task PatchAsync(string path, object body, CancellationToken ct) { using var _ = await SendAsync(HttpMethod.Patch, path, body, ct); }
}
