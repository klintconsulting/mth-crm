<#
.SYNOPSIS
  Runs a read-only SQL query against the Dataverse TDS endpoint (<org>:5558) with the login from Dataverse.psm1.
  The VS Code mssql extension cannot connect to Dataverse: it sends DATABASEPROPERTYEX on connect, which the
  endpoint does not support. SSMS works, and so does this script.

  Only SELECT is supported (the endpoint is read-only). Choice columns return numbers; the label is in <column>name,
  e.g. mth_statusname. Max 80 MB result and 2 minutes per query.

.EXAMPLE
  .\scripts\Invoke-DataverseSql.ps1 -Url https://orgxxxx.crm4.dynamics.com "SELECT TOP 10 name, address1_city FROM account"
  .\scripts\Invoke-DataverseSql.ps1 -Url https://orgxxxx.crm4.dynamics.com -File .\query.sql -Grid
  .\scripts\Invoke-DataverseSql.ps1 -Url https://orgxxxx.crm4.dynamics.com "SELECT * FROM mth_lead" -Csv leads.csv
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [Parameter(Position = 0)][string]$Query,
    [string]$File,
    [switch]$Grid,
    [string]$Csv
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url | Out-Null
if ($File) { $Query = Get-Content $File -Raw -Encoding UTF8 }
if (-not $Query) { throw 'Give a query or -File.' }

$server = ([uri]$Url).Host
$conn = New-Object System.Data.SqlClient.SqlConnection "Server=$server,5558;Encrypt=True;Connect Timeout=30"
$conn.AccessToken = Get-DataverseToken
try {
    $conn.Open()
    $cmd = $conn.CreateCommand(); $cmd.CommandText = $Query; $cmd.CommandTimeout = 120
    $table = New-Object System.Data.DataTable
    [void](New-Object System.Data.SqlClient.SqlDataAdapter $cmd).Fill($table)
} finally { $conn.Close() }

$cols = $table.Columns | ForEach-Object ColumnName
$rows = foreach ($r in $table.Rows) { $o = [ordered]@{}; foreach ($c in $cols) { $o[$c] = if ($r[$c] -is [DBNull]) { $null } else { $r[$c] } }; [pscustomobject]$o }
Write-Host "$($table.Rows.Count) row(s)"
if ($Csv) { $rows | Export-Csv $Csv -NoTypeInformation -Encoding UTF8 -Delimiter ';'; Write-Host "Wrote $Csv" }
elseif ($Grid) { $rows | Out-GridView -Title $Query }
else { $rows }
