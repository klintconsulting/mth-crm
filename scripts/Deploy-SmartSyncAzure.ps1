<#
.SYNOPSIS
  Deploys the SMART sync Azure Function end-to-end (infrastructure, code, Dataverse application user) and optionally runs it once.
  Uses only Azure REST APIs (no Azure CLI / Az module needed). Idempotent: safe to re-run after code changes.

  Steps:
   1. Device-code login to Azure (Microsoft's public "Azure PowerShell" client; token cache in %LOCALAPPDATA%\mth-crm\).
   2. Registers resource providers, creates the resource group, deploys infra/smartsync.json (Flex Consumption, managed identity).
   3. dotnet publish of functions/SmartSync/SmartSync.Functions, zip, one-deploy to the Function App.
   4. Looks up the managed identity's application (client) id in Entra ID and registers it in Dataverse as an
      application user with the roles "Basic User" + "MTH Integration" (scripts/security/roles.json - run Deploy-Roles.ps1 first).
   5. -RunNow: triggers the function once via the admin endpoint and shows the result from Dataverse.

.EXAMPLE
  .\scripts\Deploy-SmartSyncAzure.ps1 -SubscriptionId 04c66457-d61f-43e6-9752-c6b3e0e72434 -DataverseUrl https://org558bead0.crm4.dynamics.com -RunNow
#>
param(
    [Parameter(Mandatory)][string]$SubscriptionId,
    [Parameter(Mandatory)][string]$DataverseUrl,
    [string]$ResourceGroup = 'rg-mthcrm-smartsync',
    [string]$Location = 'swedencentral',
    [string]$SmartSource = 'file:testdata',
    [string]$TenantId = 'organizations',
    [switch]$SkipCode,
    [switch]$RunNow
)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$repo = Split-Path $PSScriptRoot -Parent
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force

# ---------- Azure login (device code, refresh token reused for ARM + Graph) ----------
$azClient = '1950a258-227b-4e31-a9cf-717495945fc2'   # Microsoft "Azure PowerShell" public client (supports device code)
$authority = "https://login.microsoftonline.com/$TenantId/oauth2/v2.0"
$cacheFile = Join-Path $env:LOCALAPPDATA 'mth-crm\az-refresh-token.txt'
$tokens = @{}
function Get-AzToken([string]$Resource) {
    if ($tokens[$Resource] -and $tokens[$Resource].expires -gt (Get-Date)) { return $tokens[$Resource].token }
    $scope = "$Resource/.default offline_access"
    $resp = $null
    if (Test-Path $cacheFile) {
        try { $resp = Invoke-RestMethod -Method Post -Uri "$authority/token" -Body @{ grant_type = 'refresh_token'; client_id = $azClient; refresh_token = (Get-Content $cacheFile -Raw).Trim(); scope = $scope } } catch { $resp = $null }
    }
    if (-not $resp) {
        $dc = Invoke-RestMethod -Method Post -Uri "$authority/devicecode" -Body @{ client_id = $azClient; scope = $scope }
        Write-Host $dc.message -ForegroundColor Yellow
        while (-not $resp) {
            Start-Sleep -Seconds ([int]$dc.interval)
            try { $resp = Invoke-RestMethod -Method Post -Uri "$authority/token" -Body @{ grant_type = 'urn:ietf:params:oauth:grant-type:device_code'; client_id = $azClient; device_code = $dc.device_code } }
            catch { $e = $null; try { $e = ($_.ErrorDetails.Message | ConvertFrom-Json).error } catch {}; if ($e -ne 'authorization_pending' -and $e -ne 'slow_down') { throw } }
        }
    }
    New-Item -ItemType Directory -Force (Split-Path $cacheFile) | Out-Null
    Set-Content $cacheFile $resp.refresh_token -Encoding ASCII
    $tokens[$Resource] = @{ token = $resp.access_token; expires = (Get-Date).AddSeconds([int]$resp.expires_in - 120) }
    $resp.access_token
}
function Invoke-Arm([string]$Method, [string]$Path, $Body, [string]$Resource = 'https://management.azure.com') {
    $uri = if ($Path -match '^https://') { $Path } else { "https://management.azure.com$Path" }
    $p = @{ Method = $Method; Uri = $uri; Headers = @{ Authorization = "Bearer $(Get-AzToken $Resource)" }; ContentType = 'application/json'; UseBasicParsing = $true }
    if ($null -ne $Body) { $p.Body = [Text.Encoding]::UTF8.GetBytes($(if ($Body -is [string]) { $Body } else { $Body | ConvertTo-Json -Depth 30 -Compress })) }
    try { $r = Invoke-WebRequest @p } catch { $m = $_.ErrorDetails.Message; if (-not $m) { $m = $_.Exception.Message }; throw "$Method $uri failed: $m" }
    if ($r.Content) { [Text.Encoding]::UTF8.GetString($r.RawContentStream.ToArray()) | ConvertFrom-Json }
}

$sub = "/subscriptions/$SubscriptionId"
Write-Host "Subscription: $((Invoke-Arm GET "$sub`?api-version=2022-12-01").displayName)"

# ---------- 2. Infrastructure ----------
foreach ($ns in 'Microsoft.Web', 'Microsoft.Storage', 'Microsoft.Insights', 'Microsoft.OperationalInsights') {
    $state = (Invoke-Arm GET "$sub/providers/$ns`?api-version=2021-04-01").registrationState
    if ($state -ne 'Registered') {
        Write-Host "  registering provider $ns ..."
        [void](Invoke-Arm POST "$sub/providers/$ns/register?api-version=2021-04-01" $null)
        do { Start-Sleep 10; $state = (Invoke-Arm GET "$sub/providers/$ns`?api-version=2021-04-01").registrationState } while ($state -ne 'Registered')
    }
}
$rg = "$sub/resourcegroups/$ResourceGroup"
[void](Invoke-Arm PUT "$rg`?api-version=2021-04-01" @{ location = $Location; tags = @{ project = 'mth-crm'; component = 'smart-sync' } })
Write-Host "Resource group $ResourceGroup ($Location)"

$template = Get-Content (Join-Path $repo 'infra\smartsync.json') -Raw -Encoding UTF8
$params = @{ dataverseUrl = @{ value = $DataverseUrl.TrimEnd('/') }; smartSource = @{ value = $SmartSource } } | ConvertTo-Json -Depth 5 -Compress
$depName = 'smartsync-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
$dep = "$rg/providers/Microsoft.Resources/deployments/$depName`?api-version=2021-04-01"
[void](Invoke-Arm PUT $dep "{`"properties`":{`"mode`":`"Incremental`",`"template`":$template,`"parameters`":$params}}")
Write-Host "Deploying template ($depName) ..." -NoNewline
do { Start-Sleep 15; Write-Host '.' -NoNewline; $d = Invoke-Arm GET $dep } while ($d.properties.provisioningState -notin 'Succeeded', 'Failed', 'Canceled')
Write-Host " $($d.properties.provisioningState)"
if ($d.properties.provisioningState -ne 'Succeeded') {
    $ops = Invoke-Arm GET "$rg/providers/Microsoft.Resources/deployments/$depName/operations?api-version=2021-04-01"
    $ops.value | Where-Object { $_.properties.provisioningState -eq 'Failed' } | ForEach-Object { Write-Host "  $($_.properties.targetResource.resourceType): $($_.properties.statusMessage | ConvertTo-Json -Depth 10 -Compress)" -ForegroundColor Red }
    throw 'Template deployment failed.'
}
$appName = $d.properties.outputs.functionAppName.value
$principalId = $d.properties.outputs.principalId.value
$site = Invoke-Arm GET "$rg/providers/Microsoft.Web/sites/$appName`?api-version=2024-04-01"
$hostName = $site.properties.defaultHostName
$scmHost = ($site.properties.enabledHostNames | Where-Object { $_ -match '\.scm\.' } | Select-Object -First 1)
if (-not $scmHost) { $scmHost = $hostName -replace '^([^.]+)\.', '$1.scm.' }
Write-Host "Function App: $appName ($hostName), managed identity $principalId"

# ---------- 3. Code ----------
if (-not $SkipCode) {
    $pub = Join-Path $env:TEMP 'mth-crm-smartsync-publish'
    $zip = "$pub.zip"
    Remove-Item $pub, $zip -Recurse -Force -ErrorAction SilentlyContinue
    & dotnet publish (Join-Path $repo 'functions\SmartSync\SmartSync.Functions') -c Release -o $pub --nologo -v q
    if ($LASTEXITCODE) { throw 'dotnet publish failed' }
    # Built by hand: in Windows PowerShell 5.1 both Compress-Archive and ZipFile.CreateFromDirectory write entry names with
    # backslashes, so the Linux host does not see the required ".azurefunctions" folder. Entries must use "/".
    Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
    try {
        $root = (Get-Item $pub).FullName.TrimEnd('\')   # long path form (TEMP may be an 8.3 short path)
        foreach ($file in Get-ChildItem $root -Recurse -File -Force) {
            $entry = $file.FullName.Substring($root.Length + 1).Replace('\', '/')
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry)
        }
    } finally { $archive.Dispose() }
    Write-Host "Uploading $([math]::Round((Get-Item $zip).Length / 1MB, 1)) MB to $scmHost ..."
    $h = @{ Authorization = "Bearer $(Get-AzToken 'https://management.azure.com')" }
    $prevId = $null; try { $prevId = (Invoke-RestMethod -Uri "https://$scmHost/api/deployments/latest" -Headers $h).id } catch {}
    $tries = 0
    while ($true) {
        try { [void](Invoke-WebRequest -Method Post -Uri "https://$scmHost/api/publish?RemoteBuild=false" -Headers $h -InFile $zip -ContentType 'application/zip' -UseBasicParsing -TimeoutSec 600); break }
        catch { if (++$tries -ge 6) { throw }; Write-Host "  publish not ready yet ($($_.Exception.Message)), retrying in 30 s"; Start-Sleep 30 }
    }
    # The publish call returns before the package is validated/activated: wait for the deployment result
    do { Start-Sleep 10; $last = Invoke-RestMethod -Uri "https://$scmHost/api/deployments/latest" -Headers @{ Authorization = "Bearer $(Get-AzToken 'https://management.azure.com')" } } while (-not $last.complete -or $last.id -eq $prevId)
    if ($last.status -ne 4) { throw "Code deployment failed: $($last.status_text)" }   # 4 = success, 3 = failed
    Write-Host '  code deployed'
}

# ---------- 4. Dataverse application user ----------
$appId = (Invoke-Arm GET "https://graph.microsoft.com/v1.0/servicePrincipals/$principalId`?`$select=appId" $null 'https://graph.microsoft.com').appId
Write-Host "Managed identity application id: $appId"
Connect-Dataverse -Url $DataverseUrl
$rootBu = (Invoke-Dataverse GET "businessunits?`$select=businessunitid&`$filter=_parentbusinessunitid_value eq null").value[0].businessunitid
$user = (Invoke-Dataverse GET "systemusers?`$select=systemuserid&`$filter=applicationid eq $appId").value
if ($user) { $userId = $user[0].systemuserid; Write-Host '  application user exists' }
else {
    $resp = Invoke-Dataverse POST 'systemusers' @{ applicationid = $appId; 'businessunitid@odata.bind' = "/businessunits($rootBu)" }
    $userId = [regex]::Match([string]$resp, '[0-9a-f-]{36}').Value
    Write-Host '  + application user created'
}
$have = (Invoke-Dataverse GET "systemusers($userId)/systemuserroles_association?`$select=name").value.name
foreach ($roleName in 'Basic User', 'MTH Integration') {
    if ($have -contains $roleName) { continue }
    $roleId = (Invoke-Dataverse GET "roles?`$select=roleid&`$filter=name eq '$roleName' and _businessunitid_value eq $rootBu").value[0].roleid
    if (-not $roleId) { throw "Role '$roleName' not found (run Deploy-Roles.ps1)" }
    [void](Invoke-Dataverse POST "systemusers($userId)/systemuserroles_association/`$ref" @{ '@odata.id' = "$($DataverseUrl.TrimEnd('/'))/api/data/v9.2/roles($roleId)" })
    Write-Host "  + role $roleName"
}

# ---------- 5. Optional test run ----------
if ($RunNow) {
    $before = (Invoke-Dataverse GET "mth_integrationstates?`$select=modifiedon&`$filter=mth_name eq 'SMART Projects'").value | Select-Object -First 1
    $key = (Invoke-Arm POST "$rg/providers/Microsoft.Web/sites/$appName/host/default/listKeys?api-version=2024-04-01" $null).masterKey
    $tries = 0
    while ($true) {
        try { [void](Invoke-WebRequest -Method Post -Uri "https://$hostName/admin/functions/SmartSync" -Headers @{ 'x-functions-key' = $key } -Body '{"input":""}' -ContentType 'application/json' -UseBasicParsing); break }
        catch { if (++$tries -ge 10) { throw }; Write-Host "  function host not ready ($($_.Exception.Message)), retrying in 20 s"; Start-Sleep 20 }
    }
    Write-Host 'Triggered SmartSync; waiting for the watermark to move ...' -NoNewline
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep 10; Write-Host '.' -NoNewline
        $after = (Invoke-Dataverse GET "mth_integrationstates?`$select=modifiedon,_modifiedby_value&`$filter=mth_name eq 'SMART Projects'").value | Select-Object -First 1
        if ($after -and (-not $before -or $after.modifiedon -ne $before.modifiedon)) {
            Write-Host " done (modified $($after.modifiedon), by $($after.'_modifiedby_value@OData.Community.Display.V1.FormattedValue'))"; break
        }
    }
    if ($i -ge 30) { Write-Host ' no change within 5 minutes - check Application Insights / Log stream in the portal.' -ForegroundColor Yellow }
}
Write-Host 'Done.'
