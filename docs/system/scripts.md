# Scripts

| Script | Formål | Hvornår |
|---|---|---|
| `scripts/Dataverse.psm1` | Login til Dataverse Web API med device code, token-cache og `Invoke-Dataverse` | Bruges af de andre scripts eller interaktivt |
| `scripts/Deploy-Schema.ps1` | Opretter tabeller, kolonner og opslag fra `schema/schema.json` i solutionen `MTHCRM` | Efter hver ændring af schema.json og ved opbygning af et nyt miljø |
| `scripts/New-SchemaDoc.ps1` | Genererer `docs/system/datamodel.md` fra schema.json | Efter hver ændring af schema.json |
| `scripts/schema/schema.json` | Skemadefinitionen (kilde til sandhed) | Rettes i hånden |
| `scripts/Deploy-App.ps1` | Opretter eller opdaterer visninger, formularer, sitemap og appen fra `app/app.json`, validerer og publicerer | Efter hver ændring af app.json, og efter Deploy-Schema på et nyt miljø |
| `scripts/New-TestData.ps1` | Syntetiske testdata: 10 afdelinger, 25 Accounts, 1–3 Kontakter pr. Account, 60 Salgsmuligheder med 1–3 roller. `-Remove` sletter dem igen | Kun i udviklings- og testmiljøer, **aldrig i produktion** |
| `scripts/New-DemoUsers.ps1` | Opretter 3 fiktive demobrugere (Anne Hansen `ah`, Lars Nielsen `ln`, Mette Sørensen `ms`) i Entra ID via Graph, tildeler `POWERAPPS_DEV`-licens og tilføjer dem til miljøet med Basic User + MTH Sales. Startadgangskoder skrives til `%LOCALAPPDATA%\mth-crm\demo-users.txt` | Kun i en konsulent- eller test-tenant, **aldrig i MTH's tenant**. Efter Deploy-Roles |
| `scripts/Deploy-Roles.ps1` | Opretter eller opdaterer security roles fra `security/roles.json`, erstatter deres rettigheder og giver adgang til appen | Efter hver ændring af roles.json, og efter Deploy-App på et nyt miljø |
| `scripts/security/roles.json` | Rollerne og deres rettigheder pr. tabel (Create/Read/Write/Delete/Append/AppendTo/Assign/Share × None/Basic/Local/Deep/Global) | Rettes i hånden |
| `scripts/Deploy-Plugins.ps1` | Bygger plugin-assembly'en og registrerer eller opdaterer assembly, typer og trin fra `plugins/steps.json`, plus app-indstillinger | Efter hver ændring i `plugins/` eller steps.json |
| `scripts/plugins/steps.json` | Plugin-trin (type, besked, tabel, fase, sync/async) og app-indstillinger | Rettes i hånden |
| `scripts/Deploy-Pcf.ps1` | Tester, bygger og pusher PCF-kontrollerne i `pcf/` og lægger dem i solutionen | Efter hver ændring i `pcf/` |
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
