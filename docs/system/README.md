# Systemdokumentation: MTH CRM

Dokumentationen beskriver løsningen, så den kan genskabes fra bunden uden den oprindelige udvikler.

| Dokument | Indhold |
|---|---|
| [status.md](status.md) | **Start her, når arbejdet genoptages:** hvad virker, kendte fejl, demodata og næste skridt |
| [setup.md](setup.md) | Genskab et miljø fra bunden: værktøjer, login, solution og skema, i rækkefølge |
| [app.md](app.md) | Appen: navigation, formularer, visninger, og hvordan de ændres |
| [datamodel.md](datamodel.md) | Alle tabeller, kolonner, valgsæt og relationer. **Genereres** fra `scripts/schema/schema.json` |
| [smart.md](smart.md) | SMART-integration: byggeprojekter som Leads, lead-køer, konvertering |
| [esp.md](esp.md) | ESP: fag, entrepriser, underentreprenører, kontakter og tilbudsmails |
| [sharepoint.md](sharepoint.md) | Projektsite som link på Salgsmuligheden, og hvorfor indlejring af Shared Documents blev fravalgt |
| [pcf.md](pcf.md) | PCF-kontroller (kommentarfelt med @-vælger): brug, teknik, byg og deploy |
| [plugins.md](plugins.md) | C#-plugins: hvad de gør, regler, test, deploy og fejlsøgning |
| [resources.md](resources.md) | Ressourceoverblik: belastning pr. person og uge, Gantt, fravær og tilbudsafdelinger |
| [follow.md](follow.md) | Følg Salgsmuligheder, Leads og Accounts og få besked ved ændringer og noter |
| [nne.md](nne.md) | NNE: find virksomheder i Navne & Numre Erhverv og opret dem som Account |
| [search.md](search.md) | Søgning: Dataverse search i listerne og søgekolonner pr. tabel |
| [branding.md](branding.md) | MTH-farver, logo og app-ikon fra Salesforce-temaet |
| [decisions.md](decisions.md) | Arkitektur- og designbeslutninger med begrundelse og dato |
| [scripts.md](scripts.md) | Hvad hvert script i `scripts/` gør, og hvornår det bruges |
| [../gap-analysis.md](../gap-analysis.md) | GAP-analyse: forskelle mellem Salesforce og løsningen (mangler, delvist, fravalgt) |
| [../deploy.md](../deploy.md) | Deploy til test og produktion via GitHub Actions |

## Kilde til sandhed

| Område | Kilde | Bemærkning |
|---|---|---|
| Tabeller, kolonner og opslag | `scripts/schema/schema.json` | Oprettes med `scripts/Deploy-Schema.ps1` |
| Formularer, visninger, navigation, app | `scripts/app/app.json` | Oprettes med `scripts/Deploy-App.ps1` og synkes derefter til `solutions/MTHCRM/src` |
| Security roles og andet | `solutions/MTHCRM/src` | Laves i make.powerapps.com og synkes med `pac solution sync` |
| Beslutninger | `docs/system/decisions.md` | Opdateres i samme commit som ændringen |

Når skemaet ændres, skal tre ting opdateres i samme commit: `schema.json`, `datamodel.md` (kør `scripts/New-SchemaDoc.ps1`) og den udpakkede solution (kør `pac solution sync`).
