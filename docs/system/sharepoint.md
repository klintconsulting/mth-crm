# SharePoint: link til projektsitet

Hver Salgsmulighed kan have sit eget SharePoint-projektsite, som i Salesforce. Løsningen viser **et link**: kolonnen **Project Site URL** (`mth_projectsiteurl`, tekst med URL-format) står i sektionen Opportunity på fanen General. Et klik åbner sitet i en **ny fane** i browseren. Der er ingen indlejring og ingen dokumentintegration (beslutning B14).

Selve oprettelsen af projektsites (flowet fra Salesforce' SharePoint-integration) er **ikke** bygget. Flowet skal kun oprette sitet og skrive dets URL i `mth_projectsiteurl`.

**Migrering:** Fyld `mth_projectsiteurl` fra Salesforce (`Project_Portal_Url__c`). Linkene virker uanset hvilken tenant sitene ligger i, så længe brugeren har adgang i SharePoint.

**Demo:** `scripts/New-DemoProjectSites.ps1` opretter projektsites (Microsoft 365-grupper) med eksempeldokumenter for de 3 største sager med status 20–89 og sætter `mth_projectsiteurl`.

## Fravalgt: Shared Documents indlejret med Dataverses dokumentintegration (afprøvet 2026-11-20)

Det blev afprøvet at vise sagens `Shared Documents` indlejret med Dataverses server-baserede SharePoint-integration og dokumentgitteret. Det blev fravalgt, fordi:

- **Mappenavigation virker ikke**, når biblioteket ligger i et andet site end standardsitet. Rodniveauet vises, men et klik på en mappe giver: *Folder "proj-xxxx" has been renamed or deleted from SharePoint. It was expected inside "…/sites/proj-xxxx/Delte dokumenter" path.* Afprøvet uden held: projektsite som `sharepointsite` med absolut URL, som `sharepointsite` under standardsitet med relativ sti `sites/proj-xxxx`, og guiden Document Management Settings kørt for projektsitet.
- **Absolutte dokumentplaceringer understøttes ikke** af server-baseret integration. Guiden "Enable Server-based SharePoint Integration" blokerer ("Absolute URLs Detected"), og gør det også, så længe der findes ekstra `sharepointsite`-poster ud over standardsitet.
- **Kun samme tenant:** Dataverse kan ikke vise dokumenter fra en anden organisations SharePoint. POC'en kan altså ikke vise MTH's rigtige sites.
- Testforespørgsler mod den virtuelle tabel `sharepointdocument` skal formes som gitterets (`isrecursivefetch = 0` og `link-entity` til tabellen). En minimal fetch giver misvisende fejl som "SharePoint S2S and MSTeams integration is not enabled for this entity" eller "An unexpected error occurred".

Tilstand i udviklermiljøet efter tilbagerulning: server-baseret integration er stadig slået til (miljøindstilling, standardsite `https://klintconsulting2030.sharepoint.com`), men dokumenthåndtering er slået **fra** på `mth_opportunity` og `account`, og der er ingen dokumentplaceringer. Hvis indlejring skal genovervejes, er en egen filvisning (PCF + Custom API mod Microsoft Graph) den realistiske vej.
