# tools/cleanup_orphan_fences.ps1
#
# Remove orphan "````" lines (4-backtick fences with no language tag and no
# matching opening/closing) left behind by cleanup_agent_code_blocks.ps1 when
# the original csharp block was wrapped in a 4-backtick outer fence.

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "Scripts") "EssSystem"
if (-not (Test-Path $root)) { $root = "Assets\Scripts\EssSystem" }

# Skip framework-level Agent.md (layer docs at EssSystem/Core/<Layer>/Agent.md)
$frameworkLayers = @("Base", "Platform", "Foundation", "Presentation", "Application")

$updated = 0
$agentFiles = Get-ChildItem -Path $root -Recurse -Filter "Agent.md" | Sort-Object FullName

foreach ($file in $agentFiles) {
    $rel = $file.FullName.Substring((Resolve-Path $root).Path.Length).TrimStart('\', '/')
    $parts = $rel -split '[\\/]'

    $isFrameworkDoc = $false
    if ($parts.Count -eq 2 -and $parts[0] -eq "Core" -and $parts[1] -eq "Agent.md") {
        $isFrameworkDoc = $true
    } elseif ($parts.Count -eq 3 -and $parts[0] -eq "Core" -and $frameworkLayers -contains $parts[1] -and $parts[2] -eq "Agent.md") {
        $isFrameworkDoc = $true
    } elseif ($parts.Count -eq 4 -and $parts[0] -eq "Core" -and $parts[1] -eq "Base" -and $parts[2] -eq "Manager" -and $parts[3] -eq "Agent.md") {
        $isFrameworkDoc = $true
    }
    if ($isFrameworkDoc) { continue }

    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $original = $content

    # Remove any line that is exactly 4+ backticks (with optional trailing whitespace).
    # Use [char]96 to avoid PowerShell backtick-escape mangling.
    $bt = [char]96
    $content = [regex]::Replace($content, "(?m)^${bt}{4,}\s*\r?\n", "")

    # Collapse runs of 3+ blank lines down to 2
    $content = [regex]::Replace($content, "(\r?\n){4,}", "`r`n`r`n`r`n")

    if ($content -ne $original) {
        if ($DryRun) {
            Write-Host "[DRY]  $($file.FullName)" -ForegroundColor Yellow
        } else {
            Set-Content -Path $file.FullName -Value $content -Encoding UTF8 -NoNewline
            Write-Host "[OK]   $($file.FullName)" -ForegroundColor Green
        }
        $updated++
    }
}

Write-Host ""
Write-Host "Total: $updated files updated" -ForegroundColor Cyan
