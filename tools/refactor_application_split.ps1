# Split Application/ into SingleManagers/ (no cross-Application deps) and MultiManagers/ (depends on others).
$ErrorActionPreference = 'Stop'

$root = 'Scripts/EssSystem/Core/Application'

function GitMv($src, $dst) {
    Write-Host "git mv $src -> $dst"
    git mv -- $src $dst
}

New-Item -ItemType Directory -Force -Path "$root/SingleManagers" | Out-Null
New-Item -ItemType Directory -Force -Path "$root/MultiManagers"  | Out-Null

# SingleManagers: no cross-Application dependency
foreach ($name in 'InventoryManager','EntityManager','DialogueManager') {
    GitMv "$root/$name"      "$root/SingleManagers/$name"
    GitMv "$root/$name.meta" "$root/SingleManagers/$name.meta"
}

# MultiManagers: depends on Application Managers (currently all depend on EntityManager)
foreach ($name in 'MapManager','BuildingManager','SkillManager') {
    GitMv "$root/$name"      "$root/MultiManagers/$name"
    GitMv "$root/$name.meta" "$root/MultiManagers/$name.meta"
}

# Namespace replacements (per-manager prefix, prevents flattening with ArrayList).
$replacements = [System.Collections.ArrayList]@()
[void]$replacements.Add(@('EssSystem.Core.Application.InventoryManager', 'EssSystem.Core.Application.SingleManagers.InventoryManager'))
[void]$replacements.Add(@('EssSystem.Core.Application.EntityManager',    'EssSystem.Core.Application.SingleManagers.EntityManager'))
[void]$replacements.Add(@('EssSystem.Core.Application.DialogueManager',  'EssSystem.Core.Application.SingleManagers.DialogueManager'))
[void]$replacements.Add(@('EssSystem.Core.Application.MapManager',       'EssSystem.Core.Application.MultiManagers.MapManager'))
[void]$replacements.Add(@('EssSystem.Core.Application.BuildingManager',  'EssSystem.Core.Application.MultiManagers.BuildingManager'))
[void]$replacements.Add(@('EssSystem.Core.Application.SkillManager',     'EssSystem.Core.Application.MultiManagers.SkillManager'))

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
