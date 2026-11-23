# NNE: find virksomheder og opret Accounts

NNE (Navne & Numre Erhverv, i dag drevet af D&B/Bisnode) er MTH's opslag i det danske virksomhedsregister. Det erstatter Salesforce-integrationen `NNE_Service`, `NNE_AccountImporter` og LWC'en `nneSearch` (hentet fra produktion 2026-11-21). Brugeren søger på navn, CVR, adresse eller telefon, ser detaljer og opretter virksomheden som Account med ét klik, som i Salesforce.

Den gamle NNMDATA-pakke i Salesforce er udgået og overføres ikke.

## Brug

- **Accounts → Ny fra NNE / New from NNE** (knap i listen) eller **Kunder → NNE Search** i navigationen åbner søgningen som en stor dialog.
- Søgefelter: Navn, CVR, TDC-id, Vej, Husnummer, Postnummer, By, Telefon. Afkrydsning: *Navn begynder med*, *Vej begynder med*, *Kun juridiske enheder*, *Kun aktive* (standard til) og antal hits (1–500, standard 50).
- Resultatlisten viser navn, adresse, CVR, P-nummer, virksomhedsform og status. Mærkerne **Findes** (der er allerede en aktiv Account med samme TDC-id), **Reklamebeskyttet** og inaktiv status vises ved rækken.
- Klik på navnet for detaljer. **Opret** opretter Accounten og åbner den i en ny fane. **Vis** åbner den eksisterende Account.
- Teksterne er på dansk eller engelsk efter brugerens sprog.

*Kun aktive* sender `companyStatus = 0` til NNE, præcis som Salesforce. NNE returnerer stadig enheder, som CVR har registreret som ophørt, hvis selve opslaget er aktivt (f.eks. gamle P-enheder under et nyt CVR). Status står i kolonnen, så brugeren kan se det.

## Arkitektur

```text
Web resource mth_/nne/search.html (+ search.js)
   │  fetch /api/data/v9.2/mth_NneSearch, mth_NneImportAccount   (brugerens egen session)
   ▼
Custom API (plugin, sandbox, .NET 4.6.2)          Miljøvariabler
   NneSearch / NneImportAccount  ───────────────►  mth_NneEndpoint, mth_NneAccessKey
   │  NneService: SOAP over HTTPS (TLS 1.2, 30 s timeout)
   ▼
https://nne-api.bisnode.com/nne-ws/3.1/NNE   (SOAP 3.1, rpc/encoded)
```

Kaldet til NNE sker server-side i et plugin, så adgangsnøglen aldrig når browseren. NNE er ikke IP-begrænset, så Dataverse' sandbox kan kalde den direkte.

| Fil | Indhold |
|---|---|
| `plugins/Mth.Crm.Plugins/NneSoap.cs` | Ren SOAP: bygger envelopes og parser svar (multi-ref `href="#IDn"`). `NneQuery`, `NneCompany` (JSON-kontrakt med camelCase). Kaster ved SOAP Fault eller NNE `errorMessage`. CVR/P-nr. "0" bliver tom, `xsi:nil` bliver tom. Uden afhængigheder til Dataverse, så den kan unit-testes |
| `plugins/Mth.Crm.Plugins/NneService.cs` | HTTP-kald, læsning af miljøvariabler (værdi, ellers standardværdi) og opslag af eksisterende Accounts på `mth_tdcid` |
| `plugins/Mth.Crm.Plugins/NneSearch.cs` | Custom API `mth_NneSearch` |
| `plugins/Mth.Crm.Plugins/NneImportAccount.cs` | Custom API `mth_NneImportAccount` |
| `webresources/mth_/nne/search.html`, `search.js` | Søgesiden (ingen framework, ingen eksterne biblioteker) |
| `webresources/mth_/scripts/account.js` | `Mth.Account.nneSearch()` åbner søgningen med `Xrm.Navigation.navigateTo` (dialog 90 % × 90 %) og opdaterer listen bagefter |
| `scripts/Deploy-Nne.ps1` | Miljøvariablerne og nøglens værdi |
| `tests/Mth.Crm.Plugins.Tests/NneSoapTests.cs` | Test af envelopes og parsing med opdigtede svar |

### mth_NneSearch

| Input | Type | Bemærkning |
|---|---|---|
| Name, Cvr, TdcId, Street, HouseNo, Zip, City, Phone | String | Mindst ét kriterium er påkrævet |
| NameStartsWith, StreetStartsWith, LegalEntitiesOnly | Boolean | Standard false |
| ActiveOnly | Boolean | Standard true |
| MaxHits | Integer | 1–500, standard 50 |

Output `ResultJson`: `{ "total": <antal hits i NNE>, "companies": [ … ] }`.

Forløb: `searchTargetGroup` (NNE returnerer mindst 10 pr. side, så listen beskæres til MaxHits). Derefter `fetchCompany` for de første op til 100 rækker parallelt (`Parallel.For`, højst 8 samtidige), fordi søgesvaret mangler virksomhedsform, status, P-nr., e-mail, hjemmeside, ansatte og stiftelsesår. Det er det samme, som Salesforce gør med `fetchCompanyStatuses`. Til sidst sættes `accountId` på rækker, der allerede findes som aktiv Account (opslaget kører som brugeren, så de kun ser, hvad de har adgang til). En søgning med 20 hits tager ca. 5 sekunder.

### mth_NneImportAccount

Input `TdcId`. Output `AccountId`, `Created` (Boolean), `Message`.

1. Dubletkontrol på aktive Accounts med samme `mth_tdcid`. Den kører i systemkontekst, så en dublet findes, selv om brugeren ikke kan se den. Findes den, returneres dens id med `Created = false`.
2. Ellers hentes virksomheden med `fetchCompany`, og Accounten oprettes **som brugeren** (brugerens rettigheder og ejerskab gælder).

| NNE | Account |
|---|---|
| officialName | `name` |
| street, zipCode, district | `address1_line1`, `address1_postalcode`, `address1_city`; `address1_country` = "Danmark" |
| phone, email, homePage | `telephone1`, `emailaddress1`, `websiteurl` |
| numberOfEmployees | `numberofemployees` |
| tdcId | `mth_tdcid` (dubletnøgle) |
| cvrNo, pNo | `mth_vatno`, `mth_pnumber` |
| companyForm, legalEntity, statusText | `mth_companyform`, `mth_legalentity`, `mth_companystatustext` |
| foundedYear, adProtection, tdfName | `mth_foundedyear`, `mth_adprotection`, `mth_directoryname` |
| (tidspunkt) | `mth_nneupdatedon` |

Kolonnerne vises i sektionen **Company Data (NNE)** på fanen General i Account-formularen.

## Adgangsnøgle og miljøvariabler

| Miljøvariabel | Standardværdi | Værdi |
|---|---|---|
| `mth_NneEndpoint` | `https://nne-api.bisnode.com/nne-ws/3.1/NNE` | Kun hvis endpointet skifter igen (den gamle adresse `service.nnerhverv.dk` lukkede 2026-11-30) |
| `mth_NneAccessKey` | (ingen) | MTH's adgangsnøgle. **Sættes pr. miljø og er ikke i solutionen eller repoet** |

`scripts/Deploy-Nne.ps1` opretter definitionerne i solutionen `MTHCRM` og opretter nøglens værdi **uden** solution-header. Værdien kommer derfor aldrig med i `pac solution sync` eller i git. Nøglen læses fra `-AccessKey` eller fra `%LOCALAPPDATA%\mth-crm\nne-accesskey.txt` på udviklerens maskine. Efter hver `pac solution sync` skal det tjekkes, at `solutions/MTHCRM/src/environmentvariabledefinitions/mth_NneAccessKey/` **ikke** indeholder en `environmentvariablevalues.json`.

POC'en bruger den NNE-nøgle, MTH bruger i Salesforce i dag. I test og produktion sætter MTH's IT værdien efter import. I produktion anbefales en miljøvariabel af typen *Secret* med reference til Azure Key Vault i stedet for en tekstværdi. Plugin'et læser i dag kun tekstværdier, så det kræver en lille ændring i `NneService.EnvironmentVariable` (`RetrieveEnvironmentVariableSecretValue`).

Mangler nøglen, fejler søgningen med beskeden "NNE er ikke sat op …".

## Rettigheder

Custom API'erne har ingen særskilt rettighed. Søgning kræver kun adgang til appen. Oprettelse kræver Create på Account, som MTH-rollerne har.

## Deploy

```powershell
.\scripts\Deploy-Schema.ps1       -Url https://<org>.crm4.dynamics.com   # Account-kolonnerne
.\scripts\Deploy-WebResources.ps1 -Url https://<org>.crm4.dynamics.com
.\scripts\Deploy-Nne.ps1          -Url https://<org>.crm4.dynamics.com   # miljøvariabler + nøgle
.\scripts\Deploy-Plugins.ps1      -Url https://<org>.crm4.dynamics.com   # Custom API'erne
.\scripts\Deploy-App.ps1          -Url https://<org>.crm4.dynamics.com   # knap, navigation, formular
pac solution sync --solution-folder solutions/MTHCRM
```

## Test

- Unit-test: `dotnet test tests/Mth.Crm.Plugins.Tests` (kun opdigtede SOAP-svar).
- End-to-end i udviklermiljøet 2026-11-23: søgning "MT Højgaard" (90 hits, 20 returneret på 4,8 s), oprettelse af "Mt Højgaard Danmark A/S" (TDC-id 100315159) med alle felter udfyldt, andet kald gav `Created = false` med samme id, og en ny søgning viste `accountId`.

## Fejlsøgning

- **"NNE er ikke sat op"**: miljøvariablen `mth_NneAccessKey` mangler en værdi. Kør `Deploy-Nne.ps1` eller sæt værdien i make.powerapps.com (Solutions → MTHCRM → Environment variables).
- **NNE errorMessage / SOAP Fault**: beskeden fra NNE vises direkte, typisk forkert eller udløbet nøgle.
- **Timeout**: hvert kald har 30 s. Plugin-sporingslog (Settings → Plug-in Trace Log) viser detaljer, hvis sporing er slået til.
