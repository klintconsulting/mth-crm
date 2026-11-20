# Plugins

Assembly: `Mth.Crm.Plugins` (`plugins/Mth.Crm.Plugins`), .NET Framework 4.6.2, signeret med `Mth.Crm.Plugins.snk`, kører i sandbox (isolation mode 2). Registreres med `scripts/Deploy-Plugins.ps1` ud fra `scripts/plugins/steps.json`.

| Plugin | Besked / tabel | Fase | Tilstand | Formål |
|---|---|---|---|---|
| `NoteMentionNotifier` | Create / `annotation` (note) | PostOperation (40) | Asynkron | In-app notifikation til brugere, der er @omtalt i en ny note |

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

**Teknisk:** Plugin'et kører asynkront med systemkontekst, så det kan læse brugere og oprette `appnotification` for andre end forfatteren. Notifikationen har `icontype = 100000004` (Mention) og `toasttype = 200000000` (Timed), og `data` indeholder en "Åbn"-handling med URL'en `?pagetype=entityrecord&etn=<tabel>&id=<id>`.

**Ikke understøttet endnu:** omtale ved *redigering* af en eksisterende note (kun ved oprettelse), omtale af teams, og mail i stedet for eller ud over in-app notifikationen.

## Byg, test og deploy

```powershell
dotnet test tests/Mth.Crm.Plugins.Tests                       # enhedstests (net9.0, linker kildefilen ind)
.\scripts\Deploy-Plugins.ps1 -Url https://<org>.crm4.dynamics.com   # bygger Release, registrerer assembly, type, trin og app-indstillinger
pac solution sync --solution-folder solutions/MTHCRM
```

## Fejlsøgning

Slå plugin-sporing til (kun i udvikling og test, sæt tilbage til "Exception" bagefter):

```powershell
Invoke-Dataverse PATCH "organizations(<organizationid>)" @{ plugintracelogsetting = 2 }   # 0 Off, 1 Exception, 2 All
Invoke-Dataverse GET "plugintracelogs?`$select=createdon,messageblock,exceptiondetails&`$filter=startswith(typename,'Mth.Crm.Plugins')&`$orderby=createdon desc"
```

`typename` i sporingen indeholder hele assembly-navnet, så filtrér med `startswith` og ikke `eq`. Indstillingen caches et par minutter, før den slår igennem. I udviklermiljøet er sporingen sat til **All** (2026-11-20).
