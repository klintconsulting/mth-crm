namespace Mth.SmartSync;

/// <summary>One translated SMART dictionary entry (mth_smartlookup).</summary>
public sealed record LookupEntry(string Lookup, long Id, string? Name, string? RsmCode, long? ParentId)
{
    public string Key => $"{Lookup}:{Id}";
}

/// <summary>
/// In-memory SMART dictionaries: id -> name/code. Built from /lookups (nested items flattened, children keep the
/// lookup name) plus countries/regions/districts. Mirrors SMART_ProjectSync.resolveLookupName/getRsmCode.
/// </summary>
public sealed class LookupCache
{
    private readonly Dictionary<string, LookupEntry> _map = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<LookupEntry> Entries => _map.Values;

    public static LookupCache Build(SmartDictionary dict, IEnumerable<SmartLookupItem> countries, IEnumerable<SmartLookupItem> regions, IEnumerable<SmartLookupItem> districts)
    {
        var c = new LookupCache();
        foreach (var g in dict.Items) Flatten(c, g.Lookup, g.Items, null);
        foreach (var x in countries) c.Add(new LookupEntry("countries", x.Id, x.Name, x.RsmCode, null));
        foreach (var x in regions) c.Add(new LookupEntry("regions", x.Id, x.Name, x.RsmCode, x.CountryId));
        foreach (var x in districts) c.Add(new LookupEntry("districts", x.Id, x.Name, x.RsmCode, x.RegionId));
        return c;
    }

    public static LookupCache FromEntries(IEnumerable<LookupEntry> entries)
    {
        var c = new LookupCache();
        foreach (var e in entries) c.Add(e);
        return c;
    }

    private static void Flatten(LookupCache c, string lookup, List<SmartLookupItem>? items, long? parent)
    {
        if (items == null) return;
        foreach (var i in items)
        {
            c.Add(new LookupEntry(lookup, i.Id, i.Name, i.RsmCode, parent));
            Flatten(c, lookup, i.Items, i.Id);
        }
    }

    private void Add(LookupEntry e) => _map[e.Key] = e;

    public LookupEntry? Get(string lookup, long? id) => id.HasValue && _map.TryGetValue($"{lookup}:{id}", out var e) ? e : null;

    /// <summary>Name, or a readable fallback ("SMART category_id 23") so a missing dictionary entry is visible.</summary>
    public string? Name(string lookup, long? id, string? fallbackPrefix = null)
    {
        if (!id.HasValue) return null;
        var e = Get(lookup, id);
        if (!string.IsNullOrWhiteSpace(e?.Name)) return e!.Name;
        return fallbackPrefix == null ? null : $"SMART {fallbackPrefix} {id}";
    }

    /// <summary>RSM code (legacy FaktaNet code), else the SMART id.</summary>
    public string? Code(string lookup, long? id)
    {
        if (!id.HasValue) return null;
        var rsm = Get(lookup, id)?.RsmCode;
        return string.IsNullOrWhiteSpace(rsm) ? id.Value.ToString() : rsm;
    }

    /// <summary>DK/DEN -> "Danmark", else the RSM code or name (SMART_ProjectSync.resolveCountryCode).</summary>
    public string? Country(long? id)
    {
        if (!id.HasValue) return null;
        var e = Get("countries", id);
        if (e == null) return $"SMART country_id {id}";
        var rsm = (e.RsmCode ?? "").Trim().ToUpperInvariant();
        if (rsm is "DK" or "DEN") return "Danmark";
        return string.IsNullOrWhiteSpace(e.RsmCode) ? e.Name : e.RsmCode;
    }
}
