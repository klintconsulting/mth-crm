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

## Relationer (opslag)

| Relation | Opslagskolonne | Peger på | Sletning | Påkrævet | Kilde |
|---|---|---|---|---|---|
| `mth_account_mth_opportunity_Account` | `mth_opportunity.mth_accountid` | `account` | Restrict |  | AccountId |
| `mth_orgunit_mth_opportunity_OrderDept` | `mth_opportunity.mth_orderdepartmentid` | `mth_organizationalunit` | Restrict |  | Order_Department__c |
| `mth_orgunit_mth_opportunity_TenderDept` | `mth_opportunity.mth_tenderdepartmentid` | `mth_organizationalunit` | Restrict |  | Tender_Department__c |
| `mth_opportunity_mth_opportunity_External` | `mth_opportunity.mth_externalopportunityid` | `mth_opportunity` | RemoveLink |  | External_Opportunity__c |
| `mth_opportunity_mth_opportunityrole` | `mth_opportunityrole.mth_opportunityid` | `mth_opportunity` | Parental | Ja | (new) |
| `mth_systemuser_mth_opportunityrole_User` | `mth_opportunityrole.mth_userid` | `systemuser` | Restrict | Ja | MTH_Employee__c.User__c (employees are Entra ID users) |

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
| `mth_probability` | Probability (%) | integer | 0–100 | `Probability` |
| `mth_closedate` | Close Date | date |  | `CloseDate` |
| `mth_opportunitytype` | Opportunity Type | choice | 2 valg | `Type` |
| `mth_nextstep` | Next Step | string | maks. 255 | `NextStep` |
| `mth_nextstephistory` | Next Step History | memo | maks. 32000 | `Next_Step_History__c` |
| `mth_leadsource` | Lead Source | choice | 6 valg | `LeadSource` |
| `mth_casenumber` | Case Number | string | maks. 50 | `Case_Number__c` |
| `mth_autocasenumber` | Auto Case Number | string | maks. 50 | `autosagsnummer__c` |
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
| `mth_bcprojectnumber` | BC Project Number | string | maks. 50 | `BC_Project_Number__c` |
| `mth_iskeyproject` | Key Project | boolean |  | `Key_Project__c` |
| `mth_scope` | Scope | memo | maks. 4000 | `Omfang__c` |

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
| `mth_buildingcontracts` | Building Contracts | multichoice | 214 valg | `Building_Contracts__c` |
| `mth_interestedfor` | Customer Is Interesting For | multichoice | 9 valg | `Kunden_er_interessant_for__c` |
| `mth_tdcid` | TDC ID | string | maks. 50 | `TDC_ID__c` |
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

<details><summary>Valg for <code>mth_buildingcontracts</code> (214)</summary>

| Værdi | Label | Salesforce-værdi |
|---|---|---|
| 273280000 | Alle | Alle |
| 273280001 | ABA-anlæg - 22105 | ABA-anlæg - 22105 |
| 273280002 | Additiver - 21211 | Additiver - 21211 |
| 273280003 | Adgangskontrol - 22104 | Adgangskontrol - 22104 |
| 273280004 | Advokat - 23301 | Advokat - 23301 |
| 273280005 | Afdækning,curing m.v. - 21210 | Afdækning,curing m.v. - 21210 |
| 273280006 | Afløbstilslutning - 24007 | Afløbstilslutning - 24007 |
| 273280007 | Altaner - 21409 | Altaner - 21409 |
| 273280008 | Anlægsarbejder - UE - 22001 | Anlægsarbejder - UE - 22001 |
| 273280009 | Arbejdsmiljø - 21014 | Arbejdsmiljø - 21014 |
| 273280010 | Arkitekt - 23306 | Arkitekt - 23306 |
| 273280011 | Armering - 21202 | Armering - 21202 |
| 273280012 | Artskonto mangler - 21037 | Artskonto mangler - 21037 |
| 273280013 | Asfalt - UE - 22012 | Asfalt - UE - 22012 |
| 273280014 | Automatik - 22113 | Automatik - 22113 |
| 273280015 | Badeværelseskabiner - 21411 | Badeværelseskabiner - 21411 |
| 273280016 | Belægninger - UE - 22010 | Belægninger - UE - 22010 |
| 273280017 | Belægningsmaterialer - 21226 | Belægningsmaterialer - 21226 |
| 273280018 | Benzin - arbejdsplads - 21033 | Benzin - arbejdsplads - 21033 |
| 273280019 | Benzin - maskiner - 23105 | Benzin - maskiner - 23105 |
| 273280020 | Beton støbt på stedet - UE - 22022 | Beton støbt på stedet - UE - 22022 |
| 273280021 | Betonarbejder - UE - 22020 | Betonarbejder - UE - 22020 |
| 273280022 | Betonelelementer - 21410 | Betonelelementer - 21410 |
| 273280023 | Betonrenovering - UE - 22021 | Betonrenovering - UE - 22021 |
| 273280024 | Biler leje - 23008 | Biler leje - 23008 |
| 273280025 | Blikkenslager - UE - 22047 | Blikkenslager - UE - 22047 |
| 273280026 | Boring, skæring & sprængning - UE - 22025 | Boring, skæring & sprængning - UE - 22025 |
| 273280027 | Branddøre - UE - 22056 | Branddøre - UE - 22056 |
| 273280028 | Brolægger - UE - 22011 | Brolægger - UE - 22011 |
| 273280029 | Brøndfundering - UE - 22007 | Brøndfundering - UE - 22007 |
| 273280030 | Bundsikring - 21224 | Bundsikring - 21224 |
| 273280031 | Byggetilladelse - 24002 | Byggetilladelse - 24002 |
| 273280032 | Campdrift - 21025 | Campdrift - 21025 |
| 273280033 | Campindretning - 21024 | Campindretning - 21024 |
| 273280034 | Cement og mørtel - 21205 | Cement og mørtel - 21205 |
| 273280035 | Container incl. tømning - 21022 | Container incl. tømning - 21022 |
| 273280036 | Cykelskure - UE - 22049 | Cykelskure - UE - 22049 |
| 273280037 | Diesel - arbejdsplads - 21032 | Diesel - arbejdsplads - 21032 |
| 273280038 | Diesel - maskiner - 23104 | Diesel - maskiner - 23104 |
| 273280039 | Div. Leverancer - UE - 22064 | Div. Leverancer - UE - 22064 |
| 273280040 | Diverse byggepladsomkostninger - 21012 | Diverse byggepladsomkostninger - 21012 |
| 273280041 | Diverse indkøb træ - 21113 | Diverse indkøb træ - 21113 |
| 273280042 | Diverse vedr. betonarbejder - 21237 | Diverse vedr. betonarbejder - 21237 |
| 273280043 | Dykkerarbejde - UE - 22003 | Dykkerarbejde - UE - 22003 |
| 273280044 | Ejendomsmægler - 23302 | Ejendomsmægler - 23302 |
| 273280045 | El - arbejdsplads - 21029 | El - arbejdsplads - 21029 |
| 273280046 | El - skurby - 21030 | El - skurby - 21030 |
| 273280047 | El-arbejde - 22101 | El-arbejde - 22101 |
| 273280048 | Elementmontage - UE - 22024 | Elementmontage - UE - 22024 |
| 273280049 | Elevator/kran/escalator - 22108 | Elevator/kran/escalator - 22108 |
| 273280050 | El-ingeniør - 23312 | El-ingeniør - 23312 |
| 273280051 | Eltilslutning - 24006 | Eltilslutning - 24006 |
| 273280052 | Etagedæk - 21403 | Etagedæk - 21403 |
| 273280053 | Facadebeklædning - 21102 | Facadebeklædning - 21102 |
| 273280054 | Facader (døre/vinduer) - UE - 22044 | Facader (døre/vinduer) - UE - 22044 |
| 273280055 | Finish - UE - 22026 | Finish - UE - 22026 |
| 273280056 | Fjernvarme  - arbejdsplads - 21036 | Fjernvarme  - arbejdsplads - 21036 |
| 273280057 | Fjernvarmetilslutning - 24005 | Fjernvarmetilslutning - 24005 |
| 273280058 | Fliser & klinker - 21216 | Fliser & klinker - 21216 |
| 273280059 | Fodpaneler - 21110 | Fodpaneler - 21110 |
| 273280060 | Forbrugsgods - 21013 | Forbrugsgods - 21013 |
| 273280061 | Fornybare energikilder - arbejdsplads - 21035 | Fornybare energikilder - arbejdsplads - 21035 |
| 273280062 | Fornybare energikilder - maskiner - 23107 | Fornybare energikilder - maskiner - 23107 |
| 273280063 | Forsikringer - maskiner - 23109 | Forsikringer - maskiner - 23109 |
| 273280064 | Forskalling, incl. leje - 21207 | Forskalling, incl. leje - 21207 |
| 273280065 | Friktionsfyld - 21223 | Friktionsfyld - 21223 |
| 273280066 | Fugning - UE - 22062 | Fugning - UE - 22062 |
| 273280067 | Fyrings- og marinegasolie - arbejdsplads - 21034 | Fyrings- og marinegasolie - arbejdsplads - 21034 |
| 273280068 | Færdigblandet beton - 21204 | Færdigblandet beton - 21204 |
| 273280069 | Gartner - UE - 22009 | Gartner - UE - 22009 |
| 273280070 | Gas - arbejdsplads - 21031 | Gas - arbejdsplads - 21031 |
| 273280071 | Gas - maskiner - 23103 | Gas - maskiner - 23103 |
| 273280072 | Gasanlæg - 22116 | Gasanlæg - 22116 |
| 273280073 | Gasbeton & Lecablokke - 21209 | Gasbeton & Lecablokke - 21209 |
| 273280074 | Gastilslutning - 24009 | Gastilslutning - 24009 |
| 273280075 | Geotekniker - 23314 | Geotekniker - 23314 |
| 273280076 | Glarmester - UE - 22046 | Glarmester - UE - 22046 |
| 273280077 | Grundvandssænkning - UE - 22016 | Grundvandssænkning - UE - 22016 |
| 273280078 | Grus og tilsl.mat.incl.kørsel - 21206 | Grus og tilsl.mat.incl.kørsel - 21206 |
| 273280079 | Gulvbelægning - UE - 22039 | Gulvbelægning - UE - 22039 |
| 273280080 | Gulve - 21109 | Gulve - 21109 |
| 273280081 | Halfeneisen & beslag - 21219 | Halfeneisen & beslag - 21219 |
| 273280082 | Havne- og toldafgifter - 23206 | Havne- og toldafgifter - 23206 |
| 273280083 | Hegn - UE - 22017 | Hegn - UE - 22017 |
| 273280084 | Hegn, porte, m.v. - 21405 | Hegn, porte, m.v. - 21405 |
| 273280085 | Hejse leje - 23004 | Hejse leje - 23004 |
| 273280086 | Håndlister, rækværker, m.v. - 21106 | Håndlister, rækværker, m.v. - 21106 |
| 273280087 | Indervægge - 21101 | Indervægge - 21101 |
| 273280088 | Indv. døre, træ - 21104 | Indv. døre, træ - 21104 |
| 273280089 | Ingeniør - 23308 | Ingeniør - 23308 |
| 273280090 | Installationer ingeniør - 23310 | Installationer ingeniør - 23310 |
| 273280091 | Intern Tømrer - UE - 22066 | Intern Tømrer - UE - 22066 |
| 273280092 | Inventar - 21412 | Inventar - 21412 |
| 273280093 | Inventar - UE - 22063 | Inventar - UE - 22063 |
| 273280094 | Isoleringsmaterialer - 21221 | Isoleringsmaterialer - 21221 |
| 273280095 | IT- & telefoninstallationsanlæg - 22102 | IT- & telefoninstallationsanlæg - 22102 |
| 273280096 | Jord & kloak - UE - 22006 | Jord & kloak - UE - 22006 |
| 273280097 | Jord & terrænarbejde - UE - 22005 | Jord & terrænarbejde - UE - 22005 |
| 273280098 | Jordmaterialer - 21222 | Jordmaterialer - 21222 |
| 273280099 | Kajudstyr - 21235 | Kajudstyr - 21235 |
| 273280100 | Kantsten, fliser m.v. - 21230 | Kantsten, fliser m.v. - 21230 |
| 273280101 | Kloak og dræn - 21227 | Kloak og dræn - 21227 |
| 273280102 | Konsulenthonorar - 23305 | Konsulenthonorar - 23305 |
| 273280103 | Kontrolforanstaltninger - 21016 | Kontrolforanstaltninger - 21016 |
| 273280104 | Kraner leje - 23009 | Kraner leje - 23009 |
| 273280105 | Kunststofbelægning - UE - 22040 | Kunststofbelægning - UE - 22040 |
| 273280106 | Køkken - UE - 22057 | Køkken - UE - 22057 |
| 273280107 | Køkkener - 21406 | Køkkener - 21406 |
| 273280108 | Køleanlæg - 22114 | Køleanlæg - 22114 |
| 273280109 | Kørsel materialer - 21212 | Kørsel materialer - 21212 |
| 273280110 | Kørsel og fragt - 23202 | Kørsel og fragt - 23202 |
| 273280111 | Landinspektør - 23313 | Landinspektør - 23313 |
| 273280112 | Landskabsarkitekt - 23307 | Landskabsarkitekt - 23307 |
| 273280113 | Lemme, m.v. - 21105 | Lemme, m.v. - 21105 |
| 273280114 | Letbetonmontage - UE - 22028 | Letbetonmontage - UE - 22028 |
| 273280115 | Lette facader - UE - 22042 | Lette facader - UE - 22042 |
| 273280116 | Lette vægge/lofter - UE - 22052 | Lette vægge/lofter - UE - 22052 |
| 273280117 | Lofter - 21108 | Lofter - 21108 |
| 273280118 | Luftfragt - 23204 | Luftfragt - 23204 |
| 273280119 | Løfteudstyr leje - 23015 | Løfteudstyr leje - 23015 |
| 273280120 | Maler - UE - 22059 | Maler - UE - 22059 |
| 273280121 | Marinegasolie - maskiner - 23106 | Marinegasolie - maskiner - 23106 |
| 273280122 | Materielleje, anlæg ekstern - 23006 | Materielleje, anlæg ekstern - 23006 |
| 273280123 | Materielleje, beton ekstern - 23005 | Materielleje, beton ekstern - 23005 |
| 273280124 | Membran - UE - 22038 | Membran - UE - 22038 |
| 273280125 | Miljøarbejde - UE - 22008 | Miljøarbejde - UE - 22008 |
| 273280126 | Miljøundersøgelser - 23315 | Miljøundersøgelser - 23315 |
| 273280127 | Mobilkraner leje - 23010 | Mobilkraner leje - 23010 |
| 273280128 | Murer - UE - 22030 | Murer - UE - 22030 |
| 273280129 | Murermaterialer - 21215 | Murermaterialer - 21215 |
| 273280130 | Nedhængte lofter - 21107 | Nedhængte lofter - 21107 |
| 273280131 | Nedrivning - UE - 22018 | Nedrivning - UE - 22018 |
| 273280132 | Off. myndigheder - 24001 | Off. myndigheder - 24001 |
| 273280133 | Overfladebehandling - UE - 22027 | Overfladebehandling - UE - 22027 |
| 273280134 | Pakkeomkostninger - 23201 | Pakkeomkostninger - 23201 |
| 273280135 | Pladsdrift - 21021 | Pladsdrift - 21021 |
| 273280136 | Pladsindretning - 21020 | Pladsindretning - 21020 |
| 273280137 | Platforme leje - 23001 | Platforme leje - 23001 |
| 273280138 | Porte - UE - 22055 | Porte - UE - 22055 |
| 273280139 | Pæle - UE - 22013 | Pæle - UE - 22013 |
| 273280140 | Pælekapning - UE - 22014 | Pælekapning - UE - 22014 |
| 273280141 | Rengøring - arbejdsplads - 21010 | Rengøring - arbejdsplads - 21010 |
| 273280142 | Rengøring - UE - 22060 | Rengøring - UE - 22060 |
| 273280143 | Renoveringsmaterialer - 21111 | Renoveringsmaterialer - 21111 |
| 273280144 | Reservedele og vedligeholdelse - 23102 | Reservedele og vedligeholdelse - 23102 |
| 273280145 | Revisor - 23303 | Revisor - 23303 |
| 273280146 | Sand- og ralleverancer - UE - 22004 | Sand- og ralleverancer - UE - 22004 |
| 273280147 | Sikkerhedsforanstaltninger - 21015 | Sikkerhedsforanstaltninger - 21015 |
| 273280148 | Skrot - 21028 | Skrot - 21028 |
| 273280149 | Skurleje - 23003 | Skurleje - 23003 |
| 273280150 | Skære-/bore-/slibematerialer - 21234 | Skære-/bore-/slibematerialer - 21234 |
| 273280151 | Smed - UE - 22048 | Smed - UE - 22048 |
| 273280152 | Smøre- og hydraulikolie - 23101 | Smøre- og hydraulikolie - 23101 |
| 273280153 | Snedker - UE - 22041 | Snedker - UE - 22041 |
| 273280154 | Solafskærmning - UE - 22061 | Solafskærmning - UE - 22061 |
| 273280155 | Solceller - 22107 | Solceller - 22107 |
| 273280156 | Specialmørtler & epoxy - 21214 | Specialmørtler & epoxy - 21214 |
| 273280157 | Sprinkler - 22112 | Sprinkler - 22112 |
| 273280158 | Sprængstof og detonatorer - 21233 | Sprængstof og detonatorer - 21233 |
| 273280159 | Spuns & pæle - 21228 | Spuns & pæle - 21228 |
| 273280160 | Spunsning - UE - 22015 | Spunsning - UE - 22015 |
| 273280161 | Spændarmering - UE - 22023 | Spændarmering - UE - 22023 |
| 273280162 | Stabiltgrus - 21225 | Stabiltgrus - 21225 |
| 273280163 | Stikledninger - 24004 | Stikledninger - 24004 |
| 273280164 | Stillads leje - 23002 | Stillads leje - 23002 |
| 273280165 | Storkøkkener - UE - 22058 | Storkøkkener - UE - 22058 |
| 273280166 | Stål/alufacader - UE - 22045 | Stål/alufacader - UE - 22045 |
| 273280167 | Ståldøre - 21413 | Ståldøre - 21413 |
| 273280168 | Stålkonstruktioner - UE - 22050 | Stålkonstruktioner - UE - 22050 |
| 273280169 | Stålmaterialer - 21236 | Stålmaterialer - 21236 |
| 273280170 | Ståltrapper - 21414 | Ståltrapper - 21414 |
| 273280171 | Svagstrøm diverse - 22106 | Svagstrøm diverse - 22106 |
| 273280172 | Søfragt - 23203 | Søfragt - 23203 |
| 273280173 | Tag/vægkassetter - 21402 | Tag/vægkassetter - 21402 |
| 273280174 | Tagdækning - UE - 22037 | Tagdækning - UE - 22037 |
| 273280175 | Tagelementer - UE - 22035 | Tagelementer - UE - 22035 |
| 273280176 | Tagkonstruktion - 21401 | Tagkonstruktion - 21401 |
| 273280177 | Tagsten - 21203 | Tagsten - 21203 |
| 273280178 | Teknisk isolering - 22115 | Teknisk isolering - 22115 |
| 273280179 | Tilslutningsafgifter - 24003 | Tilslutningsafgifter - 24003 |
| 273280180 | Timebaserede omkostninger - 21027 | Timebaserede omkostninger - 21027 |
| 273280181 | Trapper - 21404 | Trapper - 21404 |
| 273280182 | Trapper af træ/indv.trapper - UE - 22036 | Trapper af træ/indv.trapper - UE - 22036 |
| 273280183 | Træ og plader generelt - 21220 | Træ og plader generelt - 21220 |
| 273280184 | Trægulve - UE - 22033 | Trægulve - UE - 22033 |
| 273280185 | TV & antennetilslutning - 24008 | TV & antennetilslutning - 24008 |
| 273280186 | Tyverialarm - 22103 | Tyverialarm - 22103 |
| 273280187 | Tyverisikring - arbejdsplads - 21017 | Tyverisikring - arbejdsplads - 21017 |
| 273280188 | Tømrer/snedker - UE - 22032 | Tømrer/snedker - UE - 22032 |
| 273280189 | Uddybning - UE - 22002 | Uddybning - UE - 22002 |
| 273280190 | Udtørring - 21039 | Udtørring - 21039 |
| 273280191 | Udviklingshonorarer - 23316 | Udviklingshonorarer - 23316 |
| 273280192 | Vagt - arbejdsplads - 21011 | Vagt - arbejdsplads - 21011 |
| 273280193 | Vejskilte og tilbehør - 21232 | Vejskilte og tilbehør - 21232 |
| 273280194 | Ventilation - 22111 | Ventilation - 22111 |
| 273280195 | Vinduer og udv. døre - 21103 | Vinduer og udv. døre - 21103 |
| 273280196 | Vinduesarbejder - UE - 22043 | Vinduesarbejder - UE - 22043 |
| 273280197 | Vinterforanstaltninger - 21038 | Vinterforanstaltninger - 21038 |
| 273280198 | VVS-installationer - 22109 | VVS-installationer - 22109 |
| 273280199 | Væg- og tagbeklædning - UE - 22031 | Væg- og tagbeklædning - UE - 22031 |
| 273280200 | Vægtafgift - maskiner - 23108 | Vægtafgift - maskiner - 23108 |
| 273280201 | Værkstedsudstyr leje - 23007 | Værkstedsudstyr leje - 23007 |
| 273280202 | Værktøj (<13.200) - 21018 | Værktøj (<13.200) - 21018 |
| 273280203 | Øvrige installationsentrepriser - 22117 | Øvrige installationsentrepriser - 22117 |
| 273280204 | Øvrige lejer arbejdsplads - 23016 | Øvrige lejer arbejdsplads - 23016 |
| 273280205 | Øvrige materialer - 21231 | Øvrige materialer - 21231 |
| 273280206 | Øvrige transportudgifter - 23207 | Øvrige transportudgifter - 23207 |
| 273280207 | Øvrige tømrermaterialer - 21114 | Øvrige tømrermaterialer - 21114 |
| 273280208 | Øvrige uspec. anlægsentrepriser - UE - 22019 | Øvrige uspec. anlægsentrepriser - UE - 22019 |
| 273280209 | Øvrige uspec. bygge og anlægsentrepriser - UE - 22065 | Øvrige uspec. bygge og anlægsentrepriser - UE - 22065 |
| 273280210 | Øvrigt snedkerarbejde - 21112 | Øvrigt snedkerarbejde - 21112 |
| 273280211 | Leasing/leje gulplade biler - 29010 | Leasing/leje gulplade biler - 29010 |
| 273280212 | Kontorartikler - 45030 | Kontorartikler - 45030 |
| 273280213 | 750 - Pakkeomkos | 750 - Pakkeomkos |

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
