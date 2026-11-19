<#
.SYNOPSIS
  Generates docs/system/datamodel.md from scripts/schema/schema.json.
  Run after every change to schema.json so documentation and schema stay in sync.
#>
param(
    [string]$SchemaFile = (Join-Path $PSScriptRoot 'schema\schema.json'),
    [string]$OutFile = (Join-Path $PSScriptRoot '..\docs\system\datamodel.md')
)
$schema = Get-Content $SchemaFile -Raw -Encoding UTF8 | ConvertFrom-Json
$sb = New-Object Text.StringBuilder
function W([string]$s = '') { [void]$sb.AppendLine($s) }

W '# Datamodel'
W ''
W '> Genereret automatisk fra `scripts/schema/schema.json` af `scripts/New-SchemaDoc.ps1`. Ret ikke i filen her. Ret i schema.json og kør scriptet igen.'
W ''
W "Solution: ``$($schema.solution)`` · publisher-præfiks: ``$($schema.publisherPrefix)`` · option value-præfiks: ``$($schema.optionValuePrefix)``"
W ''
W '## Tabeller'
W ''
W '| Tabel (logisk navn) | Visningsnavn | Type | Ejerskab | Kilde i Salesforce |'
W '|---|---|---|---|---|'
foreach ($t in $schema.tables) {
    $kind = if ($t.standard) { 'Standard' } else { 'Custom' }
    W "| ``$($t.schemaName.ToLower())`` | $($t.displayName) | $kind | $($t.ownership) | $($t.source) |"
}
W ''
W '## Relationer (opslag)'
W ''
W '| Relation | Opslagskolonne | Peger på | Sletning | Påkrævet | Kilde |'
W '|---|---|---|---|---|---|'
foreach ($r in $schema.lookups) {
    $req = if ($r.required) { 'Ja' } else { '' }
    W "| ``$($r.schemaName)`` | ``$($r.referencing).$($r.lookupSchemaName.ToLower())`` | ``$($r.referenced)`` | $($r.cascade) | $req | $($r.source) |"
}
foreach ($t in $schema.tables) {
    W ''
    W "## $($t.displayName) (``$($t.schemaName.ToLower())``)"
    W ''
    if ($t.description) { W $t.description; W '' }
    if ($t.standardMapping) {
        W 'Standardkolonner, der bruges:'
        W ''
        W '| Kolonne | Kilde i Salesforce |'
        W '|---|---|'
        foreach ($m in $t.standardMapping) { W "| ``$($m.column)`` | $($m.source) |" }
        W ''
    }
    if ($t.primary) { W "Primær navnekolonne: ``$($t.primary.schemaName.ToLower())`` (tekst, maks. $($t.primary.maxLength)). Kilde: $($t.primary.source)"; W '' }
    W '| Kolonne (logisk navn) | Visningsnavn | Type | Detaljer | Kilde i Salesforce |'
    W '|---|---|---|---|---|'
    foreach ($c in $t.columns) {
        $det = switch ($c.type) {
            'string' { "maks. $($c.maxLength)$(if ($c.format) { ", $($c.format)" })" }
            'memo' { "maks. $($c.maxLength)" }
            'integer' { "$($c.min)–$($c.max)" }
            { $_ -in 'choice', 'multichoice' } { "$(@($c.options).Count) valg" }
            default { '' }
        }
        W "| ``$($c.schemaName.ToLower())`` | $($c.displayName) | $($c.type) | $det | ``$($c.source)`` |"
    }
    foreach ($c in $t.columns | Where-Object { $_.type -in 'choice', 'multichoice' }) {
        W ''
        W "<details><summary>Valg for <code>$($c.schemaName.ToLower())</code> ($(@($c.options).Count))</summary>"
        W ''
        if ($c.note) { W $c.note; W '' }
        W '| Værdi | Label | Salesforce-værdi |'
        W '|---|---|---|'
        foreach ($o in $c.options) { W "| $($o.value) | $($o.label) | $($o.sourceValue) |" }
        W ''
        W '</details>'
    }
}
$dir = Split-Path $OutFile
if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
[IO.File]::WriteAllText((Resolve-Path $dir).Path + '\' + (Split-Path $OutFile -Leaf), $sb.ToString(), (New-Object Text.UTF8Encoding $false))
Write-Host "Wrote $OutFile"
