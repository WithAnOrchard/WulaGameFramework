# tools/cleanup_event_api.ps1
#
# Batch-simplify all module Agent.md "## Event API" sections.
# Per root Agent.md project rule #5: "module Agent.md must NOT detail code usage;
# Events.md is the single source of truth."
#
# Behavior:
# 1. Scan all Agent.md under Assets\Scripts\EssSystem
# 2. Locate the "## Event API" section (until next "## " heading)
# 3. Extract every "ClassName.EVT_XXX" and bare "EVT_XXX" constant
# 4. Replace the section with a minimal list:
#      ## Event API
#      > See Events.md chapter for full definitions.
#      - Manager.EVT_XXX
#      ...
#
# Run:  powershell -ExecutionPolicy Bypass -File tools/cleanup_event_api.ps1 [-DryRun]

[CmdletBinding()]
param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$root = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "Scripts") "EssSystem"
if (-not (Test-Path $root)) { $root = "Assets\Scripts\EssSystem" }

# Map: Agent.md parent folder name -> Events.md section display name
$displayNameMap = @{
    "UIManager"           = "UIManager Event"
    "AudioManager"        = "AudioManager Event"
    "InputManager"        = "InputManager Event"
    "EffectsManager"      = "EffectsManager Event"
    "CameraManager"       = "CameraManager Event"
    "CharacterManager"    = "CharacterManager Event"
    "LightManager"        = "LightManager Event"
    "InventoryManager"    = "InventoryManager Event"
    "EntityManager"       = "EntityManager Event"
    "DialogueManager"     = "DialogueManager Event"
    "BuildingManager"     = "BuildingManager Event"
    "FarmManager"         = "FarmManager Event"
    "ShopManager"         = "ShopManager Event"
    "SkillManager"        = "SkillManager Event"
    "NetworkManager"      = "NetworkManager Event"
    "ResourceManager"     = "ResourceManager Event (40+) - facade constants"
    "AutoUpdateManager"   = "AutoUpdateManager Event"
    "LiveStatusManager"   = "LiveStatusManager"
    "DanmuManager"        = "BilibiliDanmuManager"
    "TextureService"      = "ResourceManager Event - TextureService"
    "SpriteService"       = "ResourceManager Event - SpriteService"
    "RuleTileService"     = "ResourceManager Event - RuleTileService"
    "PrefabService"       = "ResourceManager Event - PrefabService"
    "MaterialService"     = "ResourceManager Event - MaterialService"
    "ExternalImageService"= "ResourceManager Event - ExternalImageService"
    "AudioClipService"    = "ResourceManager Event - AudioClipService"
    "AnimationClipService"= "ResourceManager Event - AnimationClipService"
    "ModelAnimationService"="ResourceManager Event - ModelAnimationService"
    "SpriteSheetService"  = "ResourceManager Event - SpriteSheetService"
    "MapManager"          = "MapManager (pure C# API, no EVT_*)"
    "SceneInstanceManager"= "SceneInstanceManager (skeleton, no EVT_*)"
    "NpcManager"          = "NpcManager (skeleton, no EVT_*)"
    "CraftingManager"     = "CraftingManager (skeleton, no EVT_*)"
}

$updated = 0
$skipped = 0

$agentFiles = Get-ChildItem -Path $root -Recurse -Filter "Agent.md" | Sort-Object FullName

foreach ($file in $agentFiles) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $lines = $content -split "`r?`n"

    $startIdx = -1
    $endIdx = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^##\s+Event\s+API\b') {
            $startIdx = $i
        } elseif ($startIdx -ge 0 -and $i -gt $startIdx -and $lines[$i] -match '^##\s+') {
            $endIdx = $i
            break
        }
    }

    if ($startIdx -lt 0) { $skipped++; continue }
    if ($endIdx -lt 0)   { $endIdx = $lines.Count }

    # Extract EVT_XXX constants. Prefer ClassName.EVT_XXX form; fall back to bare EVT_XXX.
    $prefixedSet = [System.Collections.Generic.HashSet[string]]::new()
    $bareSet     = [System.Collections.Generic.HashSet[string]]::new()
    for ($i = $startIdx; $i -lt $endIdx; $i++) {
        $line = $lines[$i]
        $prefixed = [regex]::Matches($line, '\b([A-Z][A-Za-z]+(?:Manager|Service))\.EVT_[A-Z0-9_]+\b')
        foreach ($m in $prefixed) { [void]$prefixedSet.Add($m.Value) }
        $bare = [regex]::Matches($line, '\b(EVT_[A-Z0-9_]+)\b')
        foreach ($m in $bare) { [void]$bareSet.Add($m.Value) }
    }
    # Emit ClassName.EVT_XXX when available; emit bare EVT_XXX only when no prefix seen.
    $shortNames = @{}
    foreach ($p in $prefixedSet) {
        $short = ($p -split '\.')[1]
        $shortNames[$short] = $true
    }
    $evtNames = New-Object System.Collections.Generic.List[string]
    foreach ($p in ($prefixedSet | Sort-Object)) { [void]$evtNames.Add($p) }
    foreach ($b in ($bareSet | Sort-Object)) {
        if (-not $shortNames.ContainsKey($b)) { [void]$evtNames.Add($b) }
    }
    $sorted = $evtNames

    $moduleName = $file.Directory.Name
    $displayName = $displayNameMap[$moduleName]
    if (-not $displayName) { $displayName = "$moduleName Event" }

    $newSection = New-Object System.Collections.Generic.List[string]
    [void]$newSection.Add("## Event API")
    [void]$newSection.Add("")
    if ($sorted.Count -eq 0) {
        [void]$newSection.Add("> No events exposed. See root `Events.md` for the canonical list.")
        [void]$newSection.Add("")
    } else {
        [void]$newSection.Add("> Full Event definitions (params / return / side effects / usage) live in root `Events.md` -> section: **$displayName**.")
        [void]$newSection.Add("")
        foreach ($e in $sorted) {
            [void]$newSection.Add("- ``$e``")
        }
        [void]$newSection.Add("")
    }

    $before = $lines[0..($startIdx - 1)]
    $after = @()
    if ($endIdx -lt $lines.Count) {
        $after = $lines[$endIdx..($lines.Count - 1)]
    }
    $newLines = @()
    $newLines += $before
    $newLines += $newSection
    $newLines += $after
    $newContent = $newLines -join "`n"

    if ($DryRun) {
        Write-Host "[DRY] $($file.FullName) : $($sorted.Count) events" -ForegroundColor Yellow
    } else {
        Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8 -NoNewline
        Write-Host "[OK]  $($file.FullName) : $($sorted.Count) events" -ForegroundColor Green
    }
    $updated++
}

Write-Host ""
Write-Host "Total: $updated files updated, $skipped skipped" -ForegroundColor Cyan
