<#
.SYNOPSIS
  Creates tables, columns and lookups in Dataverse from scripts/schema/schema.json.
  Idempotent: existing tables/columns/relationships are skipped (not modified).

.EXAMPLE
  .\scripts\Deploy-Schema.ps1 -Url https://orgxxxx.crm4.dynamics.com
  .\scripts\Deploy-Schema.ps1 -Url https://orgxxxx.crm4.dynamics.com -WhatIf
#>
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$SchemaFile = (Join-Path $PSScriptRoot 'schema\schema.json')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
$schema = Get-Content $SchemaFile -Raw -Encoding UTF8 | ConvertFrom-Json
$sol = $schema.solution
$lang = [int]$schema.languageCode
if (-not $WhatIfPreference) { Connect-Dataverse -Url $Url }

function Label([string]$text) {
    @{ '@odata.type' = 'Microsoft.Dynamics.CRM.Label'
       LocalizedLabels = @(@{ '@odata.type' = 'Microsoft.Dynamics.CRM.LocalizedLabel'; Label = $text; LanguageCode = $lang }) }
}
function Required([bool]$req) {
    @{ Value = $(if ($req) { 'ApplicationRequired' } else { 'None' }); CanBeChanged = $true; ManagedPropertyLogicalName = 'canmodifyrequirementlevelsettings' }
}
function Exists([string]$path) {
    try { [void](Invoke-Dataverse GET "$path`?`$select=MetadataId"); $true }
    catch { if ($_.Exception.Message -match '0x80060888|does not exist|Could not find|404') { $false } else { throw } }
}

function New-AttributeMetadata($c) {
    $desc = "Source: Salesforce $($c.source)"
    if ($c.note) { $desc += ". $($c.note)" }
    $m = [ordered]@{ SchemaName = $c.schemaName; DisplayName = (Label $c.displayName); Description = (Label $desc); RequiredLevel = (Required ([bool]$c.required)) }
    switch ($c.type) {
        'string' {
            $m['@odata.type'] = 'Microsoft.Dynamics.CRM.StringAttributeMetadata'; $m.AttributeType = 'String'; $m.AttributeTypeName = @{ Value = 'StringType' }
            $m.MaxLength = [int]$c.maxLength; $m.FormatName = @{ Value = $(if ($c.format) { $c.format } else { 'Text' }) }
        }
        'memo' {
            $m['@odata.type'] = 'Microsoft.Dynamics.CRM.MemoAttributeMetadata'; $m.AttributeType = 'Memo'; $m.AttributeTypeName = @{ Value = 'MemoType' }
            $m.Format = 'TextArea'; $m.MaxLength = [int]$c.maxLength
        }
        'date' {
            $m['@odata.type'] = 'Microsoft.Dynamics.CRM.DateTimeAttributeMetadata'; $m.AttributeType = 'DateTime'; $m.AttributeTypeName = @{ Value = 'DateTimeType' }
            $m.Format = 'DateOnly'; $m.DateTimeBehavior = @{ Value = 'DateOnly' }
        }
        'currency' {
            $m['@odata.type'] = 'Microsoft.Dynamics.CRM.MoneyAttributeMetadata'; $m.AttributeType = 'Money'; $m.AttributeTypeName = @{ Value = 'MoneyType' }
            $m.PrecisionSource = 2
        }
        'integer' {
            $m['@odata.type'] = 'Microsoft.Dynamics.CRM.IntegerAttributeMetadata'; $m.AttributeType = 'Integer'; $m.AttributeTypeName = @{ Value = 'IntegerType' }
            $m.Format = 'None'; $m.MinValue = [int]$c.min; $m.MaxValue = [int]$c.max
        }
        'boolean' {
            $m['@odata.type'] = 'Microsoft.Dynamics.CRM.BooleanAttributeMetadata'; $m.AttributeType = 'Boolean'; $m.AttributeTypeName = @{ Value = 'BooleanType' }
            $m.DefaultValue = $false
            $m.OptionSet = @{ '@odata.type' = 'Microsoft.Dynamics.CRM.BooleanOptionSetMetadata'; OptionSetType = 'Boolean'
                TrueOption = @{ Value = 1; Label = (Label 'Yes') }; FalseOption = @{ Value = 0; Label = (Label 'No') } }
        }
        { $_ -in 'choice', 'multichoice' } {
            if ($c.type -eq 'choice') {
                $m['@odata.type'] = 'Microsoft.Dynamics.CRM.PicklistAttributeMetadata'; $m.AttributeType = 'Picklist'; $m.AttributeTypeName = @{ Value = 'PicklistType' }
            } else {
                $m['@odata.type'] = 'Microsoft.Dynamics.CRM.MultiSelectPicklistAttributeMetadata'; $m.AttributeType = 'Virtual'; $m.AttributeTypeName = @{ Value = 'MultiSelectPicklistType' }
            }
            $m.OptionSet = @{ '@odata.type' = 'Microsoft.Dynamics.CRM.OptionSetMetadata'; IsGlobal = $false; OptionSetType = 'Picklist'
                Options = @($c.options | ForEach-Object { @{ Value = [int]$_.value; Label = (Label $_.label) } }) }
        }
        default { throw "Unknown column type '$($c.type)' for $($c.schemaName)" }
    }
    $m
}

function Get-Cascade([string]$kind) {
    switch ($kind) {
        'Parental'   { @{ Assign = 'Cascade'; Delete = 'Cascade'; Merge = 'Cascade'; Reparent = 'Cascade'; Share = 'Cascade'; Unshare = 'Cascade'; RollupView = 'NoCascade' } }
        'Restrict'   { @{ Assign = 'NoCascade'; Delete = 'Restrict'; Merge = 'NoCascade'; Reparent = 'NoCascade'; Share = 'NoCascade'; Unshare = 'NoCascade'; RollupView = 'NoCascade' } }
        'RemoveLink' { @{ Assign = 'NoCascade'; Delete = 'RemoveLink'; Merge = 'NoCascade'; Reparent = 'NoCascade'; Share = 'NoCascade'; Unshare = 'NoCascade'; RollupView = 'NoCascade' } }
        default { throw "Unknown cascade '$kind'" }
    }
}

$created = 0; $skipped = 0

# 1. Tables
foreach ($t in $schema.tables | Where-Object { -not $_.standard }) {
    $ln = $t.schemaName.ToLower()
    if (-not $WhatIfPreference -and (Exists "EntityDefinitions(LogicalName='$ln')")) { Write-Host "  = table $ln"; $skipped++; continue }
    if ($PSCmdlet.ShouldProcess($ln, 'Create table')) {
        $primary = [ordered]@{
            '@odata.type' = 'Microsoft.Dynamics.CRM.StringAttributeMetadata'; SchemaName = $t.primary.schemaName; IsPrimaryName = $true
            AttributeType = 'String'; AttributeTypeName = @{ Value = 'StringType' }; FormatName = @{ Value = 'Text' }; MaxLength = [int]$t.primary.maxLength
            DisplayName = (Label $t.primary.displayName); Description = (Label "Source: Salesforce $($t.primary.source)"); RequiredLevel = (Required $true)
        }
        $body = [ordered]@{
            '@odata.type' = 'Microsoft.Dynamics.CRM.EntityMetadata'; SchemaName = $t.schemaName
            DisplayName = (Label $t.displayName); DisplayCollectionName = (Label $t.pluralName)
            Description = (Label "$($t.description) Source: Salesforce $($t.source).")
            OwnershipType = $t.ownership; IsActivity = $false; HasNotes = $true; HasActivities = ([bool]$t.hasActivities -or $t.schemaName -eq 'mth_Opportunity')
            Attributes = @($primary)
        }
        [void](Invoke-Dataverse POST 'EntityDefinitions' $body -Solution $sol)
        Write-Host "  + table $ln"; $created++
    }
}

# 2. Columns
foreach ($t in $schema.tables) {
    $ln = $t.schemaName.ToLower()
    foreach ($c in $t.columns) {
        $cn = $c.schemaName.ToLower()
        if (-not $WhatIfPreference -and (Exists "EntityDefinitions(LogicalName='$ln')/Attributes(LogicalName='$cn')")) { Write-Host "  = $ln.$cn"; $skipped++; continue }
        if ($PSCmdlet.ShouldProcess("$ln.$cn", "Create $($c.type) column")) {
            [void](Invoke-Dataverse POST "EntityDefinitions(LogicalName='$ln')/Attributes" (New-AttributeMetadata $c) -Solution $sol)
            Write-Host "  + $ln.$cn ($($c.type))"; $created++
        }
    }
}

# 3. Lookups (one-to-many relationships)
foreach ($r in $schema.lookups) {
    if (-not $WhatIfPreference -and (Exists "RelationshipDefinitions(SchemaName='$($r.schemaName)')")) { Write-Host "  = relationship $($r.schemaName)"; $skipped++; continue }
    if ($PSCmdlet.ShouldProcess($r.schemaName, "Create lookup $($r.referencing).$($r.lookupSchemaName.ToLower()) -> $($r.referenced)")) {
        $body = [ordered]@{
            '@odata.type' = 'Microsoft.Dynamics.CRM.OneToManyRelationshipMetadata'; SchemaName = $r.schemaName
            ReferencedEntity = $r.referenced; ReferencingEntity = $r.referencing; CascadeConfiguration = (Get-Cascade $r.cascade)
            Lookup = [ordered]@{
                '@odata.type' = 'Microsoft.Dynamics.CRM.LookupAttributeMetadata'; AttributeType = 'Lookup'; AttributeTypeName = @{ Value = 'LookupType' }
                SchemaName = $r.lookupSchemaName; DisplayName = (Label $r.displayName); Description = (Label "Source: Salesforce $($r.source)")
                RequiredLevel = (Required ([bool]$r.required))
            }
        }
        [void](Invoke-Dataverse POST 'RelationshipDefinitions' $body -Solution $sol)
        Write-Host "  + relationship $($r.schemaName)"; $created++
    }
}

# 3b. Many-to-many relationships
foreach ($r in @($schema.manyToMany)) {
    if (-not $r) { continue }
    if (-not $WhatIfPreference -and (Exists "RelationshipDefinitions(SchemaName='$($r.schemaName)')")) { Write-Host "  = N:N $($r.schemaName)"; $skipped++; continue }
    if ($PSCmdlet.ShouldProcess($r.schemaName, "Create N:N $($r.entity1) <-> $($r.entity2)")) {
        $menu = { param($label) @{ Behavior = 'UseLabel'; Group = 'Details'; Order = 10000; Label = (Label $label) } }
        $body = [ordered]@{
            '@odata.type' = 'Microsoft.Dynamics.CRM.ManyToManyRelationshipMetadata'; SchemaName = $r.schemaName
            Entity1LogicalName = $r.entity1; Entity2LogicalName = $r.entity2; IntersectEntityName = $r.schemaName.ToLower()
            Entity1AssociatedMenuConfiguration = (& $menu $r.entity2Label)
            Entity2AssociatedMenuConfiguration = (& $menu $r.entity1Label)
        }
        [void](Invoke-Dataverse POST 'RelationshipDefinitions' $body -Solution $sol)
        Write-Host "  + N:N $($r.schemaName)"; $created++
    }
}

# 3c. Alternate keys (prevent duplicates, e.g. same partner twice on a building contract)
foreach ($k in @($schema.keys)) {
    if (-not $k) { continue }
    $ln = $k.table.ToLower()
    $existing = $null
    if (-not $WhatIfPreference) { $existing = (Invoke-Dataverse GET "EntityDefinitions(LogicalName='$ln')/Keys?`$select=SchemaName&`$filter=SchemaName eq '$($k.schemaName)'").value }
    if ($existing) { Write-Host "  = key $($k.schemaName)"; $skipped++; continue }
    if ($PSCmdlet.ShouldProcess("$ln.$($k.schemaName)", "Create alternate key ($($k.columns -join ', '))")) {
        [void](Invoke-Dataverse POST "EntityDefinitions(LogicalName='$ln')/Keys" @{
            SchemaName = $k.schemaName; DisplayName = (Label $k.displayName); KeyAttributes = @($k.columns) } -Solution $sol)
        Write-Host "  + key $($k.schemaName) ($($k.columns -join ', '))"; $created++
    }
}

# 4. Publish
if ($created -gt 0 -and $PSCmdlet.ShouldProcess('all customizations', 'Publish')) {
    [void](Invoke-Dataverse POST 'PublishAllXml' @{})
    Write-Host 'Published.'
}
Write-Host "Done. Created: $created, already existed: $skipped"
