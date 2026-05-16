# Move AudioManager from Foundation to Presentation.
$ErrorActionPreference = 'Stop'

$srcDir  = 'Scripts/EssSystem/Core/Foundation/AudioManager'
$dstDir  = 'Scripts/EssSystem/Core/Presentation/AudioManager'
$srcMeta = "$srcDir.meta"
$dstMeta = "$dstDir.meta"

Write-Host "git mv $srcDir -> $dstDir"
git mv -- $srcDir  $dstDir
git mv -- $srcMeta $dstMeta

# Namespace replacement (single rule -> ArrayList to prevent flattening).
$replacements = [System.Collections.ArrayList]@()
[void]$replacements.Add(@('EssSystem.Core.Foundation.AudioManager', 'EssSystem.Core.Presentation.AudioManager'))

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
