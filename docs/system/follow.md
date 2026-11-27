# Følg (Follow)

Svarer til "Follow" i Salesforce (Chatter `EntitySubscription` + feed tracking). Brugeren følger en Salgsmulighed, et Lead eller en Account og får en notifikation under klokken, når vigtige felter ændres, eller der skrives en note. Bygget 2026-11-27 (beslutning B24).

**Brug i Salesforce (prod, 2026-11-27):** 4.297 følg på Opportunity fordelt på 44 brugere, 524 på Account og 239 på Lead.

## For brugeren

- Knappen **Follow / Unfollow** øverst på formularen for Salgsmulighed, Lead og Account. Første klik følger posten, næste klik stopper. En dialog bekræfter, hvad der skete.
- Når man følger posten, står der en blå linje øverst på formularen: "Du følger denne post …".
- Notifikationer under klokken med knappen **Åbn**:
  - "*Bruger* ændrede *post*" med en linje pr. ændret felt: `Status: 01 Potential project → 20 PQ - upcoming`, `Estimated Amount: 314.000.000 kr. → 123.456.789 kr.`
  - "*Bruger* tilføjede en note på *post*" med notens emne og et uddrag.
- Man får aldrig besked om sine egne ændringer. Er man @omtalt i noten, får man kun omtalebeskeden (se [plugins.md](plugins.md)).
- Fanen **Followers** (lige efter General) viser, hvem der følger posten, og siden hvornår (visningen MTH Followers). Alle kan se følgerne, som i Salesforce. Listen findes også under Related → Follows, som Dataverse laver automatisk ud fra relationen.
- Knappen står i **kommandolinjen øverst** over formularen, ikke under Related. Er vinduet smalt, ligger den under **⋯**, fordi kommandolinjen kun viser det antal knapper, der er plads til.
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
| Knap og formularscript | `webresources/mth_/scripts/follow.js`, `commands` og `scripts` i `scripts/app/app.json` | `Mth.Follow.toggle()` opretter eller sletter rækken med brugerens egne rettigheder. `Mth.Follow.onLoad` viser formularnotifikationen |
| Plugin `FollowNotifier` | `plugins/Mth.Crm.Plugins/FollowNotifier.cs`, `scripts/plugins/steps.json` | Asynkron. Update af de tre tabeller med filtering attributes og entity images `pre` og `post`. Create af `annotation`. Kører med systemkontekst og opretter `appnotification` til hver følger |
| Visninger | `views` i `scripts/app/app.json` | `link-entity` til `mth_follow` med `ownerid eq-userid` og `statecode = 0` |
| Rettigheder | `scripts/security/roles.json` | MTH Sales og MTH Tender Manager: `mth_follow` Read **Global** (alle kan se, hvem der følger en post) og resten på **Basic** (kun egne rækker kan oprettes og slettes). CRM Administrator: Global. MTH Integration: ingen |

**Sletning:** Slettes en Salgsmulighed, slettes dens følg-rækker med (kaskade). Dataverse tillader kun én relation med kaskadesletning pr. tabel, så for Lead og Account fjernes kun linket (RemoveLink). Følg-rækken bliver liggende uden post. Den gør ingen skade, fordi visningerne går via opslaget, og en slettet post ikke ændres. `Deploy-Schema.ps1` har fået kaskadetypen `DeleteOnly` (slet med, men behold egen ejer). `Parental` ville flytte følg-rækkerne til postens nye ejer ved en ejerskifte.

**Entity images:** `Deploy-Plugins.ps1` kan nu registrere images ud fra `images` på et trin i steps.json (`name`, `type` PreImage/PostImage, `attributes`).

**Beløb** vises i dansk format uden øre ("314.000.000 kr."). Andre værdier bruger Dataverses formaterede værdi (valgmuligheder, datoer, opslag).

## Afprøvet 2026-11-27 i udviklermiljøet

Som Lars Nielsen (almindelig bruger, MTH Sales, via `CallerObjectId`):
1. Lars følger "Almene boliger Frederikshavn". En dublet afvises af nøglen.
2. Administratoren ændrer Status og Estimated Amount → Lars får "Søren Klint ændrede …" med begge ændringer.
3. Administratoren skriver en note → Lars får "Søren Klint tilføjede en note på …".
4. En note, hvor Lars er @omtalt → Lars får kun omtalebeskeden.
5. "MTH Opportunities I Follow" viser posten for Lars og intet for administratoren.
6. Lars stopper med at følge (sletter sin egen række).

Testdata er ryddet op bagefter. **Knappen og formularlinjen er ikke klikket igennem i browseren af udvikleren.**

## Ikke understøttet endnu

- Automatisk følg, f.eks. når man opretter eller bliver ejer af en post (Salesforce kan auto-følge egne poster).
- Følg af Kontakt og andre tabeller.
- Samlet feed, som Chatter, der viser alle ændringer på fulgte poster ét sted. Notifikationerne under klokken og tidslinjen på posten dækker det delvist.
- Mail i stedet for eller ud over notifikationen.
- Migrering af de eksisterende følg (`EntitySubscription`) fra Salesforce.
