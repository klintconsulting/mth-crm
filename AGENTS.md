# AGENTS.md: kontekst til AI-assistenter og nye udviklere

Filen bruges af Claude Code, GitHub Copilot og andre agenter. Hold den opdateret, når der træffes beslutninger.

## Projektet

MT Højgaard (MTH) er en dansk entreprenørvirksomhed og et Microsoft-hus. CRM'et ligger i dag i Salesforce, og det er for dyrt. Dette repo er en **model-driven Power App på Dataverse**, som skal erstatte det.

- **Ingen Dynamics 365 Sales.** Lead og Salgsmulighed er custom tabeller, ikke Microsofts `lead`/`opportunity`.
- Account og Kontakt kan bygge på Dataverse-standardtabellerne `account`/`contact`.
- Undgå Microsofts *restricted tables*, der kræver Dynamics-licens.

## Arkitektur

| Behov | Løsning |
|---|---|
| Datamodel, formularer, visninger | Dataverse-solution `MTHCRM`, publisher `mth` |
| Transaktionel logik (som Apex-triggers) | C#-plugins i `plugins/` (.NET Framework 4.6.2) |
| Integrationer og natlige job | Azure Functions i `functions/`, der skriver via Dataverse Web API som application user |
| Enkle notifikationer | Power Automate, men ingen kompleks logik dér |
| Rapportering | Power BI. Beløbsformler fra Salesforce bliver measures, ikke kolonner |
| Filer | SharePoint, ikke Dataverse-fillager (ca. 37 GB i Salesforce) |
| Adgang | Security roles, Business Units og Entra ID-grupper via group teams |

## Datamodel (beslutninger fra feltanalysen, november 2026)

Analysen ligger i `docs/analyse/`. Et felt tæller som "aktivt", når det er udfyldt på mindst 5 % af de poster, der er oprettet de seneste 3 år.

- **Byggeprojekt** er en selvstændig tabel. I Salesforce lå ca. 47 `fnl_*`-felter (projektdata fra SMART/Byggefakta) kopieret på både Lead og Salgsmulighed. Her ligger de ét sted, og Lead og Salgsmulighed slår op til Byggeprojekt.
- **Salgsmulighed:** ca. 60 kolonner (Salesforce havde 281 custom datafelter og 104 formler).
- **Status** er en rigtig kolonne med et valgsæt (statuskoder `01`–`99`), ikke en formel over en tekst. 60–85 = vundet, 90+ = lukket uden ordre (90/92 tabt, 91 opgivet, 93 annulleret, 99 slettet).
- **Garanti:** milepæle er en undertabel **Garantimilepæl**, ikke 10 gentagne slots af felter.
- **Budrunde:** bydende og bud er en undertabel, ikke `Bydende1–5`/`Bud0–5`. Afklar forholdet til Salesforce-objektet `Bud__c` først.
- **Samarbejdspartner:** valgte kontakter er en mange-til-mange-relation til Kontakt, ikke en tekst med Id'er.
- Nummererede rollefelter (`Tender_Calculator21/22/23/24`, `Project_Manager2/22` osv.) samles til et sagsteam med roller.
- **Lotus Notes-integrationen er nedlagt.** Alt med præfikset `LN` ignoreres og migreres ikke.
- Firmadata (CVR, regnskab, NACE) kom fra NN Markedsdata. Det kræver en ny integration.

## Integrationer, der skal genopbygges

SMART (Byggefakta), SharePoint og projektportal, Business Central, NN Markedsdata, Templafy, BI-udtræk. Salesforce-integrationerne FNL og Lotus Notes udgår.

### SMART-sync (fra Salesforce-klassen `SMART_ProjectSync`)
- API `https://api.smart.byggefakta.dk`, v2.3. Auth med `POST /v2.3/auth` med client id/secret, og `result.opid` bruges som bearer.
- Endpoints: `/projects`, `/tenders`, `/companies`, `/lookups?language_id=3`. Paginering med `offset`/`limit`.
- Kør pr. side af projekter, ikke pr. tidsvindue. Watermark flyttes kun frem til starten af første side, der ikke blev færdig.
- `phone_data.number` skal normaliseres (`number` er et reserveret ord i den gamle Apex-DTO).
- Hemmeligheder ligger i Azure Key Vault, aldrig i kode.

## Sprog og terminologi

- UI og dokumentation er på **dansk**. Skriv **Salgsmulighed, Kontakt, Account, Lead** med stort.
- Sig "sager uden ordre" om lukkede, ikke-vundne sager, aldrig "tabte sager".
- `Partner_Evaluation__c` hedder **leverandørevaluering**. `Collaborating_Partner__c` hedder **samarbejdspartner**.

## Regler for agenter

- Rør aldrig MTH's Dataverse- eller Salesforce-miljøer direkte. Ændringer går via pull request og GitHub Actions.
- Commit aldrig rigtige data, hemmeligheder eller `local.settings.json`.
- Når du ændrer noget i make.powerapps.com, så synk med `pac solution sync` og commit den udpakkede solution.
