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
| G15 | Valideringsregler | Aktive i prod (2026-11-27): **32 på Opportunity** (bl.a. statusafhængige påkrævede felter `VR25`, `VR28_2`, `VR29`, `VR30`; beløb, datoer, leveringsadresse, kategorier, intern/ekstern), 3 på Account (postnr., by, adresse), `Check_Contract_Amount`, UE-regler (partner ≠ Salgsmulighedens Account, Salgsmulighed skal matche), 1 på User og regler på flere andre objekter | Ingen (kun Status Number som hjælpefelt til statusintervaller) | Mangler | Forslag: én regeltabel ("ved status X skal felterne Y være udfyldt") håndhævet af ét plugin (server) + formularscript, der markerer felterne påkrævede, så administratoren kan rette reglerne uden udvikler. Enkle regler kan laves som forretningsregler (scope Entity, "Show Error Message" for hård håndhævelse). **Påkrævede roller ved status** (sagsteamet er en relation, `mth_opportunityrole`, ikke faste felter): kan ikke laves med forretningsregler; plugin ved ændring af Status (PreOperation) og ved sletning/ændring af sagsteamsrækker, med kravene i samme regeltabel. **Krav (brugeren 2026-11-27): administrator og integrationsbruger skal kunne rette og omgå valideringen.** Pluginet springer kontrollen over, når brugeren (direkte eller via team) har en af rollerne i en miljøvariabel (standard: MTH CRM Administrator, MTH Integration), og formularscriptet markerer ikke felterne påkrævede for dem. Ikke Microsofts "bypass custom plugins", som slår alle plugins fra (notifikationer, lead-køer, følg). Forretningsregler kan ikke undtage roller, så det taler også for plugin-løsningen. Hver regels formel skal gennemgås | Salesforce Tooling API `ValidationRule` |
| G14 | Sprog | Dansk brugerflade | Engelske labels, dansk via oversættelser, som ikke er lagt ind | Delvist | Slå dansk til i miljøet og importér oversættelser | [decisions.md](system/decisions.md) B3 |

## Nye muligheder (findes ikke i Salesforce)

Funktioner, som den nye platform og datamodel gør mulige, og som MTH ikke har i dag.

| # | Område | Beskrivelse | Status | Løsning / plan | Kilde |
|---|---|---|---|---|---|
| N1 | Ressourceplanlægning | Sagsteamsroller med start, slut og belastning % (flere perioder pr. person), fravær og nedsat tid, tilbudsafdelinger. Ressourceoverblik med Gantt og ugentlig belastning pr. person, og fanen Case Team på Salgsmuligheden med sagens Gantt. Redigering direkte i diagrammet | Bygget i POC (2026-11-27) | — | [resources.md](system/resources.md), B25 |
| N2 | Tilbudssimulering: hvilke projekter kan vi nå at byde på? | Brugeren vælger kandidatprojekter (Leads og Salgsmuligheder før tilbud) og ser, om der er ledige folk til at udarbejde tilbuddene, før der siges ja. Ønsket af brugeren 2026-11-27, gerne via Copilot | Design klar (vurderet muligt) | Se nedenfor | — |

### N2 Tilbudssimulering (design)

**Vurdering:** Kan lade sig gøre. Selve simuleringen skal dog være **regler i kode**, ikke Copilot alene. Microsoft Copilot i model-drevne apps kan læse og opsummere data, men ikke regne sikkert på belastning over tid. Copilot kan bruges som en **naturligt sprog-indgang** oven på en beregning, som giver de samme svar hver gang.

1. **Bemandingsskabeloner** (ny tabel): pr. sagstype eller størrelse, hvilke tilbudsroller der skal til, med hvilken % og hvor mange uger før tilbudsfristen. F.eks. "Byggeri over 100 mio.: Tilbudschef 30 % i 6 uger, Kalkulatør 80 % i 4 uger, Koordinator 40 % i 4 uger".
2. **Foreløbige roller**: kandidatprojekter får foreløbige sagsteamsroller ud fra skabelonen (en ny status "Foreløbig" på rollen, så de ikke blandes med de besluttede). Foreløbige roller kan være uden navngiven person ("en kalkulatør fra afdeling X").
3. **Simulering i Ressourceoverblikket**: en knap "Simulér" slår de foreløbige roller til eller fra. Belastningen vises med og uden kandidaterne, og overbooking pr. uge og afdeling fremhæves.
4. **Rangering** (Custom API `mth_SimulateTenders`): for hver kandidat beregnes, om skabelonens roller kan dækkes af personer i tilbudsafdelingen med ledig kapacitet i tilbudsvinduet. Resultatet er en liste "kan bemandes / kan bemandes med overbooking / kan ikke bemandes" sammen med f.eks. projektværdi og sandsynlighed, så ledelsen kan prioritere. Kan foreslå navngivne personer.
5. **Copilot (valgfrit)**: en Copilot Studio-agent i appen, der kalder `mth_SimulateTenders` og svarer på spørgsmål som "Hvilke af de nye hospitalsprojekter kan Øst nå at byde på i marts?" eller "Hvad sker der med kalkulatørerne, hvis vi byder på Ny bro Roskilde?". Kræver Copilot-licens eller Copilot Studio-kapacitet (se G13).

**Afhængigheder:** N1 (bygget), timer/arbejdstid pr. person og helligdage giver mere præcise tal. Rækkefølge: 1 → 2 → 3 → 4 → 5. Trin 1–3 giver værdi uden Copilot.

## Ikke gennemgået endnu

En komplet GAP-analyse skal også gennemgå (tal fra opgørelsen 2026-11-09):
- **Apex og triggers:** 127 Apex-klasser og 23 triggers. Hvad gør hver, og er den dækket?
- **Flows:** ca. 25 MTH-flows.
- **Rapporter og dashboards:** hvilke bruges, og hvordan genskabes de (visninger, Excel, Power BI).
- **Felter:** aktive felter på Opportunity (104) og Account (43) er analyseret; øvrige objekter (Guarantee__c, Budrunde__c, Reference__c m.fl.) er ikke med i POC'en.
- **Filer:** ca. 37 GB / 30.000 dokumenter. Hvor skal de ligge (SharePoint)?
- **Øvrige integrationer, sikkerhedsmodel, mobilbrug og e-mail-integration.**
