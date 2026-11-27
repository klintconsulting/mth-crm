# Status og næste skridt

Øjebliksbillede af POC'en, så arbejdet kan fortsættes efter en pause. Opdateres, når status ændrer sig. Senest: **2026-11-26**.

## Hvor står vi

POC'en kører i udviklermiljøet `https://org558bead0.crm4.dynamics.com` (app-id `43a11b13-38e4-f111-84e8-000d3a496ae5`). Den er vist for / sendt til MTH's systemejer for Salesforce, og **vi afventer feedback**.

**Demomateriale** (ikke i repoet):
- Demo-manuskript (Claude Doc): https://claude.ai/artifact/5n9rZsSf8NyZVtxdKAC227
- Præsentation "CRM-platforme for MTH" (Salesforce, Dynamics 365 Sales og Power Apps/Dataverse sammenlignet, inkl. priser i EUR): https://claude.ai/artifact/WVXGPZeZt4g6WZqSbwKDX8

**Afprøvet og virker** (også som almindelig bruger, Lars Nielsen):

| Område | Dokument |
|---|---|
| SMART → Leads, parallelle lead-køer med tildeling pr. kø, Reject i én kø, Remove from All My Queues, Convert, Disqualify | [smart.md](smart.md), beslutning B23 |
| Konvertering opretter Account fra NNE ud fra bygherrens CVR | [smart.md](smart.md), [nne.md](nne.md) |
| NNE-søgning og oprettelse af Account | [nne.md](nne.md) |
| ESP: entrepriser, UE'er med Opportunity, tilbudsmails | [esp.md](esp.md) |
| Status Number på Salgsmulighed | [plugins.md](plugins.md) |
| @-omtale i noter med notifikation under klokken | [plugins.md](plugins.md), [pcf.md](pcf.md) |
| Global søgning og søgning i lister | [search.md](search.md) |
| Systemoplysninger (oprettet/ændret af og hvornår) og auditering på Lead og Salgsmulighed | [app.md](app.md) |
| MTH-branding | [branding.md](branding.md) |
| Følg (Follow) på Salgsmulighed, Lead og Account med besked ved ændringer og noter (afprøvet via API som Lars, ikke klikket i browseren) | [follow.md](follow.md), beslutning B24 |

## Kendte fejl og begrænsninger

- **Reject på undergitteret** forsvinder, når en række markeres. Workaround: åbn tildelingen og brug Reject der. Se [smart.md](smart.md).
- **Tabeller i appen** skal skrives ind i `solutions/MTHCRM/src/AppModules/mth_MTHCRM/AppModule.xml` (se [app.md](app.md)). `mth_leadassignment` står der ikke endnu. Det giver kun en advarsel i `Deploy-App.ps1` og betyder, at tildelinger ikke indgår i den globale søgning.
- **Efter hver solution-import** skal `Deploy-Plugins.ps1` køres, ellers kan asynkrone trin stå slået fra (notifikationer og lead-køer stopper). Se [plugins.md](plugins.md).
- `scripts/Deploy-SmartSyncAzure.ps1` bruger stadig device code-login, som blokeres af security defaults. Skal skiftes til browser-login (som `Connect-Dataverse`) før næste Azure-deploy. Se beslutning B22.
- SQL via TDS-endpointet virker med `scripts/Invoke-DataverseSql.ps1`, men ikke med VS Code-udvidelsen mssql (den kalder `DATABASEPROPERTYEX`). Se [scripts.md](scripts.md).

## Demodata lige nu

- Leadene **"Almene boliger Frederikshavn"** og **"Ny bro over Frederikshavn"** er konverteret under generalprøven (lukket). Der er oprettet Salgsmuligheder og Accounts (bl.a. Frederikshavn Kommune fra NNE) ud fra dem.
- Køen **"Store sager over 300 mio"** findes (oprettet live under generalprøven, notifikation slået til). Skal slettes, hvis den skal oprettes live igen.
- Til en ny demo: genåbn de to leads (status Åben, statecode 0), sæt deres tildelinger tilbage til New, slet de oprettede Salgsmuligheder og evt. Accounts, og slet køen. Accounts med Salgsmuligheder kan ikke slettes, før Salgsmulighederne er væk (restrict).

## Næste skridt

1. **Feedback fra systemejeren** indarbejdes, og beslutninger skrives i [decisions.md](decisions.md).
2. Ret Reject på undergitteret (se ovenfor).
3. Tilføj `mth_leadassignment` til AppModule.xml, importér solutionen og kør derefter `Deploy-Plugins.ps1`.
4. **Microsoft Copilot:** slået til i appen, men mangler Microsoft 365 Copilot-licens (brugeren venter med prøveversionen). Se [app.md](app.md#microsoft-copilot-i-appen-sat-op-men-ikke-aktiv).
5. Mulige forbedringer, der er nævnt men ikke besluttet: danske labels (oversættelser), Status Number også for UE'er, auditering på flere tabeller, Afvisningsflowet i manuskriptet.
6. Senere: integration til og migrering fra Business Central, validering svarende til Salesforce `Check_Contract_Amount`, samlet systemdokumentation i Word (læs alle dokumenter på ny først).

## Regler, der altid gælder

- **MTH's miljøer røres ikke direkte.** Deploy til MTH kun via PR og GitHub Actions eller ved at MTH's IT kører scriptene ([../deploy.md](../deploy.md)). Salesforce må kun læses (retrieve og query) fra produktion.
- **Hemmeligheder** (token-cache, demo-adgangskoder, Azure-token, NNE-nøglen) ligger i `%LOCALAPPDATA%\mth-crm` og aldrig i repoet. Tjek før hver commit, at NNE-nøglen ikke er med.
- Testdata: e-mails kun på example.com, SMART kun syntetiske projekter.
- **Test som almindelig bruger**, ikke kun som administrator. Administratoren skjuler manglende rettigheder.
