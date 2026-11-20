<#
.SYNOPSIS
  DEMO: Opretter et SharePoint-projektsite (Microsoft 365-gruppe) pr. Salgsmulighed med status >= 20,
  lægger eksempeldokumenter i Shared Documents og kobler sitet til sagen i Dataverse
  (mth_projectsiteurl + sharepointsite pr. projektsite + relativ sharepointdocumentlocation).
  Viser hvad det rigtige oprettelsesflow skal gøre. KUN i en test-tenant.

.EXAMPLE
  .\scripts\New-DemoProjectSites.ps1 -EnvironmentUrl https://orgxxxx.crm4.dynamics.com -Count 3
#>
param(
    [Parameter(Mandatory)][string]$EnvironmentUrl,
    [int]$Count = 3,
    [string[]]$Members = @('ah', 'ln', 'ms')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $EnvironmentUrl

# ---- Graph login (device code) ----
$clientId = '14d82eec-204b-4c2f-b7e8-296a70dab67e'   # Microsoft Graph Command Line Tools
$scope = 'https://graph.microsoft.com/Group.ReadWrite.All https://graph.microsoft.com/Sites.ReadWrite.All https://graph.microsoft.com/Files.ReadWrite.All https://graph.microsoft.com/User.Read.All'
$auth = 'https://login.microsoftonline.com/organizations/oauth2/v2.0'
$dc = Invoke-RestMethod -Method Post -Uri "$auth/devicecode" -Body @{ client_id = $clientId; scope = $scope }
Write-Host $dc.message
$token = $null
while (-not $token) {
    Start-Sleep -Seconds ([int]$dc.interval)
    try { $token = (Invoke-RestMethod -Method Post -Uri "$auth/token" -Body @{ grant_type = 'urn:ietf:params:oauth:grant-type:device_code'; client_id = $clientId; device_code = $dc.device_code }).access_token }
    catch { $e = ($_.ErrorDetails.Message | ConvertFrom-Json).error; if ($e -notin 'authorization_pending', 'slow_down') { throw } }
}
function Graph([string]$method, [string]$path, $body, [string]$contentType = 'application/json; charset=utf-8') {
    $p = @{ Method = $method; Uri = "https://graph.microsoft.com/v1.0/$path"; Headers = @{ Authorization = "Bearer $token" }; ContentType = $contentType }
    if ($body -is [byte[]]) { $p.Body = $body } elseif ($body) { $p.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10)) }
    Invoke-RestMethod @p
}

$me = Graph GET 'me'
$domain = ($me.userPrincipalName -split '@')[1]
$memberIds = @($me.id) + @($Members | ForEach-Object { try { (Graph GET "users/$_@$domain").id } catch { Write-Warning "Bruger $_@$domain findes ikke" } }) | Where-Object { $_ }

# ---- Sager med status >= 20 (option value 273280020) ----
$opps = (Invoke-Dataverse GET "mth_opportunities?`$select=mth_opportunityid,mth_name,mth_projectsiteurl&`$filter=mth_status ge 273280020 and mth_status lt 273280090&`$orderby=mth_amount desc&`$top=$Count").value
foreach ($o in $opps) {
    $nick = 'proj-' + $o.mth_opportunityid.Substring(0, 8)
    $group = (Graph GET "groups?`$filter=mailNickname eq '$nick'").value | Select-Object -First 1
    if (-not $group) {
        $group = Graph POST 'groups' @{
            displayName = "Projekt - $($o.mth_name)"; description = 'Demo-projektsite oprettet af scripts/New-DemoProjectSites.ps1'
            mailNickname = $nick; mailEnabled = $true; securityEnabled = $false; groupTypes = @('Unified'); visibility = 'Private'
            'owners@odata.bind' = @("https://graph.microsoft.com/v1.0/users/$($me.id)")
            'members@odata.bind' = @($memberIds | ForEach-Object { "https://graph.microsoft.com/v1.0/users/$_" })
        }
        Write-Host "  + gruppe $nick ($($o.mth_name))"
    } else { Write-Host "  = gruppe $nick findes" }

    # The team site is provisioned asynchronously (typically 1-5 minutes)
    $drive = $null; $site = $null
    for ($i = 0; $i -lt 40 -and -not $drive; $i++) {
        try { $site = Graph GET "groups/$($group.id)/sites/root"; $drive = Graph GET "groups/$($group.id)/drive" } catch { Start-Sleep -Seconds 15 }
    }
    if (-not $drive) { Write-Warning "Sitet for $nick er ikke klar endnu - kør scriptet igen om lidt"; continue }

    $files = [ordered]@{
        'Tilbud/Tilbudsbrev.txt'           = "Tilbudsbrev - $($o.mth_name)`r`nDemodokument."
        'Tilbud/Forbehold.txt'             = "Forbehold og forudsætninger`r`nDemodokument."
        'Tegninger/Situationsplan.txt'     = "Situationsplan`r`nDemodokument (i virkeligheden en PDF/DWG)."
        'Tidsplan.csv'                     = "Aktivitet;Start;Slut`r`nUdbud;2026-12-01;2027-01-15`r`nByggestart;2027-03-01;2027-03-01"
    }
    foreach ($k in $files.Keys) {
        $path = ($k -split '/' | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/'
        [void](Graph PUT "drives/$($drive.id)/root:/$($path):/content" ([Text.Encoding]::UTF8.GetBytes($files[$k])) 'text/plain')
    }
    Write-Host "    $($files.Count) dokumenter i Shared Documents"

    # ---- Dataverse: site-URL, SharePoint-site og RELATIV dokumentplacering på sagen ----
    # Server-based SharePoint integration does not support absolute-URL document locations.
    # Supported model: the project site is a sharepointsite record, and the opportunity's location
    # is relative (library URL segment, e.g. "Delte dokumenter" on Danish sites) under that site.
    $siteUrl = $site.webUrl.TrimEnd('/')
    $libraryUrl = [uri]::UnescapeDataString($drive.webUrl).TrimEnd('/')
    $libraryName = $libraryUrl.Substring($libraryUrl.LastIndexOf('/') + 1)
    [void](Invoke-Dataverse PATCH "mth_opportunities($($o.mth_opportunityid))" @{ mth_projectsiteurl = $siteUrl })
    $sp = (Invoke-Dataverse GET "sharepointsites?`$select=sharepointsiteid&`$filter=absoluteurl eq '$siteUrl'").value
    $spId = if ($sp) { $sp[0].sharepointsiteid } else {
        [regex]::Match([string](Invoke-Dataverse POST 'sharepointsites' @{ name = "Projekt - $($o.mth_name)"; absoluteurl = $siteUrl; isdefault = $false }), '[0-9a-f]{8}-[0-9a-f-]{27}').Value }
    $loc = (Invoke-Dataverse GET "sharepointdocumentlocations?`$select=sharepointdocumentlocationid&`$filter=_regardingobjectid_value eq $($o.mth_opportunityid)").value
    foreach ($l in $loc) { [void](Invoke-Dataverse DELETE "sharepointdocumentlocations($($l.sharepointdocumentlocationid))") }
    [void](Invoke-Dataverse POST 'sharepointdocumentlocations' @{
        name = 'Shared Documents'; relativeurl = $libraryName
        'parentsiteorlocation_sharepointsite@odata.bind' = "/sharepointsites($spId)"
        'regardingobjectid_mth_opportunity@odata.bind' = "/mth_opportunities($($o.mth_opportunityid))" })
    Write-Host "    koblet: $siteUrl + '$libraryName'"
}
Write-Host 'Done.'
