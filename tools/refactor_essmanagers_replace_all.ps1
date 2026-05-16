# Combined Phase B + corrected Phase C: 8 namespace replacements in one safe pass.
# Uses ArrayList to prevent PowerShell single-element array flattening.
$ErrorActionPreference = 'Stop'

$replacements = [System.Collections.ArrayList]@()
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.CharacterManager', 'EssSystem.Core.EssManagers.Presentation.CharacterManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.MapManager',       'EssSystem.Core.EssManagers.Gameplay.World.MapManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.EntityManager',    'EssSystem.Core.EssManagers.Gameplay.World.EntityManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.BuildingManager',  'EssSystem.Core.EssManagers.Gameplay.World.BuildingManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.InventoryManager', 'EssSystem.Core.EssManagers.Gameplay.Systems.InventoryManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.DialogueManager',  'EssSystem.Core.EssManagers.Gameplay.Systems.DialogueManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Gameplay.SkillManager',     'EssSystem.Core.EssManagers.Gameplay.Systems.SkillManager'))
[void]$replacements.Add(@('EssSystem.Core.EssManagers.Manager',                   'EssSystem.Core.Base.Manager'))

# Sanity-check: every entry must be a 2-element string array.
foreach ($r in $replacements) {
    if ($r -isnot [object[]] -or $r.Count -ne 2 -or $r[0] -isnot [string] -or $r[1] -isnot [string]) {
        throw "Bad replacement entry: $r"
    }
    Write-Host ("rule: '{0}' -> '{1}'" -f $r[0], $r[1])
}

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
        $changed++
    }
}

Write-Host ""
Write-Host "Done. $changed file(s) updated."
