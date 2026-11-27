# Ressourceplanlægning (Ressourceoverblik)

Viser, hvor belastet hver person er uge for uge, ud fra sagsteamsrollerne på Salgsmulighederne og personens fravær. Primært til tilbudsfasen (tilbudschefer, kalkulatører m.fl.), men alle roller kan planlægges. Bygget 2026-11-27 (beslutning B25). **Findes ikke i Salesforce**, hvor rollerne er faste opslagsfelter uden datoer. Det er en ny funktion, som sagsteamet som undertabel (B5) gør mulig.

## For brugeren

**Sales → Resources → Resource Overview**:
- **Én række pr. person** med belastningen pr. uge: grøn under 80 %, gul 80–100 %, rød over 100 %. Skraveret betyder fri hele ugen. En grå streg i bunden betyder delvist fri eller nedsat tid den uge.
- **Klik på navnet** for at folde personens sager ud som Gantt-bjælker: blå = tilbudsrolle, grøn = udførelsesrolle, skraveret = fravær.
- **Filtre:** periode (fra-dato og 8/13/26/52 uger), tilbudsafdeling, rollegruppe (standard: tilbudsroller), søgning på navn, "Vis sager" (fold alle ud) og "Kun personer med roller eller fravær".
- **Redigér direkte i diagrammet:**
  - **Træk en bjælke** for at flytte perioden, eller **træk i en kant** for at ændre start eller slut (hele dage). Gemmes med det samme.
  - **Klik på en bjælke** for at rette person, rolle, start, slut og belastning %, **dele perioden** (f.eks. 100 % indtil 1. februar og 50 % derefter), **fjerne** personen fra sagen eller **åbne sagen**.
  - **+ Ressource** (øverst) eller **+** ud for en person tilføjer en person til en sag. Tomme datoer udfyldes ud fra sagen.
  - **+ Fravær** opretter ferie, orlov eller nedsat tid. Klik på en fraværsbjælke for at åbne og rette den.
- **Sales → Resources → Availability** er listen over alt fravær.

**På Salgsmuligheden: fanen Case Team** (lige efter General, flyttet hertil fra General 2026-11-27):
- Øverst sagsteamet som liste (rolle, person, start, slut, belastning %; én række pr. periode).
- Under den **Gantt-diagrammet for sagen**: kun personerne på sagen. Belastningsrækken er personens **samlede** belastning på alle sager, så man kan se, om de er overbooket. Bjælker for sagen er fuldt farvet, personens andre sager er blege. Perioden sættes automatisk ud fra sagens roller.
- Samme redigering som i Ressourceoverblikket (træk, klik, del, fjern). **+ Ressource** tilføjer til denne sag. Ændringer i diagrammet opdaterer listen, og ændringer i listen opdaterer diagrammet.
- Nye sager skal gemmes først.

Formularen for en sagsteamsrække har sektionen *Resource planning*.

**Tilbudsafdelinger:** Setup → Organizational Units → en enhed med *Is Tender Department* → fanen **Tender Staff** → tilføj brugere. En person kan være i flere afdelinger.

## Regler

| Regel | Hvor |
|---|---|
| Belastning pr. dag = summen af **Allocation %** for personens roller, der er aktive den dag. Ugens belastning = summen over mandag–fredag delt med summen af personens kapacitet de dage | `overview.js` (`weekLoad`) |
| Kapacitet pr. dag = 100 %, eller den laveste **Capacity %** blandt fraværsrækker den dag (ferie, orlov, andet fravær = 0 %, nedsat tid f.eks. 60 %) | `overview.js` |
| Kun roller på sager med Status Number under 85 tæller (85 Project filed og 90–99 tabt, annulleret, slettet) | `overview.js` |
| Tomme datoer udfyldes ved oprettelse og ændring af rolle eller sag: **tilbudsroller** (Tender Manager, Tender Responsible, Tender Coordinator, Tender Calculator, Bid Manager): Invitation to Tender Date (ellers Tender Start Date) → Tender Date (ellers Initial Tender Date, ellers start + 28 dage). **Pre-Qualification Responsible**: 21 dage før Pre-Qualification Date → Pre-Qualification Date. **Primary Responsible**: Invitation to Tender Date → Order End Date. **Øvrige** (Project Manager, Design Manager, Guarantee Responsible): Order Start → Order End Date (ellers projektets datoer fra SMART) | Plugin `ResourceDefaults` |
| Tom Allocation % = 100 | Plugin `ResourceDefaults` |
| Slutdato før startdato afvises | Plugin `ResourceDefaults` |
| Flere perioder for samme person og rolle på en sag = flere sagsteamsrækker (f.eks. 100 % fra x til y og 50 % fra y til z). "Del perioden" i diagrammet opretter den nye række | Brugerens krav 2026-11-27 |
| Fravær: navnet sættes automatisk ("Ferie – Lars Nielsen 21-12-2026–01-01-2027"). Tom Capacity = 0 %, undtagen ved nedsat tid, hvor den skal udfyldes | Plugin `ResourceDefaults` |

Datoerne følger **ikke** sagen automatisk, når sagens datoer ændres bagefter. Personens egne datoer bevares. Ryd start og slut på rollen for at hente sagens datoer igen.

## Teknik

| Del | Hvor |
|---|---|
| Kolonner på `mth_opportunityrole`: `mth_startdate`, `mth_enddate` (kun dato), `mth_allocation` (heltal 0–100) | `scripts/schema/schema.json` |
| Tabel `mth_availability` (Availability), brugerejet: `mth_userid` (Person, påkrævet), `mth_type` (Vacation / Leave / Reduced Capacity / Other Absence; dansk Ferie / Orlov / Nedsat belastning / Andet fravær), `mth_startdate`, `mth_enddate`, `mth_capacity` (0–100), `mth_description` | `scripts/schema/schema.json` |
| N:N `mth_orgunit_systemuser_TenderDept` (Tender Departments ↔ Tender Staff) | `scripts/schema/schema.json` |
| Plugin `ResourceDefaults` (PreOperation, synkron): Create/Update af `mth_opportunityrole` og `mth_availability` | `plugins/Mth.Crm.Plugins/ResourceDefaults.cs`, `scripts/plugins/steps.json` |
| Siden: HTML + JavaScript uden byggetrin, data via `Xrm.WebApi` med brugerens egne rettigheder. Gantt og belastning er tegnet selv (intet eksternt bibliotek) | `webresources/mth_/resources/overview.html` og `overview.js` |
| Sagstilstand: samme side som web resource på fanen Case Team (`"webresource": { "name": …, "data": "mode=case" }` i app.json). PassParameters giver `?id=…&typename=mth_opportunity`. Siden finder formularens undergitter `CaseTeam` via `parent.Xrm.Page`, opdaterer det efter ændringer og lytter på dets `addOnLoad` | `overview.js` (`CASE`), `Deploy-App.ps1` (sektionstypen `webresource`) |
| Navigation, formularer og visninger (MTH Availability, MTH Opportunity Roles med datoer) | `scripts/app/app.json` |
| Rettigheder: `mth_availability` Sales: opret/ret/slet egne, se alle. Tender Manager og Administrator: alle. Sagsteamsroller kan rettes af alle MTH-roller (Global, som før) | `scripts/security/roles.json` |
| Demodata (kun udviklermiljø): fordeler rollerne på demobrugerne, procent pr. rolle, ét delt eksempel, afdelingsmedlemskab og fravær | `scripts/New-ResourceTestData.ps1` |

## Afprøvet 2026-11-27 i udviklermiljøet

- Plugin: alle 119 roller fik datoer ud fra sagerne, navn "Rolle – Person" og procent. Fravær fik navn og kapacitet (0 % for ferie og orlov, 60 % for nedsat tid).
- Sidens forespørgsler kørt som Lars Nielsen (MTH Sales): brugere med afdelinger, 50 roller i standardperioden (34 på aktive sager, 15 tilbudsroller), 4 fravær. Lars må rette sagsteamsroller.
- **Siden, træk og slip og dialogen er ikke klikket igennem i browseren af udvikleren.**

## Ikke med endnu

- Timer i stedet for procent, og arbejdstid pr. person (alle regnes som fuld tid, 5 dage).
- Helligdage.
- Kapacitetsplanlægning pr. afdeling (sum af belastning mod afdelingens kapacitet).
- Træk og slip af fraværsbjælker (åbn og ret dem i stedet).
- Eksport til Excel eller Power BI.
