# Beslutninger

Nyeste nederst. Hver beslutning har dato, begrundelse og konsekvens.

## B1 – Model-driven Power App på Dataverse, uden Dynamics 365 Sales (2026-11-09)
MTH synes Salesforce er for dyrt og er et Microsoft-hus. Kunden bruger Power Apps andre steder, og en mindre licens pr. bruger er acceptabel. Dynamics 365 Sales giver ingen væsentlig besparelse og bruges ikke.
**Konsekvens:** Salgsmulighed (og senere Lead) er custom tabeller. Account og Contact er Dataverse-standardtabeller.

## B2 – Ingen separat SQL-database (2026-11-19)
Dataverse er databasen (driftet Azure SQL). Datamængden er ca. 2 GB.
**Konsekvens:** Azure SQL kommer kun på tale til arkiv over historiske data, der ikke migreres. BI læser via Fabric- eller Synapse-link eller TimeXtender, ikke direkte fra appen.

## B3 – Engelske interne navne, engelske labels med dansk oversættelse (2026-11-19)
Samme princip som i Salesforce i dag (en_US master, dansk via oversættelse).
**Konsekvens:** Schema-navne som `mth_TenderDate`. Danske labels tilføjes senere via sprogpakke og oversættelse.

## B4 – Én status med koder 01–99 (2026-11-19)
Salesforce har tre overlappende felter: `StageName`, `Case_Status__c` (næsten samme liste) og `Stage__c` (Prospect/Won/Lost…). Kun StageName-listen bevares.
**Konsekvens:** `mth_status` er et valgsæt, hvor option value = 273280000 + statuskode (60 MTH contract = 273280060). Koden kan derfor udledes af værdien uden en formel. 60–85 = vundet, 90+ = lukket uden ordre. `Case_Status__c` og `Stage__c` migreres ikke.

## B5 – Sagsteam som undertabel (2026-11-19)
Salesforce har over 20 opslag til medarbejdere i roller, mange i nummererede varianter (`Tender_Calculator21/22/23/24`).
**Konsekvens:** Tabellen `mth_opportunityrole` (Salgsmulighed + Bruger + Rolle). Der kan være flere personer pr. rolle, og en ny rolle er bare en ny valgmulighed.

## B6 – Roller peger på systembrugere, ingen medarbejdertabel (2026-11-19)
Salesforce har `MTH_Employee__c` (fra CSOD). Alle i roller er brugere, der kan logge ind i organisationen, og de findes derfor i Entra ID. En separat medarbejdertabel er ikke nødvendig (besluttet af kunden).
**Konsekvens:** `mth_opportunityrole.mth_userid` er et påkrævet opslag til `systemuser`. En person kan kun sættes i en rolle, hvis vedkommende findes som bruger i Dataverse-miljøet. Entra-brugere synkes ind, når de er tilføjet miljøet (licens eller tilføjelse via sikkerhedsgruppe). Ved migreringen mappes `MTH_Employee__c` via `User__c` eller e-mail til systembrugeren. En tabel `mth_employee` blev oprettet og slettet igen samme dag.

## B7 – Kontakt har ét opslag til Account (2026-11-19)
`Active_Lookup__c`/`Inactive_Lookup__c` i Salesforce er forældede.
**Konsekvens:** Kun standardkolonnen `parentcustomerid`. Fratrådte kontakter markeres med `mth_leftdate` (og evt. inaktiv status).

## B8 – Udskudt: SMART-integration og Byggeprojekt (2026-11-19)
Starter med Account, Contact og Opportunity.
**Konsekvens:** `fnl_*`-felterne og Byggeprojekt-tabellen kommer med SMART-integrationen senere.

## B9 – Ikke med i første version (2026-11-19)
Lotus Notes-felter (`LN*`, integrationen er nedlagt), NN Markedsdata-felter (`NN_*`, kommer med ny integration), UBW- og projektportalfelter (integrationer, afklares senere), pakkefelter (`DashboardsGSP__*`), konstante felter (`Assignment_criteria__c`, `Inherit_from_Main_Opportunity__c`) og oprydningsfelterne `Last*__c`.

## B10 – Sagsnummer = BC-sagsnummer (2026-11-19)
Sagsnummeret kommer fra integrationen med Business Central. I Salesforce er `Case_Number__c` altid lig med `BC_Project_Number__c` (label "BC Sagsnr"), og `autosagsnummer__c` er Salesforces interne autonummer.
**Konsekvens:** Kun én kolonne, `mth_bcprojectnumber` med label "BC Case Number", som BC-integrationen skriver til. `mth_casenumber` og `mth_autocasenumber` blev oprettet og slettet igen.

## B11 – Security roles: Basic User + én MTH-rolle (2026-11-19)
Tre roller i `scripts/security/roles.json`: **MTH Sales** (læser alt, retter egne sager, vedligeholder Accounts og Kontakter), **MTH Tender Manager** (retter, sletter og omfordeler alle sager) og **MTH CRM Administrator** (alt, inkl. afdelinger og sletning af Accounts og Kontakter). Ingen af de to første kan slette Accounts eller Kontakter.
**Konsekvens:** Hver bruger skal have systemrollen **Basic User** (noter, aktiviteter, brugerindstillinger) **plus** én MTH-rolle. Rollerne har adgang til appen. Alle rettigheder er på organisationsniveau eller "egne poster", fordi MTH i første omgang kun har én business unit.

## B12 – @omtale i noter med in-app notifikation (2026-11-20)
Brugerne vil kunne nævne en kollega i en note (som Chatter). Valgt frem for indlæg (Posts) med @omtale, som ikke giver besked, og frem for Teams, hvor samtalen ikke ligger på posten.
**Konsekvens:** Det asynkrone plugin `NoteMentionNotifier` på oprettelse af noter. `@Fulde Navn` eller `@initialer` (mail-alias) giver en in-app notifikation med link til posten. In-app notifications er slået til i appen. Se [plugins.md](plugins.md).

## B13 – Kommentarfelt med @-vælger over tidslinjen (2026-11-20)
Brugerne forventer en navnevælger, når de taster `@`. Tidslinjens note-editor kan ikke udvides, og indlæg (Posts) med @-vælger kræver Activity Feeds, som kun følger med Dynamics 365-apps.
**Konsekvens:** Egen PCF-kontrol `mth_Mth.MentionComposer` lige over tidslinjen. Den har en `@`-vælger, understøtter flere modtagere hvor som helst i teksten og gemmer som en almindelig note, så kommentaren vises i tidslinjen, og plugin'et fra B12 sender notifikationerne. Ingen separat kommentarliste, så intet vises to gange. Se [pcf.md](pcf.md).

## B14 – Projektsite som link, der åbnes i ny fane (2026-11-20)
Hver Salgsmulighed har sit eget SharePoint-projektsite. Det blev afprøvet at vise sitets Shared Documents indlejret med Dataverses dokumentintegration, men mappenavigation virker ikke i et site pr. sag (se [sharepoint.md](sharepoint.md)). Brugerens beslutning: **bare et link, der åbner sitet i en ny fane, som i Salesforce.**
**Konsekvens:** Kolonnen `mth_projectsiteurl` (URL-format) på fanen General. Ingen dokumentintegration, ingen dokumentplaceringer og intet formularscript. Oprettelsesflowet skal kun skrive sitets URL. Ved migrering fyldes kolonnen fra `Project_Portal_Url__c`.

## B15 – ESP: samme struktur, fag som tabel, kontakter som relation (2026-11-20)
Salesforce-strukturen genbruges (Entreprise, UE pr. entreprise), men fag bliver en tabel (`mth_trade`) med N:N til Account i stedet for et multivalg med 214 værdier, og valgte kontakter bliver en N:N til Kontakt i stedet for tekstfelter med id'er og mails. Dette afgør også det åbne spørgsmål om Building Contracts på Account. Kolonnen `account.mth_buildingcontracts` er slettet.
**Konsekvens:** Se [esp.md](esp.md).

## B16 – Tilbudsmails: én pr. UE, fra brugeren, status 10 → 24 (2026-11-20)
Brugerens valg: én mail pr. UE til de valgte kontakter (i stedet for én BCC-mail), afsender er brugerens egen postkasse, og status skifter automatisk fra 10 til 24 ved afsendelse. Skabeloner, evaluering med kommentar, opret kontakt og kopiér entreprise er med i første version.
**Konsekvens:** Mails er Dataverse-e-mailaktiviteter regarding UE-posten, sendt via en Custom API. Se [esp.md](esp.md).

## B17 – SMART: Lead som i Salesforce, Azure Function, testdata (2026-11-20)
Brugerens valg: projektfelterne ligger på Lead (og kopieres til Salgsmulighed) som i Salesforce. Dette erstatter Byggeprojekt-tabellen fra B8. Synken kører som C# Azure Function, først lokalt. POC'en bruger anonymiserede SMART-testsvar, og rigtigt API og nøgler kobles på hos MTH.
**Konsekvens:** Se [smart.md](smart.md).

## B18 – Lead-køer med brugerdefinerede kriterier; første konvertering vinder (2026-11-20)
Brugerens krav: samme lead kan ramme flere køer, brugerne definerer selv kriterierne, og den første, der konverterer, får leadet, som så lukkes for de andre. Valgt som kø-tabel med faste kriterier frem for delte visninger, fordi det giver medlemmer, notifikationer og overblik.
**Konsekvens:** `mth_leadqueue` med kriterier og medlemmer, beregnet N:N lead↔kø og konvertering via Custom API med optimistisk samtidighed. Erstatter Salesforce' assignment rules, køer og afdelingsvisninger.

## B19 – SMART-synk i Azure med managed identity og Flex Consumption (2026-11-20)
Function App'en logger på Dataverse med sin egen managed identity, som er registreret som application user. Den bruger ikke en app-registrering med klienthemmelighed, så der er ingen hemmelighed at gemme, udskifte eller lække. Storage og Application Insights tilgås også med identiteten. Hostingplanen er Flex Consumption: Microsofts anbefalede serverløse plan for nye Function Apps, hvor Linux Consumption er på vej ud. Infrastrukturen er en ARM-skabelon, der deployes med REST, fordi Azure CLI ikke er tilladt på alle maskiner.
**Konsekvens:** Security role "MTH Integration" til integrationer. Tidsplanen angives i UTC. Kun SMART-API'ets egne nøgler skal i Key Vault hos MTH. Se [smart.md](smart.md#drift-i-azure).
