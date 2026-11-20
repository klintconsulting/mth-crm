# SharePoint: projektsite og Shared Documents på Salgsmuligheden

Hver Salgsmulighed kan have **sit eget SharePoint-projektsite**. Fanen **Project Site** på Salgsmuligheden viser sitets `Shared Documents` indlejret (gitter med åbn, upload, ny mappe), når **status er 20 eller derover**.

Selve oprettelsen af projektsites (flowet fra Salesforce' SharePoint-integration) er **ikke** bygget endnu. Løsningen viser kun et site, der allerede er koblet til sagen.

## Sådan hænger det sammen

| Del | Hvad | Hvor |
|---|---|---|
| Dokumenthåndtering | Slået til på `mth_opportunity` (`IsDocumentManagementEnabled = true`). Giver relationerne `mth_opportunity_SharePointDocumentLocations` og `mth_opportunity_SharePointDocuments` | Tabel-metadata (sat via Web API, se nedenfor) |
| Standardsite | `sharepointsite` med `https://<tenant>.sharepoint.com`, `isdefault = true` | Data i miljøet (følger **ikke** med solutionen) |
| Site-URL | Kolonnen `mth_projectsiteurl` (Project Site URL) | schema.json |
| Dokumentplacering | Én `sharepointdocumentlocation` pr. sag med **absolut URL** til sagens eget bibliotek, f.eks. `https://<tenant>.sharepoint.com/sites/proj-xxxx/Shared Documents`, regarding = sagen | Data, oprettes af oprettelsesflowet |
| Fanen "Project Site" | Skjult fra start (`"hidden": true`). Indeholder Project Site URL og undergitteret `SharedDocuments` (tabel `sharepointdocument`, Microsofts visning "Document Associated Grid" `0016f9f3-41cc-4276-9d11-04308d15858d`) | app.json |
| Synlighed | `mth_/scripts/opportunity.js`: viser fanen, når statuskode ≥ 20 (`MIN_STATUS`) og posten er gemt. Kører ved indlæsning og ved ændring af `mth_status` | webresources/, app.json (`scripts`) |

Dokumentgitteret henter filerne fra SharePoint med **brugerens egne SharePoint-rettigheder**. En bruger, der ikke er medlem af projektsitet, ser derfor ikke dokumenterne.

## Det skal oprettelsesflowet gøre (senere)

1. Opret projektsitet, f.eks. en Microsoft 365-gruppe eller et teamsite fra en skabelon, og giv sagsteamet adgang.
2. Sæt `mth_projectsiteurl` på sagen til sitets URL.
3. Opret `sharepointdocumentlocation` på sagen:
   ```json
   POST sharepointdocumentlocations
   { "name": "Shared Documents",
     "absoluteurl": "https://<tenant>.sharepoint.com/sites/<site>/Shared Documents",
     "regardingobjectid_mth_opportunity@odata.bind": "/mth_opportunities(<id>)" }
   ```
   Brug **altid** bibliotekets faktiske URL (`drive.webUrl` i Graph) og konstruér den ikke selv. På sites oprettet på dansk er selve URL-stien lokaliseret, f.eks. `…/sites/proj-61a80e0a/Delte dokumenter` (set i demo-tenanten 2026-11-20). På engelske sites er den `…/Shared Documents`.

`scripts/New-DemoProjectSites.ps1` gør netop disse tre ting i en demoudgave (Graph-grupper, eksempeldokumenter, kobling i Dataverse) for de 3 største sager med status 20–89.

## Opsætning i et nyt miljø

1. Slå dokumenthåndtering til på `mth_opportunity`. Det er gjort via Web API (GET EntityDefinitions, sæt `IsDocumentManagementEnabled = true`, PUT med `MSCRM.MergeLabels: true` og solution `MTHCRM`). Indstillingen ligger i tabel-metadata og følger med solutionen.
2. Opret standardsitet. Det er data og skal oprettes i hvert miljø:
   ```powershell
   Invoke-Dataverse POST 'sharepointsites' @{ name = 'SharePoint'; absoluteurl = 'https://<tenant>.sharepoint.com'; isdefault = $true }
   ```
   Alternativt i Power Platform admin center: miljøet, Settings, Integration, **Document management settings**.
3. `Deploy-WebResources.ps1` og derefter `Deploy-App.ps1`.

SharePoint Online i samme tenant som Dataverse kræver ingen yderligere godkendelse (server-baseret integration).
