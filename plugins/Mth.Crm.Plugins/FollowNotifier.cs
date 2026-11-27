using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// "Follow" as in Salesforce (EntitySubscription + feed tracking): users who follow a record (rows in mth_follow,
    /// owner = follower) get an in-app notification when
    ///  - a tracked column changes (Update of mth_opportunity, mth_lead, account; asynchronous, pre- and post-image "pre"/"post"), or
    ///  - a note is added to the record (Create of annotation; asynchronous).
    /// The user who made the change is never notified, and a follower who is @mentioned in the note gets only the mention
    /// notification (NoteMentionNotifier). Tracked columns mirror the Salesforce feed tracking on Opportunity, Lead and Account
    /// (retrieved from prod 2026-11-27); keep this list and the step's filtering attributes / images in steps.json in sync.
    /// </summary>
    public class FollowNotifier : IPlugin
    {
        public const string FollowTable = "mth_follow";
        private const int IconInfo = 100000000;
        private const int IconMention = 100000004;
        private const int ToastTimed = 200000000;
        private const int MaxBodyLength = 1000;

        public static readonly IReadOnlyDictionary<string, string[]> Tracked = new Dictionary<string, string[]>
        {
            ["mth_opportunity"] = new[] { "mth_name", "mth_status", "mth_closedate", "mth_estimatedamount", "mth_tenderamount",
                "mth_calculatedcontractamount", "mth_accountid", "ownerid", "mth_orderdepartmentid", "mth_prequalificationdate",
                "mth_externalopportunityid" },
            ["mth_lead"] = new[] { "mth_name", "ownerid", "mth_leadstatus", "mth_projecttitle", "mth_projectvalue", "mth_planningstage",
                "mth_projectstartdate", "mth_smartcontracttype", "mth_constructionsite", "mth_municipality", "mth_projectregion",
                "mth_grossfloorarea", "mth_storeys", "mth_storeysbelowground" },
            ["account"] = new[] { "name", "ownerid", "parentaccountid", "mth_accountcategory", "mth_segment", "mth_subsegment", "mth_accountstatus" },
        };

        public static readonly IReadOnlyDictionary<string, string> PrimaryName = new Dictionary<string, string>
        {
            ["mth_opportunity"] = "mth_name", ["mth_lead"] = "mth_name", ["account"] = "name",
        };

        private static readonly ConcurrentDictionary<string, string> LabelCache = new ConcurrentDictionary<string, string>();
        private static readonly CultureInfo Da = new CultureInfo("da-DK");

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(null); // system context: reads all follows, notifies other users

            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)) return;

            if (context.MessageName == "Create" && context.PrimaryEntityName == "annotation") NotifyNote(service, trace, context, target);
            else if (context.MessageName == "Update" && Tracked.ContainsKey(context.PrimaryEntityName)) NotifyChange(service, trace, context, target);
        }

        private static void NotifyChange(IOrganizationService service, ITracingService trace, IPluginExecutionContext context, Entity target)
        {
            var table = context.PrimaryEntityName;
            var pre = context.PreEntityImages.Contains("pre") ? context.PreEntityImages["pre"] : null;
            var post = context.PostEntityImages.Contains("post") ? context.PostEntityImages["post"] : null;

            var lines = new List<string>();
            foreach (var column in Tracked[table].Where(target.Attributes.Contains))
            {
                var before = Display(pre, column);
                var after = Display(post ?? target, column);
                if (before == after) continue;
                lines.Add(string.Format("{0}: {1} → {2}", Label(service, table, column), before, after));
            }
            if (lines.Count == 0) { trace.Trace("No tracked column changed."); return; }

            var record = new EntityReference(table, context.PrimaryEntityId);
            var followers = Followers(service, record, context.InitiatingUserId);
            trace.Trace("{0} changes, {1} followers", lines.Count, followers.Count);
            if (followers.Count == 0) return;

            var name = (post ?? pre)?.GetAttributeValue<string>(PrimaryName[table]) ?? RecordName(service, record);
            var title = string.Format("{0} ændrede {1}", UserName(service, context.InitiatingUserId), name);
            Notify(service, trace, followers.Select(f => f.Key), record, title, Truncate(string.Join("\n", lines), MaxBodyLength), IconInfo);
        }

        private static void NotifyNote(IOrganizationService service, ITracingService trace, IPluginExecutionContext context, Entity target)
        {
            var regarding = target.GetAttributeValue<EntityReference>("objectid");
            if (regarding == null || !Tracked.ContainsKey(regarding.LogicalName)) return;

            var followers = Followers(service, regarding, context.InitiatingUserId);
            if (followers.Count == 0) { trace.Trace("No followers."); return; }

            var text = MentionParser.ToPlainText(target.GetAttributeValue<string>("notetext"));
            var subject = target.GetAttributeValue<string>("subject");
            var fullText = string.IsNullOrEmpty(subject) ? text : subject + "\n" + text;

            // Followers who are @mentioned already get the mention notification
            var users = service.RetrieveMultiple(new QueryExpression("systemuser")
            {
                ColumnSet = new ColumnSet("fullname", "internalemailaddress"),
                Criteria = { Conditions = { new ConditionExpression("systemuserid", ConditionOperator.In, followers.Keys.Cast<object>().ToArray()) } }
            }).Entities.Select(u => new MentionCandidate(u.Id, u.GetAttributeValue<string>("fullname"), u.GetAttributeValue<string>("internalemailaddress")));
            var mentioned = new HashSet<Guid>(MentionParser.FindMentions(fullText, users).Select(m => m.Id));
            var recipients = followers.Keys.Where(id => !mentioned.Contains(id)).ToList();
            trace.Trace("{0} followers, {1} mentioned", followers.Count, mentioned.Count);
            if (recipients.Count == 0) return;

            var title = string.Format("{0} tilføjede en note på {1}", UserName(service, context.InitiatingUserId), RecordName(service, regarding));
            Notify(service, trace, recipients, regarding, title, MentionParser.Excerpt(fullText, MaxBodyLength), IconMention);
        }

        /// <summary>Active follows of the record, by follower (owner), excluding the given user.</summary>
        public static Dictionary<Guid, Guid> Followers(IOrganizationService service, EntityReference record, Guid exclude)
        {
            var query = new QueryExpression(FollowTable) { ColumnSet = new ColumnSet("ownerid") };
            query.Criteria.AddCondition("mth_recordid", ConditionOperator.Equal, record.Id.ToString("D"));
            query.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            return service.RetrieveMultiple(query).Entities
                .Select(f => new { Follow = f.Id, Owner = f.GetAttributeValue<EntityReference>("ownerid") })
                .Where(f => f.Owner != null && f.Owner.LogicalName == "systemuser" && f.Owner.Id != exclude)
                .GroupBy(f => f.Owner.Id)
                .ToDictionary(g => g.Key, g => g.First().Follow);
        }

        private static void Notify(IOrganizationService service, ITracingService trace, IEnumerable<Guid> users, EntityReference record, string title, string body, int icon)
        {
            var url = string.Format("?pagetype=entityrecord&etn={0}&id={1}", record.LogicalName, record.Id);
            var data = "{\"actions\":[{\"title\":\"Åbn\",\"data\":{\"url\":\"" + url + "\"}}]}";
            foreach (var user in users)
            {
                var n = new Entity("appnotification");
                n["title"] = Truncate(title, 200);
                n["body"] = body;
                n["ownerid"] = new EntityReference("systemuser", user);
                n["icontype"] = new OptionSetValue(icon);
                n["toasttype"] = new OptionSetValue(ToastTimed);
                n["data"] = data;
                service.Create(n);
            }
            trace.Trace("Notified {0} followers", users.Count());
        }

        /// <summary>Human-readable value of a column in an image: formatted value, lookup name or plain value.</summary>
        public static string Display(Entity entity, string column)
        {
            if (entity == null || !entity.Attributes.Contains(column) || entity[column] == null) return "–";
            // Amounts in Danish format without decimals ("314.000.000 kr."); the formatted value follows the system user's locale
            if (entity[column] is Money money) return money.Value.ToString("N0", Da) + " kr.";
            if (entity.FormattedValues.Contains(column) && !string.IsNullOrEmpty(entity.FormattedValues[column])) return entity.FormattedValues[column];
            var value = entity[column];
            switch (value)
            {
                case EntityReference r: return string.IsNullOrEmpty(r.Name) ? r.Id.ToString() : r.Name;
                case decimal d: return d.ToString("N0", Da);
                case DateTime t: return t.ToString("d. MMM yyyy", Da);
                case OptionSetValue o: return o.Value.ToString(CultureInfo.InvariantCulture);
                case bool b: return b ? "Ja" : "Nej";
                default: return Convert.ToString(value, Da);
            }
        }

        private static string Label(IOrganizationService service, string table, string column)
        {
            return LabelCache.GetOrAdd(table + "." + column, _ =>
            {
                try
                {
                    var meta = ((RetrieveAttributeResponse)service.Execute(new RetrieveAttributeRequest { EntityLogicalName = table, LogicalName = column })).AttributeMetadata;
                    return meta.DisplayName?.UserLocalizedLabel?.Label ?? column;
                }
                catch (Exception) { return column; }
            });
        }

        private static string UserName(IOrganizationService service, Guid userId)
        {
            return service.Retrieve("systemuser", userId, new ColumnSet("fullname")).GetAttributeValue<string>("fullname");
        }

        private static string RecordName(IOrganizationService service, EntityReference record)
        {
            try
            {
                var column = PrimaryName[record.LogicalName];
                return service.Retrieve(record.LogicalName, record.Id, new ColumnSet(column)).GetAttributeValue<string>(column);
            }
            catch (Exception) { return string.Empty; }
        }

        private static string Truncate(string s, int max) => s == null || s.Length <= max ? s : s.Substring(0, max - 1) + "…";
    }
}
