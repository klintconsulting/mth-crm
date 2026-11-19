<#
.SYNOPSIS
  Opretter syntetiske testdata i et udviklingsmiljø: afdelinger, Accounts, Kontakter,
  Salgsmuligheder og sagsteam. Alle firmaer, personer, CVR-numre og e-mails er OPDIGTEDE
  (e-mails på example.dk). Afdelingsnavnene er MTH's rigtige afdelinger (ingen persondata).

  Idempotent: hver post har et fast GUID afledt af en nøgle, og der bruges upsert (PATCH),
  så en ny kørsel opdaterer de samme poster i stedet for at lave dubletter.
  Tilfældighed er seedet, så samme kørsel giver samme data.

.EXAMPLE
  .\scripts\New-TestData.ps1 -Url https://orgxxxx.crm4.dynamics.com
  .\scripts\New-TestData.ps1 -Url https://orgxxxx.crm4.dynamics.com -Remove   # slet testdata igen
#>
param(
    [Parameter(Mandatory)][string]$Url,
    [int]$Accounts = 25,
    [int]$Opportunities = 60,
    [switch]$Remove
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$schema = Get-Content (Join-Path $PSScriptRoot 'schema\schema.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$rnd = New-Object System.Random 42

function Id([string]$key) {
    $md5 = [Security.Cryptography.MD5]::Create()
    $b = $md5.ComputeHash([Text.Encoding]::UTF8.GetBytes("mth-testdata:$key"))
    ([guid]$b).ToString()
}
function Pick($arr) { $arr[$rnd.Next(0, @($arr).Count)] }
function Opts([string]$table, [string]$col) {
    $t = $schema.tables | Where-Object { $_.schemaName.ToLower() -eq $table }
    @(($t.columns | Where-Object { $_.schemaName.ToLower() -eq $col }).options)
}
function OptVal([string]$table, [string]$col) { [int](Pick (Opts $table $col)).value }
function OptByLabel([string]$table, [string]$col, [string]$label) { [int]((Opts $table $col) | Where-Object { $_.label -eq $label }).value }
function MultiVal([string]$table, [string]$col, [int]$max) {
    $o = Opts $table $col; $n = $rnd.Next(1, $max + 1)
    (@(1..$n | ForEach-Object { [int](Pick $o).value }) | Select-Object -Unique) -join ','
}
function Upsert([string]$set, [string]$id, $body) { [void](Invoke-Dataverse PATCH "$set($id)" $body) }
function DateStr([datetime]$d) { $d.ToString('yyyy-MM-dd') }

# ---------------- Stamdata ----------------
$departments = @(
    @{ Key = '2051'; Name = '2051 Jord og Ledninger Øst'; No = '9006'; Tender = $false }
    @{ Key = '2124'; Name = '2124 Nybyggeri Øst'; No = '9013'; Tender = $false }
    @{ Key = '2052'; Name = '2052 Jord og Ledninger Øst 2'; No = '9006'; Tender = $false }
    @{ Key = '2030'; Name = '2030 Fundering'; No = '9003'; Tender = $false }
    @{ Key = '2121'; Name = '2121 Nybyggeri Midt og Syd'; No = '9013'; Tender = $false }
    @{ Key = '2091'; Name = '2091 Tømrer og Maler Øst'; No = '9010'; Tender = $true }
    @{ Key = '2041'; Name = '2041 Anlægskonstruktioner Øst'; No = '9005'; Tender = $false }
    @{ Key = '2050'; Name = '2050 Jord og Ledninger Vest'; No = '9006'; Tender = $false }
    @{ Key = '2111'; Name = '2111 Råhus Øst'; No = '9012'; Tender = $false }
    @{ Key = '2125'; Name = '2125 Renovering og Transformation Øst'; No = '9013'; Tender = $true }
)
$companyFirst = 'Nordlys', 'Kystvej', 'Fjordbyg', 'Granit', 'Lindholm', 'Bøgely', 'Havnefront', 'Skovbrynet', 'Strandgaard', 'Engholm', 'Solbakken', 'Mølleå', 'Klitgaard', 'Ravnsborg', 'Egeskov', 'Vesterled', 'Østerbro', 'Sønderholm', 'Nørrevang', 'Hedegaard', 'Bakkehus', 'Kildevang', 'Tårnby', 'Åkanden', 'Stenrose', 'Valmue', 'Birkely', 'Lærkevej', 'Fyrretræ', 'Pilegaard'
$companySuffix = 'Ejendomme A/S', 'Boligselskab', 'Kommune', 'Entreprise ApS', 'Byg A/S', 'Forsyning A/S', 'Region', 'Andelsboligforening', 'Invest A/S', 'Arkitekter ApS'
$cities = @(@('1050', 'København K'), @('2100', 'København Ø'), @('2300', 'København S'), @('2600', 'Glostrup'), @('2800', 'Kongens Lyngby'), @('3400', 'Hillerød'), @('4000', 'Roskilde'), @('5000', 'Odense C'), @('6000', 'Kolding'), @('7100', 'Vejle'), @('8000', 'Aarhus C'), @('8700', 'Horsens'), @('9000', 'Aalborg'), @('6700', 'Esbjerg'))
$streets = 'Havnegade', 'Vestergade', 'Nørregade', 'Industrivej', 'Strandvejen', 'Skovvej', 'Kongevejen', 'Søndergade', 'Banegårdsvej', 'Fabriksvej'
$firstNames = 'Anne', 'Mette', 'Lars', 'Jens', 'Peter', 'Hanne', 'Søren', 'Camilla', 'Mikkel', 'Line', 'Rasmus', 'Ida', 'Henrik', 'Louise', 'Thomas', 'Sofie', 'Martin', 'Pernille', 'Kasper', 'Maja'
$lastNames = 'Jensen', 'Nielsen', 'Hansen', 'Pedersen', 'Andersen', 'Christensen', 'Larsen', 'Sørensen', 'Rasmussen', 'Jørgensen', 'Madsen', 'Kristensen', 'Olsen', 'Thomsen', 'Poulsen'
$titles = 'Projektchef', 'Indkøbschef', 'Bygherrerådgiver', 'Direktør', 'Projektleder', 'Ejendomschef', 'Teknisk chef', 'Udbudsansvarlig'
$projectTypes = 'Nybyggeri af boliger', 'Renovering af skole', 'Ny bro', 'Kloakrenovering', 'Udvidelse af hospital', 'Etablering af fjernvarme', 'Kontorbyggeri', 'Daginstitution', 'Vejanlæg', 'Havneudvidelse', 'Transformation af erhvervsejendom', 'Klimatilpasning'

# Status-fordeling (labels skal matche valgene i schema.json)
$statusWeights = @(
    @('01 Potential project', 8), @('05 Prioritised project', 6), @('20 PQ - upcoming', 4), @('22 PQ - ongoing', 4), @('24 PQ - submitted', 3)
    @('52 Tender - ongoing', 6), @('54 Tender - submitted', 5), @('56 Negotiation', 3), @('60 MTH contract', 8), @('85 Project filed', 3)
    @('90 PQ - Lost tender', 5), @('91 Tender Abandoned', 2), @('92 PQ - lost', 3)
)
$statusPool = foreach ($s in $statusWeights) { 1..$s[1] | ForEach-Object { $s[0] } }
$probByCode = @{ '01' = 10; '05' = 20; '20' = 20; '22' = 25; '24' = 30; '52' = 40; '54' = 50; '56' = 70; '60' = 100; '85' = 100; '90' = 0; '91' = 0; '92' = 0 }

$me = (Invoke-Dataverse GET 'WhoAmI').UserId

# ---------------- Sletning ----------------
if ($Remove) {
    $n = 0
    for ($i = 1; $i -le $Opportunities; $i++) {
        foreach ($r in 1..3) { try { Invoke-Dataverse DELETE "mth_opportunityroles($(Id "role-$i-$r"))" | Out-Null; $n++ } catch {} }
        try { Invoke-Dataverse DELETE "mth_opportunities($(Id "opp-$i"))" | Out-Null; $n++ } catch {}
    }
    for ($i = 1; $i -le $Accounts; $i++) {
        foreach ($c in 1..3) { try { Invoke-Dataverse DELETE "contacts($(Id "contact-$i-$c"))" | Out-Null; $n++ } catch {} }
        try { Invoke-Dataverse DELETE "accounts($(Id "account-$i"))" | Out-Null; $n++ } catch {}
    }
    foreach ($d in $departments) { try { Invoke-Dataverse DELETE "mth_organizationalunits($(Id "dept-$($d.Key)"))" | Out-Null; $n++ } catch {} }
    Write-Host "Slettede $n testposter."
    return
}

# ---------------- Afdelinger ----------------
foreach ($d in $departments) {
    Upsert 'mth_organizationalunits' (Id "dept-$($d.Key)") @{
        mth_name = $d.Name; mth_departmentno = $d.No; mth_divisionname = 'Produktion'; mth_businessunitname = 'MTH Danmark'
        mth_costcenterno = $d.Key; mth_istenderdepartment = $d.Tender
    }
}
Write-Host "Afdelinger: $($departments.Count)"

# ---------------- Accounts og Kontakter ----------------
$usedNames = @{}
$contactCount = 0
for ($i = 1; $i -le $Accounts; $i++) {
    do { $name = "$(Pick $companyFirst) $(Pick $companySuffix)" } while ($usedNames[$name])
    $usedNames[$name] = $true
    $city = Pick $cities
    Upsert 'accounts' (Id "account-$i") @{
        name = $name
        mth_vatno = [string]$rnd.Next(10000000, 99999999)
        telephone1 = "+45 $($rnd.Next(20,99)) $($rnd.Next(10,99)) $($rnd.Next(10,99)) $($rnd.Next(10,99))"
        emailaddress1 = "kontakt@$(($name -split ' ')[0].ToLower() -replace '[^a-z]', '')-test.example.dk"
        address1_line1 = "$(Pick $streets) $($rnd.Next(1, 120))"; address1_postalcode = $city[0]; address1_city = $city[1]; address1_country = 'Danmark'
        mth_segment = (OptVal 'account' 'mth_segment')
        mth_subsegment = (OptVal 'account' 'mth_subsegment')
        mth_accountstatus = (OptVal 'account' 'mth_accountstatus')
        mth_accountcategory = (MultiVal 'account' 'mth_accountcategory' 2)
    }
    $nContacts = $rnd.Next(1, 4)
    for ($c = 1; $c -le $nContacts; $c++) {
        $fn = Pick $firstNames; $ln = Pick $lastNames
        $body = @{
            firstname = $fn; lastname = $ln; jobtitle = (Pick $titles)
            emailaddress1 = "$($fn.ToLower()).$($ln.ToLower())@example.dk" -replace 'ø', 'oe' -replace 'å', 'aa' -replace 'æ', 'ae'
            mobilephone = "+45 $($rnd.Next(20,99)) $($rnd.Next(10,99)) $($rnd.Next(10,99)) $($rnd.Next(10,99))"
            'parentcustomerid_account@odata.bind' = "/accounts($(Id "account-$i"))"
        }
        if ($rnd.Next(0, 10) -eq 0) { $body.mth_leftdate = DateStr ((Get-Date).AddDays(-$rnd.Next(30, 900))) }
        if ($rnd.Next(0, 3) -eq 0) { $body.mth_marketingagreementsenton = DateStr ((Get-Date).AddDays(-$rnd.Next(30, 700))) }
        Upsert 'contacts' (Id "contact-$i-$c") $body
        $contactCount++
    }
}
Write-Host "Accounts: $Accounts, Kontakter: $contactCount"

# ---------------- Salgsmuligheder og sagsteam ----------------
$roleOpts = Opts 'mth_opportunityrole' 'mth_role'
$roleCount = 0
for ($i = 1; $i -le $Opportunities; $i++) {
    $statusLabel = Pick $statusPool
    $code = $statusLabel.Substring(0, 2)
    $city = Pick $cities
    $close = (Get-Date).AddDays($rnd.Next(-500, 400)).Date
    $tender = $close.AddDays(-$rnd.Next(20, 120))
    $amount = [math]::Round($rnd.Next(2, 400) * 1000000 / 1000) * 1000
    $order = Pick $departments
    $body = @{
        mth_name = "$(Pick $projectTypes), $($city[1])"
        mth_status = (OptByLabel 'mth_opportunity' 'mth_status' $statusLabel)
        mth_probability = $probByCode[$code]
        mth_amount = [decimal]$amount
        mth_estimatedamount = [decimal]([math]::Round($amount * (0.8 + $rnd.NextDouble() * 0.4) / 1000) * 1000)
        mth_closedate = DateStr $close
        mth_tenderdate = DateStr $tender
        mth_invitationtotenderdate = DateStr $tender.AddDays(-$rnd.Next(14, 60))
        mth_orderstartdate = DateStr $close.AddDays($rnd.Next(30, 120))
        mth_orderenddate = DateStr $close.AddDays($rnd.Next(200, 900))
        mth_opportunitytype = (OptByLabel 'mth_opportunity' 'mth_opportunitytype' 'External')
        mth_constructioncategory = (OptVal 'mth_opportunity' 'mth_constructioncategory')
        mth_marketcategory = (OptVal 'mth_opportunity' 'mth_marketcategory')
        mth_casetype = (OptVal 'mth_opportunity' 'mth_casetype')
        mth_contracttype = (OptVal 'mth_opportunity' 'mth_contracttype')
        mth_buildingtypes = (MultiVal 'mth_opportunity' 'mth_buildingtypes' 3)
        mth_deliveryaddress = "$(Pick $streets) $($rnd.Next(1, 200))"; mth_deliveryzip = $city[0]; mth_deliverycity = $city[1]
        mth_casenumber = "TEST-$('{0:D5}' -f (20000 + $i))"
        mth_description = 'Syntetisk testdata, genereret af scripts/New-TestData.ps1.'
        mth_iskeyproject = ($rnd.Next(0, 8) -eq 0)
        'mth_AccountId@odata.bind' = "/accounts($(Id "account-$($rnd.Next(1, $Accounts + 1))"))"
        'mth_OrderDepartmentId@odata.bind' = "/mth_organizationalunits($(Id "dept-$($order.Key)"))"
    }
    if ([int]$code -ge 20) {
        $td = $departments | Where-Object { $_.Tender } | Select-Object -First 1
        $body['mth_TenderDepartmentId@odata.bind'] = "/mth_organizationalunits($(Id "dept-$($td.Key)"))"
        $body.mth_tenderamount = [decimal]([math]::Round($amount * (0.9 + $rnd.NextDouble() * 0.2) / 1000) * 1000)
    }
    if ([int]$code -ge 60 -and [int]$code -lt 90) { $body.mth_bcprojectnumber = "P$($rnd.Next(100000, 999999))" }
    Upsert 'mth_opportunities' (Id "opp-$i") $body

    $nRoles = $rnd.Next(1, 4)
    $roles = @($roleOpts | Sort-Object { $rnd.Next() } | Select-Object -First $nRoles)
    for ($r = 1; $r -le $nRoles; $r++) {
        $ro = $roles[$r - 1]
        Upsert 'mth_opportunityroles' (Id "role-$i-$r") @{
            mth_name = $ro.label; mth_role = [int]$ro.value
            'mth_OpportunityId@odata.bind' = "/mth_opportunities($(Id "opp-$i"))"
            'mth_UserId@odata.bind' = "/systemusers($me)"
        }
        $roleCount++
    }
}
Write-Host "Salgsmuligheder: $Opportunities, roller: $roleCount"
Write-Host 'Faerdig.'
