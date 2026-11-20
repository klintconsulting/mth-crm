# PCF-kontroller

| Kontrol | Mappe | Bruges på | Formål |
|---|---|---|---|
| `mth_Mth.MentionComposer` | `pcf/MentionComposer` | Opportunity, Account og Contact, lige over tidslinjen | Kommentarfelt med `@`-vælger. Opretter en note på posten |
| `mth_Mth.TenderWorkspace` | `pcf/TenderWorkspace` | Opportunity, fanen Tender (ESP) | Arbejdsrum til entrepriser, UE'er, kontakter, evaluering, søgning, kopiering og tilbudsmails (React + Fluent UI 9). Se [esp.md](esp.md) |

## MentionComposer: kommentar med @omtale

**For brugeren:**
1. Skriv i feltet "Skriv en kommentar…" over tidslinjen.
2. Tast `@`. Der vises en liste over aktive brugere, og listen filtreres, mens man skriver videre (fornavn, efternavn eller initialer).
3. Vælg med musen eller med piletaster og Enter/Tab. `@Fulde Navn` indsættes i teksten. Gentag for **flere modtagere hvor som helst i teksten**.
4. Under feltet står "Nævner: …" med de valgte, så man kan se, hvem der får besked.
5. Klik **Send** (eller Ctrl+Enter). Kommentaren gemmes som en note med emnet "Kommentar", tidslinjen opdateres, og de nævnte får en notifikation fra plugin'et `NoteMentionNotifier` (se [plugins.md](plugins.md)).

På en ny post, der ikke er gemt endnu, er feltet låst med teksten "Gem posten, før du skriver en kommentar".

**Teknisk:**
- Feltkontrol uden React og uden eksterne biblioteker. Den er bundet til postens primære navnekolonne (`mth_name`, `name`, `fullname`) og ændrer den aldrig (`getOutputs` returnerer intet).
- Brugersøgning via `context.webAPI` på `systemuser`: `isdisabled eq false and accessmode eq 0`, `contains(fullname)` eller `startswith(internalemailaddress)`, top 8, med 200 ms debounce.
- Noten oprettes via `context.webAPI.createRecord("annotation")` med `objectid_<tabel>@odata.bind`. Entity set-navnet hentes med `context.utils.getEntityMetadata`.
- Tidslinjen genindlæses automatisk efter Send. Kontrollen gennemløber `Xrm.Page.ui.controls`, finder tidslinjen på kontroltypen `timelinewall` (med navnene `notescontrol`/`Timeline` som reserve) og kalder `refresh()` med det samme og igen efter 1,5 sekunder, fordi første kald kan returnere tidslinjens cache. Fejler det, er noten gemt alligevel og vises ved næste genindlæsning. (Fra version 1.0.1. I 1.0.0 blev der kun kaldt `getControl("notescontrol")`, som ikke ramte tidslinjen.)
- Tekster på engelsk (1033) og dansk (1030) i `strings/*.resx`. Brugerens sprog afgør, hvilke der vises.
- Ren logik (find `@`-ordet ved markøren, indsæt omtale, find nævnte, HTML-escape, OData-forespørgsel) ligger i `mention.ts` og er dækket af 13 tests i `tests/mention.test.ts`.

**Placering på formularen:** I `scripts/app/app.json` får tidslinjesektionen `"composer": true, "composerControl": "mth_Mth.MentionComposer"`. `Deploy-App.ps1` indsætter så en celle med primærnavnet og en `controlDescription`, der binder kontrollen for web, tablet og telefon (formFactor 0, 1 og 2).

## Byg, test og deploy

```powershell
cd pcf/MentionComposer
npm ci
npm test            # node --experimental-strip-types --test (Node 22+)
npm run build

.\scripts\Deploy-Pcf.ps1 -Url https://<org>.crm4.dynamics.com   # test + build + pac pcf push + AddSolutionComponent
.\scripts\Deploy-App.ps1 -Url https://<org>.crm4.dynamics.com   # placér kontrollen på formularerne
pac solution sync --solution-folder solutions/MTHCRM
```

`pac pcf push` importerer kontrollen via en midlertidig solution `PowerAppsTools_mth`. Den kan slettes, når kontrollen ligger i `MTHCRM`. Kontrollen følger derefter med i MTHCRM ved eksport og deploy.

**Version:** Hæv `version` i `ControlManifest.Input.xml` ved hver ændring. Ellers kan Dataverse og browseren blive ved med at bruge den gamle bundle.

## Begrænsninger
- Listen vises under hele tekstfeltet, ikke ved markøren.
- Omtalen er ren tekst (`@Fulde Navn`), ikke en "pille", man kan slette med ét tryk.
- To brugere med præcis samme fulde navn får begge besked. Brug initialer (`@sk`) for at ramme én.
