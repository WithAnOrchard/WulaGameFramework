# Phase B: replace 7 namespace prefixes across all .cs and .md files.
# Preserves UTF-8 BOM presence per file. Run from Assets/.
$ErrorActionPreference = 'Stop'

$replacements = @(
    @('EssSystem.Core.EssManagers.Gameplay.CharacterManager', 'EssSystem.Core.EssManagers.Presentation.CharacterManager'),
    @('EssSystem.Core.EssManagers.Gameplay.MapManager',       'EssSystem.Core.EssManagers.Gameplay.World.MapManager'),
    @('EssSystem.Core.EssManagers.Gameplay.EntityManager',    'EssSystem.Core.EssManagers.Gameplay.World.EntityManager'),
    @('EssSystem.Core.EssManagers.Gameplay.BuildingManager',  'EssSystem.Core.EssManagers.Gameplay.World.BuildingManager'),
    @('EssSystem.Core.EssManagers.Gameplay.InventoryManager', 'EssSystem.Core.EssManagers.Gameplay.Systems.InventoryManager'),
    @('EssSystem.Core.EssManagers.Gameplay.DialogueManager',  'EssSystem.Core.EssManagers.Gameplay.Systems.DialogueManager'),
    @('EssSystem.Core.EssManagers.Gameplay.SkillManager',     'EssSystem.Core.EssManagers.Gameplay.Systems.SkillManager')
)

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$utf8Bom   = New-Object System.Text.UTF8Encoding $true

$files = @()
$files += Get-ChildItem -Path . -Recurse -File -Filter *.cs
$files += Get-ChildItem -Path . -Recurse -File -Filter *.md

$changed = 0
foreach ($f in $files) {
    $path  = $f.FullName
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $enc = if ($hasBom) { $utf8Bom } else { $utf8NoBom }

    $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $orig    = $content
    foreach ($r in $replacements) { $content = $content.Replace($r[0], $r[1]) }

    if ($content -ne $orig) {
        [System.IO.File]::WriteAllText($path, $content, $enc)
        Write-Host "fixed: $path"
        $changed++
    }
}

Write-Host ""
Write-Host "Phase B done. $changed file(s) updated."
