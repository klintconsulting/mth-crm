<#
.SYNOPSIS
  NNE (Navne & Numre Erhverv, D&B/Bisnode) setup:
   1. Environment variable definitions in the solution: mth_NneEndpoint (default https://nne-api.bisnode.com/nne-ws/3.1/NNE)
      and mth_NneAccessKey (no default).
   2. The access key VALUE for this environment, created WITHOUT the solution header, so it is never exported to
      the solution or the repo. Read from -AccessKey or from %LOCALAPPDATA%\mth-crm\nne-accesskey.txt.
  Run before Deploy-Plugins.ps1 is needed by users (the Custom APIs mth_NneSearch / mth_NneImportAccount read the variables).

.EXAMPLE
  .\scripts\Deploy-Nne.ps1 -Url https://orgxxxx.crm4.dynamics.com
  .\scripts\Deploy-Nne.ps1 -Url https://orgxxxx.crm4.dynamics.com -AccessKey <key>
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$AccessKey,
    [string]$Solution = 'MTHCRM'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url

$keyFile = Join-Path $env:LOCALAPPDATA 'mth-crm\nne-accesskey.txt'
if (-not $AccessKey -and (Test-Path $keyFile)) { $AccessKey = (Get-Content $keyFile -Raw).Trim() }

$defs = @(
    @{ schemaname = 'mth_NneEndpoint'; displayname = 'NNE Endpoint'; defaultvalue = 'https://nne-api.bisnode.com/nne-ws/3.1/NNE'
       description = 'NNE SOAP 3.1 endpoint (D&B/Bisnode). The old address service.nnerhverv.dk stopped 2026-11-30.' },
    @{ schemaname = 'mth_NneAccessKey'; displayname = 'NNE Access Key'; defaultvalue = $null
       description = 'MTH access key for NNE. The value is set per environment by scripts/Deploy-Nne.ps1 and is not part of the solution.' }
)
foreach ($d in $defs) {
    $ex = (Invoke-Dataverse GET "environmentvariabledefinitions?`$select=environmentvariabledefinitionid&`$filter=schemaname eq '$($d.schemaname)'").value
    $body = @{ displayname = $d.displayname; description = $d.description; defaultvalue = $d.defaultvalue }
    if ($ex) { $id = $ex[0].environmentvariabledefinitionid; [void](Invoke-Dataverse PATCH "environmentvariabledefinitions($id)" $body -Solution $Solution); Write-Host "  ~ $($d.schemaname)" }
    else {
        $body.schemaname = $d.schemaname; $body.type = 100000000   # String
        $resp = Invoke-Dataverse POST 'environmentvariabledefinitions' $body -Solution $Solution
        $id = [regex]::Match([string]$resp, '[0-9a-f-]{36}').Value
        Write-Host "  + $($d.schemaname)"
    }
    $d.id = $id
}

$keyDef = $defs[1].id
if ($AccessKey) {
    $val = (Invoke-Dataverse GET "environmentvariablevalues?`$select=environmentvariablevalueid&`$filter=_environmentvariabledefinitionid_value eq $keyDef").value
    if ($val) { [void](Invoke-Dataverse PATCH "environmentvariablevalues($($val[0].environmentvariablevalueid))" @{ value = $AccessKey }); Write-Host '  ~ access key value updated' }
    else {
        # No -Solution: the value must stay out of the solution (and therefore out of git)
        [void](Invoke-Dataverse POST 'environmentvariablevalues' @{ schemaname = 'mth_NneAccessKey_value'; value = $AccessKey
            'EnvironmentVariableDefinitionId@odata.bind' = "/environmentvariabledefinitions($keyDef)" })
        Write-Host '  + access key value set'
    }
} else {
    Write-Warning "No access key: put it in $keyFile or pass -AccessKey. NNE search will fail until it is set."
}
Write-Host 'Done.'
