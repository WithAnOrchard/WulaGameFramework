# tools/cleanup_agent_code_blocks.ps1
#
# Remove stray csharp code blocks from module-level Agent.md files
# Per root Agent.md rule #5:  module Agent.md must NOT contain code usage examples.
#
# Behavior:
# 1. For every Agent.md that has a `## Event API` section (i.e. module-level doc):
# 2. Remove every ```csharp ... ``` fenced block.
# 3. If the line immediately before a removed block is a "lead-in" line ending with
#    `：` or `:`, also remove that line (it was introducing the now-deleted code).
# 4. Save back the file.
#
# Framework-level Agent.md (Core/Base/Platform/Foundation/Presentation/Application
# layer docs) are SKIPPED - they describe framework architecture and may legitimately
# include attribute / base-class / register-example code.
#
# Run:  powershell -ExecutionPolicy Bypass -File tools/cleanup_agent_code_blocks.ps1 [-DryRun]

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "Scripts") "EssSystem"
if (-not (Test-Path $root)) { $root = "Assets\Scripts\EssSystem" }

# Framework-level Agent.md layer docs to skip
$frameworkLayers = @("Base", "Platform", "Foundation", "Presentation", "Application")

$updated = 0
$skipped = 0

$agentFiles = Get-ChildItem -Path $root -Recurse -Filter "Agent.md" | Sort-Object FullName

foreach ($file in $agentFiles) {
    $rel = $file.FullName.Substring((Resolve-Path $root).Path.Length).TrimStart('\', '/')
    $parts = $rel -split '[\\/]'

    # Skip framework-level Agent.md:
    #  - EssSystem/Core/Agent.md
    #  - EssSystem/Core/<Layer>/Agent.md  (e.g. Core/Base/Agent.md)
    #  - EssSystem/Core/Base/Manager/Agent.md  (BaseManager pattern doc)
    $isFrameworkDoc = $false
    if ($parts.Count -eq 2 -and $parts[0] -eq "Core" -and $parts[1] -eq "Agent.md") {
        $isFrameworkDoc = $true
    } elseif ($parts.Count -eq 3 -and $parts[0] -eq "Core" -and $frameworkLayers -contains $parts[1] -and $parts[2] -eq "Agent.md") {
        $isFrameworkDoc = $true
    } elseif ($parts.Count -eq 4 -and $parts[0] -eq "Core" -and $parts[1] -eq "Base" -and $parts[2] -eq "Manager" -and $parts[3] -eq "Agent.md") {
        $isFrameworkDoc = $true
    }

    if ($isFrameworkDoc) {
        $skipped++
        Write-Host "[SKIP] $($file.FullName)  (framework layer doc)" -ForegroundColor DarkGray
        continue
    }

    # Also skip if file does not contain `## Event API` section (not a module doc)
    $peek = Get-Content $file.FullName -Raw -Encoding UTF8
    if ($peek -notmatch '(?m)^##\s+Event\s+API\b') {
        $skipped++
        Write-Host "[SKIP] $($file.FullName)  (no ## Event API section)" -ForegroundColor DarkGray
        continue
    }

    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $original = $content

    # 1) Remove fenced csharp blocks (with optional preceding "lead-in" line ending with : or :)
    # We loop until no more matches, to handle adjacent blocks.
    $changed = $false
    do {
        $prev = $content
        # Remove a lead-in line (## heading OR plain text line ending with : or :) immediately before a csharp fence
        $content = [regex]::Replace($content, "(?m)^.*[：:]\s*\r?\n```csharp[\s\S]*?```\s*\r?\n?", "", 1)
        # Remove remaining csharp blocks (without lead-in)
        $content = [regex]::Replace($content, "```csharp[\s\S]*?```\s*\r?\n?", "", 1)
        if ($content -ne $prev) { $changed = $true }
    } while ($content -ne $prev -and $content -match '```csharp')

    # 2) Remove now-empty section headers: `## Foo\n\n` followed by another `## Bar` or EOF
    $content = [regex]::Replace($content, "(?m)^##\s+[^\r\n]+\r?\n\r?\n(?=##\s|\z)", "")

    # 3) Collapse 3+ blank lines down to 2
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
Write-Host "Total: $updated files updated, $skipped framework-layer docs skipped" -ForegroundColor Cyan
