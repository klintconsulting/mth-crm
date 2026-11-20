using System.Globalization;

namespace Mth.SmartSync;

/// <summary>
/// Updates a converted opportunity from SMART (SMART_ProjectSync.applyLeadToOpportunity):
/// all project columns are copied; changes to the logged fields are written as a history block
/// ("&lt;b&gt;Label&lt;/b&gt; ændret fra &lt;b&gt;"old"&lt;/b&gt; til &lt;b&gt;"new"&lt;/b&gt;"), newest block first.
/// While the opportunity is at status 01/05 (code &lt;= 5) order dates, close date, description and estimated amount follow SMART.
/// </summary>
public static class OpportunityHistory
{
    private static readonly CultureInfo Danish = new("da-DK");
    /// <summary>Label (Danish, as in Salesforce) -> column. "Projekt navn" is logged on the project title because the
    /// opportunity name belongs to the user (decision documented in smart.md).</summary>
    public static readonly (string Label, string Column)[] LoggedFields =
    {
        ("Projekttitel", "mth_projecttitle"), ("Byggestadie", "mth_planningstage"), ("Budget (mill)", "mth_projectvalue"),
        ("Byggestart", "mth_projectstartdate"), ("Slut dato", "mth_projectenddate"), ("Kontrakttype", "mth_smartcontracttype"),
        ("Byggeplads", "mth_constructionsite"), ("Postnr.", "mth_constructionzip"), ("By", "mth_constructiontown"),
        ("Kommune", "mth_municipality"), ("Land", "mth_projectcountry"), ("Kvadratmeter", "mth_grossfloorarea"),
        ("Etager", "mth_storeys"), ("Etager under jorden", "mth_storeysbelowground")
    };

    public sealed record Result(Dictionary<string, object?> Update, List<string> Changes);

    /// <param name="current">Current opportunity values (logical name -> value as read from the Web API).</param>
    /// <param name="smart">Mapped SMART values (SmartMapper.Map).</param>
    public static Result Build(IReadOnlyDictionary<string, object?> current, IReadOnlyDictionary<string, object?> smart, int? statusCode, DateTime nowLocal)
    {
        var update = new Dictionary<string, object?>();
        var changes = new List<string>();
        foreach (var (label, col) in LoggedFields)
        {
            current.TryGetValue(col, out var oldV); smart.TryGetValue(col, out var newV);
            if (Same(oldV, newV, col)) continue;
            changes.Add($"<b>{SmartMapper.H(label)}</b> ændret fra <b>\"{SmartMapper.H(Show(oldV))}\"</b> til <b>\"{SmartMapper.H(Show(newV))}\"</b>");
        }
        // Copy every project column that differs (logged or not); name and description are handled separately.
        foreach (var (col, newV) in smart)
        {
            if (col == "mth_name") continue;
            current.TryGetValue(col, out var oldV);
            if (!Same(oldV, newV, col)) update[col] = newV;
        }
        if (statusCode is int code && code <= 5)
        {
            var start = smart.GetValueOrDefault("mth_projectstartdate") as string;
            var end = smart.GetValueOrDefault("mth_projectenddate") as string;
            SetIfDifferent(current, update, "mth_orderstartdate", start);
            SetIfDifferent(current, update, "mth_orderenddate", end);
            if (start != null && DateTime.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s))
                SetIfDifferent(current, update, "mth_closedate", s.AddMonths(-2).ToString("yyyy-MM-dd"));
            SetIfDifferent(current, update, "mth_description", smart.GetValueOrDefault("mth_projecttexts"));
            if (smart.GetValueOrDefault("mth_projectvalue") is decimal v) SetIfDifferent(current, update, "mth_estimatedamount", v * 1_000_000m);
        }
        if (changes.Count > 0)
        {
            var block = $"<b>{nowLocal:dd-MM-yyyy HH:mm}</b><br>" + string.Join("<br>", changes);
            var previous = current.GetValueOrDefault("mth_leadhistory") as string;
            update["mth_leadhistorylatest"] = block;
            update["mth_leadhistory"] = string.IsNullOrWhiteSpace(previous) ? block : block + "<br><br>" + previous;
        }
        if (update.Count > 0) update["mth_leaddataupdatedon"] = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
        return new Result(update, changes);
    }

    private static void SetIfDifferent(IReadOnlyDictionary<string, object?> current, Dictionary<string, object?> update, string col, object? value)
    {
        if (!Same(current.GetValueOrDefault(col), value, col)) update[col] = value;
    }

    /// <summary>Null and "" are equal; numbers compare as decimals; dates on the date part; Danmark aliases are equal.</summary>
    public static bool Same(object? a, object? b, string col)
    {
        var sa = Norm(a, col); var sb = Norm(b, col);
        return string.Equals(sa, sb, StringComparison.Ordinal);
    }

    private static string Norm(object? v, string col)
    {
        if (v == null) return "";
        if (v is System.Text.Json.JsonElement je) v = je.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => je.GetDecimal(),
            System.Text.Json.JsonValueKind.String => je.GetString(),
            System.Text.Json.JsonValueKind.Null => null,
            _ => je.ToString()
        };
        if (v == null) return "";
        if (v is decimal or int or long or double)
            return Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("0.########", CultureInfo.InvariantCulture);
        var s = Convert.ToString(v, CultureInfo.InvariantCulture)!.Trim();
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) && (col.Contains("value") || col.Contains("area") || col.Contains("storeys") || col.Contains("amount")))
            return d.ToString("0.########", CultureInfo.InvariantCulture);
        if (s.Length >= 10 && s[4] == '-' && s[7] == '-' && (col.Contains("date") || col.EndsWith("on"))) return s[..10];
        if (col == "mth_projectcountry" && s.ToUpperInvariant() is "DK" or "DEN" or "DANMARK" or "DENMARK") return "Danmark";
        return s;
    }

    /// <summary>Display value in the history: dates as dd-MM-yyyy, blanks as empty.</summary>
    private static string Show(object? v)
    {
        var s = v switch
        {
            null => "",
            System.Text.Json.JsonElement je => je.ValueKind == System.Text.Json.JsonValueKind.Null ? "" : je.ToString(),
            decimal d => d.ToString("0.##", Danish),
            _ => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""
        };
        if (s.Length >= 10 && s[4] == '-' && s[7] == '-' && DateTime.TryParseExact(s[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        return s;
    }
}
