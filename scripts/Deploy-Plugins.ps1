<#
.SYNOPSIS
  Builds the plugin assembly and registers/updates it, its plugin types and steps in Dataverse
  from scripts/plugins/steps.json. Also applies the app settings listed there.
  Re-runnable: the assembly content is updated, existing types/steps/settings are reused.

.EXAMPLE
  .\scripts\Deploy-Plugins.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$StepsFile = (Join-Path $PSScriptRoot 'plugins\steps.json'),
    [switch]$SkipBuild
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$def = Get-Content $StepsFile -Raw -Encoding UTF8 | ConvertFrom-Json
$root = Split-Path $PSScriptRoot -Parent
$sol = $def.solution
function IdOf($resp) { [regex]::Match([string]$resp, '[0-9a-f]{8}-[0-9a-f-]{27}').Value }

# 1. Build
if (-not $SkipBuild) {
    & dotnet build (Join-Path $root $def.assembly.project) -c Release --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
}
$dll = Join-Path $root $def.assembly.dll
$content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($dll))

# 2. Assembly
$asm = (Invoke-Dataverse GET "pluginassemblies?`$select=pluginassemblyid&`$filter=name eq '$($def.assembly.name)'").value
if ($asm) {
    $asmId = $asm[0].pluginassemblyid
    [void](Invoke-Dataverse PATCH "pluginassemblies($asmId)" @{ content = $content } -Solution $sol)
    Write-Host "  ~ assembly $($def.assembly.name) (content updated)"
} else {
    $asmId = IdOf (Invoke-Dataverse POST 'pluginassemblies' @{ name = $def.assembly.name; content = $content; isolationmode = 2; sourcetype = 0 } -Solution $sol)
    Write-Host "  + assembly $($def.assembly.name)"
}

# 3. Types and steps
$modeValue = @{ Synchronous = 0; Asynchronous = 1 }
foreach ($s in $def.steps) {
    $pt = (Invoke-Dataverse GET "plugintypes?`$select=plugintypeid&`$filter=typename eq '$($s.type)' and _pluginassemblyid_value eq $asmId").value
    if ($pt) { $typeId = $pt[0].plugintypeid; Write-Host "  = type $($s.type)" }
    else {
        $typeId = IdOf (Invoke-Dataverse POST 'plugintypes' @{ typename = $s.type; name = $s.type; friendlyname = $s.type; 'pluginassemblyid@odata.bind' = "/pluginassemblies($asmId)" })
        Write-Host "  + type $($s.type)"
    }
    $msgId = (Invoke-Dataverse GET "sdkmessages?`$select=sdkmessageid&`$filter=name eq '$($s.message)'").value[0].sdkmessageid
    $filterId = (Invoke-Dataverse GET "sdkmessagefilters?`$select=sdkmessagefilterid&`$filter=primaryobjecttypecode eq '$($s.table)' and _sdkmessageid_value eq $msgId").value[0].sdkmessagefilterid
    $body = @{
        name = $s.name; description = $s.description; stage = [int]$s.stage; mode = $modeValue[$s.mode]; rank = [int]$s.rank
        supporteddeployment = 0; asyncautodelete = ($s.mode -eq 'Asynchronous')
        'sdkmessageid@odata.bind' = "/sdkmessages($msgId)"; 'sdkmessagefilterid@odata.bind' = "/sdkmessagefilters($filterId)"
        'eventhandler_plugintype@odata.bind' = "/plugintypes($typeId)"
    }
    $st = (Invoke-Dataverse GET "sdkmessageprocessingsteps?`$select=sdkmessageprocessingstepid&`$filter=name eq '$($s.name)'").value
    if ($st) {
        [void](Invoke-Dataverse PATCH "sdkmessageprocessingsteps($($st[0].sdkmessageprocessingstepid))" $body -Solution $sol)
        Write-Host "  ~ step $($s.name)"
    } else {
        [void](Invoke-Dataverse POST 'sdkmessageprocessingsteps' $body -Solution $sol)
        Write-Host "  + step $($s.name)"
    }
}

# 4. App settings
foreach ($a in $def.appSettings) {
    $appId = (Invoke-Dataverse GET "appmodules?`$select=appmoduleid&`$filter=uniquename eq '$($a.app)'").value[0].appmoduleid
    $sd = (Invoke-Dataverse GET "settingdefinitions?`$select=settingdefinitionid&`$filter=uniquename eq '$($a.setting)'").value[0].settingdefinitionid
    $existing = (Invoke-Dataverse GET "appsettings?`$select=appsettingid&`$filter=_parentappmoduleid_value eq $appId and _settingdefinitionid_value eq $sd").value
    if ($existing) {
        [void](Invoke-Dataverse PATCH "appsettings($($existing[0].appsettingid))" @{ value = $a.value } -Solution $sol)
        Write-Host "  ~ app setting $($a.setting) = $($a.value)"
    } else {
        [void](Invoke-Dataverse POST 'appsettings' @{
            uniquename = "mth_$($a.app -replace '^mth_', '')_$($a.setting)"; value = $a.value
            'settingdefinitionid@odata.bind' = "/settingdefinitions($sd)"; 'parentappmoduleid@odata.bind' = "/appmodules($appId)" } -Solution $sol)
        Write-Host "  + app setting $($a.setting) = $($a.value)"
    }
}
[void](Invoke-Dataverse POST 'PublishAllXml' @{})
Write-Host 'Done.'
