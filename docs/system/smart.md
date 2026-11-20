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

## Status
- [x] Trin 1: datamodel (2026-11-20)
- [ ] Trin 2: kø-matching og notifikation (plugin) og konvertering (Custom API, første vinder)
- [ ] Trin 3: SMART-synk (C#, Azure Function-projekt) med testdata
- [ ] Trin 4: app (Lead- og kø-formularer, "Leads i mine køer", Konvertér-knap)
