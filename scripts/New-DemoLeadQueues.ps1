<#
.SYNOPSIS
  DEMO: opretter fire lead-køer med kriterier og medlemmer (den kørende bruger + demobrugerne ah/ln/ms, hvis de findes),
  så SMART-testdataene fordeles i køer. Idempotent (faste GUIDs). -Remove sletter igen.

.EXAMPLE
  .\scripts\New-DemoLeadQueues.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param([Parameter(Mandatory)][string]$Url, [switch]$Remove)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$base = $Url.TrimEnd('/') + '/api/data/v9.2/'
$P = 273280000
$region = @{ Hovedstaden = $P + 0; Sjælland = $P + 1; Syddanmark = $P + 2; Midtjylland = $P + 3; Nordjylland = $P + 4 }
function Id([string]$k) { ([guid][Security.Cryptography.MD5]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes("mth-demo-queue:$k"))).ToString() }

$queues = @(
    @{ Key = 'jutland-housing'; Name = 'Jylland – boliger over 20 mio.'; Members = @('ah', 'ln'); Body = @{ mth_regions = "$($region.Midtjylland),$($region.Nordjylland)"; mth_categorykeywords = 'Bolig'; mth_minvalue = 20 } }
    @{ Key = 'south-institutions'; Name = 'Syddanmark – institutioner og erhverv'; Members = @('ms'); Body = @{ mth_regions = "$($region.Syddanmark)"; mth_categorykeywords = 'Institution;Erhverv' } }
    @{ Key = 'civil-works'; Name = 'Anlæg – hele landet over 50 mio.'; Members = @('ln'); Body = @{ mth_categorykeywords = 'Anlæg'; mth_minvalue = 50 } }
    @{ Key = 'capital-large'; Name = 'Hovedstaden – store sager (150+ mio.)'; Members = @('ah', 'ms'); Body = @{ mth_regions = "$($region.Hovedstaden)"; mth_minvalue = 150; mth_excludestarted = $true } }
)
if ($Remove) { foreach ($q in $queues) { try { Invoke-Dataverse DELETE "mth_leadqueues($(Id $q.Key))" | Out-Null } catch {} }; Write-Host 'Demo-køer slettet.'; return }

$me = (Invoke-Dataverse GET 'WhoAmI').UserId
$domain = ((Invoke-Dataverse GET "systemusers($me)?`$select=domainname").domainname -split '@')[1]
foreach ($q in $queues) {
    $id = Id $q.Key
    $body = @{ mth_name = $q.Name; mth_isactive = $true; mth_notifymembers = $true; mth_description = 'Demo-kø (scripts/New-DemoLeadQueues.ps1).' } + $q.Body
    [void](Invoke-Dataverse PATCH "mth_leadqueues($id)" $body)
    $members = @($me) + @($q.Members | ForEach-Object { (Invoke-Dataverse GET "systemusers?`$select=systemuserid&`$filter=domainname eq '$_@$domain'").value | Select-Object -First 1 -ExpandProperty systemuserid })
    foreach ($m in $members | Where-Object { $_ }) {
        try { [void](Invoke-Dataverse POST "mth_leadqueues($id)/mth_leadqueue_systemuser/`$ref" @{ '@odata.id' = "${base}systemusers($m)" }) } catch { if ($_.Exception.Message -notmatch 'duplicate|already') { throw } }
    }
    Write-Host "  $($q.Name): $(@($members | Where-Object { $_ }).Count) medlem(mer)"
}
