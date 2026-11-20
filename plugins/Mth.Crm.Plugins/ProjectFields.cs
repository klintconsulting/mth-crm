namespace Mth.Crm.Plugins
{
    /// <summary>
    /// The SMART project columns that exist with identical names on mth_lead and mth_opportunity
    /// (see ProjectCols in the schema). Copied on conversion. A unit test checks this list against schema.json.
    /// </summary>
    public static class ProjectFields
    {
        public static readonly string[] Names =
        {
            "mth_projectid", "mth_smartid", "mth_smartlink", "mth_projecttitle", "mth_projecttexts",
            "mth_clientname", "mth_clientstreet", "mth_clientpostalcode", "mth_clientcity", "mth_clientcompanytype", "mth_clientvatno", "mth_clienttdcid",
            "mth_constructionsite", "mth_constructiontown", "mth_constructionzip",
            "mth_projectstartdate", "mth_projectstarttext", "mth_projectenddate", "mth_projectendtext", "mth_constructionmonths",
            "mth_projectvalue", "mth_projectvaluetext", "mth_smartcontracttype", "mth_smartcontracttypecode",
            "mth_planningstage", "mth_projectstage", "mth_maincategory", "mth_maincategorycode", "mth_maindevtype", "mth_maindevtypecode", "mth_projectcategories",
            "mth_projectcountry", "mth_projectregion", "mth_projectregioncode", "mth_municipality", "mth_municipalitycode",
            "mth_apartments", "mth_grossfloorarea", "mth_netfloorarea", "mth_volume", "mth_storeys", "mth_storeysbelowground",
            "mth_roleshtml", "mth_tendershtml", "mth_smartupdatedon"
        };

        /// <summary>Columns the queue matcher needs from mth_lead.</summary>
        public static readonly string[] QueueMatchColumns =
        {
            "mth_name", "mth_projecttitle", "mth_projecttexts", "mth_projectregion", "mth_projectvalue", "mth_projectcategories",
            "mth_maincategory", "mth_maindevtype", "mth_planningstage", "mth_projectstage", "mth_smartcontracttype", "mth_projectstartdate", "mth_leadstatus"
        };
    }
}
