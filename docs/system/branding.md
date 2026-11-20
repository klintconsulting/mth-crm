# Branding: MTH-farver og logo

Genskaber Salesforce-temaet **MTH** (Setup → Themes and Branding, branding set `LEXTHEMINGMTH`), hentet fra produktion 2026-11-20.

| Salesforce | Værdi | Power Apps |
|---|---|---|
| Brand color | `#002F67` (mørkeblå) | App-header, navigation, links, procesbjælke |
| Page background | `#2C365A` (blågrå) | Valgt/trykket i headeren, tabelfarver, accent |
| Header background | `#EEEEEE` | Sidehoved (klassisk tema) |
| Brand image `mth3` | MT Højgaard-logo (blåt, transparent) | Hvid udgave i headeren, blå udgave som reserve, emblemet som app-ikon |

Salesforce' baggrundsbilleder (`SF_Background_31`, Kalvebod) kan ikke vises i en model-driven app og er ikke overført.

## Filer (`webresources/mth_/branding/`)

| Fil | Brug |
|---|---|
| `logo-white.png` | Logo i app-headeren (208×40, hvidt på transparent, uden "Danmark", som er ulæseligt i lille størrelse) |
| `logo.png` | Samme logo i blåt (til lys baggrund) |
| `icon.svg` | App-ikon: emblemet (64×64, PNG indlejret i SVG) |
| `theme.xml` | Moderne header-farver (`CustomTheme/AppHeaderColors`) |

Billederne er lavet ud fra `mth3.png` med System.Drawing: beskåret og skaleret. Den hvide udgave er lavet pr. pixel: blå bliver hvid, og hvid bliver gennemsigtig. Figurens hoved er nemlig hvide pixels i originalen, ikke et hul, og ville ellers forsvinde i en helt hvid flade. Hvis MTH leverer logoet som SVG, så erstat filerne.

## Deploy

```powershell
.\scripts\Deploy-WebResources.ps1 -Url https://<org>.crm4.dynamics.com
.\scripts\Deploy-Branding.ps1 -Url https://<org>.crm4.dynamics.com
```

`Deploy-Branding.ps1` gør tre ting:
1. Opretter eller opdaterer tema-posten **MTH** (tabellen `theme`) med farver og logo (`logoimage@odata.bind`) og publicerer den som organisationens standardtema (`PublishTheme`). Tema-poster følger **ikke** med i en solution, så scriptet er kilden og skal køres i hvert miljø.
2. Sætter app-indstillingen `CustomThemeDefinition` = `mth_/branding/theme.xml` på appen. Den styrer farverne i den moderne header.
3. Sætter appens ikon (`appmodule.webresourceid`) til `icon.svg`.

Genindlæs appen med Ctrl+F5 for at se ændringen.

## Begrænsninger

Model-driven apps kan kun farve headeren og navigationen. Knapper, formularer og lister bruger Microsofts Fluent-design og kan ikke farves uden at bryde supporten.