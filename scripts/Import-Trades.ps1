<#
.SYNOPSIS
  Imports the trade catalogue (fag) from scripts/data/trades.json into mth_trade.
  Upsert on the alternate key mth_code, so it can be re-run in every environment (reference data, also production).

.EXAMPLE
  .\scripts\Import-Trades.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [string]$DataFile = (Join-Path $PSScriptRoot 'data\trades.json')
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$data = Get-Content $DataFile -Raw -Encoding UTF8 | ConvertFrom-Json
$n = 0
foreach ($t in $data.trades) {
    # PATCH on the alternate key = upsert (create if missing, update if present)
    [void](Invoke-Dataverse PATCH "mth_trades(mth_code='$($t.code)')" @{ mth_name = $t.name; mth_sourcevalue = $t.sourceValue })
    $n++
}
Write-Host "Upserted $n trades."
