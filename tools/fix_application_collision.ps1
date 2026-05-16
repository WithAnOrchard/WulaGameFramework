# Fix UnityEngine.Application name collision caused by EssSystem.Core.Application namespace.
# In files where the enclosing namespace contains 'Application', the bare token 'Application'
# resolves to our namespace, shadowing UnityEngine.Application. Fix by qualifying.
$ErrorActionPreference = 'Stop'

# Properties of UnityEngine.Application that can collide. None of these names exist as
# sub-namespaces in our hierarchy, so qualified rewrite is safe.
$members = 'isPlaying','isEditor','persistentDataPath','dataPath','streamingAssetsPath',
           'temporaryCachePath','version','productName','companyName','platform',
           'isMobilePlatform','systemLanguage','targetFrameRate','runInBackground',
           'quitting','focusChanged','logMessageReceived','wantsToQuit'

# Negative lookbehind: don't match if preceded by '.' or word char (i.e., already qualified
# like 'UnityEngine.Application' or 'MyClass.Application').
$pattern = '(?<![\w.])Application\.(' + ($members -join '|') + ')\b'

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$utf8Bom   = New-Object System.Text.UTF8Encoding $true

# Any file under EssSystem.Core.* has the collision: when the compiler resolves bare
# 'Application', it walks up parent namespaces and finds sibling 'EssSystem.Core.Application'
# before reaching 'using UnityEngine;'. Scan the entire Core/ tree.
$files = Get-ChildItem -Path 'Scripts/EssSystem/Core' -Recurse -File -Filter *.cs

$changed = 0
foreach ($f in $files) {
    $path  = $f.FullName
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $enc = if ($hasBom) { $utf8Bom } else { $utf8NoBom }

    $content = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    $new = [System.Text.RegularExpressions.Regex]::Replace($content, $pattern, 'UnityEngine.Application.$1')

    if ($new -ne $content) {
        [System.IO.File]::WriteAllText($path, $new, $enc)
        Write-Host "fixed: $path"
        $changed++
    }
}

Write-Host ""
Write-Host "Done. $changed file(s) updated."
