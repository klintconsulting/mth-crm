# SharePoint: projektsite og Shared Documents på Salgsmuligheden

Hver Salgsmulighed kan have **sit eget SharePoint-projektsite**. Fanen **Project Site** på Salgsmuligheden viser sitets `Shared Documents` indlejret (gitter med åbn, upload, ny mappe), når **status er 20 eller derover**.

Selve oprettelsen af projektsites (flowet fra Salesforce' SharePoint-integration) er **ikke** bygget endnu. Løsningen viser kun et site, der allerede er koblet til sagen.

## Sådan hænger det sammen

| Del | Hvad | Hvor |
|---|---|---|
| Dokumenthåndtering | Slået til på `mth_opportunity` (`IsDocumentManagementEnabled = true`). Giver relationerne `mth_opportunity_SharePointDocumentLocations` og `mth_opportunity_SharePointDocuments` | Tabel-metadata (sat via Web API, se nedenfor) |
| Server-baseret integration | Slås til **én gang pr. miljø** med guiden "Enable Server-based SharePoint Integration" (se Opsætning). Uden den viser gitteret "Collaborate and share files easily with SharePoint – ask your admin to enable it" | Miljøindstilling (følger **ikke** med solutionen) |
| Standardsite | `sharepointsite` med `https://<tenant>.sharepoint.com`, `isdefault = true` | Data i miljøet (følger **ikke** med solutionen) |
| Projektsite | Én `sharepointsite` pr. projektsite med sitets URL, f.eks. `https://<tenant>.sharepoint.com/sites/proj-xxxx` | Data, oprettes af oprettelsesflowet |
| Site-URL | Kolonnen `mth_projectsiteurl` (Project Site URL) | schema.json |
| Dokumentplacering | Én `sharepointdocumentlocation` pr. sag med **relativ URL** = bibliotekets URL-segment (f.eks. `Delte dokumenter`) og forælder = sagens projektsite (`sharepointsite`), regarding = sagen | Data, oprettes af oprettelsesflowet |
| Fanen "Project Site" | Skjult fra start (`"hidden": true`). Indeholder Project Site URL og undergitteret `SharedDocuments` (tabel `sharepointdocument`, Microsofts visning "Document Associated Grid" `0016f9f3-41cc-4276-9d11-04308d15858d`) | app.json |
| Synlighed | `mth_/scripts/opportunity.js`: viser fanen, når statuskode ≥ 20 (`MIN_STATUS`) og posten er gemt. Kører ved indlæsning og ved ændring af `mth_status` | webresources/, app.json (`scripts`) |

> **Ikke absolutte URL'er.** Server-baseret integration understøtter ikke dokumentplaceringer med `absoluteurl`. Guiden nægter at slå integrationen til ("Absolute URLs Detected"), så længe der findes nogen. Dette blev opdaget og rettet 2026-11-20.

Dokumentgitteret henter filerne fra SharePoint med **brugerens egne SharePoint-rettigheder**. En bruger, der ikke er medlem af projektsitet, ser derfor ikke dokumenterne.

## Det skal oprettelsesflowet gøre (senere)

1. Opret projektsitet, f.eks. en Microsoft 365-gruppe eller et teamsite fra en skabelon, og giv sagsteamet adgang.
2. Sæt `mth_projectsiteurl` på sagen til sitets URL.
3. Registrér sitet og opret den relative dokumentplacering på sagen:
   ```json
   POST sharepointsites
   { "name": "Projekt - <sagsnavn>", "absoluteurl": "https://<tenant>.sharepoint.com/sites/<site>", "isdefault": false }

   POST sharepointdocumentlocations
   { "name": "Shared Documents",
     "relativeurl": "Delte dokumenter",
     "parentsiteorlocation_sharepointsite@odata.bind": "/sharepointsites(<site-id>)",
     "regardingobjectid_mth_opportunity@odata.bind": "/mth_opportunities(<id>)" }
   ```
   `relativeurl` er det sidste segment af bibliotekets faktiske URL (`drive.webUrl` i Graph). Konstruér det ikke selv. På sites oprettet på dansk er selve URL-stien lokaliseret, f.eks. `…/sites/proj-61a80e0a/Delte dokumenter` (set i demo-tenanten 2026-11-20). På engelske sites er den `…/Shared Documents`.

`scripts/New-DemoProjectSites.ps1` gør netop disse tre ting i en demoudgave (Graph-grupper, eksempeldokumenter, kobling i Dataverse) for de 3 største sager med status 20–89.

## Opsætning i et nyt miljø

1. **Slå server-baseret SharePoint-integration til** (én gang pr. miljø, kræver administrator): `https://<org>.crm4.dynamics.com/main.aspx?settingsonly=true`, Settings, Document Management, **Enable Server-based SharePoint Integration**, Online, site-URL `https://<tenant>.sharepoint.com`, valider og Enable. Derefter **Document Management Settings**: markér Opportunity, samme site-URL, ingen mappestruktur, Finish.
2. Slå dokumenthåndtering til på `mth_opportunity` (sker også via Document Management Settings). Det er gjort via Web API (GET EntityDefinitions, sæt `IsDocumentManagementEnabled = true`, PUT med `MSCRM.MergeLabels: true` og solution `MTHCRM`). Indstillingen ligger i tabel-metadata og følger med solutionen.
3. Opret standardsitet, hvis guiden ikke allerede har gjort det. Det er data og skal oprettes i hvert miljø:
   ```powershell
   Invoke-Dataverse POST 'sharepointsites' @{ name = 'SharePoint'; absoluteurl = 'https://<tenant>.sharepoint.com'; isdefault = $true }
   ```
   Alternativt i Power Platform admin center: miljøet, Settings, Integration, **Document management settings**.
4. `Deploy-WebResources.ps1` og derefter `Deploy-App.ps1`.

SharePoint Online i samme tenant som Dataverse kræver ingen yderligere godkendelse (server-baseret integration).
