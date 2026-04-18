# ============================================================
# WulaGameFramework compile check — thin wrapper over unity-compile skill
#
# Delegates to ~/.codeium/windsurf/skills/unity-compile/unity_compile.ps1
# with this project's default excludes (UGUI-dependent files under UIManager\).
#
# Fallback: if the skill is not installed, emit an install hint.
# ============================================================
[CmdletBinding()]
param(
    [string]$UnityDataDir = $null,
    [string]$EditorVersion = $null
)

$ErrorActionPreference = 'Stop'
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SkillScript = Join-Path $env:USERPROFILE '.codeium\windsurf\skills\unity-compile\unity_compile.ps1'

if (-not (Test-Path $SkillScript)) {
    Write-Host "[!] unity-compile skill not installed at: $SkillScript" -ForegroundColor Red
    Write-Host "    Install it, or copy this file to a self-contained script." -ForegroundColor Gray
    exit 2
}

$fwdArgs = @{
    ProjectPath  = $ProjectRoot
    ExcludeRegex = '\\UIManager\\'   # UGUI source-only package; validate via Unity Editor
}
if ($UnityDataDir)  { $fwdArgs.UnityDataDir  = $UnityDataDir }
if ($EditorVersion) { $fwdArgs.EditorVersion = $EditorVersion }

& $SkillScript @fwdArgs
exit $LASTEXITCODE
