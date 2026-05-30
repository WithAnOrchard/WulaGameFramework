# ============================================================
# WulaGameFramework Unity Compile Checker
# 
# Uses dotnet build on Unity's .csproj files for real compilation errors
#
# Usage:
#   .\tools\check_compile_errors.ps1              # Check main assembly
#   .\tools\check_compile_errors.ps1 -Editor      # Check editor assembly
#   .\tools\check_compile_errors.ps1 -Both        # Check both
# ============================================================

[CmdletBinding()]
param(
    [switch]$Editor,
    [switch]$Both,
    [switch]$ShowWarnings
)

$ErrorActionPreference = 'Continue'
# $PSScriptRoot is tools/ directory under Assets, go up to Assets then to project root
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

function Test-Compile($csprojPath, $name) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Compiling: $name" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    
    $csprojFullPath = Join-Path $ProjectRoot $csprojPath
    
    if (-not (Test-Path $csprojFullPath)) {
        Write-Host "  [SKIP] $csprojFullPath not found" -ForegroundColor Gray
        return $true
    }
    
    Push-Location $ProjectRoot
    
    $output = dotnet build $csprojPath 2>&1
    $exitCode = $LASTEXITCODE
    
    Pop-Location
    
    if ($exitCode -eq 0) {
        Write-Host "  [OK] Build succeeded" -ForegroundColor Green
        
        if ($ShowWarnings) {
            $output | Select-String -Pattern "warning" | ForEach-Object {
                Write-Host "    $($_.Line)" -ForegroundColor Yellow
            }
        }
        return $true
    }
    else {
        Write-Host "  [FAIL] Build failed with errors:" -ForegroundColor Red
        
        $output | Select-String -Pattern "error CS" | ForEach-Object {
            Write-Host "    $($_.Line)" -ForegroundColor Red
        }
        
        return $false
    }
}

$mainResult = Test-Compile "Assembly-CSharp.csproj" "Assembly-CSharp (Main)"

$editorResult = $true
if ($Editor -or $Both) {
    $editorResult = Test-Compile "Assembly-CSharp-Editor.csproj" "Assembly-CSharp-Editor (Editor)"
}

Write-Host ""
Write-Host "========================================" -ForegroundColor White
Write-Host "Summary" -ForegroundColor White
Write-Host "========================================" -ForegroundColor White

if ($mainResult -and $editorResult) {
    Write-Host "All compilations passed!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "Some compilations failed!" -ForegroundColor Red
    exit 1
}
