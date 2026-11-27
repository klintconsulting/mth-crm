<#
.SYNOPSIS
  Demo data for the Resource Overview (development environment only, synthetic data). Re-runnable.
   - Spreads the case team roles (mth_opportunityrole) over the demo users round-robin, sets Allocation % by role, and clears
     Start/End Date so plugin ResourceDefaults fills them from the opportunity's tender or order dates.
   - Splits one tender role in two periods (100 % then 50 %) to show several periods for the same person and role.
   - Tender department membership (N:N mth_orgunit_systemuser_TenderDept): every user in at least one, one user in two.
   - Absence (mth_availability): vacation, leave and reduced capacity. Rows from earlier runs (description "[demo]") are replaced.

.EXAMPLE
  .\scripts\New-ResourceTestData.ps1 -Url https://orgxxxx.crm4.dynamics.com
#>
param([Parameter(Mandatory)][string]$Url)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Dataverse.psm1') -Force
Connect-Dataverse -Url $Url
$fv = '@OData.Community.Display.V1.FormattedValue'
$annot = @{ Prefer = 'odata.include-annotations="*"' }

$users = (Invoke-Dataverse GET "systemusers?`$select=systemuserid,fullname&`$filter=isdisabled eq false and accessmode eq 0&`$orderby=fullname").value
if ($users.Count -lt 2) { throw 'Need at least two active users (New-DemoUsers.ps1)' }
Write-Host "Users: $($users.fullname -join ', ')"

# Allocation % by role (tender roles share their time between several tenders)
$alloc = @{ 273280000 = 30; 273280002 = 50; 273280003 = 40; 273280004 = 60; 273280005 = 40; 273280006 = 30
            273280001 = 100; 273280007 = 50; 273280008 = 10; 273280009 = 20 }

# 1. Roles: round-robin users, allocation by role, dates from the opportunity (plugin)
$roles = (Invoke-Dataverse GET "mth_opportunityroles?`$select=mth_opportunityroleid,mth_role&`$filter=statecode eq 0&`$orderby=createdon").value
$i = 0
foreach ($r in $roles) {
    $u = $users[$i % $users.Count]; $i++
    [void](Invoke-Dataverse PATCH "mth_opportunityroles($($r.mth_opportunityroleid))" @{
        'mth_UserId@odata.bind' = "/systemusers($($u.systemuserid))"; mth_allocation = $alloc[[int]$r.mth_role]; mth_startdate = $null; mth_enddate = $null })
}
Write-Host "  ~ $($roles.Count) roles spread over $($users.Count) users, dates from the opportunities"

# 2. One split example: the first tender calculator role with at least four weeks: 100 % first half, 50 % second half
$calc = (Invoke-Dataverse GET "mth_opportunityroles?`$select=mth_opportunityroleid,mth_startdate,mth_enddate,_mth_userid_value,_mth_opportunityid_value&`$filter=mth_role eq 273280004 and statecode eq 0 and mth_startdate ne null and mth_enddate ne null&`$orderby=mth_startdate desc").value |
    Where-Object { ([datetime]$_.mth_enddate - [datetime]$_.mth_startdate).TotalDays -ge 28 } | Select-Object -First 1
if ($calc) {
    $s = [datetime]$calc.mth_startdate; $e = [datetime]$calc.mth_enddate; $mid = $s.AddDays([math]::Floor(($e - $s).TotalDays / 2))
    [void](Invoke-Dataverse PATCH "mth_opportunityroles($($calc.mth_opportunityroleid))" @{ mth_enddate = $mid.AddDays(-1).ToString('yyyy-MM-dd'); mth_allocation = 100 })
    [void](Invoke-Dataverse POST 'mth_opportunityroles' @{ mth_role = 273280004; mth_startdate = $mid.ToString('yyyy-MM-dd'); mth_enddate = $e.ToString('yyyy-MM-dd'); mth_allocation = 50
        'mth_UserId@odata.bind' = "/systemusers($($calc._mth_userid_value))"; 'mth_OpportunityId@odata.bind' = "/mth_opportunities($($calc._mth_opportunityid_value))" })
    Write-Host "  + split tender calculator role: 100 % until $($mid.AddDays(-1).ToString('yyyy-MM-dd')), 50 % from $($mid.ToString('yyyy-MM-dd'))"
}

# 3. Tender department membership
$depts = (Invoke-Dataverse GET "mth_organizationalunits?`$select=mth_organizationalunitid,mth_name&`$filter=mth_istenderdepartment eq true&`$orderby=mth_name").value
if ($depts) {
    $j = 0
    foreach ($u in $users) {
        $targets = @($depts[$j % $depts.Count]); if ($j -eq 1 -and $depts.Count -gt 1) { $targets += $depts[($j + 1) % $depts.Count] }; $j++
        foreach ($d in $targets) {
            try { [void](Invoke-Dataverse POST "mth_organizationalunits($($d.mth_organizationalunitid))/mth_orgunit_systemuser_TenderDept/`$ref" @{ '@odata.id' = "$Url/api/data/v9.2/systemusers($($u.systemuserid))" }) }
            catch { if ("$_" -notmatch 'duplicate|already|Cannot insert duplicate') { throw } }
            Write-Host "  + $($u.fullname) in $($d.mth_name)"
        }
    }
}

# 4. Absence (relative to today, so the overview always has something to show)
(Invoke-Dataverse GET "mth_availabilities?`$select=mth_availabilityid&`$filter=startswith(mth_description,'[demo]')").value | ForEach-Object { [void](Invoke-Dataverse DELETE "mth_availabilities($($_.mth_availabilityid))") }
$mon = (Get-Date).Date.AddDays(-(([int](Get-Date).DayOfWeek + 6) % 7))
function Absence($user, $type, $fromWeeks, $lengthDays, $capacity, $text) {
    $body = @{ mth_type = $type; mth_startdate = $mon.AddDays(7 * $fromWeeks).ToString('yyyy-MM-dd'); mth_enddate = $mon.AddDays(7 * $fromWeeks + $lengthDays - 1).ToString('yyyy-MM-dd')
               mth_description = "[demo] $text"; 'mth_UserId@odata.bind' = "/systemusers($($user.systemuserid))" }
    if ($null -ne $capacity) { $body.mth_capacity = $capacity }
    [void](Invoke-Dataverse POST 'mth_availabilities' $body)
    Write-Host "  + $($user.fullname): $text"
}
Absence $users[0] 273280000 4 12 $null 'Juleferie'
Absence $users[1] 273280001 10 14 $null 'Orlov to uger'
if ($users.Count -gt 2) { Absence $users[2] 273280002 2 56 60 'Nedsat tid 60 % i otte uger' }
Absence $users[$users.Count - 1] 273280000 13 5 $null 'Vinterferie'
Write-Host 'Done.'
