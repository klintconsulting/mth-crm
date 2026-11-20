# Deploy

## Miljøer

| Miljø | Ejer | Solution-type | Hvordan |
|---|---|---|---|
| Udvikling (POC) | Konsulent | Unmanaged | Manuelt med `pac` eller make.powerapps.com |
| Test | MTH | Managed | GitHub Actions `deploy-solution`, environment `test` |
| Produktion | MTH | Managed | GitHub Actions `deploy-solution`, environment `production` med godkendelse |

## Opsætning i GitHub (gøres af MTH's IT ved overdragelse)

1. Opret en **app registration** i Entra ID og en client secret.
2. Tilføj den som **application user** i hvert Power Platform-miljø med rollen System Customizer (eller en dedikeret deploy-rolle).
3. Opret GitHub **environments** (`dev`, `test`, `production`) med:
   - variabel `ENVIRONMENT_URL`, f.eks. `https://orgxxxx.crm4.dynamics.com/`
   - secrets `PP_APP_ID`, `PP_CLIENT_SECRET`, `PP_TENANT_ID`
4. Slå "Required reviewers" til på `production`.

## Workflows

- **export-solution** (manuel): eksporterer `MTHCRM` fra `dev`, pakker den ud til `solutions/MTHCRM/src` og opretter en pull request.
- **deploy-solution** (manuel): pakker solutionen som managed og importerer den i det valgte miljø.

## E-mail (server-side synkronisering)

Brugere kan først sende mail fra løsningen, når deres postkasse i Dataverse er **godkendt** og **testet og aktiveret**. Ellers får de beskeden om, at deres mail er deaktiveret.

1. Brugeren skal have en Exchange Online-postkasse (licens med Exchange Online).
2. En global administrator eller Exchange-administrator godkender e-mailadressen: Power Platform admin center, miljøet, Settings, Email, Mailboxes, vælg postkasserne og **Approve Email**.
3. Vælg **Test & Enable Mailboxes**. Status for indgående og udgående mail skal blive "Success".

Via Web API (som brugt i udviklermiljøet 2026-11-20):

```powershell
Invoke-Dataverse PATCH "mailboxes(<mailboxid>)" @{ emailrouteraccessapproval = 1 }        # Approve
Invoke-Dataverse PATCH "mailboxes(<mailboxid>)" @{ testemailconfigurationscheduled = $true }  # Test & Enable
```

## Det følger ikke med i solutionen

- **Data:** migreres separat og køres inde hos MTH direkte fra Salesforce.
- **Værdier for miljøvariabler og forbindelser:** sættes ved import.
- **Azure Functions og Key Vault:** deployes separat. SMART-synken deployes med `scripts/Deploy-SmartSyncAzure.ps1` mod MTH's abonnement, kørt af MTH's IT eller fra GitHub Actions. Den opretter selv application useren i Dataverse, men kræver Owner på ressourcegruppen. SMART-nøglerne skal i Key Vault. Se `docs/system/smart.md`.
- **Tildeling af brugere og roller.**
- **Tema-posten "MTH" (farver og logo):** kør `scripts/Deploy-Branding.ps1` efter import. App-indstillingen, ikonet og web resources følger med i solutionen.
