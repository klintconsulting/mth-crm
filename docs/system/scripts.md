| `scripts/New-ResourceTestData.ps1` | Demodata til Ressourceoverblikket: fordeler sagsteamsroller på demobrugerne med procent og datoer, ét delt eksempel, tilbudsafdelinger og fravær | Kun i udviklermiljøet, kan køres igen. Se [resources.md](resources.md) |
| `scripts/New-FollowRibbon.ps1` | Skriver de klassiske knapper Follow og Unfollow i RibbonDiff.xml for Salgsmulighed, Lead og Account i den udpakkede solution | Efter `pac solution sync`, før build og import af solutionen. Se [follow.md](follow.md) |
# Scripts

| Script | Formål | Hvornår |
|---|---|---|
| `scripts/Dataverse.psm1` | Login til Dataverse Web API i browseren (PKCE, `-DeviceCode` for device code), token-cache og `Invoke-Dataverse` | Bruges af de andre scripts eller interaktivt |
| `scripts/Invoke-DataverseSql.ps1` | Kører en skrivebeskyttet SQL-forespørgsel mod Dataverse' TDS-endpoint (`<org>:5558`) med samme login. `-File` læser fra en .sql-fil, `-Grid` viser i et vindue, `-Csv` gemmer. VS Code-udvidelsen mssql kan ikke forbinde til Dataverse (den kalder `DATABASEPROPERTYEX` ved opstart). SSMS virker | Ad hoc-forespørgsler i udvikling og fejlsøgning |
| `scripts/Deploy-Audit.ps1` | Auditering (feltændringshistorik): slår det til i organisationen og på tabellerne i `-Tables` (standard Salgsmulighed og Lead) | I hvert miljø efter import. Se app.md |
| `scripts/Deploy-Nne.ps1` | NNE: miljøvariablerne `mth_NneEndpoint` og `mth_NneAccessKey` i solutionen, og nøglens værdi uden for solutionen (fra `-AccessKey` eller `%LOCALAPPDATA%\mth-crm\nne-accesskey.txt`) | Efter Deploy-Schema, før Deploy-Plugins, og i hvert miljø efter import. Se nne.md |
| `scripts/Deploy-Search.ps1` + `scripts/search/search.json` | Dataverse search: organisationsindstillinger, tabeller i indekset og Quick Find-visningernes søgekolonner | Efter Deploy-App, og i hvert miljø efter import. Se search.md |
| `scripts/Deploy-Schema.ps1` | Opretter tabeller, kolonner og opslag fra `schema/schema.json` i solutionen `MTHCRM` | Efter hver ændring af schema.json og ved opbygning af et nyt miljø |
| `scripts/New-SchemaDoc.ps1` | Genererer `docs/system/datamodel.md` fra schema.json | Efter hver ændring af schema.json |
| `scripts/schema/schema.json` | Skemadefinitionen (kilde til sandhed) | Rettes i hånden |
| `scripts/Deploy-App.ps1` | Opretter eller opdaterer visninger, formularer, sitemap og appen fra `app/app.json`, validerer og publicerer | Efter hver ændring af app.json, og efter Deploy-Schema på et nyt miljø |
| `scripts/New-TestData.ps1` | Syntetiske testdata: 10 afdelinger, 25 Accounts, 1–3 Kontakter pr. Account, 60 Salgsmuligheder med 1–3 roller. `-Remove` sletter dem igen | Kun i udviklings- og testmiljøer, **aldrig i produktion** |
| `scripts/New-DemoUsers.ps1` | Opretter 3 fiktive demobrugere (Anne Hansen `ah`, Lars Nielsen `ln`, Mette Sørensen `ms`) i Entra ID via Graph, tildeler `POWERAPPS_DEV`-licens og tilføjer dem til miljøet med Basic User + MTH Sales. Startadgangskoder skrives til `%LOCALAPPDATA%\mth-crm\demo-users.txt` | Kun i en konsulent- eller test-tenant, **aldrig i MTH's tenant**. Efter Deploy-Roles |
| `scripts/Deploy-Roles.ps1` | Opretter eller opdaterer security roles fra `security/roles.json`, erstatter deres rettigheder og giver adgang til appen | Efter hver ændring af roles.json, og efter Deploy-App på et nyt miljø |
| `scripts/security/roles.json` | Rollerne og deres rettigheder pr. tabel (Create/Read/Write/Delete/Append/AppendTo/Assign/Share × None/Basic/Local/Deep/Global). Tabelnavnet `_global` bruges til rettigheder uden tabel, f.eks. `SendAppNotification` | Rettes i hånden |
| `scripts/Deploy-Plugins.ps1` | Bygger plugin-assembly'en og registrerer eller opdaterer assembly, typer og trin fra `plugins/steps.json`, plus app-indstillinger | Efter hver ændring i `plugins/` eller steps.json |
| `scripts/plugins/steps.json` | Plugin-trin (type, besked, tabel, fase, sync/async) og app-indstillinger | Rettes i hånden |
| `scripts/Deploy-Pcf.ps1` | Tester, bygger og pusher PCF-kontrollerne i `pcf/` og lægger dem i solutionen. `-Controls MentionComposer,TenderWorkspace` | Efter hver ændring i `pcf/` |
| `scripts/Deploy-WebResources.ps1` | Opretter eller opdaterer alle filer i `webresources/` som web resources (navn = sti, f.eks. `mth_/scripts/opportunity.js`) og publicerer dem | Efter hver ændring i `webresources/`, før Deploy-App og Deploy-Branding |
| `scripts/New-DemoProjectSites.ps1` | DEMO: opretter et projektsite (Microsoft 365-gruppe) pr. sag med status 20–89, lægger eksempeldokumenter i Shared Documents og sætter Project Site URL på sagen | Kun i en test-tenant. Se sharepoint.md |
| `scripts/Import-Trades.ps1` + `scripts/data/trades.json` | Fagkataloget (213 fag) upsertes på nøglen `mth_code`. Stamdata, også til produktion | Efter Deploy-Schema på et nyt miljø |
| `scripts/New-EspTestData.ps1` | ESP-testdata: underentreprenører med fag og kontakter, entrepriser, UE-tilknytninger, skabeloner. `-Remove` sletter | Kun udvikling og test |
| `scripts/New-SmartTestData.ps1` | Syntetiske SMART-svar til `functions/SmartSync/testdata` (`-Variant 2` ændrer projekter) | Test af SMART-synken |
| `scripts/New-DemoLeadQueues.ps1` | 4 demo-lead-køer med kriterier og medlemmer (dig + demobrugere). `-Remove` sletter | Kun demo |
| `scripts/Deploy-SmartSyncAzure.ps1` + `infra/smartsync.json` | Deployer SMART-synken til Azure: ressourcegruppe, ARM-skabelon (Flex Consumption, managed identity), kode og application user i Dataverse med Basic User + MTH Integration. `-RunNow` kører den én gang. Login med device code; refresh-token i `%LOCALAPPDATA%\mth-crm\az-refresh-token.txt` | Efter Deploy-Roles, og efter hver ændring i `functions/SmartSync`. Se smart.md |
| `scripts/Deploy-Branding.ps1` | MTH-branding: tema-posten "MTH" (farver og logo, standardtema), moderne header-farver (`CustomThemeDefinition`) og app-ikon. Filerne ligger i `webresources/mth_/branding/` | Efter Deploy-WebResources og Deploy-App på et nyt miljø. Se branding.md |
| `scripts/app/app.json` | Definitionen af appen: navigation, visninger og formularer (faner, kolonner, sektioner, felter, undergitre) | Rettes i hånden |

## schema.json

```text
solution, publisherPrefix, optionValuePrefix, languageCode
tables[]:  schemaName, displayName, pluralName, standard, ownership, source, description,
           primary { schemaName, displayName, maxLength, source },
           standardMapping[] (kun standardtabeller: hvilke eksisterende kolonner Salesforce-felter mappes til),
           columns[] { schemaName, displayName, type, source, maxLength?, format?, min?, max?, options[]?, note? }
lookups[]: schemaName, referencing, referenced, lookupSchemaName, displayName, cascade (Parental|Restrict|RemoveLink), required?, source
```

Kolonnetyper: `string`, `memo`, `date` (kun dato), `currency`, `integer`, `boolean`, `choice`, `multichoice`.

Valgsæt (`options`) blev oprindeligt genereret fra Salesforce-describe af prod (`sf sobject describe`) den 2026-11-19. Kun aktive picklist-værdier er med. Option values er 273280000 + løbenummer. For `mth_status` er det 273280000 + statuskode.

## Dataverse.psm1 interaktivt

```powershell
Import-Module .\scripts\Dataverse.psm1 -Force
Connect-Dataverse -Url https://<org>.crm4.dynamics.com
Invoke-Dataverse GET 'WhoAmI'
Invoke-Dataverse GET "EntityDefinitions(LogicalName='mth_opportunity')?`$select=LogicalName"
```
