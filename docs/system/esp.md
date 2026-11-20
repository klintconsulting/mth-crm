# ESP: entrepriser, underentreprenører og tilbudsmails

Genskaber ESP-delen fra Salesforce: fag, entreprise, UE (samarbejdspartner), kontakter og mail. Strukturen er den samme, men brugerfladen samles i ét arbejdsrum på Salgsmuligheden.

## Datamodel

```
mth_opportunity 1 ──< mth_buildingcontract >── 1 mth_trade >──< account (fag UE'en kan levere)
                          │
                          1
                          ∧
                 mth_collaboratingpartner >── 1 account (UE'en)
                          │
                          >──< contact (valgte kontakter)
                          │
                          email-aktiviteter (regarding)
```

| Tabel | Svarer til i Salesforce | Indhold |
|---|---|---|
| `mth_trade` (Trade/Fag) | Globalt værdisæt `Building_Contracts` | 213 fag med navn og kode. Indlæses fra `scripts/data/trades.json` med `Import-Trades.ps1` (stamdata, også i produktion). Nøgle: `mth_code` |
| N:N `mth_account_mth_trade` | `Account.Building_Contracts__c` (multivalg) | Hvilke fag en Account kan levere. Bruges til at finde UE'er |
| `mth_buildingcontract` (Entreprise) | `Building_Contract__c` | Salgsmulighed (parental), fag, alternativt navn, tilbudsfrist. `mth_name` = alternativt navn eller fagets navn |
| `mth_collaboratingpartner` (UE på entreprise) | `Collaborating_Partner__c` | Entreprise (parental), Account, status 10–99, type, kommentar, tilbudsevaluering med kommentar, kontraktbeløb, sidste mail sendt. Har aktiviteter (mails) |
| Nøgle `mth_BuildingContractAccount` | `Building_Contract_Partner_ID__c` | Samme Account kan kun være tilknyttet samme entreprise én gang |
| N:N `mth_collabpartner_contact` | `Selected_Contacts__c` (tekst med id'er) | Valgte kontakter hos UE'en, der modtager mails |
| `mth_espemailtemplate` | (fandtes ikke) | Mailskabeloner med pladsholdere |
| E-mailaktiviteter | `Partner_Email__c` + `Partner_Email_Relation__c` | Én mail pr. UE, regarding = UE-posten. Vises på tidslinjen, og svar spores tilbage |

**Status** (`mth_status`, option value = 273280000 + kode): 10 Udvalgt til tilbudsrunde · 20 Ønsker at give tilbud · 22 Vender tilbage · 24 Afventer tilbud · 30 Tilbud modtaget · 60 Kontrakt indgået · 90 Fravalgt til opgave · 91 Fravalgt til tilbudsrunde · 92 Ønsker ikke at give tilbud · 95 Tilbud aldrig modtaget · 97 Tilbud ikke konditionsmæssigt · 99 Slettes. Engelske labels i Dataverse, danske tekster i `labelDa` i schema.json.

**Tilbudsevaluering** (`mth_tenderevaluation`): Grøn (10), Gul (5), Rød (0), Sort (-1). Gul, Rød og Sort kræver kommentar.

**Ikke migreret** (ubrugt i Salesforce-koden eller forældet): `Building_Contract_Name__c` (kontoplan-katalog, ikke brugt), `Email_sent__c` (tæller kun PDF-flet), Evaluation_1..10/Assessment_1..4 (udfyldes uden for ESP, afklares senere), `selectMergedForEmail` og PDF-flet (`Document_Merge__c`).

## Beslutninger (2026-11-20)
- **Én mail pr. UE** til UE'ens valgte kontakter (To), sendt fra brugerens egen postkasse. Erstatter én samlet BCC-mail. Svar spores til den rigtige UE.
- **Status 10 → 24** (Afventer tilbud) sættes automatisk, når mailen er sendt. Andre statusser røres ikke.
- **Med i første version:** tilbudsevaluering med kommentar, opret kontakt, kopiér entreprise (uden dubletter) og mailskabeloner.

## Testdata
`scripts/New-EspTestData.ps1`: 30 opdigtede underentreprenører med 1–3 fag og 1–2 kontakter, 4 entrepriser på hver af de 3 største sager med status 20–89, 2–4 UE'er pr. entreprise med valgt kontakt, og 2 skabeloner. `-Remove` sletter igen.

## Afsendelse: Custom API `mth_SendPartnerEmail`

Plugin `Mth.Crm.Plugins.SendPartnerEmail`, registreret med `Deploy-Plugins.ps1` (`scripts/plugins/steps.json`, sektion `customApis`). Global handling, synkron.

| Input | Type | |
|---|---|---|
| `PartnerId` | Guid | UE-posten (`mth_collaboratingpartner`) |
| `Subject`, `Body` | String | Skabelon med pladsholdere. Body er HTML |
| `AttachmentsJson` | String (valgfri) | `[{"fileName","mimeType","base64"}]` |
| `SendToSelf` | Boolean (valgfri) | Testmail til den kaldende bruger: emne `[TEST] …`, viser hvem den ville være sendt til, ingen regarding, ingen statusændring |

| Output | |
|---|---|
| `Result` | `sent`, `skipped` (ingen valgte kontakter med e-mail) eller `test` |
| `EmailId`, `Message` | Mailens id og en tekst til brugeren |

**Forløb for én UE:** henter UE, entreprise, sag, afsender og valgte kontakter (N:N, aktive, med e-mail) → fletter pladsholdere (`EmailTemplateRenderer`) → opretter `email` fra brugeren til kontakterne, regarding UE'en → vedhæfter filer (`activitymimeattachment`) → `GetTrackingTokenEmail` + `SendEmail` (sendes via brugerens postkasse med server-side sync) → sætter `mth_lastemailsent` = i dag og status 10 → 24.

**Én UE pr. kald.** Et synkront plugin kører i én transaktion, så klienten kalder API'en én gang pr. UE. En fejl hos én UE stopper derfor ikke de andre, og brugeren kan se resultatet pr. UE.

**Pladsholdere:** `{{Opportunity}}`, `{{CaseNumber}}` (BC-sagsnr.), `{{BuildingContract}}`, `{{TenderDeadline}}` (f.eks. "4. december 2026"), `{{Partner}}`, `{{ContactFirstName}}` ("Anne", "Anne og Bo", "Anne, Bo og Carl"; ingen kontakter giver "alle"), `{{Sender}}`. Store og små bogstaver er ligegyldige. Tomme værdier vises som "–". Ukendte pladsholdere bliver stående, så fejl ses i testmailen. Værdier HTML-kodes i brødteksten (kun `& < > " '`, så æøå bevares). Dækket af 12 enhedstests.

**Afprøvet 2026-11-20:** testmail til sig selv (status uændret, vedhæftning med) og en rigtig afsendelse til én UE (mailen sendt, regarding UE'en, status 10 → 24, sidste mail sat).

**Testdata må ikke kunne nå rigtige modtagere:** opdigtede adresser ligger på det reserverede domæne `example.com` (RFC 2606). Til en rigtig afsendelsestest sættes en kontakt midlertidigt til egen adresse.

## Arbejdsrummet "Tender (ESP)" (PCF `mth_Mth.TenderWorkspace`)

Fanen **Tender (ESP)** på Salgsmuligheden. Virtuel PCF-kontrol i React 16.14 og Fluent UI 9.46 (platformens biblioteker) i `pcf/TenderWorkspace`. Engelske og danske tekster i `strings.ts` (brugerens sprog).

| Område | Funktion |
|---|---|
| **Entrepriser** (venstre) | "Tilføj entreprise…": søg i de 213 fag på navn eller kode. Findes faget allerede på sagen, vælges den eksisterende (som i Salesforce). Hvert kort viser navn, fag, **tilbudsfrist** (rød = overskredet, gul = inden for 7 dage) og antal UE'er pr. status. ✎ redigerer alternativt navn og frist, ✕ sletter (med UE'er, efter bekræftelse). "Alle entrepriser" viser alle UE'er på sagen |
| **Samarbejdspartnere** (højre) | Tabel med UE (link til Account), entreprise (ved "alle"), **kontakter som badges**, **status**, **evaluering** og **kommentar**. Status, evaluering og kommentar rettes direkte i rækken og gemmes med det samme uden at genindlæse siden. Gul, Rød og Sort kræver en kommentar (dialog), og annulleres den, ændres intet. Afkrydsning bruges til mail |
| **Kontakter** | Klik på kontakt-badges: UE'ens kontakter med afkrydsning (gemmes med det samme) og **"Ny kontakt"** (fornavn, efternavn, e-mail påkrævet), som oprettes på Account og vælges. Røde badges/teksten "Ingen kontakter valgt" viser, hvem der ikke kan få mail |
| **Find UE** | Panel fra højre for den valgte entreprise. Filtre: **fag** (forudvalgt entreprisens fag, eller "Alle fag"), **region** (postnummerintervaller uden overlap: Hovedstaden inkl. Bornholm 1000–3799, Sjælland 4000–4999, Fyn 5000–5999, Syd- og Sønderjylland 6000–6999, Midtjylland 7000–8999, Nordjylland 9000–9999), **navn**, og "**Skjul UE'er, der tidligere er evalueret rød eller sort**" (standard til). Resultater viser by og **tidligere evalueringer** (antal pr. farve fra alle entrepriser). UE'er, der allerede er på entreprisen, er markeret og kan ikke vælges igen. Maks. 200 resultater. "Tilføj" opretter UE'er med status 10. Har Account'en præcis én kontakt med e-mail, vælges den automatisk |
| **Send mail (n)** | For de afkrydsede UE'er, på tværs af entrepriser. Skabelon (standardskabelonen forudvalgt), emne, tekst i en simpel editor (fed, kursiv, punktliste, **indsæt felt**), vedhæftninger. **Forhåndsvisning pr. UE** med flettede felter og modtagere. UE'er uden kontakter med e-mail vises som "springes over". **"Send test til mig"** og **"Send til n UE'er"** med fremdriftslinje og resultat pr. UE (sendt, sprunget over, fejlet). Kalder `mth_SendPartnerEmail` én gang pr. UE |
| **Kopiér til anden sag** | For den valgte entreprise: søg målsag (navn eller BC-sagsnummer), vælg UE'er. Genbruger en entreprise med samme fag på målsagen eller opretter en. UE'er, der allerede findes dér, springes over (ingen dubletter). Nye får status 10 og samme valgte kontakter |

**Teknik:** al dataadgang i `api.ts` via Dataverse Web API med brugerens session (`fetch`, same-origin), fordi N:N-til/fravalg, FetchXML med intersect-link og Custom API'en ikke findes i `context.webAPI`. Søgning: FetchXML på `account` med link til intersect-tabellen `mth_account_mth_trade`. Ren logik i `logic.ts` (regioner, frist, statusoversigt, evalueringsregler, skabelonfletning som serveren, sanitering af forhåndsvisning, escaping) er dækket af 12 tests i `tests/logic.test.ts`.

**Ikoner:** `@fluentui/react-icons` bruges ikke. Pakken er ikke et platformbibliotek og trækker `@griffel/react` med, som kræver `react/jsx-runtime` (React 17+). Det fejler med platformens React 16.14 og gav en bundle på 7,4 MB. Små tegn-ikoner i `components/icons.tsx` i stedet (bundle ca. 160 KB i dev).

**Afprøvet 2026-11-20:** build, lint og 12 tests. Alle Web API-forespørgsler og skrivestier, som kontrollen bruger, er kørt mod udviklermiljøet. **Brugerfladen er ikke afprøvet i browseren af udvikleren.**

## Kendte begrænsninger
- Editoren bruger `document.execCommand` (forældet, men understøttet i alle browsere) og er bevidst simpel.
- Vedhæftninger sendes base64 med hvert kald (én UE pr. kald). Store filer til mange UE'er er langsomme.
- Valideringsreglen fra Salesforce (`Check_Contract_Amount`: evaluering påkrævet ved kontrakt > 5 mio.) er ikke genskabt.
