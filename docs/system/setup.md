# Genskab miljøet fra bunden

Trinene i rækkefølge. De er testet første gang i november 2026 mod et Power Apps Developer-miljø.

## 1. Forudsætninger

| Hvad | Hvorfor | Hvor |
|---|---|---|
| Arbejdskonto i Entra ID (Microsoft 365) | Developer Plan og Dataverse kræver en arbejds- eller skolekonto | Kundens tenant eller en konsulent-tenant (f.eks. M365 Business Basic) |
| Power Apps-miljø med Dataverse | Selve løsningen | Developer Plan (gratis) til udvikling, produktionsmiljø hos MTH |
| .NET SDK (9 eller nyere, helst 10) | Build af solution-projektet og senere plugins | https://dotnet.microsoft.com |
| Power Platform CLI (`pac`) | Solutions, import og eksport | `dotnet tool install --global Microsoft.PowerApps.CLI.Tool` |
| Git | Kildekode | https://git-scm.com/download/win |

**Bemærk om `pac`:** Version 2.x af `Microsoft.PowerApps.CLI.Tool` kræver .NET 10 SDK. Med kun .NET 9 installeret skal du bruge `--version 1.46.1`. Globale .NET-værktøjer ligger i `%USERPROFILE%\.dotnet\tools`, som skal være på PATH.

## 2. Log ind med pac

```powershell
pac auth create --name dev --deviceCode
pac admin list                                   # find miljøets URL
pac org select --environment https://<org>.crm4.dynamics.com/
pac org who
```

## 3. Opret solution og publisher

Solution-projektet ligger i `solutions/MTHCRM` (publisher `mth`, "MT Højgaard", option value-præfiks `27328`, unikt navn `MTHCRM`). Importér det i et tomt miljø, så publisher og solution oprettes:

```powershell
dotnet build solutions/MTHCRM -c Debug
pac solution import --path solutions/MTHCRM/bin/Debug/MTHCRM.zip --publish-changes
```

## 4. Opret tabeller, kolonner og opslag

`scripts/Deploy-Schema.ps1` læser `scripts/schema/schema.json` og opretter alt via Dataverse Web API i solutionen `MTHCRM`. Det, der allerede findes, springes over. Eksisterende kolonner ændres altså **ikke**. En ændring af type eller længde på en eksisterende kolonne skal laves manuelt eller med et særskilt script.

```powershell
.\scripts\Deploy-Schema.ps1 -Url https://<org>.crm4.dynamics.com -WhatIf   # vis hvad der vil ske
.\scripts\Deploy-Schema.ps1 -Url https://<org>.crm4.dynamics.com           # udfør
```

Første gang beder scriptet om et login med device code. Det bruger Microsofts offentlige klient-id til Dataverse-eksempler (`51f81489-12ee-4a9e-aaae-a2591f45987d`). Token caches i `%LOCALAPPDATA%\mth-crm\` og **ikke** i repoet.

## 5. Synk solutionen tilbage til repoet

```powershell
pac solution sync --solution-folder solutions/MTHCRM
```

Standardtabellerne `account`, `contact` og `systemuser` ligger i solutionen med *behavior 1* (uden underkomponenter). Kun MTH's egne `mth_`-kolonner og relationer følger med, så en import overskriver ikke standardopsætningen i målmiljøet. Tjek `RootComponents` i `src/Other/Solution.xml`, hvis der tilføjes kolonner til flere standardtabeller.

## 6. Formularer, visninger og app

```powershell
.\scripts\Deploy-App.ps1 -Url https://<org>.crm4.dynamics.com
pac solution sync --solution-folder solutions/MTHCRM
```

Scriptet opretter eller opdaterer visninger, formularer, sitemap og appen `mth_MTHCRM` ud fra `scripts/app/app.json`, validerer appen og publicerer. Det skriver appens URL til sidst. Se [app.md](app.md).

**Rækkefølgen er vigtig:** Trin 4 (skema) skal være kørt før trin 6, fordi formularer og visninger slår kolonnerne op i metadata.

## 7. Testdata (kun udvikling og test)

```powershell
.\scripts\New-TestData.ps1 -Url https://<org>.crm4.dynamics.com           # opret/opdatér
.\scripts\New-TestData.ps1 -Url https://<org>.crm4.dynamics.com -Remove   # slet igen
```

- **Syntetiske data:** firmanavne, personnavne, CVR-numre og telefonnumre er opdigtede, og e-mails ligger på `example.dk`. Afdelingerne er MTH's 10 mest brugte ordreafdelinger (organisationsdata uden personoplysninger).
- **Idempotent:** hver post har et fast GUID (MD5 af en nøgle som `opp-12`), og der bruges upsert. Tilfældigheden er seedet (42), så data er de samme fra kørsel til kørsel.
- **Sagsteam:** alle roller peger på den bruger, der kører scriptet, fordi et udviklermiljø typisk kun har én bruger.
- **Kun danske data:** danske firmanavne og byer. Telefonnumre er "+45 xx xx xx xx" med fastnetserier til Accounts og mobilserier til Kontakter.
- Et nyt udviklermiljø har i forvejen Microsofts amerikanske eksempeldata ("Fourth Coffee (sample)" m.fl.). Fjern dem med Web API-handlingen `UninstallSampleData`:
  ```powershell
  Import-Module .\scripts\Dataverse.psm1; Connect-Dataverse -Url https://<org>.crm4.dynamics.com
  Invoke-Dataverse POST 'UninstallSampleData' @{}
  ```

## 8. Security roles

```powershell
.\scripts\Deploy-Roles.ps1 -Url https://<org>.crm4.dynamics.com
```

Opretter eller opdaterer rollerne i `scripts/security/roles.json`, **erstatter** deres rettigheder og giver dem adgang til appen. Tildel derefter brugerne **Basic User** plus én MTH-rolle i Power Platform admin center (Miljø, Brugere). Se beslutning B11.

## 9. Plugins (efter trin 6 og 8)

```powershell
.\scripts\Deploy-Plugins.ps1 -Url https://<org>.crm4.dynamics.com
```

Bygger `plugins/Mth.Crm.Plugins` i Release, registrerer assembly, plugin-typer og trin fra `scripts/plugins/steps.json` og slår in-app notifications til i appen. Kræver .NET SDK. Se [plugins.md](plugins.md).

## 10. PCF-kontroller (efter trin 9, før en ny kørsel af trin 6)

```powershell
.\scripts\Deploy-Pcf.ps1 -Url https://<org>.crm4.dynamics.com
.\scripts\Deploy-App.ps1 -Url https://<org>.crm4.dynamics.com
```n
Kræver Node.js 22+. Bygger, tester og pusher kontrollerne i `pcf/` og lægger dem i solutionen `MTHCRM`. Kør derefter Deploy-App igen, fordi formularerne refererer til kontrollen. **På et helt nyt miljø** skal trin 10 køres før trin 6, ellers kan formularerne ikke gemmes. Se [pcf.md](pcf.md).

## 11. SharePoint-dokumenter på Salgsmuligheden

```powershell
.\scripts\Deploy-WebResources.ps1 -Url https://<org>.crm4.dynamics.com   # formularscript (før Deploy-App)
```n
Slå dokumenthåndtering til på `mth_opportunity` og opret standard-SharePoint-sitet. Se [sharepoint.md](sharepoint.md). På et helt nyt miljø skal web resources deployes **før** trin 6, fordi formularen refererer til scriptet.
