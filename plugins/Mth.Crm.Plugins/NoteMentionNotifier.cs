using System;
using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Sends an in-app notification to every user mentioned with @ in a new note (annotation).
    /// Registration: message Create, table annotation, stage PostOperation (40), asynchronous.
    /// Requires in-app notifications to be enabled for the app (setting AppNotificationPolling).
    /// </summary>
    public class NoteMentionNotifier : IPlugin
    {
        // appnotification.icontype = Mention, toasttype = Timed
        private const int IconMention = 100000004;
        private const int ToastTimed = 200000000;
        private const int MaxBodyLength = 300;

        private static readonly ConcurrentDictionary<string, string> PrimaryNameCache = new ConcurrentDictionary<string, string>();

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(null); // system context: must be able to read users and create notifications for others

            if (context.MessageName != "Create" || context.PrimaryEntityName != "annotation") return;
            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity target)) return;

            var text = MentionParser.ToPlainText(target.GetAttributeValue<string>("notetext"));
            var subject = target.GetAttributeValue<string>("subject");
            var fullText = string.IsNullOrEmpty(subject) ? text : subject + "\n" + text;
            if (fullText.IndexOf('@') < 0) { trace.Trace("No @ in note."); return; }

            var regarding = target.GetAttributeValue<EntityReference>("objectid");
            if (regarding == null) { trace.Trace("Note has no regarding record."); return; }

            // Active, interactive users
            var query = new QueryExpression("systemuser") { ColumnSet = new ColumnSet("fullname", "internalemailaddress") };
            query.Criteria.AddCondition("isdisabled", ConditionOperator.Equal, false);
            query.Criteria.AddCondition("accessmode", ConditionOperator.Equal, 0);
            var candidates = service.RetrieveMultiple(query).Entities
                .Select(u => new MentionCandidate(u.Id, u.GetAttributeValue<string>("fullname"), u.GetAttributeValue<string>("internalemailaddress")));

            var authorId = context.InitiatingUserId;
            var mentioned = MentionParser.FindMentions(fullText, candidates).Where(m => m.Id != authorId).ToList();
            trace.Trace("Mentioned users (excluding author): {0}", mentioned.Count);
            if (mentioned.Count == 0) return;

            var author = service.Retrieve("systemuser", authorId, new ColumnSet("fullname")).GetAttributeValue<string>("fullname");
            var recordName = GetRecordName(service, regarding);
            var url = string.Format("?pagetype=entityrecord&etn={0}&id={1}", regarding.LogicalName, regarding.Id);
            var data = "{\"actions\":[{\"title\":\"Åbn\",\"data\":{\"url\":\"" + url + "\"}}]}";

            foreach (var user in mentioned)
            {
                var notification = new Entity("appnotification");
                notification["title"] = string.Format("{0} nævnte dig i en note", author);
                notification["body"] = (string.IsNullOrEmpty(recordName) ? string.Empty : recordName + ": ") + MentionParser.Excerpt(text, MaxBodyLength);
                notification["ownerid"] = new EntityReference("systemuser", user.Id);
                notification["icontype"] = new OptionSetValue(IconMention);
                notification["toasttype"] = new OptionSetValue(ToastTimed);
                notification["data"] = data;
                service.Create(notification);
                trace.Trace("Notified {0}", user.FullName);
            }
        }

        private static string GetRecordName(IOrganizationService service, EntityReference record)
        {
            try
            {
                var nameAttribute = PrimaryNameCache.GetOrAdd(record.LogicalName, table =>
                {
                    var response = (RetrieveEntityResponse)service.Execute(new RetrieveEntityRequest { LogicalName = table, EntityFilters = EntityFilters.Entity });
                    return response.EntityMetadata.PrimaryNameAttribute;
                });
                if (string.IsNullOrEmpty(nameAttribute)) return null;
                return service.Retrieve(record.LogicalName, record.Id, new ColumnSet(nameAttribute)).GetAttributeValue<string>(nameAttribute);
            }
            catch (Exception)
            {
                return null; // the notification is still useful without the record name
            }
        }
    }
}
