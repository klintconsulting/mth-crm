# Plugins

Assembly: `Mth.Crm.Plugins` (`plugins/Mth.Crm.Plugins`), .NET Framework 4.6.2, signeret med `Mth.Crm.Plugins.snk`, kører i sandbox (isolation mode 2). Registreres med `scripts/Deploy-Plugins.ps1` ud fra `scripts/plugins/steps.json`.

| Plugin | Besked / tabel | Fase | Tilstand | Formål |
|---|---|---|---|---|
| `NoteMentionNotifier` | Create / `annotation` (note) | PostOperation (40) | Asynkron | In-app notifikation til brugere, der er @omtalt i en ny note |
| `FollowNotifier` | Update af `mth_opportunity`, `mth_lead`, `account` (sporede felter, images `pre`/`post`) og Create af `annotation` | PostOperation (40) | Asynkron | Notifikation til dem, der følger posten, når et sporet felt ændres, eller der skrives en note. Se [follow.md](follow.md) |
| `LeadQueueSync` | Create/Update af `mth_lead` og `mth_leadqueue` | PostOperation (40) | Asynkron | Vedligeholder lead-tildelinger (`mth_leadassignment`): opretter status New for køer, leadet matcher, og fjerner New-tildelinger, der ikke længere matcher. Rører aldrig Rejected/Converted/Lost. Giver kømedlemmer besked. Se [smart.md](smart.md) |
| `RejectLead` | Custom API `mth_RejectLeadAssignment` (én kø) og `mth_RejectLead` (alle brugerens køer) | Main operation | Synkron | Sætter tildelingen til Rejected, kun for køens medlemmer. Lukker leadet som Fravalgt, når ingen kø har det. Se [smart.md](smart.md) |
| `ConvertLead` | Custom API `mth_ConvertLead` | Main operation | Synkron | Konverterer Lead → Salgsmulighed, første vinder. Se [smart.md](smart.md) |
| `SendPartnerEmail` | Custom API `mth_SendPartnerEmail` | Main operation | Synkron | Tilbudsmail til én UE's valgte kontakter, status 10 → 24. Se [esp.md](esp.md) |
| `CollabPartnerOpportunity` | Create/Update (`mth_buildingcontractid`) af `mth_collaboratingpartner` | PreOperation (20) | Synkron | Sætter UE'ens Opportunity til entreprisens Salgsmulighed. Se [esp.md](esp.md) |
| `OpportunityStatusNumber` | Create/Update (`mth_status`) af `mth_opportunity` | PreOperation (20) | Synkron | Sætter **Status Number** (`mth_statusnumber`) = tallet før første mellemrum i Status ("54 Tender - submitted" → 54) til filtrering og sortering, som `Status_Number__c` i Salesforce. Beregnes som valgværdi − 273280000 (alle 21 valg kontrolleret 2026-11-24). En ny statusværdi skal derfor følge reglen 273280000 + kode |
| `NneSearch` | Custom API `mth_NneSearch` | Main operation | Synkron | Søger i NNE og henter detaljer parallelt (udgående HTTPS). Se [nne.md](nne.md) |
| `NneImportAccount` | Custom API `mth_NneImportAccount` | Main operation | Synkron | Opretter en Account fra NNE med dubletkontrol på TDC-id. Se [nne.md](nne.md) |

## NoteMentionNotifier: @omtale i noter

**For brugeren:** Skriv `@Fulde Navn` eller `@initialer` i en note i tidslinjen. Initialer er delen før `@` i brugerens mailadresse, f.eks. `@sk` for sk@mth.dk. Den nævnte bruger får en notifikation under klokken øverst i appen med teksten "*Forfatter* nævnte dig i en note", postens navn, et uddrag af noten og knappen **Åbn**, som åbner posten.

**Regler** (i `MentionParser`, dækket af enhedstests i `tests/Mth.Crm.Plugins.Tests`):
- Der skelnes ikke mellem store og små bogstaver.
- En omtale må ikke efterfølges af bogstav eller tal: `@Anne` rammer ikke `@Annette`.
- En mailadresse i teksten (`kontakt@sk.dk`) er ikke en omtale.
- Samme bruger får kun én notifikation pr. note.
- Forfatteren får ikke besked om sin egen note.
- Kun aktive, interaktive brugere (`isdisabled = false`, `accessmode = 0`) kan omtales.
- Rich text (HTML) i noten konverteres til ren tekst før søgning. Notens emne søges også.

**Forudsætning:** in-app notifications skal være slået til i appen (app-indstillingen `AllowNotificationsEarlyAccess = true`, sættes af `Deploy-Plugins.ps1`).

**Rettigheder hos modtageren** (alle MTH-roller i `scripts/security/roles.json`, rettet 2026-11-25): `appnotification` Read/Write/Delete på egne (Basic), så klokken kan vise, markere og lukke dem, og `appusersetting` Create/Read/Write (Global, tabellen er organisationsejet), hvor notifikationsindstillingerne gemmes. Uden `appusersetting` viser klokken "Kunne ikke få meddelelser", og indstillingerne står som "undefined" og kan ikke gemmes. Det mærkes ikke af en systemadministrator, så **test med en almindelig bruger**.

**Teknisk:** Plugin'et kører asynkront med systemkontekst, så det kan læse brugere og oprette `appnotification` for andre end forfatteren. Notifikationen har `icontype = 100000004` (Mention) og `toasttype = 200000000` (Timed), og `data` indeholder en "Åbn"-handling med URL'en `?pagetype=entityrecord&etn=<tabel>&id=<id>`.

**Ikke understøttet endnu:** omtale ved *redigering* af en eksisterende note (kun ved oprettelse), omtale af teams, og mail i stedet for eller ud over in-app notifikationen.

## Byg, test og deploy

```powershell
dotnet test tests/Mth.Crm.Plugins.Tests                       # enhedstests (net9.0, linker kildefilen ind)
.\scripts\Deploy-Plugins.ps1 -Url https://<org>.crm4.dynamics.com   # bygger Release, registrerer assembly, type, trin og app-indstillinger
pac solution sync --solution-folder solutions/MTHCRM
```

## Fejlsøgning

**Intet sker, og der er hverken sporing eller systemjob:** Tjek om trinet er slået fra (`sdkmessageprocessingsteps`, `statecode` = 1). En solution-import efterlod 2026-11-24 alle asynkrone trin slået fra (de eksporterede trin har ingen tilstand), så @-notifikationer og lead-køer stod stille i et døgn. `Deploy-Plugins.ps1` slår nu altid trinene til igen. **Kør derfor Deploy-Plugins efter hver solution-import**, og test med en almindelig bruger. Hurtig kontrol:

```powershell
Invoke-Dataverse GET "sdkmessageprocessingsteps?`$select=name,statecode&`$filter=contains(name,'Mth.Crm.Plugins')"
```

Slå plugin-sporing til (kun i udvikling og test, sæt tilbage til "Exception" bagefter):

```powershell
Invoke-Dataverse PATCH "organizations(<organizationid>)" @{ plugintracelogsetting = 2 }   # 0 Off, 1 Exception, 2 All
Invoke-Dataverse GET "plugintracelogs?`$select=createdon,messageblock,exceptiondetails&`$filter=startswith(typename,'Mth.Crm.Plugins')&`$orderby=createdon desc"
```

`typename` i sporingen indeholder hele assembly-navnet, så filtrér med `startswith` og ikke `eq`. Indstillingen caches et par minutter, før den slår igennem. I udviklermiljøet er sporingen sat til **All** (2026-11-20).
