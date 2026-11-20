<#
.SYNOPSIS
  Kobler eksisterende SharePoint-projektsites til Salgsmuligheder i Dataverse ud fra kolonnen mth_projectsiteurl:
  opretter en sharepointsite pr. projektsite og en RELATIV sharepointdocumentlocation (biblioteket) på sagen.
  Idempotent. Bruges i demo og senere ved migrering af MTH's eksisterende projektsites
  (fyld mth_projectsiteurl fra Salesforce Project_Portal_Url__c og kør scriptet).

  Kræver at server-baseret SharePoint-integration er slået til i miljøet (se docs/system/sharepoint.md),
  og at sitene ligger i samme tenant som Dataverse.

.PARAMETER LibraryName
  Bibliotekets URL-segment: "Delte dokumenter" på sites oprettet på dansk, "Shared Documents" på engelske.

.EXAMPLE
  .\scripts\Set-ProjectSiteLocations.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$LibraryName = 'Delte dokumenter'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
function IdOf($resp) { [regex]::Match([string]$resp, '[0-9a-f]{8}-[0-9a-f-]{27}').Value }

$opps = (Invoke-Dataverse GET "mth_opportunities?`$select=mth_opportunityid,mth_name,mth_projectsiteurl&`$filter=mth_projectsiteurl ne null").value
foreach ($o in $opps) {
    $siteUrl = $o.mth_projectsiteurl.TrimEnd('/')
    $sp = (Invoke-Dataverse GET "sharepointsites?`$select=sharepointsiteid&`$filter=absoluteurl eq '$siteUrl'").value
    $spId = if ($sp) { $sp[0].sharepointsiteid } else { IdOf (Invoke-Dataverse POST 'sharepointsites' @{ name = "Projekt - $($o.mth_name)"; absoluteurl = $siteUrl; isdefault = $false }) }

    $loc = (Invoke-Dataverse GET "sharepointdocumentlocations?`$select=sharepointdocumentlocationid,relativeurl,_parentsiteorlocation_value&`$filter=_regardingobjectid_value eq $($o.mth_opportunityid)").value
    if ($loc -and $loc[0].relativeurl -eq $LibraryName -and $loc[0]._parentsiteorlocation_value -eq $spId) { Write-Host "  = $($o.mth_name)"; continue }
    foreach ($l in $loc) { [void](Invoke-Dataverse DELETE "sharepointdocumentlocations($($l.sharepointdocumentlocationid))") }
    [void](Invoke-Dataverse POST 'sharepointdocumentlocations' @{
        name = 'Shared Documents'; relativeurl = $LibraryName
        'parentsiteorlocation_sharepointsite@odata.bind' = "/sharepointsites($spId)"
        'regardingobjectid_mth_opportunity@odata.bind' = "/mth_opportunities($($o.mth_opportunityid))" })
    Write-Host "  + $($o.mth_name) -> $siteUrl/$LibraryName"
}
Write-Host "Done. $($opps.Count) sag(er) med projektsite."
