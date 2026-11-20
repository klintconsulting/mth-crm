# SMART: byggeprojekter som Leads, lead-køer og konvertering

Genskaber Salesforce' SMART-integration (`SMART_ProjectSync`): byggeprojekter fra SMART (Byggefakta) hentes som **Leads**, og konverterede sager opdateres med ændringer og historik. Ejerfordelingen i Salesforce (assignment rules, køer og afdelingsvisninger) erstattes af **lead-køer**, som brugerne selv definerer.

## Beslutninger (2026-11-20)
- **Lead som i Salesforce:** projektfelterne ligger direkte på Lead (`mth_lead`), og de samme kolonner findes på Salgsmulighed (kopieres ved konvertering, opdateres af synken bagefter). Erstatter idéen om en separat Byggeprojekt-tabel (B8).
- **Azure Function i C#** med timer kalder SMART og skriver til Dataverse. Bygges og køres først lokalt.
- **Testdata i POC'en:** anonymiserede SMART-svar i samme JSON-format. Rigtigt API og MTH's nøgler kobles på i MTH's miljø (licens og persondata).
- **Lead-køer:** brugerne definerer selv køer med kriterier. Samme lead kan ramme flere køer. Medlemmer får besked om nye leads. **Den første, der konverterer, får leadet, og det lukkes for alle andre.**
- **Med:** opdatering af konverterede sager med historik, konvertering Lead → Salgsmulighed og udbud (tenders).

## Datamodel

| Tabel | Svarer til | Indhold |
|---|---|---|
| `mth_lead` (Lead) | `Lead` (record type New) | 45 projektfelter (se nedenfor), Lead Status (Åben, Konverteret, Fravalgt), Converted On/By, Converted Opportunity. Nøgle: `mth_projectid` (SMART `source_id`). Har aktiviteter |
| `mth_opportunity` (+48 kolonner) | `fnl_*` på Opportunity | Samme 45 projektfelter + Lead History (rich text, nyeste først), Latest Lead Changes, Lead Data Updated On, opslag til Lead |
| `mth_leadqueue` (Lead Queue) | Køer, assignment rules, afdelingsvisninger | Kriterier (alle udfyldte skal være opfyldt): regioner (Hovedstaden, Sjælland, Syddanmark, Midtjylland, Nordjylland), min./maks. værdi (mio. kr.), nøgleord for kategori/byggetype, byggestadie, kontrakttype, projekttekst, udelukkelsesord, byggestart inden for N måneder, udelad påbegyndte. Nøgleord adskilles med `;`, og ét match er nok. Aktiv og "Giv medlemmer besked" |
| N:N `mth_leadqueue_systemuser` | | Køens medlemmer |
| N:N `mth_lead_mth_leadqueue` | | Beregnet: hvilke køer et lead rammer |
| `mth_smartlookup` | `SMART_Lookup__c` | Cache af SMART-ordbøger (id → navn/kode). Nøgle `mth_key` = `lookup:id` |
| `mth_integrationstate` | `SMART_Sync_State__c` | Watermark (`mth_lastrunat`), opslagsversion og resultat af sidste kørsel. Nøgle `mth_name` |

**Projektfelter (på Lead og Salgsmulighed):** SMART-id'er og link, projekttitel og -beskrivelse, bygherre (navn, adresse, virksomhedstype, CVR, TDC-id), byggeplads (adresse, by, postnr.), byggestart/-slut (dato og tekst), byggeperiode, værdi (mio. kr. og tekst), kontrakttype og -kode, byggestadie, projektstadie, hovedkategori og byggetype (med koder), kategorier, land, region, kommune (med koder), boliger, etagearealer, volumen, etager, roller (HTML), udbud (HTML) og SMART-opdateringstidspunkt. Den præcise kilde i Salesforce står i hver kolonnes beskrivelse og i [datamodel.md](datamodel.md).

**Rettet i forhold til Salesforce:** tal er tal (etager, arealer), ingen `MobilePhone = 'TO BE DELETED'`, ingen ulæst-markering ved hver opdatering, ingen tekst "null" i postnummer, og lange værdier afkortes i stedet for at hele leadet afvises.

## Lead-køer (plugin `LeadQueueSync`)

**Regler** (`QueueMatcher`, 9 enhedstests): alle udfyldte kriterier skal være opfyldt, og tomme kriterier ignoreres. Region matches på navn ("Syddanmark" rammer også "Region Syddanmark"). Min./maks. værdi i mio. kr., hvor et lead uden værdi ikke matcher et værdikriterie. Nøgleordsfelter er `;`-adskilte, ét match er nok, og der skelnes ikke mellem store og små bogstaver (også æøå). Kategori-nøgleord søges i kategorier, hovedkategori og byggetype. Udelukkelsesord søges i navn, titel, beskrivelse og kategorier. "Byggestart inden for N måneder" kræver en startdato mellem i dag og i dag + N måneder. "Udelad påbegyndte" fjerner leads med startdato før i dag (leads uden startdato beholdes).

**Hvornår der beregnes** (asynkront, efter gem):
| Hændelse | Gør |
|---|---|
| Lead oprettes | Matcher mod alle aktive køer. Medlemmer af køer med "Giv medlemmer besked" får notifikationen **"Nyt lead i køen …"** (projekt, region, værdi, knap Åbn) |
| Lead ændres (kun projektkolonner, der indgår i kriterier, og status) | Genberegner. Køer, leadet kommer ind i, giver "Lead kom i køen …" |
| Kø oprettes eller kriterier ændres | Alle åbne leads genberegnes mod køen, uden notifikationer |

Kun **åbne** leads placeres i køer. Et konverteret eller fravalgt lead beholder sine kø-koblinger som historik, men vises ikke i køernes visninger, fordi de filtrerer på åbne leads. Kø-jobbet og lead-jobbet kan køre samtidig og koble det samme par: `SafeLink` tåler dubletter (testet 2026-11-20).

**Bemærk til produktion:** genberegning af en kø gennemløber alle åbne leads i ét asynkront job (2 minutters grænse). Ved mange tusinde åbne leads flyttes det til en Azure Function.

## Konvertering (Custom API `mth_ConvertLead`)

Input `LeadId`. Output `OpportunityId` og `Message`. Plugin `ConvertLead` (synkront):
1. **Gør krav på leadet først**: opdaterer status til Konverteret med en kontrol af rækkeversionen (`ConcurrencyBehavior.IfRowVersionMatches`). Konverterer to brugere samtidig, får den anden fejlen "En anden har netop konverteret leadet", og alt rulles tilbage for ham. Er leadet allerede lukket: "Leadet er allerede konverteret af X den dd-MM-yyyy HH:mm".
2. **Bygherre som Account**: findes på CVR (`mth_vatno`), derefter på præcist navn, ellers oprettes den med navn, CVR og adresse.
3. **Salgsmulighed** ejet af brugeren: alle 45 projektkolonner kopieres 1:1 (`ProjectFields.Names`, og en test sikrer, at listen svarer til schema.json). Navn, status 01, type External, beskrivelse = projektbeskrivelse, ordrestart/-slut = byggestart/-slut, lukkedato = byggestart − 2 mdr. (ellers i dag + 3 mdr.), estimeret beløb = værdi × 1 mio., opslag til Lead.
4. **Leadet lukkes** (inaktivt) og kobles til Salgsmuligheden. Konverteret af/den sættes.

**Afprøvet 2026-11-20:** kø med kriterier og medlem, matchende lead kom i køen og gav notifikation, konvertering oprettede Account og Salgsmulighed, og anden konvertering blev afvist. Testposterne er slettet.

**Rettigheder** (`security/roles.json`): alle roller læser og redigerer leads (Global) og læser opslag og synkstatus. Sales opretter og redigerer egne køer. Tender Manager og Administrator kan alle køer.

## SMART-synken (`functions/SmartSync`)

| Projekt | Indhold |
|---|---|
| `SmartSync.Core` | SMART-modeller, `ISmartSource` (`HttpSmartSource` = rigtigt API, `FileSmartSource` = mappe med svar i samme format), `LookupCache`, `SmartMapper` (feltmapping), `OpportunityHistory` (konverterede sager), `DataverseClient` (Web API, genforsøg ved 429/5xx) og `SyncRunner` (én kørsel) |
| `SmartSync.Cli` | Lokal kørsel: `dotnet SmartSync.Cli\bin\Debug\net9.0\smart-sync.dll --env <url> --source file:testdata [--dry-run]`. Logger ind med token-cachen fra `scripts/Dataverse.psm1` eller en app-registrering (`DATAVERSE_TENANT_ID/CLIENT_ID/CLIENT_SECRET`). Kør DLL'en via `dotnet`, fordi firmaets politik blokerer usignerede `.exe` |
| `SmartSync.Functions` | Azure Function (isolated, .NET 9) med timer `%SMART_SCHEDULE%` (standard `0 0 6 * * 1-5` = kl. 7 dansk vintertid, hverdage som i Salesforce). Indstillinger i `local.settings.example.json` |
| `SmartSync.Tests` | 17 tests: mapping mod Salesforce-testklassens COWI-eksempel, udbud, roller, datoer, værdi, afkortning, manglende opslag, historik og testdatafilerne |
| `testdata/` | Syntetiske SMART-svar fra `scripts/New-SmartTestData.ps1` (25 projekter, 8 udbud, 15 firmaer, opdigtede navne og example.com-mails). `-Variant 2` ændrer hvert 4. projekt til test af opdateringer |

**En kørsel** (`SyncRunner`):
1. Watermark fra `mth_integrationstate` "SMART Projects" (første gang: nu − 1 døgn).
2. Ordbøger: `/lookups` + lande, regioner og kommuner. Kun nye eller ændrede rækker skrives til `mth_smartlookup`. I Salesforce blev ca. 3.600 rækker skrevet hver dag, fordi "version" er et tidsstempel.
3. Projekter med `updated_at` i vinduet. Pr. side på 50: udbud via `updates[]` (seneste REGISTERED/UNREGISTERED pr. udbud afgør) med fallback på `project_id`, og firmaer for rollerne.
4. Pr. projekt: **konverteret** (aktiv Salgsmulighed type External med samme `mth_projectid`) → opdatér ændrede kolonner og skriv historik. **Åbent lead** → opdatér kun ændrede kolonner, så køerne ikke genberegnes unødigt. **Lukket lead uden sag** → spring over. **Nyt** → opret åbent lead, hvorefter køplugin'et fordeler det.
5. Watermark rykkes til kørslens start eller til `updated_at` for det første projekt, der fejlede, så fejl prøves igen næste gang. Resultatet gemmes i `mth_lastresult`.

**Konverterede sager** (`OpportunityHistory`): Logger ændringer i projekttitel, byggestadie, budget (mill), byggestart, slut dato, kontrakttype, byggeplads, postnr., by, kommune, land, kvadratmeter, etager og etager under jorden, nyeste blok først (`<b>dd-MM-yyyy HH:mm</b><br><b>Label</b> ændret fra <b>"…"</b> til <b>"…"</b>`), med datoer som dd-MM-yyyy og tal med dansk komma. Øvrige projektkolonner opdateres uden log. Ved status 01/05 følger ordrestart/-slut, lukkedato (byggestart − 2 mdr.), beskrivelse og estimeret beløb SMART. **Afvigelse fra Salesforce:** "Projekt navn" logges på projekttitlen, fordi Salgsmulighedens navn tilhører brugeren.

**Afprøvet 2026-11-20 mod udviklermiljøet:** 25 nye leads blev fordelt i 4 demokøer (5/4/4/3) med notifikationer. Kørsel 2 med samme data gav 25 uændrede og 0 opslag skrevet. Efter konvertering af et lead og variant 2 blev Salgsmuligheden opdateret med 4 loggede ændringer (stadie, budget, start, slut), og 6 åbne leads blev opdateret.

**Rigtigt API hos MTH:** `SMART_SOURCE=api` + `SMART_CLIENT_ID/SECRET` (Key Vault). Ingen kodeændring. Afprøv først med `--dry-run` mod et testmiljø.

## Drift i Azure

Deployes med `scripts/Deploy-SmartSyncAzure.ps1` (kun Azure REST API, kræver hverken Azure CLI eller Az-modulet). Infrastrukturen er beskrevet i `infra/smartsync.json` (ARM-skabelon). Scriptet kan køres igen efter kodeændringer.

```powershell
.\scripts\Deploy-Roles.ps1 -Url https://<org>.crm4.dynamics.com      # rollen "MTH Integration" skal findes
.\scripts\Deploy-SmartSyncAzure.ps1 -SubscriptionId <id> -DataverseUrl https://<org>.crm4.dynamics.com [-SmartSource api] [-RunNow]
```

| Ressource (ressourcegruppe `rg-mthcrm-smartsync`, Sweden Central) | Indstilling |
|---|---|
| Function App `mthcrm-smartsync-<suffix>` | Flex Consumption (FC1), Linux, .NET 9 isolated, maks. 40 instanser à 2 GB. Systemtildelt managed identity |
| Storage account | Kun adgang med identitet (delte nøgler slået fra). Containeren `app-package` holder kodepakken. Identiteten har *Storage Blob Data Owner* |
| Log Analytics + Application Insights | Logning med Entra-godkendelse (lokal godkendelse slået fra). Identiteten har *Monitoring Metrics Publisher* |

**Ingen hemmeligheder (B19):** Function App'ens managed identity er registreret i Dataverse som **application user** med rollerne *Basic User* og *MTH Integration*. Rollen kan læse og skrive leads, SMART-opslag og integrationsstatus, opdatere salgsmuligheder, læse køer og Accounts og sende notifikationer. Koden henter token med `ManagedIdentityTokenProvider`. Kun hvis `DATAVERSE_CLIENT_SECRET` er sat, bruges en app-registrering i stedet.

**App settings** (sættes af skabelonen): `DATAVERSE_URL`, `SMART_SOURCE` (`file:testdata` i POC'en, hvor testdataene ligger i pakken), `SMART_SCHEDULE` (`0 0 6 * * 1-5`). Tidsplanen er i **UTC**, fordi Flex Consumption ikke understøtter tidszoner: kl. 7 dansk vintertid og kl. 8 sommertid. Historikken på salgsmuligheder skrives i dansk tid (`Europe/Copenhagen`).

**Det rigtige API hos MTH:** kør scriptet med `-SmartSource api`, og tilføj `SMART_CLIENT_ID` og `SMART_CLIENT_SECRET` som Key Vault-referencer på Function App'en. Testdataene kan derefter fjernes fra `SmartSync.Functions.csproj`.

**Kør manuelt:** `-RunNow` kalder `POST https://<app>.azurewebsites.net/admin/functions/SmartSync` med host-masternøglen. Resultatet står i `mth_integrationstate` ("SMART Projects", `mth_lastresult`) og i Application Insights.

**Faldgruber, der er løst i scriptet:**
- Zip-filen bygges med `/` i stinavnene. I Windows PowerShell 5.1 skriver `Compress-Archive` og `ZipFile.CreateFromDirectory` `\`, og så afviser Flex pakken med "Cannot find required .azurefunctions directory".
- Upload-kaldet (`/api/publish`) svarer, før pakken er valideret, så scriptet venter på `/api/deployments/latest`.
- Den første kørsel af funktionen kan give 404 i et par minutter, mens værten starter.

**Afprøvet 2026-11-20:** deploy fra bunden til abonnementet "MTH CRM POC" og manuel kørsel. Synken kørte som application useren: 25 projekter, 6 leads opdateret, 2 salgsmuligheder opdateret (1 med historik), 0 fejl. Køpluginnet kørte uden fejl.

## Appen

**Navigation:** gruppen **Leads** med *Lead* og *Lead Queue* øverst i området Sales.

| Visning | Indhold |
|---|---|
| **MTH Leads in My Queues** (standard for Lead) | Åbne leads i de køer, brugeren er medlem af (FetchXML: lead → `mth_lead_mth_leadqueue` → `mth_leadqueue_systemuser`, `systemuserid eq-userid`, distinct). Nyeste først |
| MTH Open Leads | Alle åbne leads |
| MTH Closed Leads | Konverterede og fravalgte leads med konverteret af/den og salgsmulighed |
| **MTH My Lead Queues** (standard for Lead Queue) | Køer, brugeren er medlem af |
| MTH Active Lead Queues | Alle aktive køer |

**Lead-formularen:**
- **General:** projekt, værdi og datoer, klassifikation, bygherre, konvertering, og tidslinje med kommentarfelt (@-vælger).
- **Description, Roles and Tenders:** roller og udbud vises med rich text-editoren.
- **Queues:** de køer, leadet er i.
- **Details:** bygningsdata og SMART-koder.

**Knapper** (moderne kommandoer, `appaction`, defineret under `commands` i app.json og kalder `webresources/mth_/scripts/lead.js`):
- **Convert:** bekræftelse, derefter `mth_ConvertLead` og åbning af den nye salgsmulighed. Har en anden allerede konverteret, vises dennes besked, og formularen opdateres.
- **Disqualify:** status Fravalgt og leadet lukkes, så det forsvinder fra alle køer.

**Kø-formularen:** navn, aktiv, giv medlemmer besked, beskrivelse og ejer. **Kriterier** med en forklaring i sektionsoverskriften. Fanen **Leads and Members** viser åbne leads i køen og medlemmerne (tilføj via "Add existing" på brugere).

**Salgsmulighed:** ny fane **SMART Project** med lead-opslag, projektdata, *Changes from SMART* (seneste og fuld historik) og roller og udbud.

**Deploy-App.ps1** kan nu (bruges her og fremover): visninger med `filterXml`/`linkXml`, undergitre med `systemView` (eksisterende Microsoft-visning), automatisk rich text-editor på memo-kolonner med format RichText, og `commands` (appaction med JavaScript).

**Afprøvet 2026-11-20:** "Leads i mine køer" returnerer 16 af 24 åbne leads for en bruger, der er medlem af alle 4 demokøer. Rich text-kontrollerne og knapperne er gemt. **Knapperne og formularerne er ikke afprøvet i browseren af udvikleren.**

## Status
- [x] Trin 1: datamodel (2026-11-20)
- [x] Trin 2: kø-matching og notifikation (plugin) og konvertering (Custom API, første vinder) (2026-11-20)
- [x] Trin 3: SMART-synk (C#, Azure Function-projekt) med testdata (2026-11-20). Deploy, se nedenfor
- [x] Trin 4: app (Lead- og kø-formularer, "Leads i mine køer", Konvertér-knap) (2026-11-20)
- [x] Deploy af synken til Azure (Flex Consumption, managed identity som application user) i abonnementet "MTH CRM POC" (2026-11-20)
- [ ] Danske labels (afventer dansk sprog i miljøet, B3)
