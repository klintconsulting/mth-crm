<#
.SYNOPSIS
  Opretter fiktive demobrugere i Entra ID (via Microsoft Graph), giver dem en Power Apps-licens
  og tilføjer dem til Dataverse-miljøet med Basic User + en MTH-rolle (via pac).
  KUN til udviklings- og demomiljøer i en konsulent-/test-tenant - aldrig i MTH's tenant.

  Startadgangskoder skrives til %LOCALAPPDATA%\mth-crm\demo-users.txt (ikke i repoet).
  Brugerne skal skifte adgangskode ved første login.

.EXAMPLE
  .\scripts\New-DemoUsers.ps1 -EnvironmentUrl https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$EnvironmentUrl,
    [string]$Role = 'MTH Sales',
    [string]$LicenseSku = 'POWERAPPS_DEV'
)
$ErrorActionPreference = 'Stop'

$users = @(
    @{ Alias = 'ah'; First = 'Anne'; Last = 'Hansen'; Title = 'Tilbudschef' }
    @{ Alias = 'ln'; First = 'Lars'; Last = 'Nielsen'; Title = 'Projektchef' }
    @{ Alias = 'ms'; First = 'Mette'; Last = 'Sørensen'; Title = 'Kalkulatør' }
)

# ---- Graph login (device code, Microsoft Graph Command Line Tools public client) ----
$clientId = '14d82eec-204b-4c2f-b7e8-296a70dab67e'
$scope = 'https://graph.microsoft.com/User.ReadWrite.All https://graph.microsoft.com/Organization.Read.All https://graph.microsoft.com/LicenseAssignment.ReadWrite.All'
$auth = 'https://login.microsoftonline.com/organizations/oauth2/v2.0'
$dc = Invoke-RestMethod -Method Post -Uri "$auth/devicecode" -Body @{ client_id = $clientId; scope = $scope }
Write-Host $dc.message
$token = $null
while (-not $token) {
    Start-Sleep -Seconds ([int]$dc.interval)
    try { $token = (Invoke-RestMethod -Method Post -Uri "$auth/token" -Body @{ grant_type = 'urn:ietf:params:oauth:grant-type:device_code'; client_id = $clientId; device_code = $dc.device_code }).access_token }
    catch { $e = ($_.ErrorDetails.Message | ConvertFrom-Json).error; if ($e -notin 'authorization_pending', 'slow_down') { throw } }
}
$h = @{ Authorization = "Bearer $token"; 'Content-Type' = 'application/json; charset=utf-8' }
function Graph([string]$method, [string]$path, $body) {
    $p = @{ Method = $method; Uri = "https://graph.microsoft.com/v1.0/$path"; Headers = $h }
    if ($body) { $p.Body = [Text.Encoding]::UTF8.GetBytes(($body | ConvertTo-Json -Depth 10)) }
    Invoke-RestMethod @p
}

$domain = ((Graph GET 'domains').value | Where-Object { $_.isDefault }).id
$sku = (Graph GET 'subscribedSkus').value | Where-Object { $_.skuPartNumber -eq $LicenseSku }
if (-not $sku) { throw "Licens $LicenseSku findes ikke i tenanten (tilmeld dig Power Apps Developer Plan først)." }
Write-Host "Domæne: $domain  Licens: $LicenseSku ($($sku.consumedUnits)/$($sku.prepaidUnits.enabled) brugt)"

$out = Join-Path $env:LOCALAPPDATA 'mth-crm\demo-users.txt'
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
$lines = @("Demobrugere oprettet $(Get-Date -Format 'yyyy-MM-dd HH:mm') i $domain (skift adgangskode ved første login)")

foreach ($u in $users) {
    $upn = "$($u.Alias)@$domain"
    $existing = $null
    try { $existing = Graph GET "users/$upn" } catch {}
    if ($existing) {
        Write-Host "  = $upn findes"
        $id = $existing.id
    } else {
        $pw = 'Mth-' + [guid]::NewGuid().ToString('N').Substring(0, 10) + '!'
        $created = Graph POST 'users' @{
            accountEnabled = $true; displayName = "$($u.First) $($u.Last)"; givenName = $u.First; surname = $u.Last
            jobTitle = $u.Title; mailNickname = $u.Alias; userPrincipalName = $upn; usageLocation = 'DK'
            passwordProfile = @{ password = $pw; forceChangePasswordNextSignIn = $true }
        }
        $id = $created.id
        $lines += "$upn  $pw"
        Write-Host "  + $upn"
    }
    $lic = (Graph GET "users/$id/licenseDetails").value | Where-Object { $_.skuId -eq $sku.skuId }
    if (-not $lic) {
        [void](Graph POST "users/$id/assignLicense" @{ addLicenses = @(@{ skuId = $sku.skuId; disabledPlans = @() }); removeLicenses = @() })
        Write-Host "    licens $LicenseSku tildelt"
    }
}
$lines | Set-Content $out -Encoding UTF8
Write-Host "Startadgangskoder: $out"

# ---- Tilføj til Dataverse-miljøet med roller (pac bruger den aktive pac-profil) ----
foreach ($u in $users) {
    $upn = "$($u.Alias)@$domain"
    foreach ($r in 'Basic User', $Role) {
        pac admin assign-user --environment $EnvironmentUrl --user $upn --role $r | Out-Null
        if ($LASTEXITCODE) { Write-Warning "pac admin assign-user fejlede for $upn / $r" } else { Write-Host "  $upn -> $r" }
    }
}
Write-Host 'Done.'
