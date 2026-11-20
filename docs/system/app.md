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

## Kendte begrænsninger i scriptet

- Selve app-posten (navn og beskrivelse) opdateres ikke, hvis appen findes. En PATCH på en upubliceret app opfører sig som en oprettelse. Ret navnet i make.powerapps.com.
- Formularer har endnu ingen header eller forretningsregler.
- Tabelkomponenter sendes igen ved hver kørsel, fordi `appmodulecomponent.objectid` for tabeller ikke er tabellens MetadataId. `AddAppComponents` er idempotent, så der opstår ingen dubletter.
- Labels er engelske. Dansk kommer via oversættelse (se beslutning B3).
