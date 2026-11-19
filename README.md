# MTH CRM

Proof of concept på en model-driven Power App på Dataverse, som skal erstatte MT Højgaards Salesforce-CRM. Løsningen bruger ikke Dynamics 365 Sales.

## Indhold

| Mappe | Indhold |
|---|---|
| `solutions/MTHCRM` | Dataverse-solutionen (tabeller, formularer, visninger, app, security roles), udpakket som kildekode |
| `plugins/` | C#-plugins til logik, der skal køre i transaktionen (svarer til Apex-triggers) |
| `functions/` | Azure Functions til integrationer og batchjob, f.eks. SMART-sync |
| `pcf/` | PCF-komponenter, hvor standard-UI ikke slår til |
| `docs/` | Datamodel, beslutninger og feltanalysen fra Salesforce |
| `.github/workflows/` | Build og deploy via Power Platform Actions |
| `scripts/` | Hjælpescripts til udvikling |

## Kom i gang lokalt

Forudsætninger: .NET SDK, Power Platform CLI (`pac`), Git og VS Code med Power Platform Tools.

```powershell
pac auth create --name dev --deviceCode
pac org select --environment https://<dit-miljø>.crm4.dynamics.com/

# Byg og importér solutionen (unmanaged) i udviklingsmiljøet
dotnet build solutions/MTHCRM -c Debug
pac solution import --path solutions/MTHCRM/bin/Debug/MTHCRM.zip --publish-changes

# Hent ændringer lavet i make.powerapps.com tilbage til repoet
pac solution sync --solution-folder solutions/MTHCRM
```

## Konventioner

- Publisher `mth`, så alle custom komponenter får præfikset `mth_`.
- Ingen hårdkodede URL'er, id'er eller miljønavne. Brug miljøvariabler og forbindelsesreferencer.
- Ingen rigtige MTH-data i repoet eller i udviklingsmiljøer uden for MTH. Brug anonymiserede testdata.
- Se [AGENTS.md](AGENTS.md) for domæneviden og arkitekturbeslutninger.

## Deploy

Test og produktion får en **managed** solution, som importeres via GitHub Actions med en service principal. Se [docs/deploy.md](docs/deploy.md).
