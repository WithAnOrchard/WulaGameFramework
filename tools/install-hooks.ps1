# ----------------------------------------------------------------------
# install-hooks.ps1 -- copy versioned hooks into .git/hooks/
#
# Run once after clone:
#   .\tools\install-hooks.ps1
# ----------------------------------------------------------------------
[CmdletBinding()]
param([switch]$Force)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$srcDir      = Join-Path $projectRoot 'tools\git-hooks'
$dstDir      = Join-Path $projectRoot '.git\hooks'

if (-not (Test-Path $dstDir)) {
    Write-Host "[install-hooks] $dstDir not found; not a git working tree?" -ForegroundColor Red
    exit 1
}

$hooks = Get-ChildItem $srcDir -File -ErrorAction Stop
if ($hooks.Count -eq 0) {
    Write-Host "[install-hooks] No hooks under $srcDir" -ForegroundColor Yellow
    exit 0
}

foreach ($h in $hooks) {
    $dst = Join-Path $dstDir $h.Name
    if ((Test-Path $dst) -and -not $Force) {
        $existing = Get-Content $dst -Raw -ErrorAction SilentlyContinue
        $incoming = Get-Content $h.FullName -Raw
        if ($existing -eq $incoming) {
            Write-Host "[install-hooks] $($h.Name) already up-to-date" -ForegroundColor DarkGray
            continue
        }
        Write-Host "[install-hooks] $($h.Name) exists and differs; pass -Force to overwrite" -ForegroundColor Yellow
        continue
    }
    Copy-Item $h.FullName $dst -Force
    # Best-effort: mark executable on git-bash side (no-op on pure NTFS but harmless)
    try { & git update-index --chmod=+x $dst 2>$null } catch {}
    Write-Host "[install-hooks] installed $($h.Name)" -ForegroundColor Green
}

Write-Host ''
Write-Host '[install-hooks] Done. pre-commit will now run agent_lint.ps1 -Strict.' -ForegroundColor Cyan
Write-Host '                Bypass once with: git commit --no-verify' -ForegroundColor DarkGray
