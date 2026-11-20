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

## Mangler (næste trin)
- Custom API til afsendelse af mails (én pr. UE, status 10 → 24).
- PCF-arbejdsrummet "Udbud" på Salgsmuligheden.
