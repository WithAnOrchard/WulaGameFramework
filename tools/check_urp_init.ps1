# Check-URPInit.ps1
# 体检项目 URP 初始化状态：7 项关键指标

Write-Host "`n========== URP 初始化体检 ==========" -ForegroundColor Cyan

# 1) Package installation
$manifestPath = Join-Path (Split-Path $PSScriptRoot -Parent) "Packages\manifest.json"
$pkgInstalled = $false
if (Test-Path $manifestPath) {
    $content = Get-Content $manifestPath -Raw
    $pkgInstalled = $content.Contains('"com.unity.render-pipelines.universal"')
}
$status1 = if ($pkgInstalled) { "OK" } else { "MISS" }
Write-Host "[1] URP package 安装:              $($status1.PadRight(6)) $(if($pkgInstalled){'com.unity.render-pipelines.universal 已声明'}else{'manifest.json 缺少 com.unity.render-pipelines.universal'})" -ForegroundColor $(if($pkgInstalled){'Green'}else{'Red'})

# 2) URP_INSTALLED define symbol - check Unity generated files
$csprojDirs = Get-ChildItem -Path (Split-Path $PSScriptRoot -Parent) -Recurse -Filter "*.csproj" -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*Assembly-CSharp*" }
$defineSet = $false
foreach ($csproj in $csprojDirs) {
    if (Select-String -Path $csproj.FullName -Pattern "URP_INSTALLED" -Quiet) {
        $defineSet = $true
        break
    }
}
$status2 = if ($defineSet) { "OK" } else { "WARN" }
Write-Host "[2] URP_INSTALLED 编译符号:        $($status2.PadRight(6)) $(if($defineSet){'已设置，LightManager URP 分支会编译'}else{'未设置，LightManager 走 stub，RegisterLight 等事件会 fail'})" -ForegroundColor $(if($defineSet){'Green'}else{'Yellow'})

# 3) URP Asset
$urpAsset = Join-Path (Split-Path $PSScriptRoot -Parent) "Assets\Settings\URP\URP-Default.asset"
$urpAssetExists = Test-Path $urpAsset
$status3 = if ($urpAssetExists) { "OK" } else { "MISS" }
Write-Host "[3] URP Asset 文件:                $($status3.PadRight(6)) $urpAsset $(if($urpAssetExists){''}else{'不存在'})" -ForegroundColor $(if($urpAssetExists){'Green'}else{'Red'})

# 4) Renderer Data
$forwardRenderer = Join-Path (Split-Path $PSScriptRoot -Parent) "Assets\Settings\URP\URP-ForwardRenderer.asset"
$renderer2D = Join-Path (Split-Path $PSScriptRoot -Parent) "Assets\Settings\URP\URP-2DRenderer.asset"
$hasForward = Test-Path $forwardRenderer
$has2D = Test-Path $renderer2D
$status4 = if ($hasForward -or $has2D) { "OK" } else { "MISS" }
Write-Host "[4] Renderer Data 文件:            $($status4.PadRight(6)) 3D=$hasForward / 2D=$has2D" -ForegroundColor $(if($hasForward -or $has2D){'Green'}else{'Red'})

# 5) GraphicsSettings (read from ProjectSettings/GraphicsSettings.asset YAML)
$graphicsSettingsPath = Join-Path (Split-Path $PSScriptRoot -Parent) "ProjectSettings\GraphicsSettings.asset"
$graphicsRP = $null
if (Test-Path $graphicsSettingsPath) {
    $gcontent = Get-Content $graphicsSettingsPath -Raw
    $match = [regex]::Match($gcontent, 'm_CustomRenderPipeline:\s*\{fileID:\s*(\d+)\}')
    if ($match.Success) {
        $graphicsRP = $match.Groups[1].Value
    }
}
$status5 = if ($graphicsRP -and $graphicsRP -ne "0") { "OK" } else { "MISS" }
Write-Host "[5] Graphics.defaultRenderPipeline: $($status5.PadRight(6)) $(if($graphicsRP -and $graphicsRP -ne '0'){"fileID=$graphicsRP"}else{'未设置 m_CustomRenderPipeline，URP 不会接管渲染'})" -ForegroundColor $(if($status5 -eq 'OK'){'Green'}else{'Red'})

# 6) QualitySettings (per quality level)
$qualitySettingsPath = Join-Path (Split-Path $PSScriptRoot -Parent) "ProjectSettings\QualitySettings.asset"
$qualityCount = 0
$qualityWithRP = 0
if (Test-Path $qualitySettingsPath) {
    $qcontent = Get-Content $qualitySettingsPath -Raw
    $qualityMatches = [regex]::Matches($qcontent, '-?\s*name:\s*[A-Z]\w+')
    $qualityCount = $qualityMatches.Count
    $rpMatches = [regex]::Matches($qcontent, 'renderPipeline:\s*\{fileID:\s*\d+\}')
    $qualityWithRP = $rpMatches.Count
}
$status6 = if ($qualityWithRP -ge 1) { "OK" } else { "MISS" }
Write-Host "[6] QualitySettings renderPipeline: $($status6.PadRight(6)) 已有 $qualityWithRP / $qualityCount 个 Quality Level 绑定 RP Asset" -ForegroundColor $(if($status6 -eq 'OK'){'Green'}else{'Red'})

# 7) LightManager URP block compiled (search for URP namespace usage)
$lmPath = Join-Path (Split-Path $PSScriptRoot -Parent) "Assets\Scripts\EssSystem\Core\Presentation\LightManager\LightManager.cs"
$urpBlockCompiles = $false
if (Test-Path $lmPath) {
    $lmContent = Get-Content $lmPath -Raw
    $urpBlockCompiles = $lmContent -match 'namespace\s+EssSystem\.Core\.Presentation\.LightManager\s*\{[\s\S]*?public\s+class\s+LightManager\s*:' -and
                       $lmContent.Contains("using UnityEngine.Rendering.Universal;")
}
$status7 = if ($urpBlockCompiles) { "OK" } else { "INFO" }
Write-Host "[7] LightManager URP 块结构:        $($status7.PadRight(6)) $(if($urpBlockCompiles){'URP 块存在并引用 URP namespace'}else{'检查 URP 块结构'})" -ForegroundColor $(if($status7 -eq 'OK'){'Green'}else{'Cyan'})

Write-Host ""
Write-Host "========== 总结 ==========" -ForegroundColor Cyan
$allOK = $status1 -eq "OK" -and $status2 -eq "OK" -and $status3 -eq "OK" -and $status4 -eq "OK" -and $status5 -eq "OK" -and $status6 -eq "OK"
if ($allOK) {
    Write-Host "URP 初始化完整 ✓" -ForegroundColor Green
    Write-Host "  • Package 已装" -ForegroundColor Green
    Write-Host "  • URP_INSTALLED 符号已设" -ForegroundColor Green
    Write-Host "  • URP Asset + Renderer Data 已建" -ForegroundColor Green
    Write-Host "  • Graphics / Quality Settings 已指派" -ForegroundColor Green
} else {
    Write-Host "URP 初始化不完整，缺失：" -ForegroundColor Yellow
    if ($status1 -ne "OK") { Write-Host "  • [1] URP package 未安装" -ForegroundColor Red }
    if ($status2 -ne "OK") { Write-Host "  • [2] URP_INSTALLED 编译符号未设置" -ForegroundColor Yellow }
    if ($status3 -ne "OK") { Write-Host "  • [3] URP-Default.asset 不存在" -ForegroundColor Red }
    if ($status4 -ne "OK") { Write-Host "  • [4] Renderer Data 不存在" -ForegroundColor Red }
    if ($status5 -ne "OK") { Write-Host "  • [5] Graphics.defaultRenderPipeline 未设置" -ForegroundColor Red }
    if ($status6 -ne "OK") { Write-Host "  • [6] QualitySettings 未绑定 RP Asset" -ForegroundColor Red }
    Write-Host ""
    Write-Host "修复方式：" -ForegroundColor Cyan
    Write-Host "  1. 菜单 Tools/EssSystem/LightManager/Install URP Package（自动检测+安装）" -ForegroundColor White
    Write-Host "  2. 装完后自动 Bootstrap URP Project；或手动菜单：Bootstrap URP Project (3D/Forward)/(2D)" -ForegroundColor White
}
Write-Host ""
