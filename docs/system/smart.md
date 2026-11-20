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

## Status
- [x] Trin 1: datamodel (2026-11-20)
- [x] Trin 2: kø-matching og notifikation (plugin) og konvertering (Custom API, første vinder) (2026-11-20)
- [ ] Trin 3: SMART-synk (C#, Azure Function-projekt) med testdata
- [ ] Trin 4: app (Lead- og kø-formularer, "Leads i mine køer", Konvertér-knap)
