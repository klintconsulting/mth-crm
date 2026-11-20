<#
.SYNOPSIS
  Syntetiske testdata til ESP (udbud): underentreprenører (Accounts med fag og kontakter), entrepriser på sager,
  UE-tilknytninger med valgte kontakter og to mailskabeloner. Alle firmaer, personer og e-mails er OPDIGTEDE.
  Kræver at Import-Trades.ps1 og New-TestData.ps1 er kørt. Idempotent (faste GUIDs + upsert). -Remove sletter igen.

.EXAMPLE
  .\scripts\New-EspTestData.ps1 -Url https://orgxxxx.crm4.dynamics.com
  .\scripts\New-EspTestData.ps1 -Url https://orgxxxx.crm4.dynamics.com -Remove
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [int]$Subcontractors = 30,
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$base = $Url.TrimEnd('/')
$rnd = New-Object System.Random 7
$P = 273280000

function Id([string]$key) { $b = [Security.Cryptography.MD5]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes("mth-esp-testdata:$key")); ([guid]$b).ToString() }
function Pick($arr) { $arr[$rnd.Next(0, @($arr).Count)] }
function Upsert([string]$set, [string]$id, $body) { [void](Invoke-Dataverse PATCH "$set($id)" $body) }
function Associate([string]$path, [string]$targetUrl) {
    try { [void](Invoke-Dataverse POST "$path/`$ref" @{ '@odata.id' = $targetUrl }) }
    catch { if ($_.Exception.Message -notmatch 'duplicate|already|0x80040237|Cannot insert duplicate') { throw } }
}

# Most used trades in Salesforce (last 3 years)
$tradeCodes = '22101', '22109', '22059', '22111', '22037', '22030', '22032', '22018', '22006', '22039', '22050', '21109', '22108', '22063', '22009'
$nameParts = @{
    '22101' = 'El'; '22109' = 'VVS'; '22059' = 'Malerfirma'; '22111' = 'Ventilation'; '22037' = 'Tagdækning'; '22030' = 'Murerfirma'
    '22032' = 'Tømrer & Snedker'; '22018' = 'Nedrivning'; '22006' = 'Jord & Kloak'; '22039' = 'Gulve'; '22050' = 'Stålbyg'
    '21109' = 'Gulvservice'; '22108' = 'Elevator'; '22063' = 'Inventar'; '22009' = 'Anlægsgartner' }
$towns = @(@('2300', 'København S'), @('2600', 'Glostrup'), @('3400', 'Hillerød'), @('4000', 'Roskilde'), @('4700', 'Næstved'), @('5000', 'Odense C'),
           @('5700', 'Svendborg'), @('6000', 'Kolding'), @('6200', 'Aabenraa'), @('6700', 'Esbjerg'), @('7100', 'Vejle'), @('7400', 'Herning'),
           @('8000', 'Aarhus C'), @('8700', 'Horsens'), @('9000', 'Aalborg'), @('9800', 'Hjørring'))
$prefixes = 'Nordic', 'Dansk', 'Jysk', 'Sjællands', 'Fyns', 'Kyst', 'Hede', 'Fjord', 'Øst', 'Vest', 'Midt', 'Bølge', 'Granit', 'Lind', 'Egholt'
$firstNames = 'Anders', 'Bente', 'Claus', 'Dorte', 'Erik', 'Frida', 'Gitte', 'Hans', 'Inge', 'Jakob', 'Karen', 'Lene', 'Morten', 'Nina', 'Ole', 'Pia'
$lastNames = 'Holm', 'Bach', 'Lund', 'Dahl', 'Krogh', 'Vestergaard', 'Skov', 'Bang', 'Friis', 'Mikkelsen', 'Toft', 'Juhl'
$landline = 32, 33, 36, 43, 44, 46, 56, 62, 65, 74, 75, 76, 86, 87, 96, 98
$mobile = 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 40, 41, 42, 50, 51, 52, 53, 60, 61, 71, 81, 91, 92, 93
function DkPhone($p) { '+45 {0} {1:D2} {2:D2} {3:D2}' -f (Pick $p), $rnd.Next(0, 100), $rnd.Next(0, 100), $rnd.Next(0, 100) }
function Ascii([string]$s) { $s.ToLower().Replace('ø', 'oe').Replace('æ', 'ae').Replace('å', 'aa') -replace '[^a-z0-9]', '' }

$opps = (Invoke-Dataverse GET "mth_opportunities?`$select=mth_opportunityid,mth_name&`$filter=mth_status ge 273280020 and mth_status lt 273280090&`$orderby=mth_amount desc&`$top=3").value

if ($Remove) {
    $n = 0
    foreach ($o in $opps) { foreach ($bc in (Invoke-Dataverse GET "mth_buildingcontracts?`$select=mth_buildingcontractid&`$filter=_mth_opportunityid_value eq $($o.mth_opportunityid)").value) {
        Invoke-Dataverse DELETE "mth_buildingcontracts($($bc.mth_buildingcontractid))" | Out-Null; $n++ } }   # partners are deleted by cascade
    for ($i = 1; $i -le $Subcontractors; $i++) {
        foreach ($c in 1..2) { try { Invoke-Dataverse DELETE "contacts($(Id "ue-contact-$i-$c"))" | Out-Null; $n++ } catch {} }
        try { Invoke-Dataverse DELETE "accounts($(Id "ue-$i"))" | Out-Null; $n++ } catch {}
    }
    foreach ($k in 'tpl-request', 'tpl-reminder') { try { Invoke-Dataverse DELETE "mth_espemailtemplates($(Id $k))" | Out-Null; $n++ } catch {} }
    Write-Host "Slettede $n poster."; return
}

# ---------------- Underentreprenører ----------------
$ueByTrade = @{}; $ueContacts = @{}; $usedNames = @{}
for ($i = 1; $i -le $Subcontractors; $i++) {
    $main = $tradeCodes[($i - 1) % $tradeCodes.Count]
    $codes = @($main) + @(1..$rnd.Next(0, 3) | ForEach-Object { Pick $tradeCodes }) | Select-Object -Unique
    do { $name = "$(Pick $prefixes) $($nameParts[$main]) $(Pick @('ApS', 'A/S', 'ApS', 'I/S'))" } while ($usedNames[$name])
    $usedNames[$name] = $true
    $town = Pick $towns
    $id = Id "ue-$i"
    $domain = (Ascii (($name -split ' ')[0] + ($name -split ' ')[1])) + '-test.example.dk'
    Upsert 'accounts' $id @{
        name = $name; mth_vatno = [string]$rnd.Next(10000000, 99999999); telephone1 = (DkPhone $landline); emailaddress1 = "tilbud@$domain"
        address1_line1 = "$(Pick @('Industrivej', 'Håndværkervej', 'Erhvervsparken', 'Fabriksvej', 'Mileparken')) $($rnd.Next(1, 80))"
        address1_postalcode = $town[0]; address1_city = $town[1]; address1_country = 'Danmark'
        mth_accountstatus = $P + 0
    }
    foreach ($code in $codes) {
        Associate "accounts($id)/mth_account_mth_trade" "$base/api/data/v9.2/mth_trades(mth_code='$code')"
        if (-not $ueByTrade[$code]) { $ueByTrade[$code] = @() }; $ueByTrade[$code] += $i
    }
    $ueContacts[$i] = @()
    foreach ($c in 1..$rnd.Next(1, 3)) {
        $fn = Pick $firstNames; $ln = Pick $lastNames; $cid = Id "ue-contact-$i-$c"
        Upsert 'contacts' $cid @{ firstname = $fn; lastname = $ln; jobtitle = (Pick @('Tilbudsansvarlig', 'Direktør', 'Projektleder', 'Kalkulatør'))
            emailaddress1 = "$(Ascii $fn).$(Ascii $ln)@$domain"; mobilephone = (DkPhone $mobile); 'parentcustomerid_account@odata.bind' = "/accounts($id)" }
        $ueContacts[$i] += $cid
    }
}
Write-Host "Underentreprenører: $Subcontractors med fag og kontakter"

# ---------------- Entrepriser og UE-tilknytninger ----------------
$statusPool = 10, 10, 10, 20, 22, 24, 24, 30, 30, 92
$bcCount = 0; $cpCount = 0
foreach ($o in $opps) {
    $oppKey = $o.mth_opportunityid.Substring(0, 8)
    $chosen = $tradeCodes | Sort-Object { $rnd.Next() } | Select-Object -First 4
    foreach ($code in $chosen) {
        $trade = Invoke-Dataverse GET "mth_trades(mth_code='$code')?`$select=mth_tradeid,mth_name"
        $bcId = Id "bc-$oppKey-$code"
        Upsert 'mth_buildingcontracts' $bcId @{
            mth_name = $trade.mth_name; mth_tenderdeadline = (Get-Date).AddDays($rnd.Next(7, 45)).ToString('yyyy-MM-dd')
            'mth_OpportunityId@odata.bind' = "/mth_opportunities($($o.mth_opportunityid))"; 'mth_TradeId@odata.bind' = "/mth_trades($($trade.mth_tradeid))" }
        $bcCount++
        $candidates = @($ueByTrade[$code]) | Sort-Object { $rnd.Next() } | Select-Object -First $rnd.Next(2, 5)
        foreach ($i in $candidates) {
            $acc = Invoke-Dataverse GET "accounts($(Id "ue-$i"))?`$select=name"
            $cpId = Id "cp-$oppKey-$code-$i"
            Upsert 'mth_collaboratingpartners' $cpId @{
                mth_name = $acc.name; mth_status = $P + (Pick $statusPool); mth_partnertype = $P + 0
                'mth_BuildingContractId@odata.bind' = "/mth_buildingcontracts($bcId)"; 'mth_AccountId@odata.bind' = "/accounts($(Id "ue-$i"))" }
            Associate "mth_collaboratingpartners($cpId)/mth_collabpartner_contact" "$base/api/data/v9.2/contacts($($ueContacts[$i][0]))"
            $cpCount++
        }
    }
}
Write-Host "Entrepriser: $bcCount på $($opps.Count) sager, UE-tilknytninger: $cpCount"

# ---------------- Mailskabeloner ----------------
$sig = '<p>Venlig hilsen<br>{{Sender}}<br>MT Højgaard</p>'
Upsert 'mth_espemailtemplates' (Id 'tpl-request') @{
    mth_name = 'Tilbudsindhentning'; mth_isdefault = $true
    mth_subject = 'Indbydelse til at give tilbud: {{BuildingContract}} – {{Opportunity}}'
    mth_body = "<p>Kære {{ContactFirstName}}</p><p>MT Højgaard indbyder hermed {{Partner}} til at give tilbud på entreprisen <b>{{BuildingContract}}</b> på sagen <b>{{Opportunity}}</b> (sagsnr. {{CaseNumber}}).</p><p>Tilbudsfrist: <b>{{TenderDeadline}}</b>.</p><p>Udbudsmaterialet er vedhæftet. Giv os venligst besked, om I ønsker at give tilbud.</p>$sig" }
Upsert 'mth_espemailtemplates' (Id 'tpl-reminder') @{
    mth_name = 'Påmindelse om tilbudsfrist'; mth_isdefault = $false
    mth_subject = 'Påmindelse: tilbudsfrist {{TenderDeadline}} – {{BuildingContract}}'
    mth_body = "<p>Kære {{ContactFirstName}}</p><p>Vi minder om tilbudsfristen <b>{{TenderDeadline}}</b> for entreprisen {{BuildingContract}} på sagen {{Opportunity}}.</p><p>Vi ser frem til at modtage jeres tilbud.</p>$sig" }
Write-Host 'Mailskabeloner: 2'
Write-Host 'Faerdig.'
