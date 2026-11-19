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

## Det følger ikke med i solutionen

- **Data:** migreres separat og køres inde hos MTH direkte fra Salesforce.
- **Værdier for miljøvariabler og forbindelser:** sættes ved import.
- **Azure Functions og Key Vault:** deployes separat.
- **Tildeling af brugere og roller.**
