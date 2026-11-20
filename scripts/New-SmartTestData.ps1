<#
.SYNOPSIS
  Genererer SYNTETISKE SMART-API-svar (samme JSON-format som api.smart.byggefakta.dk v2.3) til test af SMART-synken:
  lookups.json, countries.json, regions.json, districts.json, projects.json, tenders.json, companies.json.
  Alle projekter, firmaer, personer, CVR-numre og e-mails er OPDIGTEDE (e-mails på det reserverede example.com).
  Deterministisk (seed). -Variant 2 ændrer udvalgte projekter (værdi, stadie, byggestart) for at teste opdateringer/historik.

.EXAMPLE
  .\scripts\New-SmartTestData.ps1                 # skriver til functions/SmartSync/testdata
  .\scripts\New-SmartTestData.ps1 -Variant 2
#>
param(
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) 'functions\SmartSync\testdata'),
    [int]$Projects = 25,
    [int]$Variant = 1
)
$ErrorActionPreference = 'Stop'
$rnd = New-Object System.Random 2026
function Pick($a) { $a[$rnd.Next(0, @($a).Count)] }
function Write-Json($name, $obj) { [IO.File]::WriteAllText((Join-Path $OutDir $name), ($obj | ConvertTo-Json -Depth 20), (New-Object Text.UTF8Encoding $false)) }
function ListEnv($data) { @{ result = @{ meta = @{ offset = 0; size = @($data).Count; total = @($data).Count }; data = @($data) } } }
New-Item -ItemType Directory -Force $OutDir | Out-Null
$now = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')

# ---------------- dictionaries ----------------
function Items($pairs) { foreach ($p in $pairs) { @{ id = $p[0]; name = $p[1]; rsm_code = [string]$p[0] } } }
$lookups = @{ result = @{ dict = @{ version = [int64]([DateTimeOffset]::UtcNow.ToUnixTimeSeconds()); language_id = 3; items = @(
    @{ lookup = 'contract_types'; items = @(Items @(@(1, 'Totalentreprise'), @(2, 'Hovedentreprise'), @(3, 'Fagentreprise'), @(4, 'Storentreprise'), @(5, 'Partnering'), @(6, 'OPP'))) }
    @{ lookup = 'project_stages'; items = @(Items @(@(1, 'Idé/planlægning'), @(2, 'Projektering'), @(3, 'Udbud'), @(4, 'Udførelse'), @(5, 'Afsluttet'))) }
    @{ lookup = 'detailed_stages'; items = @(Items @(@(11, 'Idéoplæg'), @(12, 'Lokalplan'), @(21, 'Dispositionsforslag'), @(22, 'Projektforslag'), @(23, 'Hovedprojekt'), @(31, 'Prækvalifikation'), @(32, 'Licitation'), @(41, 'Byggestart'))) }
    @{ lookup = 'categories'; items = @(Items @(@(10, 'Bolig'), @(20, 'Erhverv'), @(30, 'Institution'), @(40, 'Anlæg'), @(50, 'Industri'), @(60, 'Kultur og fritid'))) }
    @{ lookup = 'development_types'; items = @(Items @(@(1, 'Nybyggeri'), @(2, 'Renovering'), @(3, 'Tilbygning'), @(4, 'Nedrivning'), @(5, 'Transformation'))) }
    @{ lookup = 'company_roles'; items = @(Items @(@(20, 'Bygherre'), @(21, 'Totalrådgiver'), @(22, 'Arkitekt'), @(23, 'Ingeniør'), @(24, 'Hovedentreprenør'), @(25, 'Bygherrerådgiver'))) }
    @{ lookup = 'company_role_tender'; items = @(Items @(@(46, 'Hovedentreprenør'), @(47, 'Totalentreprenør'), @(48, 'Fagentreprenør'))) }
    @{ lookup = 'company_types'; items = @(Items @(@(1, 'Privat virksomhed'), @(2, 'Kommune'), @(3, 'Region'), @(4, 'Boligselskab'), @(5, 'Stat'))) }
    @{ lookup = 'tender_types'; items = @(Items @(@(1, 'Offentlig'), @(2, 'Begrænset'), @(3, 'Privat'))) }
) } } }
Write-Json 'lookups.json' $lookups
Write-Json 'countries.json' (ListEnv @(@{ id = 2; name = 'Danmark'; rsm_code = 'DK' }))
$regions = @(@(81, 'Nordjylland'), @(82, 'Midtjylland'), @(83, 'Syddanmark'), @(84, 'Hovedstaden'), @(85, 'Sjælland'))
Write-Json 'regions.json' (ListEnv @($regions | ForEach-Object { @{ id = $_[0]; name = $_[1]; rsm_code = [string](1000 + $_[0]); country_id = 2 } }))
$districts = @(@(851, 'Aalborg', 81, '9000'), @(813, 'Frederikshavn', 81, '9900'), @(751, 'Aarhus', 82, '8000'), @(615, 'Horsens', 82, '8700'), @(657, 'Herning', 82, '7400'),
    @(461, 'Odense', 83, '5000'), @(621, 'Kolding', 83, '6000'), @(561, 'Esbjerg', 83, '6700'), @(630, 'Vejle', 83, '7100'),
    @(101, 'København', 84, '2300'), @(173, 'Lyngby-Taarbæk', 84, '2800'), @(219, 'Hillerød', 84, '3400'), @(265, 'Roskilde', 85, '4000'), @(370, 'Næstved', 85, '4700'))
Write-Json 'districts.json' (ListEnv @($districts | ForEach-Object { @{ id = $_[0]; name = $_[1]; rsm_code = [string]$_[0]; region_id = $_[2] } }))

# ---------------- companies (fictitious) ----------------
$clientNames = @(@('Fjordby Kommune', 2), @('Bølgehavn Boligselskab', 4), @('Region Midtland', 3), @('Nordlys Ejendomme A/S', 1), @('Havnestaden Udvikling A/S', 1),
    @('Engbo Andelsboligforening', 4), @('Skovlund Kommune', 2), @('Dansk Testinstitut (Staten)', 5), @('Kystens Logistik A/S', 1), @('Lindholm Kommune', 2))
$advisorNames = 'Tegnestuen Lys & Rum ApS', 'Konstruktion Nord A/S', 'Rådgivning Syd ApS', 'Arkitema Test A/S', 'Ingeniørhuset Øst A/S'
$firstNames = 'Anne', 'Jens', 'Mette', 'Lars', 'Sofie', 'Henrik', 'Pernille', 'Rasmus', 'Ida', 'Thomas'
$lastNames = 'Holm', 'Bach', 'Lund', 'Dahl', 'Krogh', 'Friis', 'Juhl', 'Toft'
$companies = @(); $cid = 900000
$clientCompanies = foreach ($c in $clientNames) {
    $cid++; $d = Pick $districts
    $co = @{ id = $cid; name = $c[0]; org_nr = [string]$rnd.Next(10000000, 99999999); par_id = [string]$rnd.Next(100000000, 999999999); company_type_id = $c[1]
        city = $d[1]; postcode_text = $d[3]; street = (Pick @('Rådhuspladsen', 'Torvet', 'Havnegade', 'Stationsvej')); street_number = [string]$rnd.Next(1, 60)
        addresses = @(@{ address_type_id = 1; main = $true; city = $d[1]; postcode_text = $d[3]; street = (Pick @('Rådhuspladsen', 'Torvet', 'Havnegade')); street_number = [string]$rnd.Next(1, 60) }) }
    $companies += $co; $co }
$advisorCompanies = foreach ($n in $advisorNames) { $cid++; $d = Pick $districts
    $co = @{ id = $cid; name = $n; org_nr = [string]$rnd.Next(10000000, 99999999); company_type_id = 1; city = $d[1]; postcode_text = $d[3]; street = 'Erhvervsvej'; street_number = [string]$rnd.Next(1, 90); addresses = @() }
    $companies += $co; $co }
Write-Json 'companies.json' (ListEnv $companies)

function Mail($first, $company) { ($first.ToLower() + '@' + ($company -replace '[^A-Za-z]', '').ToLower() + '.example.com') -replace 'ø', 'oe' -replace 'å', 'aa' -replace 'æ', 'ae' }
function RoleObj($roleId, $roleName, $co) {
    $fn = Pick $firstNames; $ln = Pick $lastNames
    @{ company_role_id = $roleId; company_id = $co.id; main = $false; company_role_card = @{ id = $roleId; name = $roleName }
       company_card = @{ id = $co.id; name = $co.name; org_nr = $co.org_nr; par_id = $co.par_id
           communications = @(@{ communication_type_id = 1; value = "45 $($rnd.Next(30000000, 99999999))"; phone_data = @{ number = [string]$rnd.Next(30000000, 99999999); calling_code = '45' } },
                              @{ communication_type_id = 4; value = (Mail 'kontakt' $co.name); phone_data = $null }) }
       contacts = @(@{ contact_card = @{ first_name = $fn; last_name = $ln; communications = @(
           @{ communication_type_id = 2; value = "45 2$($rnd.Next(1000000, 9999999))"; phone_data = @{ number = "2$($rnd.Next(1000000, 9999999))"; calling_code = '45' } },
           @{ communication_type_id = 4; value = (Mail $fn $co.name); phone_data = $null }) } }) }
}

# ---------------- projects ----------------
$templates = @(
    @('Nyt plejecenter', 30, 1), @('Renovering af skole', 30, 2), @('Boligbyggeri, etape', 10, 1), @('Almene boliger', 10, 1), @('Facaderenovering af boligblok', 10, 2),
    @('Nyt hovedkontor', 20, 1), @('Ombygning af kontorhus', 20, 5), @('Ny bro over', 40, 1), @('Kloakseparering', 40, 2), @('Udvidelse af sygehus', 30, 3),
    @('Logistikcenter', 50, 1), @('Idrætshal', 60, 1), @('Daginstitution', 30, 1), @('Transformation af silo til boliger', 10, 5), @('Ny produktionshal', 50, 1))
$projectsOut = @(); $tendersOut = @(); $tid = 7000
for ($i = 1; $i -le $Projects; $i++) {
    $t = $templates[($i - 1) % $templates.Count]; $d = Pick $districts
    $value = [int64]($rnd.Next(5, 450) * 1000000)
    $stage = Pick @(1, 2, 2, 3, 3, 4); $detailed = switch ($stage) { 1 { Pick @(11, 12) } 2 { Pick @(21, 22, 23) } 3 { Pick @(31, 32) } default { 41 } }
    $start = [DateTime]::Today.AddMonths($rnd.Next(-4, 26)); $start = New-Object DateTime $start.Year, $start.Month, 1
    $months = $rnd.Next(8, 36)
    if ($Variant -ge 2 -and ($i % 4 -eq 1)) { $value = [int64]($value * 1.25); $stage = [Math]::Min(4, $stage + 1); $detailed = if ($stage -eq 3) { 31 } elseif ($stage -eq 4) { 41 } else { 22 }; $start = $start.AddMonths(2) }
    $pid2 = 500000 + $i; $sid = 9100000 + $i
    $client = $clientCompanies[($i - 1) % $clientCompanies.Count]; $adv = Pick $advisorCompanies
    $roles = @((RoleObj 20 'Bygherre' $client), (RoleObj (Pick @(21, 22, 23)) (Pick @('Totalrådgiver', 'Arkitekt', 'Ingeniør')) $adv))
    $updates = @()
    if ($i % 3 -eq 0) {
        $tid++; $updates += @{ id = "u$tid"; update_key = 'PROJECT_TENDER_REGISTERED'; tender_id = $tid; company_role_card = @{ id = 46; name = 'Hovedentreprenør' }; updated_at = $now }
        $tendersOut += @{ id = $tid; project_id = $pid2; description = 'Udbud af hovedentreprise.'; content_standard = "Udbud af $($t[0].ToLower()) som hovedentreprise. Prækvalifikation af 5 tilbudsgivere."
            company_role_id = 46; tender_type_id = (Pick @(1, 2)); tender_status_id = 1; archived = 0; deleted = $false
            first_day_materials_text = $start.AddMonths(-5).ToString('dd-MM-yyyy'); last_day_application_text = $start.AddMonths(-3).ToString('dd-MM-yyyy'); prequalification_date_text = $start.AddMonths(-4).ToString('dd-MM-yyyy')
            company_role_card = @{ id = 46; name = 'Hovedentreprenør' }; tender_type_card = @{ id = 1; name = 'Offentlig' } }
    }
    $projectsOut += @{
        id = $pid2; source_id = $sid; version = $Variant; project_name = "$($t[0]) $($d[1])"; title = "$($t[0]) i $($d[1]) – $((Pick @('ca. 4.000', 'ca. 8.500', 'ca. 12.000', 'ca. 2.500'))) m2"
        description = "Testprojekt (syntetisk). $($t[0]) i $($d[1]) kommune. Projektet omfatter $((Pick @('nybyggeri i 3 etager', 'gennemgribende renovering', 'tilbygning og ombygning', 'anlægsarbejder og belægninger'))). Udbydes som $((Pick @('totalentreprise', 'hovedentreprise', 'fagentreprise')))."
        apartments = $(if ($t[1] -eq 10) { $rnd.Next(12, 180) } else { $null }); floors = $rnd.Next(1, 7); underground_floors = $rnd.Next(0, 2)
        gross_area = $rnd.Next(800, 25000); usage_area = $rnd.Next(600, 20000); gross_volume = $rnd.Next(3000, 90000)
        contract_type_id = (Pick @(1, 1, 2, 3, 4, 5)); stage_id = $stage; detailed_stage_id = $detailed; category_id = $t[1]; dev_type_id = $t[2]
        country_id = 2; region_id = $d[2]; district_id = $d[0]; city = $d[1]; postcode = [int]$d[3]; postcode_text = $d[3]; street = (Pick @('Byggepladsvej', 'Engvej', 'Havnefronten', 'Skolevej')); street_number = [string]$rnd.Next(1, 120)
        start_date = $start.ToString('yyyy-MM-ddT00:00:00Z'); start_text = $start.ToString('MMMM yyyy', [Globalization.CultureInfo]'da-DK'); start_date_accuracy_id = 2
        finish_date = $start.AddMonths($months).ToString('yyyy-MM-ddT00:00:00Z'); finish_text = $start.AddMonths($months).ToString('MMMM yyyy', [Globalization.CultureInfo]'da-DK'); finish_date_accuracy_id = 2
        months = $months; value = $value; updated_at = $now
        roles = $roles; cat_dev_type = @(@{ category_id = $t[1]; dev_type_id = $t[2]; main = $true; priority = 1 }); updates = $updates
    }
}
Write-Json 'projects.json' (ListEnv $projectsOut)
Write-Json 'tenders.json' (ListEnv $tendersOut)
Write-Host "Skrev $($projectsOut.Count) projekter, $($tendersOut.Count) udbud, $($companies.Count) firmaer til $OutDir (variant $Variant)"
