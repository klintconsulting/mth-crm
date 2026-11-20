<#
.SYNOPSIS
  Creates/updates the MTH security roles from scripts/security/roles.json and gives them access to the app.
  Privileges are REPLACED on every run (ReplacePrivilegesRole), so roles.json is the single source of truth.

.EXAMPLE
  .\scripts\Deploy-Roles.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$RolesFile = (Join-Path $PSScriptRoot 'security\roles.json')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$def = Get-Content $RolesFile -Raw -Encoding UTF8 | ConvertFrom-Json
$base = $Url.TrimEnd('/')

$rootBu = (Invoke-Dataverse GET "businessunits?`$select=businessunitid&`$filter=_parentbusinessunitid_value eq null").value[0].businessunitid
$appId = (Invoke-Dataverse GET "appmodules?`$select=appmoduleid&`$filter=uniquename eq '$($def.app)'").value[0].appmoduleid
if (-not $appId) { throw "App $($def.app) not found (run Deploy-App.ps1 first)" }

# Privilege name -> id cache
$privCache = @{}
function Get-PrivilegeId([string]$op, [string]$table) {
    # Table "_global" holds privileges that belong to no table, e.g. "SendAppNotification" -> prvSendAppNotification
    $name = if ($table -eq '_global') { "prv$op" } else { "prv$op$table" }
    if (-not $privCache.ContainsKey($name)) {
        $p = (Invoke-Dataverse GET "privileges?`$select=privilegeid,name&`$filter=name eq '$name'").value
        if (-not $p) { throw "Privilege $name not found" }
        $privCache[$name] = $p[0].privilegeid
    }
    $privCache[$name]
}

foreach ($r in $def.roles) {
    $existing = (Invoke-Dataverse GET "roles?`$select=roleid&`$filter=name eq '$($r.name)' and _businessunitid_value eq $rootBu").value
    if ($existing) {
        $roleId = $existing[0].roleid
        [void](Invoke-Dataverse PATCH "roles($roleId)" @{ description = $r.description } -Solution $def.solution)
        Write-Host "  ~ role $($r.name)"
    } else {
        $resp = Invoke-Dataverse POST 'roles' @{ name = $r.name; description = $r.description; 'businessunitid@odata.bind' = "/businessunits($rootBu)" } -Solution $def.solution
        $roleId = [regex]::Match([string]$resp, '[0-9a-f-]{36}').Value
        Write-Host "  + role $($r.name)"
    }

    $privs = @()
    foreach ($table in $r.privileges.PSObject.Properties.Name) {
        $ops = $r.privileges.$table
        foreach ($op in $ops.PSObject.Properties.Name) {
            $depth = $ops.$op
            if ($depth -eq 'None') { continue }
            $privs += @{ PrivilegeId = (Get-PrivilegeId $op $table); Depth = $depth; BusinessUnitId = $rootBu }
        }
    }
    [void](Invoke-Dataverse POST "roles($roleId)/Microsoft.Dynamics.CRM.ReplacePrivilegesRole" @{ Privileges = $privs })
    Write-Host "    $($privs.Count) privileges set"

    # Give the role access to the app (ignore 'already associated')
    try {
        [void](Invoke-Dataverse POST "appmodules($appId)/appmoduleroles_association/`$ref" @{ '@odata.id' = "$base/api/data/v9.2/roles($roleId)" })
        Write-Host '    app access granted'
    } catch {
        if ($_.Exception.Message -match 'duplicate|already|0x80040237') { Write-Host '    app access already granted' } else { throw }
    }
}
[void](Invoke-Dataverse POST 'PublishAllXml' @{})
Write-Host 'Done.'
