# Update Markdown docs: textual 'Gameplay' references -> 'Application'.
# Note: scoped to .md files only; .cs already handled by previous script.
$ErrorActionPreference = 'Stop'

$replacements = [System.Collections.ArrayList]@()
[void]$replacements.Add(@('Gameplay/', 'Application/'))
[void]$replacements.Add(@('`Gameplay`', '`Application`'))
# Plain word boundary cases in prose:
[void]$replacements.Add(@('Gameplay 业务', 'Application 业务'))
[void]$replacements.Add(@('Gameplay 模块', 'Application 模块'))
[void]$replacements.Add(@('Gameplay/World', 'Application/World'))
[void]$replacements.Add(@('Gameplay/Systems', 'Application/Systems'))
[void]$replacements.Add(@(' Gameplay ', ' Application '))
[void]$replacements.Add(@('Gameplay**', 'Application**'))
[void]$replacements.Add(@('**Gameplay', '**Application'))
[void]$replacements.Add(@('Foundation                Presentation               Gameplay',
                          'Foundation                Presentation               Application'))

foreach ($r in $replacements) {
    Write-Host ("rule: '{0}' -> '{1}'" -f $r[0], $r[1])
}

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
        $changed++
        Write-Host "fixed: $path"
    }
}

Write-Host ""
Write-Host "Done. $changed .md file(s) updated."
