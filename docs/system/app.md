# Appen "MTH CRM"

En model-driven app (`mth_MTHCRM`), defineret i `scripts/app/app.json` og oprettet med `scripts/Deploy-App.ps1`. Den udpakkede solution under `solutions/MTHCRM/src` (`AppModules/`, `AppModuleSiteMaps/`, `FormXml/`, `SavedQueries/`) er resultatet efter `pac solution sync`.

## Navigation (sitemap)

| Område | Gruppe | Tabeller |
|---|---|---|
| Sales | Leads | Lead, Lead Queue (se [smart.md](smart.md)) |
| Sales | Sales | Opportunity |
| | Customers | Account, Contact |
| | Setup | Organizational Unit |

`systemuser` er med i appen uden formular eller visning, kun så opslag til brugere (ejer og sagsteam) virker. Valideringen giver derfor advarslen "Entity doesn't reference a form or view". Den er forventet.

## Formularer (main forms)

| Tabel | Formular | Faner |
|---|---|---|
| Opportunity | MTH Opportunity | General (stamdata inkl. **Project Site URL**-link, beskrivelse, beløb, **sagsteam som undergitter**, **tidslinje**) · **Tender (ESP)** (arbejdsrummet, se [esp.md](esp.md)) · Tender and Dates · Classification · Delivery |
| Opportunity Role | MTH Opportunity Role | General (Salgsmulighed, rolle, bruger) |
| Account | MTH Account | General (stamdata, adresse, klassifikation, **tidslinje**) · Contacts and Opportunities (undergitre) · Building Contracts |
| Contact | MTH Contact | General (stamdata, fratrådt, markedsføringsaftale, **tidslinje**) |
| Organizational Unit | MTH Organizational Unit | General |

**Tidslinje** (noter, opslag og aktiviteter som opgaver, mails og møder) ligger i tredje kolonne på fanen General med **kommentarfeltet med @-vælger** (PCF `MentionComposer`, se [pcf.md](pcf.md)) lige over. I `app.json` er det en sektion med `"composer": true` og `"timeline": true`. En vilkårlig PCF-kontrol over flere rækker placeres med `"pcf": { "control": "mth_Mth.X", "rows": 30 }`.

**System Information** (som i Salesforce): `Deploy-App.ps1` tilføjer automatisk en sektion nederst i sidste kolonne på den sidste synlige fane i alle formularer, med Owner, Created By, Created On, Modified By og Modified On. Den defineres ikke i `app.json`. Felter, der allerede står på formularen, springes over, og det samme gør Owner på tabeller ejet af organisationen. Felterne er skrivebeskyttede systemfelter. Den fulde ændringshistorik (hvem ændrede hvilket felt fra hvad til hvad og hvornår, som Field History i Salesforce) er **auditering**. Den er slået til for Salgsmulighed og Lead med `scripts/Deploy-Audit.ps1` (2026-11-24) og vises på posten under **Related → Audit History**. Kun ændringer efter aktiveringen registreres. Auditdata tæller i Dataverse' log-kapacitet, så sæt en opbevaringsperiode i Power Platform admin center (Environment → Settings → Audit settings). Organisationsindstillingen følger ikke med i solutionen. Kør scriptet i hvert miljø efter import.

Appen bruger kun disse formularer. Standardformularerne på Account og Contact røres ikke og er ikke med i solutionen.

## Visninger

| Tabel | Visning | Kolonner | Sortering |
|---|---|---|---|
| Opportunity | MTH Active Opportunities (standard) | Name, Account, Status, Amount, Close Date, Order Department, Owner | Close Date stigende |
| Opportunity Role | MTH Opportunity Roles | Role, User | Role |
| Account | MTH Active Accounts | Name, VAT No., Segment, Status, City, Phone, Owner | Name |
| Contact | MTH Active Contacts | Full Name, Account, Job Title, Email, Mobile, Left Date | Full Name |
| Organizational Unit | MTH Active Organizational Units | Name, Department No., Division, Business Unit, Tender Department | Department No. |

Alle visninger viser kun aktive poster (`statecode = 0`).

## Sådan ændres appen

1. Ret `scripts/app/app.json` (felter i sektioner, kolonner i visninger, navigation).
2. Kør `.\scripts\Deploy-App.ps1 -Url https://<org>.crm4.dynamics.com`. Visninger, formularer og sitemap matches på navn og overskrives.
3. Kør `pac solution sync --solution-folder solutions/MTHCRM` og commit.

Ændringer lavet direkte i formular-designeren i make.powerapps.com bliver **overskrevet** næste gang scriptet køres. Ret derfor altid i `app.json`, eller stop med at bruge scriptet for den pågældende formular.

## Microsoft Copilot i appen (sat op, men ikke aktiv)

Microsoft Copilot (chat, der svarer på spørgsmål om data i appen via Dataverse search) er **slået til i appen**, men vises ikke endnu, fordi licensen mangler. Status 2026-11-26:

| Krav | Status i udviklermiljøet |
|---|---|
| App-indstillingen `m365copilotmodelappenabled` = 2 (app-designer: Settings → Features → "Microsoft Copilot in model-driven apps") | **Sat og publiceret.** Sættes også af `Deploy-Plugins.ps1` ud fra `appSettings` i `scripts/plugins/steps.json` |
| Microsoft 365 Copilot-licens pr. bruger (oven i Power Apps Premium) | **Mangler.** Brugeren har kun Microsoft 365 Business Basic og Power Apps Developer. "Microsoft 365 Copilot (add-on)" har en prøveversion i Microsoft 365 admin center → Fakturering → Køb tjenester → AI-løsninger. Brugeren venter med den |
| Power Apps Premium-licens | Mangler (kun Developer-adgang). Uvist, om Developer er nok |
| Tenant: Microsoft 365 admin center → Copilot → Settings → "Dataverse data available in Microsoft Copilot" | Ikke kontrolleret |
| Miljø: Power Platform admin center → Copilot → Settings → Power Apps → Microsoft Copilot = On | Ikke kontrolleret |
| Dataverse search slået til | OK (se [search.md](search.md)) |

**Faldgrube:** Indstillingen i app-designeren gemmes som en **kladde** (ikke-publiceret `appsetting`) og virker først, når appen publiceres. Kladden ses ikke med en almindelig GET, kun med `appsettings/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()`, og en POST af samme indstilling fejler med en dubletfejl.

Copilot kan kun læse data (ingen oprettelse eller rettelse uden en egen agent) og virker ikke i Power Apps-mobilappen. Den gamle "Copilot chat" (`appcopilotenabled`) kræver ikke en Microsoft 365 Copilot-licens, men er ved at blive udfaset og er ikke afprøvet. For MTH betyder det: Copilot koster en Microsoft 365 Copilot-licens pr. bruger oven i Power Apps, medmindre de allerede har den. Kilde: [Microsoft Learn](https://learn.microsoft.com/en-us/power-apps/maker/model-driven-apps/add-microsoft-365-copilot).

## Kendte begrænsninger i scriptet

- Selve app-posten (navn og beskrivelse) opdateres ikke, hvis appen findes. En PATCH på en upubliceret app opfører sig som en oprettelse. Ret navnet i make.powerapps.com.
- Formularer har endnu ingen header eller forretningsregler.
- **Tabeller tilføjes IKKE til appen af scriptet.** `AddAppComponents` via Web API med en tabelreference gemmer systemtabellen `entity` i stedet for den tabel, man peger på. Appen indeholdt derfor fra 2026-11-19 til 2026-11-24 kun `entity` (10 gange) og ingen af MTH's tabeller. Navigationen virkede, fordi den kommer fra sitemappet, men den globale søgning fejlede med `SearchableEntityNotFound` (se [search.md](search.md)). Appens tabeller står nu i `solutions/MTHCRM/src/AppModules/mth_MTHCRM/AppModule.xml` som `<AppModuleComponent type="1" schemaName="…" />` og kommer med ved import af solutionen. **Tilføjes en ny tabel til appen**, skal den skrives ind i AppModule.xml, og solutionen importeres (`dotnet build solutions/MTHCRM` + `pac solution import --path solutions/MTHCRM/bin/Debug/MTHCRM.zip --publish-changes`). Scriptet advarer, hvis en tabel fra `app.json` mangler i filen. Web API'ets `appmodulecomponents` viser fortsat `entity` som objectid for tabeller i en unmanaged app, så den kan ikke bruges til kontrol. Brug AppModule.xml efter `pac solution sync`.
- Labels er engelske. Dansk kommer via oversættelse (se beslutning B3).
