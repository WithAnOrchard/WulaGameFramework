# Audit cross-Application dependencies.
# For each Application Manager X, list which OTHER Application Manager namespaces are imported.
$ErrorActionPreference = 'Stop'

$root = 'Scripts/EssSystem/Core/Application'
# (group, manager) — managers live under SingleManagers/ or MultiManagers/.
$managers = @(
    @('SingleManagers','InventoryManager'),
    @('SingleManagers','EntityManager'),
    @('SingleManagers','DialogueManager'),
    @('MultiManagers','MapManager'),
    @('MultiManagers','BuildingManager'),
    @('MultiManagers','SkillManager')
)

$results = [ordered]@{}
foreach ($pair in $managers) {
    $group = $pair[0]; $m = $pair[1]
    $deps = [ordered]@{}
    $files = Get-ChildItem -Path "$root/$group/$m" -Recurse -File -Filter *.cs -ErrorAction SilentlyContinue
    foreach ($f in $files) {
        $content = [System.IO.File]::ReadAllText($f.FullName, [System.Text.Encoding]::UTF8)
        $lines = $content -split "`r?`n"
        foreach ($line in $lines) {
            if ($line -match '^\s*using\s+EssSystem\.Core\.Application\.(?:SingleManagers|MultiManagers)\.(\w+)') {
                $target = $matches[1]
                if ($target -ne $m) {
                    if (-not $deps.Contains($target)) { $deps[$target] = 0 }
                    $deps[$target]++
                }
            }
        }
    }
    $results[$m] = $deps
}

Write-Host "===== Cross-Application Manager dependency audit ====="
Write-Host ""
foreach ($pair in $managers) {
    $group = $pair[0]; $m = $pair[1]
    $deps = $results[$m]
    $label = "$group/$m"
    if ($deps.Count -eq 0) {
        Write-Host ("{0,-40}  ->  (none — does NOT depend on any other Application Manager)" -f $label) -ForegroundColor Green
    } else {
        $depList = ($deps.GetEnumerator() | ForEach-Object { "$($_.Key)[$($_.Value)]" }) -join ', '
        Write-Host ("{0,-40}  ->  {1}" -f $label, $depList) -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Legend: 'X[n]' means n using statements referencing namespace EssSystem.Core.Application.X.*"
