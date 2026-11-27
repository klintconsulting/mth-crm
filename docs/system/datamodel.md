# Datamodel

> Genereret automatisk fra `scripts/schema/schema.json` af `scripts/New-SchemaDoc.ps1`. Ret ikke i filen her. Ret i schema.json og kør scriptet igen.

Solution: `MTHCRM` · publisher-præfiks: `mth` · option value-præfiks: `27328`

## Tabeller

| Tabel (logisk navn) | Visningsnavn | Type | Ejerskab | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_organizationalunit` | Organizational Unit | Custom | OrganizationOwned | Organizational_unit__c |
| `mth_opportunity` | Opportunity | Custom | UserOwned | Opportunity |
| `mth_opportunityrole` | Opportunity Role | Custom | UserOwned | Role lookups on Opportunity (numbered variants 21/22/23/24 are merged): Tender_Manager2__c, Project_Manager2__c, Tender_Responsible2__c, Tender_Coordinator21__c, Tender_Calculator21__c, Pre_Q_Responsible21__c, Bid_Manager2__c, Projekteringsleder__c, Guarantee_Responsible21__c, Primary_Responsible2__c |
| `account` | Account | Standard |  | Account |
| `contact` | Contact | Standard |  | Contact |
| `mth_trade` | Trade | Custom | OrganizationOwned | Global value set Building_Contracts (Account/Opportunity/Building_Contract__c picklists) |
| `mth_buildingcontract` | Building Contract | Custom | UserOwned | Building_Contract__c (Entreprise) |
| `mth_collaboratingpartner` | Collaborating Partner | Custom | UserOwned | Collaborating_Partner__c (Samarbejdspartner / UE) |
| `mth_espemailtemplate` | ESP E-mail Template | Custom | OrganizationOwned | (new) - Salesforce had no templates |
| `mth_lead` | Lead | Custom | UserOwned | Lead (record type New, LeadSource FaktaNet Live) |
| `mth_leadqueue` | Lead Queue | Custom | UserOwned | (new) - replaces Salesforce lead queues, assignment rules and per-department list views |
| `mth_smartlookup` | SMART Lookup | Custom | OrganizationOwned | SMART_Lookup__c |
| `mth_integrationstate` | Integration State | Custom | OrganizationOwned | SMART_Sync_State__c |
| `mth_leadassignment` | Lead Assignment | Custom | OrganizationOwned | (new) Lead_Assignment__c from the parallel-queue design (Salesforce proposal 2026-09-11) |
| `mth_follow` | Follow | Custom | UserOwned | (new) EntitySubscription (Salesforce "Follow") |

## Relationer (opslag)

| Relation | Opslagskolonne | Peger på | Sletning | Påkrævet | Kilde |
|---|---|---|---|---|---|
| `mth_opportunity_mth_lead_Converted` | `mth_lead.mth_convertedopportunityid` | `mth_opportunity` | RemoveLink |  | ConvertedOpportunityId |
| `mth_systemuser_mth_lead_ConvertedBy` | `mth_lead.mth_convertedbyid` | `systemuser` | RemoveLink |  | (new) |
| `mth_lead_mth_opportunity_Lead` | `mth_opportunity.mth_leadid` | `mth_lead` | RemoveLink |  | Lead.ConvertedOpportunityId (reverse) |
| `mth_trade_mth_buildingcontract` | `mth_buildingcontract.mth_tradeid` | `mth_trade` | Restrict | Ja | Building_Contract__c.Building_Contract__c (picklist) |
| `mth_opportunity_mth_buildingcontract` | `mth_buildingcontract.mth_opportunityid` | `mth_opportunity` | Parental | Ja | Building_Contract__c.Opportunity__c (master-detail) |
| `mth_buildingcontract_mth_collabpartner` | `mth_collaboratingpartner.mth_buildingcontractid` | `mth_buildingcontract` | Parental | Ja | Collaborating_Partner__c.Subcontract__c |
| `mth_account_mth_collabpartner` | `mth_collaboratingpartner.mth_accountid` | `account` | Restrict | Ja | Collaborating_Partner__c.Account__c (master-detail) |
| `mth_opportunity_mth_collabpartner` | `mth_collaboratingpartner.mth_opportunityid` | `mth_opportunity` | RemoveLink |  | Derived: the building contract's opportunity, set by plugin CollabPartnerOpportunity |
| `mth_account_mth_opportunity_Account` | `mth_opportunity.mth_accountid` | `account` | Restrict |  | AccountId |
| `mth_orgunit_mth_opportunity_OrderDept` | `mth_opportunity.mth_orderdepartmentid` | `mth_organizationalunit` | Restrict |  | Order_Department__c |
| `mth_orgunit_mth_opportunity_TenderDept` | `mth_opportunity.mth_tenderdepartmentid` | `mth_organizationalunit` | Restrict |  | Tender_Department__c |
| `mth_opportunity_mth_opportunity_External` | `mth_opportunity.mth_externalopportunityid` | `mth_opportunity` | RemoveLink |  | External_Opportunity__c |
| `mth_opportunity_mth_opportunityrole` | `mth_opportunityrole.mth_opportunityid` | `mth_opportunity` | Parental | Ja | (new) |
| `mth_systemuser_mth_opportunityrole_User` | `mth_opportunityrole.mth_userid` | `systemuser` | Restrict | Ja | MTH_Employee__c.User__c (employees are Entra ID users) |
| `mth_lead_mth_leadassignment` | `mth_leadassignment.mth_leadid` | `mth_lead` | Parental | Ja | Lead_Assignment__c.Lead__c |
| `mth_leadqueue_mth_leadassignment` | `mth_leadassignment.mth_leadqueueid` | `mth_leadqueue` | RemoveLink | Ja | Lead_Assignment__c.Department_Queue__c |
| `mth_systemuser_mth_leadassignment_ChangedBy` | `mth_leadassignment.mth_statuschangedbyid` | `systemuser` | RemoveLink |  | (new) |
| `mth_opportunity_mth_follow` | `mth_follow.mth_opportunityid` | `mth_opportunity` | DeleteOnly |  | EntitySubscription.ParentId |
| `mth_lead_mth_follow` | `mth_follow.mth_leadid` | `mth_lead` | RemoveLink |  | EntitySubscription.ParentId |
| `mth_account_mth_follow` | `mth_follow.mth_accountid` | `account` | RemoveLink |  | EntitySubscription.ParentId |

## Organizational Unit (`mth_organizationalunit`)

MTH department/cost centre. Used for order and tender department on opportunities.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: Name

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_departmentno` | Department No. | string | maks. 20 | `Department_No__c` |
| `mth_divisionno` | Division No. | string | maks. 20 | `Division_Number__c` |
| `mth_divisionname` | Division Name | string | maks. 200 | `Division_Name__c` |
| `mth_businessunitno` | Business Unit No. | string | maks. 20 | `Business_Unit_No__c` |
| `mth_businessunitname` | Business Unit Name | string | maks. 200 | `Business_Unit_Name__c` |
| `mth_costcenterno` | Cost Center No. | string | maks. 20 | `Cost_Center_No__c` |
| `mth_istenderdepartment` | Tender Department | boolean |  | `Tender_Department__c` |

## Opportunity (`mth_opportunity`)

MTH opportunity (sag). Custom table - Dynamics 365 Sales is not used.

Primær navnekolonne: `mth_name` (tekst, maks. 300). Kilde: Name

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_description` | Description | memo | maks. 32000 | `Description` |
| `mth_status` | Status | choice | 21 valg | `StageName` |
| `mth_amount` | Amount | currency |  | `Amount` |
| `mth_estimatedamount` | Estimated Amount | currency |  | `Estimated_Amount__c` |
| `mth_tenderamount` | Tender Amount | currency |  | `Tender_Amount__c` |
| `mth_calculatedcontractamount` | Calculated Contract Amount | currency |  | `set_Calculated_Contract_Amount_c__c` |
| `mth_statusnumber` | Status Number | integer | 0–99 | `Status_Number__c (formula). Set by plugin OpportunityStatusNumber: the number before the first space in Status (= option value - 273280000)` |
| `mth_probability` | Probability (%) | integer | 0–100 | `Probability` |
| `mth_closedate` | Close Date | date |  | `CloseDate` |
| `mth_opportunitytype` | Opportunity Type | choice | 2 valg | `Type` |
| `mth_nextstep` | Next Step | string | maks. 255 | `NextStep` |
| `mth_nextstephistory` | Next Step History | memo | maks. 32000 | `Next_Step_History__c` |
| `mth_leadsource` | Lead Source | choice | 6 valg | `LeadSource` |
| `mth_prequalificationdate` | Pre-Qualification Date | date |  | `Pre_Qualification_Date__c` |
| `mth_invitationtotenderdate` | Invitation to Tender Date | date |  | `Invitation_To_Tender_Date__c` |
| `mth_tenderstartdate` | Tender Start Date | date |  | `Tender_Start__c` |
| `mth_initialtenderdate` | Initial Tender Date | date |  | `Indledende_tilbudsdato__c` |
| `mth_tenderdate` | Tender Date | date |  | `Tender_Date__c` |
| `mth_orderstartdate` | Order Start Date | date |  | `Order_Start_Date__c` |
| `mth_orderenddate` | Order End Date | date |  | `Order_End_Date__c` |
| `mth_constructioncategory` | Construction Category | choice | 4 valg | `Construction_Category__c` |
| `mth_marketcategory` | Market Category | choice | 5 valg | `Market_Category__c` |
| `mth_casetype` | Type of Case | choice | 45 valg | `Type_of_Case__c` |
| `mth_buildingtypes` | Building Types | multichoice | 53 valg | `Building_Types__c` |
| `mth_contracttype` | Type of Contract | choice | 12 valg | `Type_Of_Contract__c` |
| `mth_invitationtotendertype` | Type of Invitation to Tender | choice | 10 valg | `Type_Of_Invitation_To_Tender__c` |
| `mth_revenuesplit` | Revenue Split | choice | 2 valg | `Revenue_split__c` |
| `mth_deliveryaddress` | Delivery Address | memo | maks. 1000 | `Delivery_Address__c` |
| `mth_deliveryzip` | Delivery Postal Code | string | maks. 20 | `Delivery_Zip__c` |
| `mth_deliverycity` | Delivery City | string | maks. 100 | `Delivery_City__c` |
| `mth_bcprojectnumber` | BC Case Number | string | maks. 50 | `BC_Project_Number__c / Case_Number__c (identical)` |
| `mth_iskeyproject` | Key Project | boolean |  | `Key_Project__c` |
| `mth_projectsiteurl` | Project Site URL | string | maks. 500, Url | `SharepointIntegration / Project Portal (Opportunity_in_Project_Portal__c, Project_Portal_Url__c)` |
| `mth_scope` | Scope | memo | maks. 4000 | `Omfang__c` |
| `mth_projectid` | SMART Project Id | string | maks. 50 | `fnl_Project_Id__c (SMART source_id)` |
| `mth_smartid` | SMART Internal Id | string | maks. 20 | `SMART_Project_Id__c (SMART id)` |
| `mth_smartlink` | SMART Link | string | maks. 300, Url | `fnl_Link__c` |
| `mth_projecttitle` | Project Title | string | maks. 1000 | `fnl_Project_Desc__c (title)` |
| `mth_projecttexts` | Project Description | memo | maks. 100000 | `fnl_Project_Texts__c (description)` |
| `mth_clientname` | Client (Bygherre) | string | maks. 300 | `Lead.Company (role Bygherre)` |
| `mth_clientstreet` | Client Street | string | maks. 300 | `Lead.Street (Bygherre main address)` |
| `mth_clientpostalcode` | Client Postal Code | string | maks. 20 | `Lead.PostalCode` |
| `mth_clientcity` | Client City | string | maks. 100 | `Lead.City` |
| `mth_clientcompanytype` | Client Company Type | string | maks. 255 | `fnl_Company_Type_Desc__c` |
| `mth_clientvatno` | Client VAT No. | string | maks. 50 | `fnl_VAT_no__c (org_nr)` |
| `mth_clienttdcid` | Client TDC Id | string | maks. 50 | `fnl_TDCID__c (par_id)` |
| `mth_constructionsite` | Construction Site | string | maks. 300 | `fnl_Construction_Site__c` |
| `mth_constructiontown` | Construction Town | string | maks. 100 | `fnl_Construction_Town__c` |
| `mth_constructionzip` | Construction Postal Code | string | maks. 20 | `fnl_Zip_Code__c` |
| `mth_projectstartdate` | Construction Start | date |  | `fnl_Start_Date__c` |
| `mth_projectstarttext` | Construction Start (text) | string | maks. 255 | `fnl_Start_Date_Desc__c` |
| `mth_projectenddate` | Construction End | date |  | `fnl_End_Date__c` |
| `mth_projectendtext` | Construction End (text) | string | maks. 255 | `fnl_End_Date_Desc__c` |
| `mth_constructionmonths` | Construction Period (months) | integer | 0–1000 | `fnl_Construction_Period__c` |
| `mth_projectvalue` | Project Value (DKK m) | decimal |  | `fnl_Value__c (value / 1,000,000)` |
| `mth_projectvaluetext` | Project Value (text) | string | maks. 255 | `fnl_Value_Desc__c` |
| `mth_smartcontracttype` | SMART Contract Type | string | maks. 255 | `fnl_Contract_Type__c` |
| `mth_smartcontracttypecode` | SMART Contract Type Code | string | maks. 50 | `fnl_Contract_Type_Code__c` |
| `mth_planningstage` | Planning Stage | string | maks. 255 | `fnl_Planning_Stage__c` |
| `mth_projectstage` | Project Stage | string | maks. 255 | `fnl_Stage__c` |
| `mth_maincategory` | Main Category | string | maks. 255 | `fnl_Main_Category_Desc__c` |
| `mth_maincategorycode` | Main Category Code | string | maks. 50 | `fnl_Main_Category_Code__c` |
| `mth_maindevtype` | Main Development Type | string | maks. 255 | `fnl_Main_Dev_Type_Desc__c` |
| `mth_maindevtypecode` | Main Development Type Code | string | maks. 50 | `fnl_Main_Dev_Type_Code__c` |
| `mth_projectcategories` | Categories | memo | maks. 4000 | `fnl_Categories__c ("category - devtype;...")` |
| `mth_projectcountry` | Country | string | maks. 100 | `fnl_Country_Code__c` |
| `mth_projectregion` | Region | string | maks. 100 | `fnl_County_Desc__c` |
| `mth_projectregioncode` | Region Code | string | maks. 50 | `fnl_County_Code__c` |
| `mth_municipality` | Municipality | string | maks. 100 | `fnl_Municipality_Desc__c` |
| `mth_municipalitycode` | Municipality Code | string | maks. 50 | `fnl_Municipality_Code__c` |
| `mth_apartments` | Apartments | integer | 0–1000000 | `fnl_Apartments__c` |
| `mth_grossfloorarea` | Gross Floor Area (m2) | integer | 0–100000000 | `fnl_Gross_Floor_Area__c` |
| `mth_netfloorarea` | Net Floor Area (m2) | integer | 0–100000000 | `fnl_Net_Floor_Area__c` |
| `mth_volume` | Volume (m3) | integer | 0–1000000000 | `fnl_Volume__c` |
| `mth_storeys` | Storeys | integer | 0–1000 | `fnl_Storeys__c` |
| `mth_storeysbelowground` | Storeys Below Ground | integer | 0–100 | `fnl_Storeys_Below_Ground__c` |
| `mth_roleshtml` | Roles | memo | maks. 100000 | `fnl_Roles_RT__c` |
| `mth_tendershtml` | Tenders | memo | maks. 100000 | `fnl_Tenders__c` |
| `mth_smartupdatedon` | SMART Updated On | datetime |  | `SMART updated_at` |
| `mth_leadhistory` | Lead History | memo | maks. 100000 | `fnl_Lead_History__c` |
| `mth_leadhistorylatest` | Latest Lead Changes | memo | maks. 20000 | `fnl_Lead_History_Latest__c` |
| `mth_leaddataupdatedon` | Lead Data Updated On | datetime |  | `fnl_Lead_Data_Updated__c / fnl_Last_Updated__c` |

<details><summary>Valg for <code>mth_status</code> (21)</summary>

Option value = 273280000 + status code (e.g. 60 MTH contract = 273280060).

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280001 | 01 Potential project | 01 Potential project |
| 273280005 | 05 Prioritised project | 05 Prioritised project |
| 273280020 | 20 PQ - upcoming | 20 PQ - upcoming |
| 273280022 | 22 PQ - ongoing | 22 PQ - ongoing |
| 273280024 | 24 PQ - submitted | 24 PQ - submitted |
| 273280030 | 30 Qualification systems/Framework agreements | 30 Qualification systems/Framework agreements |
| 273280040 | 40 _Project estimate | 40 _Project estimate |
| 273280052 | 52 Tender - ongoing | 52 Tender - ongoing |
| 273280054 | 54 Tender - submitted | 54 Tender - submitted |
| 273280056 | 56 Negotiation | 56 Negotiation |
| 273280057 | 57 Won, but not contracted | 57 Won, but not contracted |
| 273280058 | 58 Collaboration agreement | 58 Collaboration agreement |
| 273280060 | 60 MTH contract | 60 MTH contract |
| 273280065 | 65 _ project handed over _ ongoing remed | 65 _ project handed over _ ongoing remed |
| 273280080 | 80 MTH guaranteen project | 80 MTH guaranteen project |
| 273280085 | 85 Project filed | 85 Project filed |
| 273280090 | 90 PQ - Lost tender | 90 PQ - Lost tender |
| 273280091 | 91 Tender Abandoned | 91 Tender Abandoned |
| 273280092 | 92 PQ - lost | 92 PQ - lost |
| 273280093 | 93 PQ/Tender canceled | 93 PQ/Tender canceled |
| 273280099 | 99 Deleted | 99 Deleted |

</details>

<details><summary>Valg for <code>mth_opportunitytype</code> (2)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Internal | Internal |
| 273280001 | External | External |

</details>

<details><summary>Valg for <code>mth_leadsource</code> (6)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | FaktaNet Live | FaktaNet Live |
| 273280001 | Media | Media |
| 273280002 | Databases | Databases |
| 273280003 | Other | Other |
| 273280004 | Advisor | Advisor |
| 273280005 | Customer | Customer |

</details>

<details><summary>Valg for <code>mth_constructioncategory</code> (4)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Civil works new | Civil works new |
| 273280001 | Civil works renovation | Civil works renovation |
| 273280002 | Construction new | Construction new |
| 273280003 | Construction renovation | Construction renovation |

</details>

<details><summary>Valg for <code>mth_marketcategory</code> (5)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Civil works new | Civil works new |
| 273280001 | Civil works renovation | Civil works renovation |
| 273280002 | Housing | Housing |
| 273280003 | Private | Private |
| 273280004 | Public | Public |

</details>

<details><summary>Valg for <code>mth_casetype</code> (45)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Civil works - bridges and tunnels (115) | Civil works - bridges and tunnels (115) |
| 273280001 | Civil works - combined heat and power plants (125) | Civil works - combined heat and power plants (125) |
| 273280002 | Civil works - drilling and cutting works (104) | Civil works - drilling and cutting works (104) |
| 273280003 | Civil works - earthworks, drainage and sewerage installations (105) | Civil works - earthworks, drainage and sewerage installations (105) |
| 273280004 | Civil works - environmental works (126) | Civil works - environmental works (126) |
| 273280005 | Civil works - flooring (133) | Civil works - flooring (133) |
| 273280006 | Civil works - marine works (109) | Civil works - marine works (109) |
| 273280007 | Civil works - moisture-resistance and proofing (132) | Civil works - moisture-resistance and proofing (132) |
| 273280008 | Civil works - Offshore and marine works (128) | Civil works - Offshore and marine works (128) |
| 273280009 | Civil works - operation and maintenance (127) | Civil works - operation and maintenance (127) |
| 273280010 | Civil works - parking facilities (129) | Civil works - parking facilities (129) |
| 273280011 | Civil works - piling and sheet piling (108) | Civil works - piling and sheet piling (108) |
| 273280012 | Civil works - pipeline works (106) | Civil works - pipeline works (106) |
| 273280013 | Civil works - roads (110) | Civil works - roads (110) |
| 273280014 | Civil works - shell construction (134) | Civil works - shell construction (134) |
| 273280015 | Civil works - surfacing (107) | Civil works - surfacing (107) |
| 273280016 | Civil works - wastewater treatment plant (120) | Civil works - wastewater treatment plant (120) |
| 273280017 | Culture and sports - cultural facilities, museums, churches etc. (382) | Culture and sports - cultural facilities, museums, churches etc. (382) |
| 273280018 | Culture and sports - sports facility (381) | Culture and sports - sports facility (381) |
| 273280019 | Educational and institutional facilities - care and activity centres for the elderly (314) | Educational and institutional facilities - care and activity centres for the elderly (314) |
| 273280020 | Educational and institutional facilities - day care centre (313) | Educational and institutional facilities - day care centre (313) |
| 273280021 | Educational and institutional facilities - hospitals, clinics, etc. (325) | Educational and institutional facilities - hospitals, clinics, etc. (325) |
| 273280022 | Educational and institutional facilities - schools, education (315) | Educational and institutional facilities - schools, education (315) |
| 273280023 | Housing - Cluster houses (306) | Housing - Cluster houses (306) |
| 273280024 | Housing - Housing for the disabled and sheltered housing (309) | Housing - Housing for the disabled and sheltered housing (309) |
| 273280025 | Housing - multi-storey residence (305) | Housing - multi-storey residence (305) |
| 273280026 | Housing - single-family houses (307) | Housing - single-family houses (307) |
| 273280027 | Housing - Youth residences and dormitories (308) | Housing - Youth residences and dormitories (308) |
| 273280028 | Industrial and commercial construction - administration (210) | Industrial and commercial construction - administration (210) |
| 273280029 | Industrial and commercial construction - agricultural buildings (220) | Industrial and commercial construction - agricultural buildings (220) |
| 273280030 | Industrial and commercial construction - hotels, restaurants and holiday resorts (230) | Industrial and commercial construction - hotels, restaurants and holiday resorts (230) |
| 273280031 | Industrial and commercial construction - pharmaceutical and biotech. (217) | Industrial and commercial construction - pharmaceutical and biotech. (217) |
| 273280032 | Industrial and commercial construction - process industry (216) | Industrial and commercial construction - process industry (216) |
| 273280033 | Industrial and commercial construction - production and storage (205) | Industrial and commercial construction - production and storage (205) |
| 273280034 | Industrial and commercial construction - stores and shopping centres (215) | Industrial and commercial construction - stores and shopping centres (215) |
| 273280035 | Industrial and commercial construction - transport (airports, etc.) | Industrial and commercial construction - transport (airports, etc.) |
| 273280036 | Mining - consultancy (510) | Mining - consultancy (510) |
| 273280037 | Mining - feasibility study (505) | Mining - feasibility study (505) |
| 273280038 | Mining - Infrastructure (civil works) (515) | Mining - Infrastructure (civil works) (515) |
| 273280039 | Mining - operation (520) | Mining - operation (520) |
| 273280040 | Minor project - misc. (405) | Minor project - misc. (405) |
| 273280041 | Renovatio - industrial and commercial construction (225) | Renovatio - industrial and commercial construction (225) |
| 273280042 | Renovation - culture and sport facilities (383) | Renovation - culture and sport facilities (383) |
| 273280043 | Renovation - educational and institutional facilities (330) | Renovation - educational and institutional facilities (330) |
| 273280044 | Renovation - housing (310) | Renovation - housing (310) |

</details>

<details><summary>Valg for <code>mth_buildingtypes</code> (53)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Basin plants | Basin plants |
| 273280001 | Car park#public | Car park#public |
| 273280002 | Cloudburst Security | Cloudburst Security |
| 273280003 | Coast protection | Coast protection |
| 273280004 | Concrete bridges | Concrete bridges |
| 273280005 | Concrete carcass | Concrete carcass |
| 273280006 | Concrete foundation | Concrete foundation |
| 273280007 | Construction/Civil | Construction/Civil |
| 273280008 | Construction pits | Construction pits |
| 273280009 | Data Center | Data Center |
| 273280010 | Domiciles#private | Domiciles#private |
| 273280011 | Donor Project | Donor Project |
| 273280012 | Energy | Energy |
| 273280013 | Fjernvarme | Fjernvarme |
| 273280014 | Ground/Road and Ramming | Ground/Road and Ramming |
| 273280015 | Harbours/Docks | Harbours/Docks |
| 273280016 | Havvind | Havvind |
| 273280017 | Health offices#public | Health offices#public |
| 273280018 | Hospitals#public | Hospitals#public |
| 273280019 | Hotel / resort#private | Hotel / resort#private |
| 273280020 | Housing#housing | Housing#housing |
| 273280021 | Infrastructure | Infrastructure |
| 273280022 | Kalundborg Beton | Kalundborg Beton |
| 273280023 | Klimatilpasning | Klimatilpasning |
| 273280024 | Marine engineering | Marine engineering |
| 273280025 | Marine Works | Marine Works |
| 273280026 | Museums#public | Museums#public |
| 273280027 | Nursing / retirement home#public | Nursing / retirement home#public |
| 273280028 | Offices/administration#private | Offices/administration#private |
| 273280029 | Ombygningsarbejder | Ombygningsarbejder |
| 273280030 | Parking garage/basement | Parking garage/basement |
| 273280031 | Powerplant#public | Powerplant#public |
| 273280032 | Private#private | Private#private |
| 273280033 | Production Facilities#private | Production Facilities#private |
| 273280034 | Pumping station | Pumping station |
| 273280035 | Rensningsanlæg | Rensningsanlæg |
| 273280036 | Resorts | Resorts |
| 273280037 | Roads/Roundabouts | Roads/Roundabouts |
| 273280038 | Schools#public | Schools#public |
| 273280039 | Separate Sewerage | Separate Sewerage |
| 273280040 | Service & Maintenance | Service & Maintenance |
| 273280041 | Sewerage/Climate protection | Sewerage/Climate protection |
| 273280042 | Shopping centre#private | Shopping centre#private |
| 273280043 | Skyscrapers#housing | Skyscrapers#housing |
| 273280044 | Steel constructions | Steel constructions |
| 273280045 | Stock#private | Stock#private |
| 273280046 | Stålbroer | Stålbroer |
| 273280047 | Svømmehal | Svømmehal |
| 273280048 | Tunnels | Tunnels |
| 273280049 | Universities#public | Universities#public |
| 273280050 | Vandkraftværk | Vandkraftværk |
| 273280051 | Vandværk | Vandværk |
| 273280052 | Vindmøller | Vindmøller |

</details>

<details><summary>Valg for <code>mth_contracttype</code> (12)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Design-build | Design-build |
| 273280001 | Main contract | Main contract |
| 273280002 | Multi-trades contract | Multi-trades contract |
| 273280003 | Subcontract | Subcontract |
| 273280004 | Cost-plus | Cost-plus |
| 273280005 | PPP | PPP |
| 273280006 | PPS | PPS |
| 273280007 | Partnering | Partnering |
| 273280008 | Project development | Project development |
| 273280009 | Joint Venture | Joint Venture |
| 273280010 | Råhusentreprise | Råhusentreprise |
| 273280011 | DI | DI |

</details>

<details><summary>Valg for <code>mth_invitationtotendertype</code> (10)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Tender (restricted procedure) | Tender (restricted procedure) |
| 273280001 | Sole bid | Sole bid |
| 273280002 | EU licitation | EU licitation |
| 273280003 | Negotiation project | Negotiation project |
| 273280004 | Invited tender | Invited tender |
| 273280005 | Competitive dialogue | Competitive dialogue |
| 273280006 | Estimate | Estimate |
| 273280007 | Licitation after negotiation | Licitation after negotiation |
| 273280008 | RFP | RFP |
| 273280009 | Reverse tender | Reverse tender |

</details>

<details><summary>Valg for <code>mth_revenuesplit</code> (2)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Lineær | Lineær |
| 273280001 | S-Kurve | S-Kurve |

</details>

## Opportunity Role (`mth_opportunityrole`)

Case team: a user (Entra ID) in a role on an opportunity. Replaces the numbered role lookups in Salesforce.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: (role + employee)

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_role` | Role | choice | 10 valg | `(field name)` |

<details><summary>Valg for <code>mth_role</code> (10)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Tender Manager | Tender_Manager2__c |
| 273280001 | Project Manager | Project_Manager2__c |
| 273280002 | Tender Responsible | Tender_Responsible2__c |
| 273280003 | Tender Coordinator | Tender_Coordinator21__c |
| 273280004 | Tender Calculator | Tender_Calculator21__c |
| 273280005 | Pre-Qualification Responsible | Pre_Q_Responsible21__c |
| 273280006 | Bid Manager | Bid_Manager2__c |
| 273280007 | Design Manager | Projekteringsleder__c |
| 273280008 | Guarantee Responsible | Guarantee_Responsible21__c |
| 273280009 | Primary Responsible | Primary_Responsible2__c |

</details>

## Account (`account`)

Standardkolonner, der bruges:

| Kolonne | Kilde i Salesforce |
|---|---|
| `name` | Name |
| `telephone1` | Phone |
| `websiteurl` | Website |
| `emailaddress1` | Account_Email__c |
| `address1_line1` | ShippingStreet |
| `address1_postalcode` | ShippingPostalCode |
| `address1_city` | ShippingCity |
| `address1_country` | ShippingCountry |
| `ownerid` | OwnerId |

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_vatno` | VAT No. | string | maks. 20 | `VAT_no__c` |
| `mth_accountcategory` | Account Category | multichoice | 4 valg | `Account_Category__c` |
| `mth_segment` | Segment | choice | 4 valg | `Segment__c` |
| `mth_subsegment` | Sub-Segment | choice | 12 valg | `Sub_Segment__c` |
| `mth_accountstatus` | Account Status | choice | 2 valg | `Status__c` |
| `mth_interestedfor` | Customer Is Interesting For | multichoice | 9 valg | `Kunden_er_interessant_for__c` |
| `mth_tdcid` | TDC ID | string | maks. 50 | `TDC_ID__c` |
| `mth_pnumber` | P-Number | string | maks. 20 | `NN_P_Number__c` |
| `mth_companyform` | Company Form | string | maks. 100 | `NNE companyForm (new)` |
| `mth_legalentity` | Legal Entity (Headquarter) | boolean |  | `NN_Headquarter__c` |
| `mth_companystatustext` | Company Status (NNE) | string | maks. 200 | `NNE statusText (new)` |
| `mth_foundedyear` | Founded Year | integer | 0–3000 | `NNE foundedYear (new)` |
| `mth_adprotection` | Advertising Protection | boolean |  | `NNE adProtection (new)` |
| `mth_directoryname` | Directory Name (NNE) | string | maks. 200 | `NN_TDF_Name__c` |
| `mth_nneupdatedon` | NNE Data Updated On | datetime |  | `NNE import (new)` |
| `mth_leadsourcetext` | Lead Source | string | maks. 255 | `Lead_Source__c` |

<details><summary>Valg for <code>mth_accountcategory</code> (4)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Client | Client |
| 273280001 | Partner | Partner |
| 273280002 | Competitor | Competitor |
| 273280003 | Vendor | Vendor |

</details>

<details><summary>Valg for <code>mth_segment</code> (4)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Housing Association | Housing Association |
| 273280001 | Public | Public |
| 273280002 | Private | Private |
| 273280003 | Investor | Investor |

</details>

<details><summary>Valg for <code>mth_subsegment</code> (12)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Municipality | Municipality |
| 273280001 | Region | Region |
| 273280002 | State | State |
| 273280003 | Professional commercial client | Professional commercial client |
| 273280004 | Other | Other |
| 273280005 | Retail | Retail |
| 273280006 | Medical | Medical |
| 273280007 | Private home owner | Private home owner |
| 273280008 | Social housing | Social housing |
| 273280009 | Institutial investor | Institutial investor |
| 273280010 | Developer | Developer |
| 273280011 | Supply | Supply |

</details>

<details><summary>Valg for <code>mth_accountstatus</code> (2)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Active | Active |
| 273280001 | Inactive | Inactive |

</details>

<details><summary>Valg for <code>mth_interestedfor</code> (9)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Anlæg Øst | Anlæg Øst |
| 273280001 | Renovering Øst | Renovering Øst |
| 273280002 | Tømrer og Murer Øst | Tømrer og Murer Øst |
| 273280003 | Byggeri Øst 2 | Byggeri Øst 2 |
| 273280004 | Byggeri Øst 1 | Byggeri Øst 1 |
| 273280005 | Specielle Projekter | Specielle Projekter |
| 273280006 | Tømrer og Murer Vest | Tømrer og Murer Vest |
| 273280007 | Byggeri Vest | Byggeri Vest |
| 273280008 | Anlæg Vest | Anlæg Vest |

</details>

## Contact (`contact`)

Standardkolonner, der bruges:

| Kolonne | Kilde i Salesforce |
|---|---|
| `firstname` | FirstName |
| `lastname` | LastName |
| `emailaddress1` | Email |
| `mobilephone` | MobilePhone |
| `telephone1` | Phone |
| `jobtitle` | Title |
| `parentcustomerid` | AccountId (Active_Lookup__c/Inactive_Lookup__c are obsolete) |
| `ownerid` | OwnerId |

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_leftdate` | Left Date | date |  | `Left_Date__c` |
| `mth_marketingagreementdate` | Marketing Agreement Date | date |  | `Marketing_Agreement__c` |
| `mth_marketingagreementreturned` | Marketing Agreement Returned | string | maks. 100 | `Marketing_Agreement_Returned__c` |
| `mth_sendmarketingagreementdate` | Send Marketing Agreement Date | date |  | `Send_Marketing_Agreement_Date__c` |
| `mth_marketingagreementsenton` | Marketing Agreement Sent On | date |  | `Marketing_Agreement_Sent_on__c` |

## Trade (`mth_trade`)

Trade catalogue (fag). Accounts are linked to the trades they can deliver; a building contract is for one trade.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: value label before " - code"

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_code` | Code | string | maks. 20 | `value label after " - "` |
| `mth_sourcevalue` | Salesforce Value | string | maks. 255 | `full picklist value, used for migration` |

## Building Contract (`mth_buildingcontract`)

Building contract (entreprise) on an opportunity: one trade package that subcontractors are invited to tender for.

Primær navnekolonne: `mth_name` (tekst, maks. 300). Kilde: Used_Building_Contract_Name__c (alternative name or trade)

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_alternativename` | Alternative Name | string | maks. 255 | `Alternative_Building_Contract_Name__c` |
| `mth_tenderdeadline` | Tender Deadline | date |  | `Tender_Deadline__c` |

## Collaborating Partner (`mth_collaboratingpartner`)

A partner (account) invited on a building contract. Selected contacts are an N:N to contact. E-mails are activities regarding this record.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: Name (account name)

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_status` | Status | choice | 12 valg | `Subcontractor_Status__c` |
| `mth_partnertype` | Partner Type | choice | 3 valg | `Collaborating_Partner_Type__c` |
| `mth_comments` | Comments | memo | maks. 4000 | `Subcontractor_Comments__c` |
| `mth_tenderevaluation` | Tender Evaluation | choice | 4 valg | `Tender_Evaluation__c (global value set Evaluation)` |
| `mth_tenderevaluationcomment` | Tender Evaluation Comment | string | maks. 255 | `Tender_Evaluation_Comments__c` |
| `mth_contractamount` | Contract Amount | currency |  | `Contract_Amount__c` |
| `mth_lastemailsent` | Last E-mail Sent | date |  | `(new) set when a tender e-mail is sent` |

<details><summary>Valg for <code>mth_status</code> (12)</summary>

Option value = 273280000 + status code. Sending a tender e-mail moves 10 -> 24.

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280010 | 10 Selected for tender round | 10 |
| 273280020 | 20 Wants to submit tender | 20 |
| 273280022 | 22 Will get back | 22 |
| 273280024 | 24 Awaiting tender | 24 |
| 273280030 | 30 Tender received | 30 |
| 273280060 | 60 Contract signed | 60 |
| 273280090 | 90 Deselected for the job | 90 |
| 273280091 | 91 Deselected for tender round | 91 |
| 273280092 | 92 Declines to submit tender | 92 |
| 273280095 | 95 Tender never received | 95 |
| 273280097 | 97 Tender not compliant | 97 |
| 273280099 | 99 To be deleted | 99 |

</details>

<details><summary>Valg for <code>mth_partnertype</code> (3)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Subcontractor | Subcontractor |
| 273280001 | Supplier | Supplier |
| 273280002 | Advisor | Advisor |

</details>

<details><summary>Valg for <code>mth_tenderevaluation</code> (4)</summary>

Yellow, Red and Black require a comment.

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Green | 10 |
| 273280001 | Yellow | 5 |
| 273280002 | Red | 0 |
| 273280003 | Black | -1 |

</details>

## ESP E-mail Template (`mth_espemailtemplate`)

E-mail template for tender e-mails to partners. Placeholders: {{Opportunity}}, {{CaseNumber}}, {{BuildingContract}}, {{TenderDeadline}}, {{Partner}}, {{ContactFirstName}}, {{Sender}}.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: (new)

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_subject` | Subject | string | maks. 400 | `(new)` |
| `mth_body` | Body (HTML) | memo | maks. 100000 | `(new)` |
| `mth_isdefault` | Default | boolean |  | `(new)` |

## Lead (`mth_lead`)

Construction project from SMART (Byggefakta). Matches lead queues by criteria; the first user to convert it gets it and it is closed for everyone else.

Primær navnekolonne: `mth_name` (tekst, maks. 300). Kilde: fnl_Project_Name__c / LastName (project_name or title)

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_projectid` | SMART Project Id | string | maks. 50 | `fnl_Project_Id__c (SMART source_id)` |
| `mth_smartid` | SMART Internal Id | string | maks. 20 | `SMART_Project_Id__c (SMART id)` |
| `mth_smartlink` | SMART Link | string | maks. 300, Url | `fnl_Link__c` |
| `mth_projecttitle` | Project Title | string | maks. 1000 | `fnl_Project_Desc__c (title)` |
| `mth_projecttexts` | Project Description | memo | maks. 100000 | `fnl_Project_Texts__c (description)` |
| `mth_clientname` | Client (Bygherre) | string | maks. 300 | `Lead.Company (role Bygherre)` |
| `mth_clientstreet` | Client Street | string | maks. 300 | `Lead.Street (Bygherre main address)` |
| `mth_clientpostalcode` | Client Postal Code | string | maks. 20 | `Lead.PostalCode` |
| `mth_clientcity` | Client City | string | maks. 100 | `Lead.City` |
| `mth_clientcompanytype` | Client Company Type | string | maks. 255 | `fnl_Company_Type_Desc__c` |
| `mth_clientvatno` | Client VAT No. | string | maks. 50 | `fnl_VAT_no__c (org_nr)` |
| `mth_clienttdcid` | Client TDC Id | string | maks. 50 | `fnl_TDCID__c (par_id)` |
| `mth_constructionsite` | Construction Site | string | maks. 300 | `fnl_Construction_Site__c` |
| `mth_constructiontown` | Construction Town | string | maks. 100 | `fnl_Construction_Town__c` |
| `mth_constructionzip` | Construction Postal Code | string | maks. 20 | `fnl_Zip_Code__c` |
| `mth_projectstartdate` | Construction Start | date |  | `fnl_Start_Date__c` |
| `mth_projectstarttext` | Construction Start (text) | string | maks. 255 | `fnl_Start_Date_Desc__c` |
| `mth_projectenddate` | Construction End | date |  | `fnl_End_Date__c` |
| `mth_projectendtext` | Construction End (text) | string | maks. 255 | `fnl_End_Date_Desc__c` |
| `mth_constructionmonths` | Construction Period (months) | integer | 0–1000 | `fnl_Construction_Period__c` |
| `mth_projectvalue` | Project Value (DKK m) | decimal |  | `fnl_Value__c (value / 1,000,000)` |
| `mth_projectvaluetext` | Project Value (text) | string | maks. 255 | `fnl_Value_Desc__c` |
| `mth_smartcontracttype` | SMART Contract Type | string | maks. 255 | `fnl_Contract_Type__c` |
| `mth_smartcontracttypecode` | SMART Contract Type Code | string | maks. 50 | `fnl_Contract_Type_Code__c` |
| `mth_planningstage` | Planning Stage | string | maks. 255 | `fnl_Planning_Stage__c` |
| `mth_projectstage` | Project Stage | string | maks. 255 | `fnl_Stage__c` |
| `mth_maincategory` | Main Category | string | maks. 255 | `fnl_Main_Category_Desc__c` |
| `mth_maincategorycode` | Main Category Code | string | maks. 50 | `fnl_Main_Category_Code__c` |
| `mth_maindevtype` | Main Development Type | string | maks. 255 | `fnl_Main_Dev_Type_Desc__c` |
| `mth_maindevtypecode` | Main Development Type Code | string | maks. 50 | `fnl_Main_Dev_Type_Code__c` |
| `mth_projectcategories` | Categories | memo | maks. 4000 | `fnl_Categories__c ("category - devtype;...")` |
| `mth_projectcountry` | Country | string | maks. 100 | `fnl_Country_Code__c` |
| `mth_projectregion` | Region | string | maks. 100 | `fnl_County_Desc__c` |
| `mth_projectregioncode` | Region Code | string | maks. 50 | `fnl_County_Code__c` |
| `mth_municipality` | Municipality | string | maks. 100 | `fnl_Municipality_Desc__c` |
| `mth_municipalitycode` | Municipality Code | string | maks. 50 | `fnl_Municipality_Code__c` |
| `mth_apartments` | Apartments | integer | 0–1000000 | `fnl_Apartments__c` |
| `mth_grossfloorarea` | Gross Floor Area (m2) | integer | 0–100000000 | `fnl_Gross_Floor_Area__c` |
| `mth_netfloorarea` | Net Floor Area (m2) | integer | 0–100000000 | `fnl_Net_Floor_Area__c` |
| `mth_volume` | Volume (m3) | integer | 0–1000000000 | `fnl_Volume__c` |
| `mth_storeys` | Storeys | integer | 0–1000 | `fnl_Storeys__c` |
| `mth_storeysbelowground` | Storeys Below Ground | integer | 0–100 | `fnl_Storeys_Below_Ground__c` |
| `mth_roleshtml` | Roles | memo | maks. 100000 | `fnl_Roles_RT__c` |
| `mth_tendershtml` | Tenders | memo | maks. 100000 | `fnl_Tenders__c` |
| `mth_smartupdatedon` | SMART Updated On | datetime |  | `SMART updated_at` |
| `mth_leadstatus` | Lead Status | choice | 3 valg | `Status` |
| `mth_convertedon` | Converted On | datetime |  | `(new)` |

<details><summary>Valg for <code>mth_leadstatus</code> (3)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Open | Open |
| 273280001 | Converted | Converted |
| 273280002 | Disqualified | Unqualified |

</details>

## Lead Queue (`mth_leadqueue`)

A user-defined queue: leads matching ALL filled-in criteria are placed in the queue (a lead can be in several queues). Members are notified about new leads. Keyword fields: ;-separated, match if ANY keyword occurs (case-insensitive).

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: (new)

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_description` | Description | memo | maks. 2000 | `(new)` |
| `mth_isactive` | Active | boolean |  | `(new)` |
| `mth_notifymembers` | Notify Members About New Leads | boolean |  | `(new)` |
| `mth_regions` | Regions | multichoice | 5 valg | `SMART region (mth_projectregion)` |
| `mth_minvalue` | Min. Value (DKK m) | decimal |  | `mth_projectvalue` |
| `mth_maxvalue` | Max. Value (DKK m) | decimal |  | `mth_projectvalue` |
| `mth_categorykeywords` | Category / Development Type Contains | string | maks. 1000 | `mth_projectcategories, mth_maincategory, mth_maindevtype` |
| `mth_stagekeywords` | Planning Stage Contains | string | maks. 1000 | `mth_planningstage, mth_projectstage` |
| `mth_contracttypekeywords` | Contract Type Contains | string | maks. 1000 | `mth_smartcontracttype` |
| `mth_textkeywords` | Project Text Contains | string | maks. 1000 | `mth_name, mth_projecttitle, mth_projecttexts` |
| `mth_excludekeywords` | Exclude If Text Contains | string | maks. 1000 | `mth_name, mth_projecttitle, mth_projecttexts, categories` |
| `mth_startwithinmonths` | Construction Start Within (months) | integer | 0–240 | `mth_projectstartdate (from today)` |
| `mth_excludestarted` | Exclude Projects Already Started | boolean |  | `mth_projectstartdate < today` |

<details><summary>Valg for <code>mth_regions</code> (5)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Hovedstaden | Hovedstaden |
| 273280001 | Sjælland | Sjælland |
| 273280002 | Syddanmark | Syddanmark |
| 273280003 | Midtjylland | Midtjylland |
| 273280004 | Nordjylland | Nordjylland |

</details>

## SMART Lookup (`mth_smartlookup`)

Cache of SMART dictionaries (/lookups, countries, regions, districts) used to translate ids to names.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: External_Key__c ("lookup:id")

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_key` | Key | string | maks. 200 | `External_Key__c` |
| `mth_lookup` | Lookup | string | maks. 100 | `Lookup__c` |
| `mth_smartitemid` | SMART Id | string | maks. 50 | `Smart_Id__c (not mth_SmartLookupId: that is the table's primary key name)` |
| `mth_smartname` | SMART Name | string | maks. 500 | `Smart_Name__c` |
| `mth_rsmcode` | RSM Code | string | maks. 50 | `Rsm_Code__c` |
| `mth_parentsmartid` | Parent SMART Id | string | maks. 50 | `Parent_Smart_Id__c` |

## Integration State (`mth_integrationstate`)

Watermark and status per integration (e.g. SMART projects).

Primær navnekolonne: `mth_name` (tekst, maks. 100). Kilde: Name ("Projects")

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_lastrunat` | Watermark (last run at) | datetime |  | `Last_Run_At__c` |
| `mth_lookupversion` | Lookup Version | string | maks. 50 | `Lookup_Version__c` |
| `mth_lastresult` | Last Result | memo | maks. 20000 | `(new) summary of the last run` |
| `mth_lastfinishedon` | Last Finished On | datetime |  | `(new)` |

## Lead Assignment (`mth_leadassignment`)

One row per lead and lead queue the lead matches, with the queue's own status. Replaces the N:N mth_lead_mth_leadqueue. Maintained by LeadQueueSync; status set by RejectLead and ConvertLead.

Primær navnekolonne: `mth_name` (tekst, maks. 200). Kilde: (new) name of the queue

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_status` | Status | choice | 4 valg | `Lead_Assignment__c.Status__c` |
| `mth_reason` | Reason | memo | maks. 2000 | `(new)` |
| `mth_statuschangedon` | Status Changed On | datetime |  | `(new)` |

<details><summary>Valg for <code>mth_status</code> (4)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | New | New |
| 273280001 | Rejected | Rejected |
| 273280002 | Converted | Converted |
| 273280003 | Lost | Lost |

</details>

## Follow (`mth_follow`)

One row per user and record the user follows. Owner = the follower. Followers get an in-app notification when a tracked field changes or a note is added (plugin FollowNotifier). Created and deleted by the Follow command (webresources/mth_/scripts/follow.js).

Primær navnekolonne: `mth_name` (tekst, maks. 400). Kilde: (new) name of the followed record

| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |
|---|---|---|---|---|
| `mth_recordtable` | Record Table | string | maks. 100 | `EntitySubscription.Parent type` |
| `mth_recordid` | Record Id | string | maks. 100 | `EntitySubscription.ParentId (guid, lower case, no braces)` |
| `mth_key` | Key | string | maks. 200 | `(new) <record id>|<user id>, unique so a user follows a record once` |
