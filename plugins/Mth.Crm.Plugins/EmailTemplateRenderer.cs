using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Mth.Crm.Plugins
{
    /// <summary>Values available to an ESP e-mail template.</summary>
    public sealed class TemplateValues
    {
        public string Opportunity { get; set; }
        public string CaseNumber { get; set; }
        public string BuildingContract { get; set; }
        public DateTime? TenderDeadline { get; set; }
        public string Partner { get; set; }
        public IList<string> ContactFirstNames { get; set; } = new List<string>();
        public string Sender { get; set; }
    }

    /// <summary>
    /// Replaces {{Placeholder}} tokens in ESP e-mail templates. Pure logic (unit tested).
    /// Unknown placeholders are left untouched so typos are visible in the test mail.
    /// Values are HTML-encoded in the body and plain in the subject.
    /// </summary>
    public static class EmailTemplateRenderer
    {
        private static readonly Regex Token = new Regex(@"\{\{\s*([A-Za-z]+)\s*\}\}", RegexOptions.Compiled);
        private static readonly CultureInfo Danish = new CultureInfo("da-DK");

        public static readonly string[] Placeholders =
            { "Opportunity", "CaseNumber", "BuildingContract", "TenderDeadline", "Partner", "ContactFirstName", "Sender" };

        public static string RenderSubject(string template, TemplateValues v) => Render(template, v, html: false);

        public static string RenderBody(string template, TemplateValues v) => Render(template, v, html: true);

        private static string Render(string template, TemplateValues v, bool html)
        {
            if (string.IsNullOrEmpty(template)) return template ?? string.Empty;
            return Token.Replace(template, m =>
            {
                string value;
                if (!TryGet(m.Groups[1].Value, v, out value)) return m.Value;
                if (string.IsNullOrWhiteSpace(value)) value = "–";
                return html ? HtmlEncode(value) : value;
            });
        }

        /// <summary>
        /// Minimal HTML encoding (only &amp; &lt; &gt; &quot; &#39;). WebUtility.HtmlEncode on .NET Framework also turns
        /// Danish letters (æ, ø, å) into numeric entities, which is valid but unnecessary.
        /// </summary>
        public static string HtmlEncode(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private static bool TryGet(string name, TemplateValues v, out string value)
        {
            switch (name.ToLowerInvariant())
            {
                case "opportunity": value = v.Opportunity ?? string.Empty; return true;
                case "casenumber": value = v.CaseNumber ?? string.Empty; return true;
                case "buildingcontract": value = v.BuildingContract ?? string.Empty; return true;
                case "tenderdeadline": value = v.TenderDeadline.HasValue ? v.TenderDeadline.Value.ToString("d. MMMM yyyy", Danish) : "(ingen frist)"; return true;
                case "partner": value = v.Partner ?? string.Empty; return true;
                case "contactfirstname": value = JoinNames(v.ContactFirstNames); return true;
                case "sender": value = v.Sender ?? string.Empty; return true;
                default: value = null; return false;
            }
        }

        /// <summary>"Anne", "Anne og Bo", "Anne, Bo og Carl". Empty list gives "alle".</summary>
        public static string JoinNames(IList<string> names)
        {
            var n = (names ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
            if (n.Count == 0) return "alle";
            if (n.Count == 1) return n[0];
            return string.Join(", ", n.Take(n.Count - 1)) + " og " + n[n.Count - 1];
        }
    }
}
