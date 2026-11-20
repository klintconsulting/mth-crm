<#
.SYNOPSIS
  MTH branding (colours and logo from the Salesforce theme "MTH", branding set LEXTHEMINGMTH).
   1. Theme record "MTH" (classic theme table): header/navigation colours, link colours and the logo in the app header.
      Created/updated and published as the organisation's default theme. Theme records are not solution-aware, so this script is the source.
   2. App setting CustomThemeDefinition = mth_/branding/theme.xml (modern app header colours) on the app.
   3. App icon = mth_/branding/icon.svg.
  Run Deploy-WebResources.ps1 first (the files live in webresources/mth_/branding/).

.EXAMPLE
  .\scripts\Deploy-WebResources.ps1 -Url https://orgxxxx.crm4.dynamics.com
  .\scripts\Deploy-Branding.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$App = 'mth_MTHCRM',
    [string]$Solution = 'MTHCRM'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url

function Get-WebResourceId([string]$name) {
    $w = (Invoke-Dataverse GET "webresourceset?`$select=webresourceid&`$filter=name eq '$name'").value
    if (-not $w) { throw "Web resource $name not found (run Deploy-WebResources.ps1)" }
    $w[0].webresourceid
}

# MTH colours: brand #002F67 (dark blue), page background #2C365A (blue-grey), light header #EEEEEE
$brand = '#002F67'; $slate = '#2C365A'
$theme = @{
    name                       = 'MTH'
    navbarbackgroundcolor      = $brand
    navbarshelfcolor           = '#E6EBF2'
    headercolor                = $brand
    globallinkcolor            = $brand
    selectedlinkeffect         = '#C9D6EA'
    hoverlinkeffect            = '#E6EBF2'
    processcontrolcolor        = $brand
    defaultentitycolor         = $slate
    defaultcustomentitycolor   = $slate
    controlshade               = '#F3F6FA'
    controlborder              = '#B6C2D3'
    backgroundcolor            = '#FFFFFF'
    maincolor                  = $brand
    accentcolor                = $slate
    pageheaderbackgroundcolor  = '#EEEEEE'
    panelheaderbackgroundcolor = '#E6EBF2'
    logotooltip                = 'MT Højgaard'
    'logoimage@odata.bind'        = "/webresourceset($(Get-WebResourceId 'mth_/branding/logo-white.png'))"
}
$existing = (Invoke-Dataverse GET "themes?`$select=themeid&`$filter=name eq 'MTH'").value
if ($existing) { $themeId = $existing[0].themeid; [void](Invoke-Dataverse PATCH "themes($themeId)" $theme); Write-Host '  ~ theme MTH' }
else { $resp = Invoke-Dataverse POST 'themes' $theme; $themeId = [regex]::Match([string]$resp, '[0-9a-f-]{36}').Value; Write-Host '  + theme MTH' }
[void](Invoke-Dataverse POST "themes($themeId)/Microsoft.Dynamics.CRM.PublishTheme" @{})
Write-Host '    published as default theme'

# Modern header colours + app icon
$appId = (Invoke-Dataverse GET "appmodules?`$select=appmoduleid&`$filter=uniquename eq '$App'").value[0].appmoduleid
if (-not $appId) { throw "App $App not found" }
$sd = (Invoke-Dataverse GET "settingdefinitions?`$select=settingdefinitionid&`$filter=uniquename eq 'CustomThemeDefinition'").value[0].settingdefinitionid
$value = 'mth_/branding/theme.xml'
$s = (Invoke-Dataverse GET "appsettings?`$select=appsettingid&`$filter=_parentappmoduleid_value eq $appId and _settingdefinitionid_value eq $sd").value
if ($s) { [void](Invoke-Dataverse PATCH "appsettings($($s[0].appsettingid))" @{ value = $value } -Solution $Solution); Write-Host "  ~ app setting CustomThemeDefinition = $value" }
else {
    [void](Invoke-Dataverse POST 'appsettings' @{ uniquename = "mth_MTHCRM_CustomThemeDefinition"; value = $value
        'settingdefinitionid@odata.bind' = "/settingdefinitions($sd)"; 'parentappmoduleid@odata.bind' = "/appmodules($appId)" } -Solution $Solution)
    Write-Host "  + app setting CustomThemeDefinition = $value"
}
[void](Invoke-Dataverse PATCH "appmodules($appId)" @{ 'webresourceid' = (Get-WebResourceId 'mth_/branding/icon.svg') } -Solution $Solution)
Write-Host '  ~ app icon mth_/branding/icon.svg'
[void](Invoke-Dataverse POST 'PublishXml' @{ ParameterXml = "<importexportxml><appmodules><appmodule>$appId</appmodule></appmodules></importexportxml>" })
Write-Host 'Done. Reload the app (Ctrl+F5) to see the new header.'
