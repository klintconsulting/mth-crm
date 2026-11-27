using System;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Mth.Crm.Plugins
{
    /// <summary>
    /// Defaults for resource planning (Resource Overview, docs/system/resources.md). PreOperation (20), synchronous.
    ///  - mth_opportunityrole Create/Update (mth_role, mth_opportunityid): Start/End Date from the opportunity when empty,
    ///    Allocation 100 % when empty. Tender roles get the tender phase, the others the order (execution) period.
    ///  - mth_availability Create/Update: name "Vacation – Lars Nielsen 01-07-2027 – 14-07-2027", Capacity 0 % for
    ///    vacation/leave/other when empty, Capacity required for reduced capacity.
    /// Both: End Date may not be before Start Date.
    /// </summary>
    public class ResourceDefaults : IPlugin
    {
        public const int TenderManager = 273280000, ProjectManager = 273280001, TenderResponsible = 273280002, TenderCoordinator = 273280003,
            TenderCalculator = 273280004, PreQualificationResponsible = 273280005, BidManager = 273280006, DesignManager = 273280007,
            GuaranteeResponsible = 273280008, PrimaryResponsible = 273280009;
        public const int Vacation = 273280000, Leave = 273280001, Reduced = 273280002, OtherAbsence = 273280003;

        private static readonly CultureInfo Da = new CultureInfo("da-DK");

        public void Execute(IServiceProvider serviceProvider)
        {
            var context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            var factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            var service = factory.CreateOrganizationService(null);
            if (!(context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)) return;

            // Current values on Update (target only holds the changed columns)
            Entity current = null;
            if (context.MessageName == "Update")
                current = service.Retrieve(target.LogicalName, target.Id, new ColumnSet(true));
            Func<string, object> get = column => target.Attributes.Contains(column) ? target[column] : current?.Attributes.Contains(column) == true ? current[column] : null;

            if (target.LogicalName == "mth_opportunityrole") RoleDefaults(service, target, get);
            else if (target.LogicalName == "mth_availability") AvailabilityDefaults(service, target, get);

            var start = get("mth_startdate") as DateTime?;
            var end = get("mth_enddate") as DateTime?;
            if (start.HasValue && end.HasValue && end.Value.Date < start.Value.Date)
                throw new InvalidPluginExecutionException("Slutdatoen må ikke ligge før startdatoen. / End Date cannot be before Start Date.");
        }

        private static readonly System.Collections.Generic.Dictionary<int, string> RoleNames = new System.Collections.Generic.Dictionary<int, string>
        {
            [TenderManager] = "Tender Manager", [ProjectManager] = "Project Manager", [TenderResponsible] = "Tender Responsible",
            [TenderCoordinator] = "Tender Coordinator", [TenderCalculator] = "Tender Calculator", [PreQualificationResponsible] = "Pre-Qualification Responsible",
            [BidManager] = "Bid Manager", [DesignManager] = "Design Manager", [GuaranteeResponsible] = "Guarantee Responsible", [PrimaryResponsible] = "Primary Responsible",
        };

        private static void RoleDefaults(IOrganizationService service, Entity target, Func<string, object> get)
        {
            if (get("mth_allocation") == null) target["mth_allocation"] = 100;

            // Name "Tender Calculator – Lars Nielsen" (rows created from the Resource Overview have no name)
            var roleValue = (get("mth_role") as OptionSetValue)?.Value;
            var userRef = get("mth_userid") as EntityReference;
            if (roleValue.HasValue && userRef != null && (target.Contains("mth_role") || target.Contains("mth_userid") || string.IsNullOrEmpty(get("mth_name") as string)))
            {
                var fullname = service.Retrieve("systemuser", userRef.Id, new ColumnSet("fullname")).GetAttributeValue<string>("fullname");
                target["mth_name"] = (RoleNames.TryGetValue(roleValue.Value, out var label) ? label : "Role") + " – " + fullname;
            }
            if (get("mth_startdate") != null && get("mth_enddate") != null) return;

            var opportunity = get("mth_opportunityid") as EntityReference;
            var role = (get("mth_role") as OptionSetValue)?.Value;
            if (opportunity == null || role == null) return;

            var o = service.Retrieve("mth_opportunity", opportunity.Id, new ColumnSet("mth_prequalificationdate", "mth_invitationtotenderdate",
                "mth_tenderstartdate", "mth_tenderdate", "mth_initialtenderdate", "mth_orderstartdate", "mth_orderenddate", "mth_projectstartdate", "mth_projectenddate"));
            var period = DefaultPeriod(role.Value, o);
            if (get("mth_startdate") == null && period.Item1.HasValue) target["mth_startdate"] = period.Item1.Value;
            if (get("mth_enddate") == null && period.Item2.HasValue) target["mth_enddate"] = period.Item2.Value;
        }

        /// <summary>Default period of a role from the opportunity's dates. Public for unit tests.</summary>
        public static Tuple<DateTime?, DateTime?> DefaultPeriod(int role, Entity o)
        {
            Func<string, DateTime?> d = column => o.GetAttributeValue<DateTime?>(column);
            DateTime? start, end;
            switch (role)
            {
                case PreQualificationResponsible:
                    end = d("mth_prequalificationdate") ?? d("mth_invitationtotenderdate");
                    start = end?.AddDays(-21);
                    break;
                case TenderManager: case TenderResponsible: case TenderCoordinator: case TenderCalculator: case BidManager:
                    start = d("mth_invitationtotenderdate") ?? d("mth_tenderstartdate");
                    end = d("mth_tenderdate") ?? d("mth_initialtenderdate") ?? start?.AddDays(28);
                    break;
                case PrimaryResponsible:
                    start = d("mth_invitationtotenderdate") ?? d("mth_orderstartdate") ?? d("mth_projectstartdate");
                    end = d("mth_orderenddate") ?? d("mth_projectenddate") ?? d("mth_tenderdate");
                    break;
                default: // Project Manager, Design Manager, Guarantee Responsible: the execution period
                    start = d("mth_orderstartdate") ?? d("mth_projectstartdate");
                    end = d("mth_orderenddate") ?? d("mth_projectenddate");
                    break;
            }
            if (start.HasValue && end.HasValue && end < start) end = start;
            return Tuple.Create(start, end);
        }

        private static void AvailabilityDefaults(IOrganizationService service, Entity target, Func<string, object> get)
        {
            var type = (get("mth_type") as OptionSetValue)?.Value ?? Vacation;
            if (get("mth_type") == null) target["mth_type"] = new OptionSetValue(Vacation);
            if (get("mth_capacity") == null)
            {
                if (type == Reduced) throw new InvalidPluginExecutionException("Angiv Capacity (%) for nedsat belastning, f.eks. 60. / Enter Capacity (%) for reduced capacity, e.g. 60.");
                target["mth_capacity"] = 0;
            }

            var user = get("mth_userid") as EntityReference;
            var name = user == null ? "" : service.Retrieve("systemuser", user.Id, new ColumnSet("fullname")).GetAttributeValue<string>("fullname");
            var typeName = type == Vacation ? "Ferie" : type == Leave ? "Orlov" : type == Reduced ? "Nedsat belastning " + get("mth_capacity") + " %" : "Fravær";
            var start = get("mth_startdate") as DateTime?;
            var end = get("mth_enddate") as DateTime?;
            target["mth_name"] = string.Format("{0} – {1} {2}–{3}", typeName, name,
                start?.ToString("d", Da) ?? "?", end?.ToString("d", Da) ?? "?");
        }
    }
}
