<#
.SYNOPSIS
  Creates/updates web resources from the webresources/ folder in solution MTHCRM and publishes them.
  The web resource name is the path below webresources/ with forward slashes,
  e.g. webresources/mth_/scripts/opportunity.js -> mth_/scripts/opportunity.js

.EXAMPLE
  .\scripts\Deploy-WebResources.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$Solution = 'MTHCRM'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$root = Join-Path (Split-Path $PSScriptRoot -Parent) 'webresources'
$types = @{ '.htm' = 1; '.html' = 1; '.css' = 2; '.js' = 3; '.xml' = 4; '.png' = 5; '.jpg' = 6; '.gif' = 7; '.xsl' = 9; '.ico' = 10; '.svg' = 11; '.resx' = 12 }
$ids = @()
foreach ($f in Get-ChildItem $root -Recurse -File) {
    $name = $f.FullName.Substring($root.Length + 1).Replace('\', '/')
    $type = $types[$f.Extension.ToLower()]
    if (-not $type) { Write-Warning "Skipping $name (unknown type)"; continue }
    $content = [Convert]::ToBase64String([IO.File]::ReadAllBytes($f.FullName))
    $existing = (Invoke-Dataverse GET "webresourceset?`$select=webresourceid&`$filter=name eq '$name'").value
    if ($existing) {
        $id = $existing[0].webresourceid
        [void](Invoke-Dataverse PATCH "webresourceset($id)" @{ content = $content } -Solution $Solution)
        Write-Host "  ~ $name"
    } else {
        $resp = Invoke-Dataverse POST 'webresourceset' @{ name = $name; displayname = $name; webresourcetype = $type; content = $content } -Solution $Solution
        $id = [regex]::Match([string]$resp, '[0-9a-f]{8}-[0-9a-f-]{27}').Value
        Write-Host "  + $name"
    }
    $ids += $id
}
if ($ids.Count) {
    $xml = '<importexportxml><webresources>' + (($ids | ForEach-Object { "<webresource>{$_}</webresource>" }) -join '') + '</webresources></importexportxml>'
    [void](Invoke-Dataverse POST 'PublishXml' @{ ParameterXml = $xml })
    Write-Host "Published $($ids.Count) web resource(s)."
}
