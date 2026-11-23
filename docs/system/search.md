# Søgning

Standardsøgningen i en model-driven app ("Filter by keyword" over listen) er *quick find*: den matcher kun fra starten af de kolonner, der er valgt i tabellens Quick Find-visning. Derfor fandt "Aalborg" ikke "Havneudvidelse, Aalborg". Løsningen er **Dataverse search**, som matcher ord hvor som helst i teksten, tåler stavefejl og rangerer resultaterne. Den bruges både i den globale søgelinje øverst og i listernes søgefelt.

## Opsætning (`scripts/search/search.json`)

| Indstilling | Værdi | Virkning |
|---|---|---|
| `isexternalsearchindexenabled` (organisation) | true | Dataverse search er slået til i miljøet |
| `tablescopeddvsearchinapps` (organisation) | true | Listernes søgefelt bruger Dataverse search i stedet for quick find |
| `SyncToExternalSearchIndex` (pr. tabel) | true | Tabellen indekseres |
| Quick Find-visningens søgekolonner | se nedenfor | Hvilke kolonner der søges i (bruges af begge søgninger) |

| Tabel | Søgekolonner |
|---|---|
| Salgsmulighed (`mth_opportunity`) | navn, Account, BC-projektnummer, SMART-projekt-id, projekttitel, leveringsby, byggeby, kommune, bygherre, beskrivelse |
| Lead (`mth_lead`) | navn, projekttitel, SMART-projekt-id, byggeby, kommune, region, bygherre, hovedkategori, projekttekster |
| Account | navn, kontonummer, e-mail, telefon 1 og 2, by |
| Kontakt | fulde navn, fornavn, efternavn, e-mail, Account, telefon, mobil |
| UE (`mth_collaboratingpartner`) | navn, Account, entreprise |

## Deploy

```powershell
.\scripts\Deploy-Search.ps1 -Url https://<org>.crm4.dynamics.com
pac solution sync --solution-folder solutions/MTHCRM
```

Scriptet sætter organisationsindstillingerne, tilføjer tabellerne til indekset (metadata kræver GET af hele definitionen og PUT tilbage), erstatter `<filter isquickfindfields="1">` i hver Quick Find-visning og publicerer. Indekset bygges i baggrunden. Nye tabeller og kolonner kan være op til ca. en time om at blive søgbare.

**Kendt faldgrube:** En PATCH af en Quick Find-visning (`savedqueries`, `querytype` 4) med kun `fetchxml` fejler med `0x80040216 "An unexpected error occurred"`. Den lykkes, når `returnedtypecode`, `querytype` og `layoutxml` sendes med. Det gør scriptet.

Organisationsindstillingerne følger ikke med i solutionen. Kør scriptet i hvert miljø efter import. Visningerne og indeks-flaget følger med.

## Test

Udviklermiljøet 2026-11-23: `POST searchquery { search: "Aalborg" }` gav "Havneudvidelse, Aalborg" og "Udvidelse af hospital, Aalborg" (Salgsmuligheder) og to Leads.
