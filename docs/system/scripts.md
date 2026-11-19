# Scripts

| Script | Formål | Hvornår |
|---|---|---|
| `scripts/Dataverse.psm1` | Login til Dataverse Web API med device code, token-cache og `Invoke-Dataverse` | Bruges af de andre scripts eller interaktivt |
| `scripts/Deploy-Schema.ps1` | Opretter tabeller, kolonner og opslag fra `schema/schema.json` i solutionen `MTHCRM` | Efter hver ændring af schema.json og ved opbygning af et nyt miljø |
| `scripts/New-SchemaDoc.ps1` | Genererer `docs/system/datamodel.md` fra schema.json | Efter hver ændring af schema.json |
| `scripts/schema/schema.json` | Skemadefinitionen (kilde til sandhed) | Rettes i hånden |

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
