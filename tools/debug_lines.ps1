$f = 'Scripts\EssSystem\Core\Presentation\UIManager\Agent.md'
$lines = Get-Content $f -Encoding UTF8
for ($i = 87; $i -le 96; $i++) {
    $l = $lines[$i]
    $len = if ($null -eq $l) { 0 } else { $l.Length }
    Write-Host ('[' + $len + '] ' + $l)
}
