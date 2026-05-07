# ----------------------------------------------------------------------
# agent_lint.ps1 -- WulaGameFramework / EssSystem doc-vs-code consistency
#
# Extends check_event_docs.ps1 to cover full Agent.md rules:
#
#   [1] Bare-string [Event("...")] in definition side -- forbidden
#   [2] Collect EVT_XXX const definitions (literal + alias)
#   [3] Each EVT must be referenced in its module's Agent.md
#   [4] Each module folder containing [Event] must have Agent.md with
#       a "## Event API" section
#   [5] Assets/Agent.md global Event index must list every public EVT
#   [6] Consumer-side bare-strings ([EventListener] / TriggerEventMethod /
#       TriggerEvent / HasListener) must reference a known EVT value
#       (per design rule §4.1: 跨模块调用走字符串协议)
#
# Usage:
#   .\tools\agent_lint.ps1                # report only
#   .\tools\agent_lint.ps1 -Strict        # fail (exit 1) on any issue
#   .\tools\agent_lint.ps1 -Verbose       # dump every constant
# ----------------------------------------------------------------------

[CmdletBinding()]
param([switch]$Strict)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$scriptsRoot = Join-Path $projectRoot 'Scripts'
$rootAgent   = Join-Path $projectRoot 'Agent.md'

$errors   = New-Object System.Collections.ArrayList
$warnings = New-Object System.Collections.ArrayList

function Add-Err  ([string]$m) { [void]$errors.Add($m) }
function Add-Warn ([string]$m) { [void]$warnings.Add($m) }
function RelPath  ([string]$abs) { $abs.Substring($projectRoot.Length + 1) -replace '\\', '/' }

# ----------------------------------------------------------------------
# [1] Bare-string [Event(...)] / [EventListener(...)]
# ----------------------------------------------------------------------
Write-Host '[1/6] Scanning for bare-string [Event(...)] (definition side)...'
$cs = Get-ChildItem $scriptsRoot -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '\\obj\\|\\bin\\' }

# Strip C# comments (block /* */, line //, XML doc ///) before scanning.
function Remove-CSharpComments ([string]$src) {
    # block comments
    $s = [regex]::Replace($src, '/\*.*?\*/', '', 'Singleline')
    # line comments (// and ///) -- consume to end-of-line
    $s = [regex]::Replace($s, '//[^\r\n]*', '')
    return $s
}

# Find class containing a given character index (nearest preceding class declaration)
function Resolve-ClassAt ([string]$src, [int]$idx) {
    $sub = $src.Substring(0, $idx)
    $cm = [regex]::Matches($sub, '\bclass\s+(\w+)')
    if ($cm.Count -eq 0) { return '<unknown>' }
    return $cm[$cm.Count - 1].Groups[1].Value
}

# Definition side: [Event("...")] must use const. [EventListener("...")] is
# allowed (consumer side, validated in step [6] against the known EVT pool).
foreach ($f in $cs) {
    $stripped = Remove-CSharpComments (Get-Content $f.FullName -Raw)
    # Negative lookahead so 'EventListener' doesn't match 'Event'
    $bare = [regex]::Matches($stripped, '\[Event(?!Listener)\("([^"]+)"')
    foreach ($m in $bare) {
        Add-Err "  [ERR] $(RelPath $f.FullName) :: bare-string [Event(""$($m.Groups[1].Value)"")] (definition must use const)"
    }
}

# ----------------------------------------------------------------------
# [2] Collect EVT_XXX constants (literal + alias)
# ----------------------------------------------------------------------
Write-Host '[2/6] Collecting EVT_XXX constants...'
$constMap   = @{}    # FQN "Class.EVT_NAME" -> @{ Name; Value; File; Class; IsAlias }
$valueToFqn = @{}    # value -> ArrayList of FQN (collision detection)

foreach ($f in $cs) {
    $stripped = Remove-CSharpComments (Get-Content $f.FullName -Raw)

    # literal: public const string EVT_XXX = "value";
    foreach ($m in [regex]::Matches($stripped, 'public\s+const\s+string\s+(EVT_\w+)\s*=\s*"([^"]+)"\s*;')) {
        $name  = $m.Groups[1].Value
        $value = $m.Groups[2].Value
        $className = Resolve-ClassAt $stripped $m.Index
        $fqn   = "$className.$name"
        $constMap[$fqn] = @{
            Name = $name; Value = $value; File = $f.FullName
            Class = $className; IsAlias = $false
        }
        if (-not $valueToFqn.ContainsKey($value)) {
            $valueToFqn[$value] = New-Object System.Collections.ArrayList
        }
        [void]$valueToFqn[$value].Add($fqn)
    }

    # alias: public const string EVT_XXX = OtherClass.EVT_YYY;
    foreach ($m in [regex]::Matches($stripped, 'public\s+const\s+string\s+(EVT_\w+)\s*=\s*(\w+)\.(EVT_\w+)\s*;')) {
        $name = $m.Groups[1].Value
        $className = Resolve-ClassAt $stripped $m.Index
        $fqn  = "$className.$name"
        $constMap[$fqn] = @{
            Name = $name
            Value = "(alias->$($m.Groups[2].Value).$($m.Groups[3].Value))"
            File = $f.FullName; Class = $className; IsAlias = $true
        }
    }
}

Write-Host "    Found $($constMap.Count) EVT_XXX constants"
if ($VerbosePreference -eq 'Continue') {
    foreach ($k in ($constMap.Keys | Sort-Object)) {
        $v = $constMap[$k]
        Write-Verbose ("    {0,-55} = {1}" -f $k, $v.Value)
    }
}

# Same-string-multi-source warning (potential _eventMethods key collision)
foreach ($val in $valueToFqn.Keys) {
    $list = $valueToFqn[$val]
    if ($list.Count -gt 1) {
        Add-Warn "  [WARN] same string '$val' declared by multiple constants: $($list -join ', ') (single handler dict, last wins)"
    }
}

# ----------------------------------------------------------------------
# [3] Each EVT referenced in its module's Agent.md
# ----------------------------------------------------------------------
Write-Host '[3/6] Checking each EVT is mentioned in module Agent.md...'
foreach ($fqn in $constMap.Keys) {
    $info = $constMap[$fqn]
    if ($info.IsAlias) { continue }

    $moduleDir = Split-Path -Parent $info.File
    $moduleAgent = Join-Path $moduleDir 'Agent.md'
    if (-not (Test-Path $moduleAgent)) {
        Add-Err "  [ERR] $fqn :: module missing Agent.md (expected $(RelPath $moduleDir)/Agent.md)"
        continue
    }
    $modContent = Get-Content $moduleAgent -Raw
    if ($modContent -notmatch [regex]::Escape($info.Name)) {
        Add-Err "  [ERR] $fqn :: $(RelPath $moduleAgent) does not mention constant name $($info.Name)"
    }
}

# ----------------------------------------------------------------------
# [4] Each module containing [Event] must have Agent.md with ## Event API
# ----------------------------------------------------------------------
Write-Host '[4/6] Checking module Agent.md has ## Event API section...'
$moduleDirs = @{}
foreach ($f in $cs) {
    $content = Get-Content $f.FullName -Raw
    if ($content -match '\[Event\(' -or $content -match '\[EventListener\(') {
        $d = Split-Path -Parent $f.FullName
        $moduleDirs[$d] = $true
    }
}
foreach ($d in $moduleDirs.Keys) {
    $a = Join-Path $d 'Agent.md'
    if (-not (Test-Path $a)) {
        Add-Err "  [ERR] $(RelPath $d) :: contains [Event] but no Agent.md"
        continue
    }
    $aContent = Get-Content $a -Raw
    if ($aContent -notmatch '(?m)^##\s+Event API') {
        Add-Warn "  [WARN] $(RelPath $a) :: missing '## Event API' section"
    }
}

# ----------------------------------------------------------------------
# [5] Assets/Agent.md global Event index coverage
# ----------------------------------------------------------------------
Write-Host '[5/6] Checking Assets/Agent.md global Event index...'
if (-not (Test-Path $rootAgent)) {
    Add-Err '  [ERR] Assets/Agent.md not found'
} else {
    $rootContent = Get-Content $rootAgent -Raw
    foreach ($fqn in $constMap.Keys) {
        $info = $constMap[$fqn]
        if ($info.IsAlias) { continue }
        # Allow generic suffix in docs, e.g. "Service<T>.EVT_INITIALIZED" matches FQN "Service.EVT_INITIALIZED"
        $pattern = [regex]::Escape($info.Class) + '(?:<[^>]+>)?\.' + [regex]::Escape($info.Name)
        if ($rootContent -notmatch $pattern) {
            Add-Warn "  [WARN] Assets/Agent.md global index missing: $fqn (= '$($info.Value)')"
        }
    }
}

# ----------------------------------------------------------------------
# [6] Consumer-side bare-strings cross-ref against known EVT values
# ----------------------------------------------------------------------
# Per rule §4.1: cross-module callers/listeners must use bare strings
# (no `using` of foreign modules just to read EVT_X constants). The string
# must, however, correspond to a real EVT_XXX value declared somewhere -- this
# catches typos and stale event names without requiring `using` decoupling.
Write-Host '[6/6] Cross-ref consumer-side bare-strings against known EVT values...'

# Build value -> FQN-list map already exists ($valueToFqn). Build a fast lookup.
$knownValues = @{}
foreach ($val in $valueToFqn.Keys) { $knownValues[$val] = $true }

# Patterns to scan (all consumer-side bare-string usages).
$consumerPatterns = @(
    '\[EventListener\("([^"]+)"',
    'TriggerEventMethod\(\s*"([^"]+)"',
    'TriggerEvent\(\s*"([^"]+)"',
    'HasListener\(\s*"([^"]+)"'
)

foreach ($f in $cs) {
    $stripped = Remove-CSharpComments (Get-Content $f.FullName -Raw)
    foreach ($pat in $consumerPatterns) {
        foreach ($m in [regex]::Matches($stripped, $pat)) {
            $val = $m.Groups[1].Value
            if (-not $knownValues.ContainsKey($val)) {
                Add-Err "  [ERR] $(RelPath $f.FullName) :: bare-string `"$val`" does not match any EVT_XXX const value"
            }
        }
    }
}

# ----------------------------------------------------------------------
# Summary
# ----------------------------------------------------------------------
Write-Host ''
Write-Host '========================================='
if ($errors.Count -eq 0 -and $warnings.Count -eq 0) {
    Write-Host '[OK] All checks passed' -ForegroundColor Green
    exit 0
}

if ($errors.Count -gt 0) {
    Write-Host "[FAIL] $($errors.Count) error(s):" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host $_ -ForegroundColor Red }
}
if ($warnings.Count -gt 0) {
    if ($errors.Count -gt 0) { Write-Host '' }
    Write-Host "[WARN] $($warnings.Count) warning(s):" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host $_ -ForegroundColor Yellow }
}

if ($Strict -and ($errors.Count -gt 0 -or $warnings.Count -gt 0)) { exit 1 }
if ($errors.Count -gt 0) { exit 1 }
exit 0
