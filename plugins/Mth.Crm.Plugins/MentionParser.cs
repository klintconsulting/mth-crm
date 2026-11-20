using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Mth.Crm.Plugins
{
    /// <summary>A user that can be mentioned.</summary>
    public sealed class MentionCandidate
    {
        public MentionCandidate(Guid id, string fullName, string email)
        {
            Id = id;
            FullName = fullName;
            Email = email;
        }

        public Guid Id { get; }
        public string FullName { get; }
        public string Email { get; }
    }

    /// <summary>
    /// Finds @mentions in note text. Pure logic without Dataverse dependencies so it can be unit tested.
    /// Supported forms: "@Full Name" and "@alias", where alias is the part of the user's e-mail before '@'
    /// (at MTH typically the initials). Matching is case-insensitive, and a mention must not be followed
    /// by a letter or digit ("@Anne" does not match "@Annette").
    /// </summary>
    public static class MentionParser
    {
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LineBreaks = new Regex(@"<\s*(br|/p|/div|/li)\s*/?>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex Spaces = new Regex(@"[ \t ]+", RegexOptions.Compiled);

        /// <summary>Converts rich-text (HTML) note content to plain text.</summary>
        public static string ToPlainText(string html)
        {
            if (string.IsNullOrEmpty(html)) return string.Empty;
            var text = LineBreaks.Replace(html, "\n");
            text = Tags.Replace(text, string.Empty);
            text = WebUtility.HtmlDecode(text);
            return Spaces.Replace(text, " ").Trim();
        }

        /// <summary>Returns the distinct candidates mentioned in the text.</summary>
        public static IList<MentionCandidate> FindMentions(string plainText, IEnumerable<MentionCandidate> candidates)
        {
            var result = new List<MentionCandidate>();
            if (string.IsNullOrEmpty(plainText) || plainText.IndexOf('@') < 0 || candidates == null) return result;

            foreach (var c in candidates)
            {
                if (result.Any(r => r.Id == c.Id)) continue;
                foreach (var key in Keys(c))
                {
                    var pattern = "(?<![\\p{L}\\p{Nd}._-])@" + Regex.Escape(key) + "(?![\\p{L}\\p{Nd}])";
                    if (Regex.IsMatch(plainText, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    {
                        result.Add(c);
                        break;
                    }
                }
            }
            return result;
        }

        private static IEnumerable<string> Keys(MentionCandidate c)
        {
            if (!string.IsNullOrWhiteSpace(c.FullName)) yield return Spaces.Replace(c.FullName.Trim(), " ");
            if (!string.IsNullOrWhiteSpace(c.Email))
            {
                var at = c.Email.IndexOf('@');
                if (at > 0) yield return c.Email.Substring(0, at);
            }
        }

        /// <summary>Shortens text for a notification body.</summary>
        public static string Excerpt(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxLength) return text ?? string.Empty;
            return text.Substring(0, maxLength - 1).TrimEnd() + "…";
        }
    }
}
