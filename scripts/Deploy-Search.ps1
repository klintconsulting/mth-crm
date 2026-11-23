<#
.SYNOPSIS
  Search setup from scripts/search/search.json:
   1. Organisation settings: Dataverse search on (isexternalsearchindexenabled) and Dataverse search in the
      list search box "Filter by keyword" (tablescopeddvsearchinapps): matches words anywhere, tolerates typos, ranks results.
   2. Per table: the Quick Find view's find columns (also what Dataverse search indexes) and the table is added to the search index.
  The index is built in the background; new tables/columns can take up to ~1 hour before they are searchable.

.EXAMPLE
  .\scripts\Deploy-Search.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$ConfigFile = (Join-Path $PSScriptRoot 'search\search.json')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$def = Get-Content $ConfigFile -Raw -Encoding UTF8 | ConvertFrom-Json

# 1. Organisation settings
$orgId = (Invoke-Dataverse GET 'organizations?$select=organizationid').value[0].organizationid
$settings = @{}; foreach ($p in $def.organization.PSObject.Properties) { $settings[$p.Name] = $p.Value }
[void](Invoke-Dataverse PATCH "organizations($orgId)" $settings)
Write-Host "Organisation: $(($settings.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join ', ')"

# 2. Tables
$publish = @()
foreach ($t in $def.tables.PSObject.Properties) {
    $table = $t.Name; $cols = @($t.Value.findColumns)
    $meta = Invoke-Dataverse GET "EntityDefinitions(LogicalName='$table')?`$select=MetadataId,SyncToExternalSearchIndex,CanEnableSyncToExternalSearchIndex"
    if (-not $meta.SyncToExternalSearchIndex -and $meta.CanEnableSyncToExternalSearchIndex.Value) {
        # Metadata updates need the full definition: GET, change, PUT
        $full = Invoke-Dataverse GET "EntityDefinitions($($meta.MetadataId))"
        $full.SyncToExternalSearchIndex = $true
        [void](Invoke-Dataverse PUT "EntityDefinitions($($meta.MetadataId))" $full -Solution $def.solution -Headers @{ 'MSCRM.MergeLabels' = 'true' })
        Write-Host "  + $table added to the Dataverse search index"
    }

    $view = (Invoke-Dataverse GET "savedqueries?`$select=savedqueryid,name,fetchxml,layoutxml&`$filter=returnedtypecode eq '$table' and querytype eq 4 and isdefault eq true").value[0]
    if (-not $view) { throw "No Quick Find view for $table" }
    [xml]$x = $view.fetchxml
    $entity = $x.fetch.entity
    $old = $entity.SelectNodes('filter') | Where-Object { $_.isquickfindfields -eq '1' }
    foreach ($f in @($old)) { [void]$entity.RemoveChild($f) }
    $filter = $x.CreateElement('filter'); $filter.SetAttribute('type', 'or'); $filter.SetAttribute('isquickfindfields', '1')
    foreach ($c in $cols) {
        $cond = $x.CreateElement('condition'); $cond.SetAttribute('attribute', $c); $cond.SetAttribute('operator', 'like'); $cond.SetAttribute('value', '{0}')
        [void]$filter.AppendChild($cond)
    }
    [void]$entity.AppendChild($filter)
    # A fetchxml-only PATCH of a Quick Find view fails with 0x80040216 "An unexpected error occurred";
    # it succeeds when returnedtypecode, querytype and layoutxml are sent along with it.
    [void](Invoke-Dataverse PATCH "savedqueries($($view.savedqueryid))" @{
        fetchxml = $x.OuterXml; layoutxml = $view.layoutxml; returnedtypecode = $table; querytype = 4 } -Solution $def.solution)
    Write-Host "  ~ $table '$($view.name)': $($cols -join ', ')"
    $publish += $table
}
$xml = '<importexportxml><entities>' + (($publish | ForEach-Object { "<entity>$_</entity>" }) -join '') + '</entities></importexportxml>'
[void](Invoke-Dataverse POST 'PublishXml' @{ ParameterXml = $xml })
Write-Host 'Published. The search index updates in the background (up to ~1 hour for new tables/columns).'
