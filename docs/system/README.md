# Systemdokumentation: MTH CRM

Dokumentationen beskriver løsningen, så den kan genskabes fra bunden uden den oprindelige udvikler.

| Dokument | Indhold |
|---|---|
| [setup.md](setup.md) | Genskab et miljø fra bunden: værktøjer, login, solution og skema, i rækkefølge |
| [datamodel.md](datamodel.md) | Alle tabeller, kolonner, valgsæt og relationer. **Genereres** fra `scripts/schema/schema.json` |
| [decisions.md](decisions.md) | Arkitektur- og designbeslutninger med begrundelse og dato |
| [scripts.md](scripts.md) | Hvad hvert script i `scripts/` gør, og hvornår det bruges |
| [../deploy.md](../deploy.md) | Deploy til test og produktion via GitHub Actions |

## Kilde til sandhed

| Område | Kilde | Bemærkning |
|---|---|---|
| Tabeller, kolonner og opslag | `scripts/schema/schema.json` | Oprettes med `scripts/Deploy-Schema.ps1` |
| Formularer, visninger, app, security roles | `solutions/MTHCRM/src` | Laves i make.powerapps.com og synkes med `pac solution sync` |
| Beslutninger | `docs/system/decisions.md` | Opdateres i samme commit som ændringen |

Når skemaet ændres, skal tre ting opdateres i samme commit: `schema.json`, `datamodel.md` (kør `scripts/New-SchemaDoc.ps1`) og den udpakkede solution (kør `pac solution sync`).
