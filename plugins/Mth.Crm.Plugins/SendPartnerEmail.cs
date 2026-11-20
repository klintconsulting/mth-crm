using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    [DataContract]
    public sealed class EmailAttachment
    {
        [DataMember(Name = "fileName")] public string FileName { get; set; }
        [DataMember(Name = "mimeType")] public string MimeType { get; set; }
        [DataMember(Name = "base64")] public string Base64 { get; set; }
    }

    /// <summary>
    /// Custom API mth_SendPartnerEmail (global action). Sends ONE tender e-mail for ONE collaborating partner
    /// (UE on a building contract) to the partner's selected contacts, from the calling user's mailbox.
    /// The e-mail is regarding the partner record (timeline + reply tracking). After sending:
    /// mth_lastemailsent = today, and status 10 (selected for tender round) -> 24 (awaiting tender).
    /// SendToSelf = true sends a rendered test to the calling user only (no regarding, no status change).
    /// The client calls the API once per partner so one failure does not roll back the others.
    ///
    /// Inputs : PartnerId (Guid), Subject (String), Body (String, HTML), AttachmentsJson (String, optional), SendToSelf (Boolean, optional)
    /// Outputs: Result (String: sent | skipped | test), EmailId (String), Message (String)
    /// </summary>
    public class SendPartnerEmail : IPlugin
    {
        private const int StatusSelected = 273280010;
        private const int StatusAwaitingTender = 273280024;

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var trace = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(context.InitiatingUserId); // send as the user (security + sender)

            var partnerId = Get<Guid>(context, "PartnerId");
            var subjectTemplate = Get<string>(context, "Subject");
            var bodyTemplate = Get<string>(context, "Body");
            var attachmentsJson = Get<string>(context, "AttachmentsJson");
            var sendToSelf = Get<bool>(context, "SendToSelf");
            if (partnerId == Guid.Empty) throw new InvalidPluginExecutionException("PartnerId is required.");
            if (string.IsNullOrWhiteSpace(subjectTemplate)) throw new InvalidPluginExecutionException("Emne mangler.");
            if (string.IsNullOrWhiteSpace(bodyTemplate)) throw new InvalidPluginExecutionException("Mailtekst mangler.");

            var partner = service.Retrieve("mth_collaboratingpartner", partnerId, new ColumnSet("mth_name", "mth_status", "mth_buildingcontractid"));
            var bcRef = partner.GetAttributeValue<EntityReference>("mth_buildingcontractid");
            var bc = service.Retrieve("mth_buildingcontract", bcRef.Id, new ColumnSet("mth_name", "mth_tenderdeadline", "mth_opportunityid"));
            var oppRef = bc.GetAttributeValue<EntityReference>("mth_opportunityid");
            var opp = service.Retrieve("mth_opportunity", oppRef.Id, new ColumnSet("mth_name", "mth_bcprojectnumber"));
            var sender = service.Retrieve("systemuser", context.InitiatingUserId, new ColumnSet("fullname"));
            var contacts = SelectedContacts(service, partnerId);
            trace.Trace("Partner {0}: {1} contact(s) with e-mail", partnerId, contacts.Count);

            var values = new TemplateValues
            {
                Opportunity = opp.GetAttributeValue<string>("mth_name"),
                CaseNumber = opp.GetAttributeValue<string>("mth_bcprojectnumber"),
                BuildingContract = bc.GetAttributeValue<string>("mth_name"),
                TenderDeadline = bc.GetAttributeValue<DateTime?>("mth_tenderdeadline"),
                Partner = partner.GetAttributeValue<string>("mth_name"),
                ContactFirstNames = contacts.Select(c => c.GetAttributeValue<string>("firstname")).ToList(),
                Sender = sender.GetAttributeValue<string>("fullname")
            };
            var subject = EmailTemplateRenderer.RenderSubject(subjectTemplate, values);
            var body = EmailTemplateRenderer.RenderBody(bodyTemplate, values);

            if (!sendToSelf && contacts.Count == 0)
            {
                context.OutputParameters["Result"] = "skipped";
                context.OutputParameters["EmailId"] = string.Empty;
                context.OutputParameters["Message"] = "Ingen valgte kontakter med e-mail.";
                return;
            }

            var email = new Entity("email");
            email["subject"] = sendToSelf ? "[TEST] " + subject : subject;
            email["description"] = sendToSelf
                ? "<p style=\"color:#a4262c\"><b>Testmail.</b> Ville være sendt til: " + System.Net.WebUtility.HtmlEncode(string.Join("; ", contacts.Select(c => c.GetAttributeValue<string>("emailaddress1")))) + "</p>" + body
                : body;
            email["directioncode"] = true;
            email["from"] = Parties(new EntityReference("systemuser", context.InitiatingUserId));
            email["to"] = sendToSelf
                ? Parties(new EntityReference("systemuser", context.InitiatingUserId))
                : Parties(contacts.Select(c => c.ToEntityReference()).ToArray());
            if (!sendToSelf) email["regardingobjectid"] = new EntityReference("mth_collaboratingpartner", partnerId);
            var emailId = service.Create(email);

            foreach (var a in ParseAttachments(attachmentsJson))
            {
                var att = new Entity("activitymimeattachment");
                att["objectid"] = new EntityReference("email", emailId);
                att["objecttypecode"] = "email";
                att["filename"] = a.FileName;
                att["mimetype"] = string.IsNullOrEmpty(a.MimeType) ? "application/octet-stream" : a.MimeType;
                att["body"] = a.Base64;
                service.Create(att);
            }

            var token = ((GetTrackingTokenEmailResponse)service.Execute(new GetTrackingTokenEmailRequest { Subject = subject })).TrackingToken;
            service.Execute(new SendEmailRequest { EmailId = emailId, IssueSend = true, TrackingToken = token ?? string.Empty });

            if (!sendToSelf)
            {
                var update = new Entity("mth_collaboratingpartner", partnerId);
                update["mth_lastemailsent"] = DateTime.Today;
                var status = partner.GetAttributeValue<OptionSetValue>("mth_status");
                if (status != null && status.Value == StatusSelected) update["mth_status"] = new OptionSetValue(StatusAwaitingTender);
                service.Update(update);
            }

            context.OutputParameters["Result"] = sendToSelf ? "test" : "sent";
            context.OutputParameters["EmailId"] = emailId.ToString();
            context.OutputParameters["Message"] = sendToSelf ? "Testmail sendt til dig." : "Sendt til " + contacts.Count + " kontakt(er).";
        }

        private static List<Entity> SelectedContacts(IOrganizationService service, Guid partnerId)
        {
            var q = new QueryExpression("contact") { ColumnSet = new ColumnSet("firstname", "fullname", "emailaddress1") };
            q.Criteria.AddCondition("emailaddress1", ConditionOperator.NotNull);
            q.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);
            var link = q.AddLink("mth_collabpartner_contact", "contactid", "contactid");
            link.LinkCriteria.AddCondition("mth_collaboratingpartnerid", ConditionOperator.Equal, partnerId);
            return service.RetrieveMultiple(q).Entities.ToList();
        }

        private static EntityCollection Parties(params EntityReference[] refs)
        {
            var c = new EntityCollection();
            foreach (var r in refs)
            {
                var p = new Entity("activityparty");
                p["partyid"] = r;
                c.Entities.Add(p);
            }
            return c;
        }

        public static IList<EmailAttachment> ParseAttachments(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<EmailAttachment>();
            using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                var list = (EmailAttachment[])new DataContractJsonSerializer(typeof(EmailAttachment[])).ReadObject(ms);
                return (list ?? new EmailAttachment[0]).Where(a => !string.IsNullOrEmpty(a.FileName) && !string.IsNullOrEmpty(a.Base64)).ToList();
            }
        }

        private static T Get<T>(IPluginExecutionContext c, string name)
        {
            return c.InputParameters.Contains(name) && c.InputParameters[name] is T ? (T)c.InputParameters[name] : default(T);
        }
    }
}
