# ============================================================
# WulaGameFramework Unity Compile Checker
#
# Uses dotnet build on Unity's .csproj files for real compilation errors.
# Output is split into three sections:
#   1) Errors   (error CS*)   - red, listed in full
#   2) Warnings (warning CS*) - grouped by CS code, first N per code + total
#   3) Summary  - error count / warning count / final verdict
#
# Usage:
#   .\tools\check_compile_errors.ps1                  # BOTH (runtime + editor, 默认 —— 不要漏掉 Editor 文件的错)
#   .\tools\check_compile_errors.ps1 -MainOnly        # 只查 runtime (Assembly-CSharp)
#   .\tools\check_compile_errors.ps1 -EditorOnly      # 只查 editor (Assembly-CSharp-Editor)
#   .\tools\check_compile_errors.ps1 -Silent          # Suppress warning output
#   .\tools\check_compile_errors.ps1 -TopN 5          # Max lines per warning code (default 3)
#   .\tools\check_compile_errors.ps1 -Strict          # Non-zero exit if any warning exists
# ============================================================

[CmdletBinding()]
param(
    [switch]$MainOnly,
    [switch]$EditorOnly,
    [switch]$Silent,
    [switch]$Strict,
    [switch]$IncludeCs0649,
    [int]   $TopN = 3
)

$ErrorActionPreference = 'Continue'
# $PSScriptRoot is tools/ directory under Assets, go up to Assets then to project root
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$CompileCheckRoot = Join-Path ([System.IO.Path]::GetTempPath()) 'WulaGameFrameworkCompileCheck'

# --- Helpers -----------------------------------------------------------
function Format-Line {
    param($line)
    # Strip leading whitespace from dotnet output
    return ($line -replace '^\s+', '')
}

function Summarize-Build {
    param($output)

    $errors   = @($output | Select-String -Pattern '\berror (?:(CS|MSB)\d+\b|:)'   | ForEach-Object { Format-Line $_.Line })
    $warnings = @($output | Select-String -Pattern '\bwarning (CS|MSB)\d+\b' | ForEach-Object { Format-Line $_.Line })

    $errCount  = $errors.Count
    $warnCount = $warnings.Count

    # Group warnings by CS code
    $warnByCode = @{}
    foreach ($w in $warnings) {
        if ($w -match 'warning ((?:CS|MSB)\d+):') {
            $code = $matches[1]
            if (-not $warnByCode.ContainsKey($code)) { $warnByCode[$code] = @() }
            $warnByCode[$code] += $w
        }
    }

    return [pscustomobject]@{
        Errors     = $errors
        Warnings   = $warnings
        ErrCount   = $errCount
        WarnCount  = $warnCount
        WarnByCode = $warnByCode
    }
}

function Print-Summary {
    param($summary)

    Write-Host ""
    Write-Host "--- Errors ($($summary.ErrCount)) ---" -ForegroundColor Red
    if ($summary.ErrCount -eq 0) {
        Write-Host "  (none)" -ForegroundColor Gray
    }
    else {
        $shown = [Math]::Min($TopN, $summary.Errors.Count)
        for ($i = 0; $i -lt $shown; $i++) {
            Write-Host "  $($summary.Errors[$i])" -ForegroundColor Red
        }
        if ($summary.Errors.Count -gt $shown) {
            Write-Host "  ... (+$($summary.Errors.Count - $shown) more, see all with -TopN 999)" -ForegroundColor DarkGray
        }
    }

    if (-not $Silent) {
        Write-Host ""
        Write-Host "--- Warnings ($($summary.WarnCount)) grouped by code ---" -ForegroundColor Yellow
        if ($summary.WarnCount -eq 0) {
            Write-Host "  (none)" -ForegroundColor Gray
        }
        else {
            $i = 0
            foreach ($code in ($summary.WarnByCode.Keys | Sort-Object)) {
                $items = $summary.WarnByCode[$code]
                $i++
                Write-Host ""
                Write-Host "  [$i] $code x $($items.Count)" -ForegroundColor Yellow
                $shown = [Math]::Min($TopN, $items.Count)
                for ($k = 0; $k -lt $shown; $k++) {
                    Write-Host "      $($items[$k])" -ForegroundColor DarkYellow
                }
                if ($items.Count -gt $shown) {
                    Write-Host "      ... (+$($items.Count - $shown) more, see all with -TopN 999)" -ForegroundColor DarkGray
                }
            }
        }
    }
}

function Test-Compile($csprojPath, $name) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Compiling: $name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan

    $csprojFullPath = Join-Path $ProjectRoot $csprojPath

    if (-not (Test-Path $csprojFullPath)) {
        Write-Host "  [SKIP] $csprojFullPath not found" -ForegroundColor Gray
        return [pscustomobject]@{ OK = $true;  ErrCount = 0; WarnCount = 0 }
    }

    Push-Location $ProjectRoot
    $workRoot = $CompileCheckRoot
    $objRoot = Join-Path $workRoot 'obj'
    $binRoot = Join-Path $workRoot 'bin'
    New-Item -ItemType Directory -Force -Path $objRoot, $binRoot | Out-Null
    $safeName = $name -replace '[^\w.-]', '_'
    $objPath = ((Join-Path $objRoot $safeName) -replace '\\', '/') + '/'
    $binPath = ((Join-Path $binRoot $safeName) -replace '\\', '/') + '/'

    $output = dotnet build $csprojPath `
        "/p:BaseIntermediateOutputPath=$objPath" `
        "/p:BaseOutputPath=$binPath" `
        "/p:OutputPath=$binPath" 2>&1
    $exitCode = $LASTEXITCODE
    Pop-Location

    $summary = Summarize-Build $output

    if ($exitCode -eq 0) {
        Write-Host "  [OK] Build succeeded" -ForegroundColor Green
    }
    else {
        Write-Host "  [FAIL] Build failed" -ForegroundColor Red
    }

    Print-Summary $summary

    $ok = $exitCode -eq 0
    if ($Strict -and $summary.WarnCount -gt 0) { $ok = $false }

    return [pscustomobject]@{
        OK        = $ok
        ErrCount  = $summary.ErrCount
        WarnCount = $summary.WarnCount
    }
}

# --- Static analysis (CS0414 / CS0168 / CS0649) ------------------------
# Unity csproj via dotnet build only does IDE intellisense and does NOT
# emit real "usage" warnings (private field assigned but never read,
# local declared but never used, etc.). Those require Unity Editor to
# actually compile. This tool provides a lightweight Roslyn-style
# heuristic pass to surface them without Unity.
#
# Detected:
#   CS0414 - field assigned but never read
#   CS0168 - local variable declared + assigned but never read
#   CS0649 - field never assigned (csproj has NoWarn=0649; reported here for ref)
function Invoke-StaticAnalysis {
    param($root)

    $findings = @()
    $csFiles  = @(Get-ChildItem -Path $root -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue |
                 Where-Object { $_.FullName -notmatch '\\Library\\|\\Temp\\|\\obj\\|\\bin\\' })

    foreach ($f in $csFiles) {
        $lines = [System.IO.File]::ReadAllLines($f.FullName)

        # Build a "cleaned" whole-file string for global search (strip line comments
        # and string literals to avoid false matches on _x in comments / strings).
        $cleaned = ($lines -join "`n")
        $cleaned = [regex]::Replace($cleaned, '"(?:\\.|[^"\\])*"', '""')
        $cleaned = [regex]::Replace($cleaned, '/\*[\s\S]*?\*/', '')
        $cleaned = [regex]::Replace($cleaned, '//[^\n]*', '')

        # --- CS0414 / CS0649: per-line private field scan ---
        # Pattern matches a single LINE that ends with a private/internal field decl.
        # Anchoring to single line prevents \s* from jumping to the next line's ';'.
        $fieldLinePat = '^(?<lead>\s*(?:\[[^\]]*\]\s*)*)(?<vis>private|internal|protected\s+internal)\s+(?<mods>(?:static\s+|readonly\s+|const\s+)*)(?<type>[\w<>,\s\[\]\?]+?)\s+(?<name>_[\w]+)(?<init>(?:\s*=[^;]+)?)[ \t]*;[ \t]*(?://.*)?$'
        for ($li = 0; $li -lt $lines.Count; $li++) {
            $lineText = $lines[$li]
            $lm = [regex]::Match($lineText, $fieldLinePat)
            if (-not $lm.Success) { continue }

            $name  = $lm.Groups['name'].Value
            $init  = $lm.Groups['init'].Value
            $lead  = $lm.Groups['lead'].Value
            $mods  = $lm.Groups['mods'].Value
            $type  = $lm.Groups['type'].Value
            $vis   = $lm.Groups['vis'].Value

            # Skip delegates / events / UnityEvent (reflection usage)
            if ($type -match '\bevent\b|\bAction\b|\bFunc\b|\bUnityEvent\b|\bDelegate\b') { continue }
            if ($mods -match 'const\b') { continue }
            # Skip if the lead section contains HideInInspector / Obsolete (intentional)
            if ($lead -match 'HideInInspector|Obsolete') { continue }

            $hasInit = ($init -ne '')

            # Count usages of $name in the WHOLE file (excluding the declaration line)
            $refPat = '(?<![A-Za-z0-9_])' + [regex]::Escape($name) + '(?![A-Za-z0-9_])'
            $allRefs = @([regex]::Matches($cleaned, $refPat))
            $useCount = $allRefs.Count - 1   # -1 for the declaration

            if ($useCount -le 0) {
                $code = if ($hasInit) { 'CS0414' } else { 'CS0649' }
                if ($code -eq 'CS0649' -and -not $IncludeCs0649) { continue }
                $msg  = if ($hasInit) {
                    "The field '$name' is assigned but its value is never used"
                } else {
                    "Field '$name' is never assigned to, and will always have its default value"
                }
                $findings += [pscustomobject]@{
                    File    = $f.FullName
                    Line    = $li + 1
                    Code    = $code
                    Name    = $name
                    Message = $msg
                }
            }
        }

        # --- CS0168: local variable declared + assigned but never read ---
        $localPat = '^\s*(?:var|[A-Z]\w*(?:<[^>]+>)?)\s+(_[a-z]\w*)\s*=\s*[^;]+;\s*$'
        for ($li = 0; $li -lt $lines.Count; $li++) {
            $lineText = $lines[$li]
            $mm = [regex]::Match($lineText, $localPat)
            if (-not $mm.Success) { continue }
            $name = $mm.Groups[1].Value
            $rest = ($lines[0..($li-1)] + $lines[($li+1)..($lines.Count-1)]) -join "`n"
            if (-not [regex]::IsMatch($rest, '(?<![A-Za-z0-9_])' + [regex]::Escape($name) + '(?![A-Za-z0-9_])')) {
                $findings += [pscustomobject]@{
                    File    = $f.FullName
                    Line    = $li + 1
                    Code    = 'CS0168'
                    Name    = $name
                    Message = "The variable '$name' is declared but its value is never used"
                }
            }
        }
    }

    return $findings
}

function Print-StaticFindings {
    param($findings)

    if ($Silent) { return }
    if ($findings.Count -eq 0) {
        Write-Host "  (none)" -ForegroundColor Gray
        return
    }

    # 按代码分组（用 List 避免 PS hashtable += 的 NullArray 坑）
    $byCode = @{}
    foreach ($x in $findings) {
        if (-not $byCode.ContainsKey($x.Code)) {
            $byCode[$x.Code] = New-Object System.Collections.Generic.List[object]
        }
        $byCode[$x.Code].Add($x) | Out-Null
    }

    $i = 0
    foreach ($code in ($byCode.Keys | Sort-Object)) {
        $items = $byCode[$code]
        $i++
        Write-Host ""
        Write-Host "  [$i] $code x $($items.Count)" -ForegroundColor Yellow
        $shown = [Math]::Min($TopN, $items.Count)
        for ($k = 0; $k -lt $shown; $k++) {
            $x = $items[$k]
            $rel = $x.File.Replace($ProjectRoot + '\', '')
            Write-Host "      $rel($($x.Line)): $($x.Message)" -ForegroundColor DarkYellow
        }
        if ($items.Count -gt $shown) {
            Write-Host "      ... (+$($items.Count - $shown) more, see all with -TopN 999)" -ForegroundColor DarkGray
        }
    }
}

# --- Main flow ---------------------------------------------------------
# 默认两个 assembly 都查 —— Editor 文件（UnityEditor.* API）的错误必须查，
# 否则会漏掉 #if UNITY_EDITOR 块里的 CS0234 / CS0104 / CS0117 等问题。
# 用户可显式 -MainOnly / -EditorOnly 限制范围。
$mainResult   = $null
$editorResult = $null

if (-not $EditorOnly) {
    $mainResult = Test-Compile "Assembly-CSharp.csproj" "Assembly-CSharp (Main)"
}
if (-not $MainOnly) {
    $editorResult = Test-Compile "Assembly-CSharp-Editor.csproj" "Assembly-CSharp-Editor (Editor)"
}

# 防止下游把 $null 当 [pscustomobject] 用（result.OK / .ErrCount）
if ($null -eq $mainResult)   { $mainResult   = [pscustomobject]@{ OK = $true; ErrCount = 0; WarnCount = 0 } }
if ($null -eq $editorResult) { $editorResult = [pscustomobject]@{ OK = $true; ErrCount = 0; WarnCount = 0 } }

# Static analysis (CS0414 / CS0168 / CS0649)
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Static analysis (CS0414 / CS0168 / CS0649)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
$staticFindings = @(Invoke-StaticAnalysis -root (Join-Path $ProjectRoot 'Assets'))
Print-StaticFindings $staticFindings

# Dump findings to JSON for downstream processing
if ($staticFindings.Count -gt 0) {
    $jsonPath = Join-Path $CompileCheckRoot 'static_findings.json'
    $staticFindings | ConvertTo-Json -Depth 5 | Set-Content -Path $jsonPath -Encoding UTF8
    Write-Host ""
    Write-Host "  [INFO] findings dumped to: $jsonPath" -ForegroundColor DarkCyan
}

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "Summary" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White
Write-Host ("  Compile    : errors={0,-3}  warnings={1,-3}" -f ($mainResult.ErrCount + $editorResult.ErrCount), ($mainResult.WarnCount + $editorResult.WarnCount)) -ForegroundColor White
Write-Host ("  Static     : findings={0,-3}" -f $staticFindings.Count) -ForegroundColor White
$totalErr     = $mainResult.ErrCount + $editorResult.ErrCount
$totalCompileWarn = $mainResult.WarnCount + $editorResult.WarnCount
$totalStaticWarn  = $staticFindings.Count
Write-Host ("  TOTAL      : errors={0,-3}  warnings={1,-3}" -f $totalErr, ($totalCompileWarn + $totalStaticWarn)) -ForegroundColor White

$allOk = $mainResult.OK -and $editorResult.OK
if ($Strict -and ($totalCompileWarn + $totalStaticWarn) -gt 0) { $allOk = $false }

if ($allOk) {
    Write-Host ""
    Write-Host "All compilations passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host ""
    Write-Host "Some compilations failed!" -ForegroundColor Red
    exit 1
}
