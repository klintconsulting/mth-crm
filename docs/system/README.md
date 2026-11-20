# Systemdokumentation: MTH CRM

Dokumentationen beskriver løsningen, så den kan genskabes fra bunden uden den oprindelige udvikler.

| Dokument | Indhold |
|---|---|
| [setup.md](setup.md) | Genskab et miljø fra bunden: værktøjer, login, solution og skema, i rækkefølge |
| [app.md](app.md) | Appen: navigation, formularer, visninger, og hvordan de ændres |
| [datamodel.md](datamodel.md) | Alle tabeller, kolonner, valgsæt og relationer. **Genereres** fra `scripts/schema/schema.json` |
| [esp.md](esp.md) | ESP: fag, entrepriser, underentreprenører, kontakter og tilbudsmails |
| [sharepoint.md](sharepoint.md) | Projektsite som link på Salgsmuligheden, og hvorfor indlejring af Shared Documents blev fravalgt |
| [pcf.md](pcf.md) | PCF-kontroller (kommentarfelt med @-vælger): brug, teknik, byg og deploy |
| [plugins.md](plugins.md) | C#-plugins: hvad de gør, regler, test, deploy og fejlsøgning |
| [decisions.md](decisions.md) | Arkitektur- og designbeslutninger med begrundelse og dato |
| [scripts.md](scripts.md) | Hvad hvert script i `scripts/` gør, og hvornår det bruges |
| [../deploy.md](../deploy.md) | Deploy til test og produktion via GitHub Actions |

## Kilde til sandhed

| Område | Kilde | Bemærkning |
|---|---|---|
| Tabeller, kolonner og opslag | `scripts/schema/schema.json` | Oprettes med `scripts/Deploy-Schema.ps1` |
| Formularer, visninger, navigation, app | `scripts/app/app.json` | Oprettes med `scripts/Deploy-App.ps1` og synkes derefter til `solutions/MTHCRM/src` |
| Security roles og andet | `solutions/MTHCRM/src` | Laves i make.powerapps.com og synkes med `pac solution sync` |
| Beslutninger | `docs/system/decisions.md` | Opdateres i samme commit som ændringen |

Når skemaet ændres, skal tre ting opdateres i samme commit: `schema.json`, `datamodel.md` (kør `scripts/New-SchemaDoc.ps1`) og den udpakkede solution (kør `pac solution sync`).
