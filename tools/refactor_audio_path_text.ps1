# Update text references "Foundation/AudioManager" -> "Presentation/AudioManager" in markdown.
$ErrorActionPreference = 'Stop'

$replacements = [System.Collections.ArrayList]@()
[void]$replacements.Add(@('Foundation/AudioManager', 'Presentation/AudioManager'))

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$utf8Bom   = New-Object System.Text.UTF8Encoding $true

$files = Get-ChildItem -Path . -Recurse -File -Filter *.md

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
Write-Host "Done. $changed file(s)."
