<#
.SYNOPSIS
  Creates/updates the model-driven app "MTH CRM" from scripts/app/app.json:
  views (savedquery), main forms (systemform), sitemap and the app module.
  Re-runnable: existing views/forms/sitemap (matched by name) are updated, not duplicated.

.EXAMPLE
  .\scripts\Deploy-App.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$AppFile = (Join-Path $PSScriptRoot 'app\app.json')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$def = Get-Content $AppFile -Raw -Encoding UTF8 | ConvertFrom-Json
$sol = $def.solution
$lang = 1033

# Default model-driven app icon (system web resource)
$DefaultAppIcon = '953b9fac-1e5e-e611-80d6-00155ded156f'

$ClassId = @{
    String = '{4273EDBD-AC1D-40d3-9FB2-095C621B552D}'; Memo = '{E0DECE4B-6FC8-4a8f-A065-082708572369}'
    DateTime = '{5B773807-9FB2-42db-97C3-7A91EFF8ADFF}'; Money = '{533B9E00-756B-4312-95A0-DC888637AC78}'
    Integer = '{C6D124CA-7EDA-4a60-AEA9-7FB8D318B68F}'; Decimal = '{C3EFE0C3-0EC6-42be-8349-CBD9079DFD8E}'
    Picklist = '{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}'; State = '{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}'; Status = '{3EF39988-22BB-4f0b-BBBE-64B5A3748AEE}'
    Boolean = '{67FAC785-CD58-4f9f-ABB3-4B7DDC6ED5ED}'
    Lookup = '{270BD3DB-D9AF-4782-9025-509E298DEC0A}'; Customer = '{270BD3DB-D9AF-4782-9025-509E298DEC0A}'; Owner = '{270BD3DB-D9AF-4782-9025-509E298DEC0A}'
    MultiSelect = '{4AA28AB7-9C13-4F57-A73D-AD894D048B5F}'; Subgrid = '{E7A81278-8635-4d9e-8D4D-59480B391C5B}'
}

function X([string]$s) { [Security.SecurityElement]::Escape($s) }
function G { '{' + [guid]::NewGuid().ToString() + '}' }
function Lbl([string]$text) { "<labels><label description=`"$(X $text)`" languagecode=`"$lang`" /></labels>" }
function Get-Id($resp, [string]$prop) {
    if ($resp -is [string]) { if ($resp -match '\(([0-9a-f-]{36})\)') { return $Matches[1] } }
    $resp.$prop
}

# ---- Table metadata cache ----
$meta = @{}
function Get-TableMeta([string]$table) {
    if (-not $meta[$table]) {
        $e = Invoke-Dataverse GET "EntityDefinitions(LogicalName='$table')?`$select=MetadataId,ObjectTypeCode,PrimaryIdAttribute,PrimaryNameAttribute&`$expand=Attributes(`$select=LogicalName,AttributeType,AttributeTypeName,DisplayName)"
        $attrs = @{}
        foreach ($a in $e.Attributes) { $attrs[$a.LogicalName] = $a }
        $meta[$table] = [pscustomobject]@{ MetadataId = $e.MetadataId; Otc = $e.ObjectTypeCode; IdAttr = $e.PrimaryIdAttribute; NameAttr = $e.PrimaryNameAttribute; Attrs = $attrs }
    }
    $meta[$table]
}
function Get-Attr([string]$table, [string]$field) {
    $a = (Get-TableMeta $table).Attrs[$field]
    if (-not $a) { throw "Column $table.$field does not exist" }
    $a
}
function Get-ControlClass($a) {
    if ($a.AttributeTypeName.Value -eq 'MultiSelectPicklistType') { return $ClassId.MultiSelect }
    $c = $ClassId[[string]$a.AttributeType]
    if (-not $c) { throw "No control mapping for $($a.LogicalName) ($($a.AttributeType))" }
    $c
}

# ---- 1. Views ----
$viewIds = @{}
foreach ($v in $def.views) {
    $m = Get-TableMeta $v.table
    $attrs = @($m.IdAttr) + @($v.columns | ForEach-Object { $_[0] }) | Select-Object -Unique
    foreach ($c in $v.columns) { [void](Get-Attr $v.table $c[0]) }
    $fetch = "<fetch version=`"1.0`" mapping=`"logical`"><entity name=`"$($v.table)`">" +
        (($attrs | ForEach-Object { "<attribute name=`"$_`" />" }) -join '') +
        "<order attribute=`"$($v.order.attribute)`" descending=`"$($v.order.descending.ToString().ToLower())`" />" +
        $(if ($v.activeOnly) { '<filter type="and"><condition attribute="statecode" operator="eq" value="0" /></filter>' } else { '' }) +
        '</entity></fetch>'
    $layout = "<grid name=`"resultset`" object=`"$($m.Otc)`" jump=`"$($m.NameAttr)`" select=`"1`" icon=`"1`" preview=`"1`"><row name=`"result`" id=`"$($m.IdAttr)`">" +
        (($v.columns | ForEach-Object { "<cell name=`"$($_[0])`" width=`"$($_[1])`" />" }) -join '') + '</row></grid>'
    $existing = (Invoke-Dataverse GET "savedqueries?`$select=savedqueryid&`$filter=name eq '$($v.name)' and returnedtypecode eq '$($v.table)'").value
    $body = @{ name = $v.name; returnedtypecode = $v.table; querytype = 0; fetchxml = $fetch; layoutxml = $layout; description = 'Defined in scripts/app/app.json' }
    if ($existing) {
        $id = $existing[0].savedqueryid
        [void](Invoke-Dataverse PATCH "savedqueries($id)" $body -Solution $sol)
        Write-Host "  ~ view $($v.table): $($v.name)"
    } else {
        $id = Get-Id (Invoke-Dataverse POST 'savedqueries' $body -Solution $sol) 'savedqueryid'
        Write-Host "  + view $($v.table): $($v.name)"
    }
    if ($v.default) { [void](Invoke-Dataverse PATCH "savedqueries($id)" @{ isdefault = $true } -Solution $sol) }
    $viewIds["$($v.table)|$($v.name)"] = $id
}

# ---- 2. Forms ----
function New-SectionXml($table, $s) {
    $rows = New-Object Text.StringBuilder
    if ($s.subgrid) {
        $sg = $s.subgrid
        $vid = $viewIds["$($sg.table)|$($sg.view)"]
        if (-not $vid) { throw "Subgrid view '$($sg.view)' for $($sg.table) is not defined in app.json" }
        $span = 8
        [void]$rows.Append("<row><cell id=`"$(G)`" showlabel=`"false`" rowspan=`"$span`" colspan=`"1`" auto=`"false`">$(Lbl $s.label)" +
            "<control id=`"$($sg.id)`" classid=`"$($ClassId.Subgrid)`" indicationOfSubgrid=`"true`"><parameters>" +
            "<RecordsPerPage>10</RecordsPerPage><AutoExpand>Fixed</AutoExpand><EnableQuickFind>false</EnableQuickFind><EnableViewPicker>false</EnableViewPicker>" +
            "<EnableJumpBar>false</EnableJumpBar><ChartGridMode>Grid</ChartGridMode><VisualizationId /><IsUserView>false</IsUserView>" +
            "<ViewId>{$vid}</ViewId><TargetEntityType>$($sg.table)</TargetEntityType><ViewIds>{$vid}</ViewIds><EnableChartPicker>false</EnableChartPicker>" +
            "<RelationshipName>$($sg.relationship)</RelationshipName></parameters></control></cell></row>")
        for ($i = 1; $i -lt $span; $i++) { [void]$rows.Append('<row />') }
    } else {
        foreach ($f in $s.fields) {
            $a = Get-Attr $table $f
            $label = $a.DisplayName.UserLocalizedLabel.Label
            [void]$rows.Append("<row><cell id=`"$(G)`" showlabel=`"true`" locklevel=`"0`">$(Lbl $label)" +
                "<control id=`"$f`" classid=`"$(Get-ControlClass $a)`" datafieldname=`"$f`" disabled=`"false`" /></cell></row>")
        }
    }
    "<section name=`"$($s.name)`" id=`"$(G)`" IsUserDefined=`"1`" showlabel=`"true`" showbar=`"false`" locklevel=`"0`" columns=`"1`" labelwidth=`"115`" celllabelalignment=`"Left`" celllabelposition=`"Left`">" +
        "$(Lbl $s.label)<rows>$($rows.ToString())</rows></section>"
}
$formIds = @()
foreach ($f in $def.forms) {
    $tabs = foreach ($t in $f.tabs) {
        $width = [math]::Floor(100 / @($t.columns).Count)
        $cols = foreach ($col in $t.columns) {
            "<column width=`"$width%`"><sections>" + ((@($col) | ForEach-Object { New-SectionXml $f.table $_ }) -join '') + '</sections></column>'
        }
        "<tab name=`"$($t.name)`" id=`"$(G)`" IsUserDefined=`"1`" locklevel=`"0`" showlabel=`"true`" expanded=`"true`">$(Lbl $t.label)<columns>$($cols -join '')</columns></tab>"
    }
    $formXml = "<form><tabs>$($tabs -join '')</tabs></form>"
    $existing = (Invoke-Dataverse GET "systemforms?`$select=formid&`$filter=name eq '$($f.name)' and objecttypecode eq '$($f.table)' and type eq 2").value
    $body = @{ name = $f.name; objecttypecode = $f.table; type = 2; formxml = $formXml; description = 'Defined in scripts/app/app.json' }
    if ($existing) {
        $id = $existing[0].formid
        [void](Invoke-Dataverse PATCH "systemforms($id)" @{ formxml = $formXml } -Solution $sol)
        Write-Host "  ~ form $($f.table): $($f.name)"
    } else {
        $id = Get-Id (Invoke-Dataverse POST 'systemforms' $body -Solution $sol) 'formid'
        Write-Host "  + form $($f.table): $($f.name)"
    }
    $formIds += $id
}

# ---- 3. Sitemap ----
$areas = foreach ($ar in $def.sitemap) {
    $groups = foreach ($g in $ar.groups) {
        $subs = foreach ($t in $g.tables) { "<SubArea Id=`"mth_sub_$t`" Entity=`"$t`" />" }
        "<Group Id=`"$($g.id)`" IsProfile=`"false`"><Titles><Title LCID=`"$lang`" Title=`"$(X $g.title)`" /></Titles>$($subs -join '')</Group>"
    }
    "<Area Id=`"$($ar.id)`" ShowGroups=`"true`"><Titles><Title LCID=`"$lang`" Title=`"$(X $ar.title)`" /></Titles>$($groups -join '')</Area>"
}
$siteXml = "<SiteMap IntroducedVersion=`"7.0.0.0`">$($areas -join '')</SiteMap>"
$sm = (Invoke-Dataverse GET "sitemaps?`$select=sitemapid&`$filter=sitemapnameunique eq '$($def.app.uniqueName)'").value
if ($sm) {
    $siteMapId = $sm[0].sitemapid
    [void](Invoke-Dataverse PATCH "sitemaps($siteMapId)" @{ sitemapxml = $siteXml } -Solution $sol)
    Write-Host '  ~ sitemap'
} else {
    $siteMapId = Get-Id (Invoke-Dataverse POST 'sitemaps' @{ sitemapname = $def.app.name; sitemapnameunique = $def.app.uniqueName; sitemapxml = $siteXml; isappaware = $true } -Solution $sol) 'sitemapid'
    Write-Host '  + sitemap'
}

# ---- 4. App module ----
# A new app is unpublished until PublishAllXml runs, so look in the unpublished layer as well.
$am = (Invoke-Dataverse GET "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?`$select=appmoduleid,appmoduleidunique&`$filter=uniquename eq '$($def.app.uniqueName)'").value
if ($am) {
    $appId = $am[0].appmoduleid
    # PATCH on an unpublished app behaves like a create of the published row, so the app record itself is not updated.
    # Change the app name/description in make.powerapps.com if needed.
    Write-Host '  = app (exists)'
} else {
    $appId = Get-Id (Invoke-Dataverse POST 'appmodules' @{ name = $def.app.name; uniquename = $def.app.uniqueName; description = $def.app.description; webresourceid = $DefaultAppIcon; clienttype = 4 } -Solution $sol) 'appmoduleid'
    Write-Host '  + app'
}

# ---- 5. App components (only those not already in the app) ----
$have = @{}
$appUnique = (Invoke-Dataverse GET "appmodules/Microsoft.Dynamics.CRM.RetrieveUnpublishedMultiple()?`$select=appmoduleidunique&`$filter=appmoduleid eq $appId").value[0].appmoduleidunique
$comp = Invoke-Dataverse GET "appmodulecomponents?`$select=objectid&`$filter=_appmoduleidunique_value eq $appUnique"
foreach ($c in $comp.value) { $have[[string]$c.objectid] = $true }
$tables = @($def.sitemap.groups.tables) + @($def.forms.table) + @($def.views.table) + @('systemuser') | Select-Object -Unique
$components = @()
foreach ($t in $tables) { $mid = (Get-TableMeta $t).MetadataId; if (-not $have[$mid]) { $components += @{ '@odata.type' = 'Microsoft.Dynamics.CRM.entity'; entityid = $mid } } }
foreach ($id in $formIds) { if (-not $have[$id]) { $components += @{ '@odata.type' = 'Microsoft.Dynamics.CRM.systemform'; formid = $id } } }
foreach ($id in $viewIds.Values) { if (-not $have[$id]) { $components += @{ '@odata.type' = 'Microsoft.Dynamics.CRM.savedquery'; savedqueryid = $id } } }
if (-not $have[$siteMapId]) { $components += @{ '@odata.type' = 'Microsoft.Dynamics.CRM.sitemap'; sitemapid = $siteMapId } }
if ($components.Count) {
    [void](Invoke-Dataverse POST 'AddAppComponents' @{ AppId = $appId; Components = $components })
    Write-Host "  + $($components.Count) app components"
}

# ---- 6. Validate and publish ----
$val = Invoke-Dataverse GET "ValidateApp(AppModuleId=$appId)"
$issues = @($val.AppValidationResponse.ValidationIssueList)
Write-Host "Validation: $($val.AppValidationResponse.ValidationSuccess) ($($issues.Count) issues)"
foreach ($i in $issues) { Write-Host "  [$($i.ErrorType)] $($i.Message)" }
[void](Invoke-Dataverse POST 'PublishAllXml' @{})
Write-Host "Published. App id: $appId"
Write-Host "Open: $($Url.TrimEnd('/'))/main.aspx?appid=$appId"
