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

## B14 – SharePoint Shared Documents indlejret med Dataverses dokumentintegration (2026-11-20)
Hver Salgsmulighed har sit eget SharePoint-projektsite (som i Salesforce). Brugeren vil se Shared Documents indlejret og ikke et link. En IFRAME blev fravalgt, fordi SharePoint ofte ikke vil vises i en frame.
**Konsekvens:** Dataverses indbyggede dokumenthåndtering med én `sharepointdocumentlocation` pr. sag og en absolut URL til sagens eget bibliotek. Fanen **Project Site** vises fra status 20 (formularscript). Oprettelsesflowet (site og placering) bygges senere. Se [sharepoint.md](sharepoint.md).
