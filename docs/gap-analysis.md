# GAP-analyse: Salesforce → Power Apps/Dataverse

Levende liste over forskelle mellem MTH's Salesforce og POC'en i Dataverse: hvad der mangler, hvad der er dækket delvist, og hvad der er fravalgt med vilje. Oprettet 2026-11-27. Nye mangler skrives ind her, når de opdages, i samme commit som dokumentationen.

**Dette er ikke en komplet GAP-analyse endnu.** Den samler de mangler, der er fundet under arbejdet med POC'en. En komplet analyse skal gennemgå hele Salesforce-opsætningen systematisk (se [Ikke gennemgået endnu](#ikke-gennemgået-endnu)).

**Status:**
- **Mangler**: findes i Salesforce, ikke i POC'en.
- **Delvist**: findes i POC'en, men ikke helt som i Salesforce.
- **Design klar**: løsningen er beskrevet, men ikke bygget.
- **Fravalgt**: bevidst ikke med (begrundelse i kolonnen).

## Kendte forskelle

| # | Område | Salesforce | POC (Dataverse) | Status | Løsning / plan | Kilde |
|---|---|---|---|---|---|---|
| G1 | Følg: feed | Chatter-feedet viser alle ændringer og opslag på fulgte poster | Kun notifikationer under klokken (forsvinder, når de lukkes) og tidslinjen på den enkelte post | Design klar | Tabel `mth_feeditem` + siden "My Feed" (liste, evt. tidslinje-side). Ikke en del af POC'en | [follow.md](system/follow.md#senere-feed-og-daglig-mail-design-ikke-bygget) |
| G2 | Følg: daglig mail | Chatter email digest: 92 af 101 aktive brugere daglig, 3 ugentlig, 6 ingen (prod 2026-11-27) | Ingen mail | Design klar | Tidsstyret job (Azure Function eller Power Automate), én mail pr. bruger, valg Daily/Weekly/None pr. bruger. Ikke en del af POC'en | [follow.md](system/follow.md#senere-feed-og-daglig-mail-design-ikke-bygget) |
| G3 | Følg: automatisk | Kan auto-følge poster, man opretter | Man følger kun ved at klikke Follow | Mangler | Plugin ved Create/ejerskifte, der opretter `mth_follow` | [follow.md](system/follow.md) |
| G4 | Følg: migrering | 4.297 følg på Opportunity (44 brugere), 524 på Account, 239 på Lead | — | Mangler | Migrér `EntitySubscription` til `mth_follow` sammen med data | [follow.md](system/follow.md) |
| G5 | Følg: andre tabeller | Følg på bl.a. Tender_Notification__c (29), ContentDocument (19), User (15), Reference__c (6), Dashboard (5) | Kun Salgsmulighed, Lead og Account | Delvist | Afklar, om de øvrige bruges nok til at tage med | [follow.md](system/follow.md) |
| G6 | @omtale | Chatter-omtale i opslag og kommentarer, også teams/grupper, med mail | Omtale i nye noter med notifikation. Ikke ved redigering, ikke teams, ingen mail | Delvist | Kan udvides efter behov | [plugins.md](system/plugins.md) |
| G7 | Validering af kontraktbeløb | `Check_Contract_Amount`: evaluering påkrævet ved kontrakt > 5 mio. | Ikke genskabt | Mangler | Plugin eller forretningsregel på UE | [esp.md](system/esp.md) |
| G8 | ESP: PDF-flet og evalueringer | PDF-flet (`Document_Merge__c`), Evaluation_1..10/Assessment_1..4 | Ikke med | Fravalgt (afklares) | PDF-flet bruges ikke; evalueringer udfyldes uden for ESP | [esp.md](system/esp.md) |
| G9 | Lukkeårsager | `Close_Reasons__c`, `Close_Comments__c` på Opportunity (feed-sporet) | Findes ikke i datamodellen | Mangler | Tilføj kolonner og følg-sporing | [follow.md](system/follow.md) |
| G10 | Business Central | Integration (sagsnummer m.m.) | Kun kolonnen `mth_bcprojectnumber` | Mangler | BC-integration | [decisions.md](system/decisions.md) B10 |
| G11 | UBW og projektportal | Integrationsfelter | Ikke med | Mangler (afklares) | Afklar, om integrationerne skal genskabes | [decisions.md](system/decisions.md) B9 |
| G12 | Lotus Notes | `LN*`-felter, objekter og kode | Ikke med | Fravalgt | Integrationen er nedlagt | [decisions.md](system/decisions.md) B9 |
| G13 | Copilot / AI | — | Microsoft Copilot er slået til i appen, men kræver Microsoft 365 Copilot-licens pr. bruger | Delvist | Licensbeslutning hos MTH | [app.md](system/app.md#microsoft-copilot-i-appen-sat-op-men-ikke-aktiv) |
| G14 | Sprog | Dansk brugerflade | Engelske labels, dansk via oversættelser, som ikke er lagt ind | Delvist | Slå dansk til i miljøet og importér oversættelser | [decisions.md](system/decisions.md) B3 |

## Ikke gennemgået endnu

En komplet GAP-analyse skal også gennemgå (tal fra opgørelsen 2026-11-09):
- **Apex og triggers:** 127 Apex-klasser og 23 triggers. Hvad gør hver, og er den dækket?
- **Flows:** ca. 25 MTH-flows.
- **Rapporter og dashboards:** hvilke bruges, og hvordan genskabes de (visninger, Excel, Power BI).
- **Felter:** aktive felter på Opportunity (104) og Account (43) er analyseret; øvrige objekter (Guarantee__c, Budrunde__c, Reference__c m.fl.) er ikke med i POC'en.
- **Filer:** ca. 37 GB / 30.000 dokumenter. Hvor skal de ligge (SharePoint)?
- **Øvrige integrationer, sikkerhedsmodel, mobilbrug og e-mail-integration.**
