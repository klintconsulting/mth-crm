using System;
using System.Collections.Generic;
using System.Linq;

namespace Mth.Crm.Plugins
{
    /// <summary>The lead values the queue criteria look at.</summary>
    public sealed class LeadFacts
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Texts { get; set; }
        public string Region { get; set; }
        public decimal? Value { get; set; }
        public string Categories { get; set; }
        public string MainCategory { get; set; }
        public string MainDevType { get; set; }
        public string PlanningStage { get; set; }
        public string ProjectStage { get; set; }
        public string ContractType { get; set; }
        public DateTime? StartDate { get; set; }
    }

    /// <summary>Criteria of one lead queue. Empty criteria are ignored; all filled-in criteria must match.</summary>
    public sealed class QueueCriteria
    {
        public IList<string> Regions { get; set; } = new List<string>();
        public decimal? MinValue { get; set; }
        public decimal? MaxValue { get; set; }
        public string CategoryKeywords { get; set; }
        public string StageKeywords { get; set; }
        public string ContractTypeKeywords { get; set; }
        public string TextKeywords { get; set; }
        public string ExcludeKeywords { get; set; }
        public int? StartWithinMonths { get; set; }
        public bool ExcludeStarted { get; set; }
    }

    /// <summary>
    /// Decides whether a lead belongs in a queue. Pure logic (unit tested).
    /// Keywords are ';'-separated; one hit is enough (case-insensitive substring). Regions match on name
    /// (SMART may say "Region Hovedstaden" or "Hovedstaden"). A filled-in criterion with no lead value does not match.
    /// </summary>
    public static class QueueMatcher
    {
        public static bool Matches(LeadFacts lead, QueueCriteria q, DateTime today)
        {
            if (lead == null || q == null) return false;

            if (q.Regions != null && q.Regions.Count > 0)
            {
                var r = (lead.Region ?? string.Empty).Trim();
                if (r.Length == 0 || !q.Regions.Any(x => !string.IsNullOrWhiteSpace(x) && Contains(r, x.Trim()))) return false;
            }
            if (q.MinValue.HasValue && (!lead.Value.HasValue || lead.Value.Value < q.MinValue.Value)) return false;
            if (q.MaxValue.HasValue && (!lead.Value.HasValue || lead.Value.Value > q.MaxValue.Value)) return false;

            if (!AnyKeyword(q.CategoryKeywords, lead.Categories, lead.MainCategory, lead.MainDevType)) return false;
            if (!AnyKeyword(q.StageKeywords, lead.PlanningStage, lead.ProjectStage)) return false;
            if (!AnyKeyword(q.ContractTypeKeywords, lead.ContractType)) return false;
            if (!AnyKeyword(q.TextKeywords, lead.Name, lead.Title, lead.Texts)) return false;

            var exclude = Keywords(q.ExcludeKeywords);
            if (exclude.Count > 0 && HasAny(exclude, lead.Name, lead.Title, lead.Texts, lead.Categories, lead.MainCategory, lead.MainDevType)) return false;

            var t = today.Date;
            if (q.ExcludeStarted && lead.StartDate.HasValue && lead.StartDate.Value.Date < t) return false;
            if (q.StartWithinMonths.HasValue)
            {
                if (!lead.StartDate.HasValue) return false;
                var s = lead.StartDate.Value.Date;
                if (s < t || s > t.AddMonths(q.StartWithinMonths.Value)) return false;
            }
            return true;
        }

        /// <summary>True if no keywords are given, or any keyword occurs in any of the values.</summary>
        private static bool AnyKeyword(string keywords, params string[] values)
        {
            var k = Keywords(keywords);
            return k.Count == 0 || HasAny(k, values);
        }

        private static bool HasAny(IList<string> keywords, params string[] values) =>
            keywords.Any(k => values.Any(v => !string.IsNullOrEmpty(v) && Contains(v, k)));

        public static IList<string> Keywords(string text) =>
            (text ?? string.Empty).Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

        private static bool Contains(string value, string part) =>
            value.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0; // deterministic in the sandbox; handles æ/ø/å
    }
}
