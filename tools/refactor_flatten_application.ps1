# Flatten Application/World/* and Application/Systems/* up to Application/.
$ErrorActionPreference = 'Stop'

$root = 'Scripts/EssSystem/Core/Application'

function GitMv($src, $dst) {
    Write-Host "git mv $src -> $dst"
    git mv -- $src $dst
}

# Move 3 modules out of World/
foreach ($name in 'MapManager','EntityManager','BuildingManager') {
    GitMv "$root/World/$name"      "$root/$name"
    GitMv "$root/World/$name.meta" "$root/$name.meta"
}

# Move 3 modules out of Systems/
foreach ($name in 'InventoryManager','DialogueManager','SkillManager') {
    GitMv "$root/Systems/$name"      "$root/$name"
    GitMv "$root/Systems/$name.meta" "$root/$name.meta"
}

# Remove empty World/ and Systems/ + their meta siblings
git rm -- "$root/World.meta"
git rm -- "$root/Systems.meta"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$root/World"
Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "$root/Systems"

# Namespace replacement
$replacements = [System.Collections.ArrayList]@()
[void]$replacements.Add(@('EssSystem.Core.Application.World.',   'EssSystem.Core.Application.'))
[void]$replacements.Add(@('EssSystem.Core.Application.Systems.', 'EssSystem.Core.Application.'))

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
