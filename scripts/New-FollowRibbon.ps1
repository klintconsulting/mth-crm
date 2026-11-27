<#
.SYNOPSIS
  Writes the classic ribbon buttons "Follow" and "Unfollow" into RibbonDiff.xml for Opportunity, Lead and Account in
  the unpacked solution (solutions/MTHCRM/src/Entities/<table>/RibbonDiff.xml). Only the button that fits is shown:
  the enable rules Mth.Follow.canFollow / canUnfollow (webresources/mth_/scripts/follow.js) hide the other one.

  Why classic ribbon and not a modern command (appaction): a modern command has a fixed label and can only be hidden
  with a Power Fx formula (command component library), which cannot be created through the Web API.
  Ribbon changes only reach Dataverse through a solution import, so afterwards:

    dotnet build solutions/MTHCRM
    pac solution import --path solutions/MTHCRM/bin/Debug/MTHCRM.zip --publish-changes
    .\scripts\Deploy-Plugins.ps1 -Url <org>      # a solution import can leave async plugin steps disabled

.EXAMPLE
  .\scripts\New-FollowRibbon.ps1
#>
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$tables = [ordered]@{ mth_opportunity = 'mth_Opportunity'; mth_lead = 'mth_Lead'; account = 'Account' }
$lib = '$webresource:mth_/scripts/follow.js'
$icon = '$webresource:mth_/icons/follow.svg'

function Button($table, $kind, $label, $tooltip, $seq) {
@"
    <CustomAction Id="mth.$table.$kind.CustomAction" Location="Mscrm.Form.$table.MainTab.Save.Controls._children" Sequence="$seq">
      <CommandUIDefinition>
        <Button Id="mth.$table.$kind.Button" Command="mth.$table.$kind.Command" Sequence="$seq" LabelText="$label" Alt="$label" ToolTipTitle="$label" ToolTipDescription="$tooltip" TemplateAlias="o1" Image16by16="$icon" Image32by32="$icon" ModernImage="$icon" />
      </CommandUIDefinition>
    </CustomAction>
"@
}
function Command($table, $kind, $function, $rule) {
@"
    <CommandDefinition Id="mth.$table.$kind.Command">
      <EnableRules>
        <EnableRule Id="mth.$table.$rule" />
      </EnableRules>
      <DisplayRules />
      <Actions>
        <JavaScriptFunction FunctionName="$function" Library="$lib">
          <CrmParameter Value="PrimaryControl" />
        </JavaScriptFunction>
      </Actions>
    </CommandDefinition>
"@
}
function Rule($table, $rule, $function) {
@"
      <EnableRule Id="mth.$table.$rule">
        <CustomRule FunctionName="$function" Library="$lib" Default="false">
          <CrmParameter Value="PrimaryControl" />
        </CustomRule>
      </EnableRule>
"@
}

foreach ($t in $tables.Keys) {
    $file = Join-Path $root "solutions/MTHCRM/src/Entities/$($tables[$t])/RibbonDiff.xml"
    if (-not (Test-Path $file)) { throw "Missing $file - run pac solution sync first" }
    $xml = @"
<?xml version="1.0" encoding="utf-8"?>
<RibbonDiffXml xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CustomActions>
$(Button $t 'Follow' 'Follow' 'Follow to get a notification (bell icon) when key fields change or a note is added.' 25)
$(Button $t 'Unfollow' 'Unfollow' 'Stop following. You will no longer be notified about changes and notes.' 26)
  </CustomActions>
  <Templates>
    <RibbonTemplates Id="Mscrm.Templates"></RibbonTemplates>
  </Templates>
  <CommandDefinitions>
$(Command $t 'Follow' 'Mth.Follow.follow' 'CanFollow')
$(Command $t 'Unfollow' 'Mth.Follow.unfollow' 'CanUnfollow')
  </CommandDefinitions>
  <RuleDefinitions>
    <TabDisplayRules />
    <DisplayRules />
    <EnableRules>
$(Rule $t 'CanFollow' 'Mth.Follow.canFollow')
$(Rule $t 'CanUnfollow' 'Mth.Follow.canUnfollow')
    </EnableRules>
  </RuleDefinitions>
  <LocLabels />
</RibbonDiffXml>
"@
    [xml]$xml | Out-Null   # validate
    [IO.File]::WriteAllText($file, $xml, (New-Object Text.UTF8Encoding($false)))
    Write-Host "  ~ $file"
}
