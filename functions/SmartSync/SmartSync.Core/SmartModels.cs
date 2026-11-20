using System.Text.Json;
using System.Text.Json.Serialization;

namespace Mth.SmartSync;

// SMART (Byggefakta) API v2.3 models - only the fields the sync uses. Shape documented in docs/system/smart.md.

public sealed class SmartList<T> { [JsonPropertyName("result")] public SmartListResult<T>? Result { get; set; } }
public sealed class SmartListResult<T>
{
    [JsonPropertyName("meta")] public SmartMeta? Meta { get; set; }
    [JsonPropertyName("data")] public List<T> Data { get; set; } = new();
}
public sealed class SmartMeta
{
    [JsonPropertyName("offset")] public int Offset { get; set; }
    [JsonPropertyName("size")] public int Size { get; set; }
    [JsonPropertyName("total")] public int Total { get; set; }
}

public sealed class SmartCard { [JsonPropertyName("id")] public long? Id { get; set; } [JsonPropertyName("name")] public string? Name { get; set; } }

public sealed class SmartPhone { [JsonPropertyName("number")] public string? Number { get; set; } [JsonPropertyName("calling_code")] public string? CallingCode { get; set; } }

public sealed class SmartCommunication
{
    [JsonPropertyName("communication_type_id")] public int? TypeId { get; set; } // 1 tel, 2 mobile, 3 fax, 4 e-mail, 5 web
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("phone_data")] public SmartPhone? Phone { get; set; }
}

public sealed class SmartCompanyCard
{
    [JsonPropertyName("id")] public long? Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("org_nr")] public string? OrgNr { get; set; }
    [JsonPropertyName("par_id")] public string? ParId { get; set; }
    [JsonPropertyName("communications")] public List<SmartCommunication> Communications { get; set; } = new();
}

public sealed class SmartContactCard
{
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("communications")] public List<SmartCommunication> Communications { get; set; } = new();
}
public sealed class SmartContact { [JsonPropertyName("contact_card")] public SmartContactCard? Card { get; set; } }

public sealed class SmartRole
{
    [JsonPropertyName("company_role_id")] public int? CompanyRoleId { get; set; }
    [JsonPropertyName("company_id")] public long? CompanyId { get; set; }
    [JsonPropertyName("company_role_card")] public SmartCard? RoleCard { get; set; }
    [JsonPropertyName("company_card")] public SmartCompanyCard? CompanyCard { get; set; }
    [JsonPropertyName("contacts")] public List<SmartContact> Contacts { get; set; } = new();
}

public sealed class SmartCatDevType
{
    [JsonPropertyName("category_id")] public int? CategoryId { get; set; }
    [JsonPropertyName("dev_type_id")] public int? DevTypeId { get; set; }
    [JsonPropertyName("priority")] public int? Priority { get; set; }
}

public sealed class SmartUpdate
{
    [JsonPropertyName("update_key")] public string? Key { get; set; }
    [JsonPropertyName("tender_id")] public long? TenderId { get; set; }
    [JsonPropertyName("company_role_card")] public SmartCard? RoleCard { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
}

public sealed class SmartProject
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("source_id")] public long? SourceId { get; set; }
    [JsonPropertyName("project_name")] public string? ProjectName { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("apartments")] public decimal? Apartments { get; set; }
    [JsonPropertyName("floors")] public decimal? Floors { get; set; }
    [JsonPropertyName("underground_floors")] public decimal? UndergroundFloors { get; set; }
    [JsonPropertyName("gross_area")] public decimal? GrossArea { get; set; }
    [JsonPropertyName("usage_area")] public decimal? UsageArea { get; set; }
    [JsonPropertyName("gross_volume")] public decimal? GrossVolume { get; set; }
    [JsonPropertyName("contract_type_id")] public int? ContractTypeId { get; set; }
    [JsonPropertyName("stage_id")] public int? StageId { get; set; }
    [JsonPropertyName("detailed_stage_id")] public int? DetailedStageId { get; set; }
    [JsonPropertyName("category_id")] public int? CategoryId { get; set; }
    [JsonPropertyName("dev_type_id")] public int? DevTypeId { get; set; }
    [JsonPropertyName("country_id")] public int? CountryId { get; set; }
    [JsonPropertyName("region_id")] public int? RegionId { get; set; }
    [JsonPropertyName("district_id")] public int? DistrictId { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("postcode")] public int? Postcode { get; set; }
    [JsonPropertyName("postcode_text")] public string? PostcodeText { get; set; }
    [JsonPropertyName("street")] public string? Street { get; set; }
    [JsonPropertyName("street_number")] public string? StreetNumber { get; set; }
    [JsonPropertyName("start_date")] public string? StartDate { get; set; }
    [JsonPropertyName("start_text")] public string? StartText { get; set; }
    [JsonPropertyName("start_date_accuracy_id")] public int? StartAccuracy { get; set; }
    [JsonPropertyName("finish_date")] public string? FinishDate { get; set; }
    [JsonPropertyName("finish_text")] public string? FinishText { get; set; }
    [JsonPropertyName("finish_date_accuracy_id")] public int? FinishAccuracy { get; set; }
    [JsonPropertyName("months")] public int? Months { get; set; }
    [JsonPropertyName("value")] public decimal? Value { get; set; }
    [JsonPropertyName("value_text")] public string? ValueText { get; set; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; set; }
    [JsonPropertyName("roles")] public List<SmartRole> Roles { get; set; } = new();
    [JsonPropertyName("cat_dev_type")] public List<SmartCatDevType> CatDevTypes { get; set; } = new();
    [JsonPropertyName("updates")] public List<SmartUpdate> Updates { get; set; } = new();
}

public sealed class SmartTender
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("project_id")] public long? ProjectId { get; set; }
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("content_standard")] public string? ContentStandard { get; set; }
    [JsonPropertyName("content_ted")] public string? ContentTed { get; set; }
    [JsonPropertyName("company_role_id")] public int? CompanyRoleId { get; set; }
    [JsonPropertyName("tender_type_id")] public int? TenderTypeId { get; set; }
    [JsonPropertyName("first_day_materials_text")] public string? FirstDayMaterials { get; set; }
    [JsonPropertyName("last_day_application_text")] public string? LastDayApplication { get; set; }
    [JsonPropertyName("prequalification_date_text")] public string? PrequalificationDate { get; set; }
    [JsonPropertyName("archived")] public int? Archived { get; set; }
    [JsonPropertyName("deleted")] public bool? Deleted { get; set; }
    [JsonPropertyName("company_role_card")] public SmartCard? RoleCard { get; set; }
    [JsonPropertyName("tender_type_card")] public SmartCard? TypeCard { get; set; }

    public bool IsCurrent => Deleted != true && (Archived ?? 0) == 0;
}

public sealed class SmartAddress
{
    [JsonPropertyName("address_type_id")] public int? TypeId { get; set; }
    [JsonPropertyName("main")] public bool? Main { get; set; }
    [JsonPropertyName("street")] public string? Street { get; set; }
    [JsonPropertyName("street_number")] public string? StreetNumber { get; set; }
    [JsonPropertyName("postcode_text")] public string? PostcodeText { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
}

public sealed class SmartCompany
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("company_type_id")] public int? CompanyTypeId { get; set; }
    [JsonPropertyName("street")] public string? Street { get; set; }
    [JsonPropertyName("street_number")] public string? StreetNumber { get; set; }
    [JsonPropertyName("postcode_text")] public string? PostcodeText { get; set; }
    [JsonPropertyName("city")] public string? City { get; set; }
    [JsonPropertyName("addresses")] public List<SmartAddress> Addresses { get; set; } = new();
}

public sealed class SmartLookupItem
{
    [JsonPropertyName("id")] public long Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("rsm_code")] public string? RsmCode { get; set; }
    [JsonPropertyName("country_id")] public long? CountryId { get; set; }
    [JsonPropertyName("region_id")] public long? RegionId { get; set; }
    [JsonPropertyName("items")] public List<SmartLookupItem>? Items { get; set; }
}
public sealed class SmartLookupGroup
{
    [JsonPropertyName("lookup")] public string Lookup { get; set; } = "";
    [JsonPropertyName("items")] public List<SmartLookupItem> Items { get; set; } = new();
}
public sealed class SmartDictionary
{
    [JsonPropertyName("version")] public JsonElement Version { get; set; }
    [JsonPropertyName("items")] public List<SmartLookupGroup> Items { get; set; } = new();
}
public sealed class SmartLookups { [JsonPropertyName("result")] public SmartLookupsResult? Result { get; set; } }
public sealed class SmartLookupsResult { [JsonPropertyName("dict")] public SmartDictionary? Dict { get; set; } }

public static class SmartJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
}
