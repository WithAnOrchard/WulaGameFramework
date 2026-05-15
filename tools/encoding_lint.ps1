#!/usr/bin/env pwsh
# ----------------------------------------------------------------------
# encoding_lint.ps1 — 拦截非 UTF-8 文件和已损坏字符
#
# 检测两类问题：
#   1. 文件字节序列不是合法 UTF-8（常见于 PS 5.1 的 Set-Content 把 UTF-8
#      文件按 GB18030 重写后产生的破损字节）
#   2. 文件含有 U+FFFD 替代字符（已经发生过编码转换丢失的痕迹）
#
# 用法：
#   pwsh tools/encoding_lint.ps1            # 扫描全工程
#   pwsh tools/encoding_lint.ps1 -Strict    # 同上 + 失败时 exit 1（用于 pre-commit）
# ----------------------------------------------------------------------
param(
    [switch]$Strict
)

$ErrorActionPreference = 'Stop'
$repoRoot = (& git rev-parse --show-toplevel 2>$null)
if ($LASTEXITCODE -ne 0) { $repoRoot = (Get-Location).Path }
Set-Location $repoRoot

$extensions = @('*.cs', '*.md', '*.json', '*.shader', '*.cginc', '*.hlsl', '*.txt')
$utf8Strict = New-Object System.Text.UTF8Encoding -ArgumentList $false, $true  # throwOnInvalidBytes=true

$invalidUtf8 = New-Object System.Collections.ArrayList
$hasReplacementChar = New-Object System.Collections.ArrayList
$total = 0

foreach ($ext in $extensions) {
    foreach ($f in (Get-ChildItem -Path "Assets","Demo","Scripts","Resources" -Recurse -Filter $ext -ErrorAction SilentlyContinue)) {
        if ($null -eq $f.FullName) { continue }
        if ($f.FullName -match '\\Library\\|\\Temp\\|\\obj\\|\\bin\\') { continue }
        $total++
        try {
            $bytes = [System.IO.File]::ReadAllBytes($f.FullName)
            $text = $utf8Strict.GetString($bytes)
            if ($text.Contains([char]0xFFFD)) {
                [void]$hasReplacementChar.Add($f.FullName)
            }
        } catch {
            [void]$invalidUtf8.Add($f.FullName)
        }
    }
}

Write-Host "[encoding_lint] Scanned $total files"

$exitCode = 0
if ($invalidUtf8.Count -gt 0) {
    Write-Host "`n[FAIL] 检测到 $($invalidUtf8.Count) 个非 UTF-8 文件（很可能被 PS 5.1 的 Set-Content 用系统 codepage 重写过）:" -ForegroundColor Red
    foreach ($p in $invalidUtf8) {
        Write-Host "  $($p.Replace($repoRoot + '\', ''))" -ForegroundColor Red
    }
    $exitCode = 1
}
if ($hasReplacementChar.Count -gt 0) {
    Write-Host "`n[WARN] 检测到 $($hasReplacementChar.Count) 个文件含 U+FFFD 替代字符（疑似编码转换破坏后残留）:" -ForegroundColor Yellow
    foreach ($p in $hasReplacementChar) {
        Write-Host "  $($p.Replace($repoRoot + '\', ''))" -ForegroundColor Yellow
    }
    if ($Strict) { $exitCode = 1 }
}

if ($exitCode -eq 0) {
    Write-Host "[OK] All files are valid UTF-8 without replacement chars`n" -ForegroundColor Green
} else {
    Write-Host "`n[encoding_lint] 修复建议:" -ForegroundColor Cyan
    Write-Host "  - 用 PowerShell 7+ (pwsh) 而非 PS 5.1 (powershell)"
    Write-Host "  - 批量改文件用：[System.IO.File]::ReadAllText(path, [System.Text.UTF8Encoding]::new(`$false))"
    Write-Host "                 + [System.IO.File]::WriteAllText(path, content, [System.Text.UTF8Encoding]::new(`$false))"
    Write-Host "  - 禁止用 Get-Content -Raw / Set-Content 直接写回（PS 5.1 默认用系统 codepage）"
    if ($Strict) { exit $exitCode }
}
