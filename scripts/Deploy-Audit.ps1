<#
.SYNOPSIS
  Auditing (field history, like Salesforce Field History Tracking):
   1. Organisation: auditing on (isauditenabled) and user access auditing off.
   2. Per table in -Tables: IsAuditEnabled on the table (all columns are audited by default).
  The history is shown on each record under Related -> Audit History. Audit data counts towards Dataverse log
  capacity; set a retention period in Power Platform admin center (Environment -> Settings -> Audit settings).

.EXAMPLE
  .\scripts\Deploy-Audit.ps1 -Url https://orgxxxx.crm4.dynamics.com
  .\scripts\Deploy-Audit.ps1 -Url https://orgxxxx.crm4.dynamics.com -Tables mth_opportunity,mth_lead,account
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string[]]$Tables = @('mth_opportunity', 'mth_lead'),
    [string]$Solution = 'MTHCRM'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url

$orgId = (Invoke-Dataverse GET 'organizations?$select=organizationid').value[0].organizationid
[void](Invoke-Dataverse PATCH "organizations($orgId)" @{ isauditenabled = $true; isuseraccessauditenabled = $false })
Write-Host 'Organisation: auditing on'

foreach ($t in $Tables) {
    $meta = Invoke-Dataverse GET "EntityDefinitions(LogicalName='$t')?`$select=MetadataId,IsAuditEnabled"
    if ($meta.IsAuditEnabled.Value) { Write-Host "  = $t"; continue }
    # Metadata updates need the full definition: GET, change, PUT
    $full = Invoke-Dataverse GET "EntityDefinitions($($meta.MetadataId))"
    $full.IsAuditEnabled.Value = $true
    [void](Invoke-Dataverse PUT "EntityDefinitions($($meta.MetadataId))" $full -Solution $Solution -Headers @{ 'MSCRM.MergeLabels' = 'true' })
    Write-Host "  + $t audited"
}
$xml = '<importexportxml><entities>' + (($Tables | ForEach-Object { "<entity>$_</entity>" }) -join '') + '</entities></importexportxml>'
[void](Invoke-Dataverse POST 'PublishXml' @{ ParameterXml = $xml })
Write-Host 'Published.'
