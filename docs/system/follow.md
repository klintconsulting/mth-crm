# Følg (Follow)

Svarer til "Follow" i Salesforce (Chatter `EntitySubscription` + feed tracking). Brugeren følger en Salgsmulighed, et Lead eller en Account og får en notifikation under klokken, når vigtige felter ændres, eller der skrives en note. Bygget 2026-11-27 (beslutning B24).

**Brug i Salesforce (prod, 2026-11-27):** 4.297 følg på Opportunity fordelt på 44 brugere, 524 på Account og 239 på Lead.

## For brugeren

- Knappen med en gul stjerne i **kommandolinjen øverst** på formularen for Salgsmulighed, Lead og Account hedder **Follow**, når man ikke følger posten, og **Unfollow**, når man gør. Den skifter med det samme efter et klik, og en dialog bekræfter, hvad der skete. Er vinduet smalt, ligger den under **⋯**, fordi kommandolinjen kun viser de knapper, der er plads til.
- Når man følger posten, står der en blå linje øverst på formularen: "★ Du følger denne Salgsmulighed …" (eller dette Lead, denne Account).
- I listerne **MTH Active Opportunities**, **MTH Open Leads**, **MTH Leads in My Queues** og **MTH Active Accounts** viser kolonnen **Following** "Yes" ud for de poster, brugeren selv følger. De andre rækker står tomme.
- Notifikationer under klokken med knappen **Åbn**:
  - "*Bruger* ændrede *post*" med en linje pr. ændret felt: `Status: 01 Potential project → 20 PQ - upcoming`, `Estimated Amount: 314.000.000 kr. → 123.456.789 kr.`
  - "*Bruger* tilføjede en note på *post*" med notens emne og et uddrag.
- Man får aldrig besked om sine egne ændringer. Er man @omtalt i noten, får man kun omtalebeskeden (se [plugins.md](plugins.md)).
- **Related → Follows** viser, hvem der følger posten, og siden hvornår (visningen MTH Followers). Alle kan se følgerne, som i Salesforce. (En egen fane Followers blev prøvet og fjernet igen efter brugerens ønske 2026-11-27.)
- Visningerne **MTH Opportunities I Follow**, **MTH Leads I Follow** og **MTH Accounts I Follow** i listernes visningsvælger viser de poster, brugeren selv følger.

## Felter, der giver besked

De samme felter som Salesforces feed tracking i prod (hentet 2026-11-27), dog uden felter, der ikke er migreret.

| Tabel | Felter |
|---|---|
| Salgsmulighed (`mth_opportunity`) | Navn, Status, Close Date, Estimated Amount, Tender Amount, Calculated Contract Amount, Account, Ejer, Order Department, Pre Qualification Date, External Opportunity |
| Lead (`mth_lead`) | Navn, Ejer, Lead Status, projekttitel, værdi, planlægningsstadie, startdato, kontraktform, byggeplads, kommune, region, etageareal, etager, etager under terræn |
| Account | Navn, Ejer, Parent Account, Account Category, Segment, Sub Segment, Account Status |

Ikke med fra Salesforce: Close Reasons og Close Comments (findes ikke i datamodellen endnu), Lead-felterne med projekttekster, roller og udbud (lange HTML-tekster, der ændres ved hver SMART-opdatering), Account Rating og Creation date for latest CSS. Ændringer fra SMART-integrationen giver besked som "*integrationsbruger* ændrede …", som i Salesforce.

**Ændres listen**, skal tre steder rettes ens: `Tracked` i `plugins/Mth.Crm.Plugins/FollowNotifier.cs` og både `filteringAttributes` og de to `images` for trinet i `scripts/plugins/steps.json`. Kør derefter `Deploy-Plugins.ps1`.

## Teknik

| Del | Hvor | Indhold |
|---|---|---|
| Tabel `mth_follow` (Follow) | `scripts/schema/schema.json` | Én række pr. bruger og post. **Ejer = den, der følger.** Kolonner: `mth_name` (postens navn), `mth_recordtable`, `mth_recordid` (guid med små bogstaver), `mth_key` = `<post-id>\|<bruger-id>` med alternativ nøgle `mth_FollowKey`, så man kun kan følge en post én gang. Opslag `mth_opportunityid`, `mth_leadid`, `mth_accountid` til visningerne |
| Knapperne Follow og Unfollow | `solutions/MTHCRM/src/Entities/<tabel>/RibbonDiff.xml`, skrevet af `scripts/New-FollowRibbon.ps1`. Ikon `webresources/mth_/icons/follow.svg` | **Klassiske ribbon-knapper**, ikke moderne kommandoer (`appaction`): en moderne kommando har fast tekst og kan kun skjules med en Power Fx-formel (komponentbibliotek), som ikke kan oprettes via Web API'et. Begge knapper ligger i formularens kommandolinje (`Mscrm.Form.<tabel>.MainTab.Save.Controls._children`, sekvens 25 og 26). Enable rules med `CustomRule` (`Mth.Follow.canFollow` / `canUnfollow`) skjuler den knap, der ikke passer. I Unified Interface skjules en knap, hvis enable rule er falsk. Svaret caches pr. post og bruger, og efter et klik kaldes `formContext.ui.refreshRibbon()` |
| Formularscript | `webresources/mth_/scripts/follow.js`, `scripts` i `scripts/app/app.json` | `Mth.Follow.toggle()` opretter eller sletter rækken med brugerens egne rettigheder (`mth_following` = Ja). `Mth.Follow.onLoad` viser formularnotifikationen |
| Kolonnen Following i listerne | `views` i `scripts/app/app.json`, kolonne `follow.mth_following` | Ydre `link-entity` til `mth_follow` med alias `follow`, `ownerid eq-userid` og `statecode = 0`. Alle rækker vises, men kun brugerens egne følg har en værdi. `Deploy-App.ps1` understøtter kolonner på formen `alias.kolonne`, hvor linkXml selv vælger attributten. Web API'et returnerer den som `follow_x002e_mth_following` |
| Plugin `FollowNotifier` | `plugins/Mth.Crm.Plugins/FollowNotifier.cs`, `scripts/plugins/steps.json` | Asynkron. Update af de tre tabeller med filtering attributes og entity images `pre` og `post`. Create af `annotation`. Kører med systemkontekst og opretter `appnotification` til hver følger |
| Visninger | `views` i `scripts/app/app.json` | `link-entity` til `mth_follow` med `ownerid eq-userid` og `statecode = 0` |
| Rettigheder | `scripts/security/roles.json` | MTH Sales og MTH Tender Manager: `mth_follow` Read **Global** (alle kan se, hvem der følger en post) og resten på **Basic** (kun egne rækker kan oprettes og slettes). CRM Administrator: Global. MTH Integration: ingen |

**Sletning:** Slettes en Salgsmulighed, slettes dens følg-rækker med (kaskade). Dataverse tillader kun én relation med kaskadesletning pr. tabel, så for Lead og Account fjernes kun linket (RemoveLink). Følg-rækken bliver liggende uden post. Den gør ingen skade, fordi visningerne går via opslaget, og en slettet post ikke ændres. `Deploy-Schema.ps1` har fået kaskadetypen `DeleteOnly` (slet med, men behold egen ejer). `Parental` ville flytte følg-rækkerne til postens nye ejer ved en ejerskifte.

**Entity images:** `Deploy-Plugins.ps1` kan nu registrere images ud fra `images` på et trin i steps.json (`name`, `type` PreImage/PostImage, `attributes`).

**Beløb** vises i dansk format uden øre ("314.000.000 kr."). Andre værdier bruger Dataverses formaterede værdi (valgmuligheder, datoer, opslag).

**Deploy af knapperne:** ribbon-ændringer kommer kun ind med en solution-import. Rækkefølge: `Deploy-WebResources.ps1` → `pac solution sync` (så solutionen har de nyeste web resources) → `New-FollowRibbon.ps1` (sync overskriver RibbonDiff.xml med miljøets version, som efter første import er den samme) → `dotnet build solutions/MTHCRM` → `pac solution import --path solutions/MTHCRM/bin/Debug/MTHCRM.zip --publish-changes` → **`Deploy-Plugins.ps1`**. Importen 2026-11-27 slog alle plugin-trin fra, også de synkrone.

## Afprøvet 2026-11-27 i udviklermiljøet

Som Lars Nielsen (almindelig bruger, MTH Sales, via `CallerObjectId`):
1. Lars følger "Almene boliger Frederikshavn". En dublet afvises af nøglen.
2. Administratoren ændrer Status og Estimated Amount → Lars får "Søren Klint ændrede …" med begge ændringer.
3. Administratoren skriver en note → Lars får "Søren Klint tilføjede en note på …".
4. En note, hvor Lars er @omtalt → Lars får kun omtalebeskeden.
5. "MTH Opportunities I Follow" viser posten for Lars og intet for administratoren.
6. Lars stopper med at følge (sletter sin egen række).

7. "MTH Active Opportunities" som Lars: alle 65 aktive rækker vises, og kun den fulgte har Following = Yes.
8. Knapperne Follow og Unfollow er importeret (ses i `ribbondiffs`) og bevares ved `pac solution sync`.

Testdata er ryddet op bagefter. **Knapperne, skiftet mellem dem og formularlinjen er ikke klikket igennem i browseren af udvikleren.**

## Senere: feed og daglig mail (design, ikke bygget)

Aftalt med brugeren 2026-11-27: gemmes til senere, **ikke en del af POC'en**. Svarer til Chatter-feedet og Chatter email digest i Salesforce. I Salesforce prod har 92 af 101 aktive brugere daglig digest, 3 ugentlig og 6 ingen (`User.DigestFrequency`, hentet 2026-11-27). Daglig er Salesforces standard, så tallet viser ikke nødvendigvis et bevidst valg.

**Hvorfor ikke bare notifikationerne:** `appnotification` slettes, når brugeren lukker dem, og når de udløber (`ttlinseconds`). De kan ikke bruges som historik.

**1. Tabel `mth_feeditem` (Feed Item), organisationsejet, én række pr. hændelse (ikke pr. modtager)**
- Kolonner: `mth_name` (overskrift, f.eks. "Søren Klint ændrede Almene boliger Frederikshavn"), `mth_kind` (valg: Change / Note), `mth_changes` (memo: én linje pr. felt, "Status: 01 → 20"), `mth_excerpt` (notens uddrag), `mth_recordtable`, `mth_recordid`, opslag `mth_opportunityid` / `mth_leadid` / `mth_accountid`, `mth_changedbyid` (systemuser), `createdon` som tidspunkt.
- `FollowNotifier` skriver rækken, før den sender notifikationer. Teksterne bygges allerede i plugin'et. Rækken skrives også, når posten ingen følgere har, så en ny følger kan se historikken.
- Rettigheder: Read Global for alle MTH-roller, ingen Create/Write (kun plugin'et skriver med systemkontekst).
- Oprydning: en natlig sletning af rækker ældre end f.eks. 12 måneder (bulk delete-job), så tabellen ikke vokser frit.

**2. Siden "My Feed"**
- Trin 1: visningen "MTH My Feed" på `mth_feeditem` med `link-entity` til `mth_follow` (samme `mth_recordid`, `ownerid eq-userid`), nyeste først, og et punkt i navigationen. Kan filtreres og eksporteres til Excel.
- Trin 2 (valgfrit): en tidslinje-side som HTML-web resource (som NNE-søgningen) med kort grupperet pr. dag, filter på Salgsmulighed / Lead / Account og klik til posten. Henter via `Xrm.WebApi` med samme FetchXML som visningen.

**3. Daglig mail med resumé**
- Brugerens valg: kolonnen `mth_feeddigest` (valg: Daily / Weekly / None, standard Daily som i Salesforce) på `systemuser`, sat i brugerens egen formular eller på en lille indstillingsside.
- Et tidsstyret job hver morgen kl. 6 (ugentlig: mandag) finder for hver bruger med Daily/Weekly hændelserne siden sidste kørsel på de poster, brugeren følger, udelader brugerens egne ændringer og sender **én mail pr. bruger** (ingen mail uden hændelser). Mailen grupperer pr. post med links (`main.aspx?appid=…&pagetype=entityrecord&etn=…&id=…`).
- Afsendelse som Dataverse-mail (`email` + `SendEmail`) fra en fælles postkasse via server-side sync, så den også gemmes i systemet.
- Kører som en Azure Function (timer trigger) ved siden af SMART-integrationen, i tråd med beslutningen om logik i kode. Alternativ: planlagt Power Automate-flow, som MTH selv kan rette. Vælges ved implementeringen.
- Sidste kørselstidspunkt gemmes i `mth_integrationstate` (som SMART-sync), så et mislykket døgn tages med næste gang.

**Rækkefølge:** 1 → 2 trin 1 → 3 → 2 trin 2. Feed-tabellen er grundlaget for både siden og mailen.

## Ikke understøttet endnu

- Automatisk følg, f.eks. når man opretter eller bliver ejer af en post (Salesforce kan auto-følge egne poster).
- Følg af Kontakt og andre tabeller.
- Samlet feed og daglig mail: designet ovenfor, ikke bygget.
- Migrering af de eksisterende følg (`EntitySubscription`) fra Salesforce.
