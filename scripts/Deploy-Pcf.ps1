<#
.SYNOPSIS
  Builds, tests and deploys the PCF controls in pcf/ and adds them to the MTHCRM solution.
  Uses the active pac auth profile (pac org select) for the push and Web API for AddSolutionComponent.

.EXAMPLE
  .\scripts\Deploy-Pcf.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string[]]$Controls = @('MentionComposer', 'TenderWorkspace'),
    [string]$Solution = 'MTHCRM',
    [string]$PublisherPrefix = 'mth'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$root = Split-Path $PSScriptRoot -Parent

foreach ($name in $Controls) {
    $dir = Join-Path $root "pcf\$name"
    Push-Location $dir
    try {
        if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE) { throw 'npm ci failed' } }
        npm test; if ($LASTEXITCODE) { throw "Unit tests failed for $name" }
        npm run build; if ($LASTEXITCODE) { throw "Build failed for $name" }
        # pac pcf push imports the control via a temporary solution "PowerAppsTools_<prefix>"
        pac pcf push --publisher-prefix $PublisherPrefix; if ($LASTEXITCODE) { throw "pac pcf push failed for $name" }
    } finally { Pop-Location }

    $cc = (Invoke-Dataverse GET "customcontrols?`$select=customcontrolid,name&`$filter=name eq '$($PublisherPrefix)_Mth.$name'").value
    if (-not $cc) { throw "Custom control $($PublisherPrefix)_Mth.$name not found after push" }
    [void](Invoke-Dataverse POST 'AddSolutionComponent' @{ ComponentId = $cc[0].customcontrolid; ComponentType = 66; SolutionUniqueName = $Solution; AddRequiredComponents = $false })
    Write-Host "  + $($cc[0].name) in solution $Solution"
}
Write-Host 'Done. Run Deploy-App.ps1 if the forms reference new controls, then pac solution sync.'
