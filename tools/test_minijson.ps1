# MiniJson round-trip sanity test
# Compiles MiniJson.cs + a tiny Program.cs into an exe and runs it via Tuanjie's dotnet.
$ErrorActionPreference = 'Stop'

$UnityData = 'D:\Unity-Editor\2022.3.61t8\Editor\Data'
$DotNet    = Join-Path $UnityData 'NetCoreRuntime\dotnet.exe'
$Csc       = Join-Path $UnityData 'DotNetSdkRoslyn\csc.dll'
$NetStd    = Join-Path $UnityData 'NetStandard\ref\2.1.0\netstandard.dll'

$SrcFile   = 'D:\wula\WulaGameFramework\Scripts\EssSystem\Core\Util\MiniJson.cs'
$BuildDir  = 'D:\wula\WulaGameFramework\.build'
$ProgCs    = Join-Path $BuildDir 'MiniJsonTestMain.cs'
$TestExe   = Join-Path $BuildDir 'MiniJsonTest.exe'
$RtCfg     = Join-Path $BuildDir 'MiniJsonTest.runtimeconfig.json'

New-Item -ItemType Directory -Force -Path $BuildDir | Out-Null

# 1) Write a minimal test harness
@'
using System;
using System.Collections.Generic;
using EssSystem.Core.Util;

static class Prog
{
    static int Main()
    {
        var inner = new Dictionary<string, object>
        {
            ["hp"] = 100,
            ["name"] = "Potion<x>\"ok\"\n/line",
            ["weight"] = 0.5,
            ["nullField"] = null
        };
        var categories = new Dictionary<string, object>
        {
            ["potions"] = inner,
            ["empty"] = new Dictionary<string, object>()
        };
        var record = new Dictionary<string, object>
        {
            ["service_name"] = "InventoryService",
            ["categories"] = categories
        };
        var dataList = new List<object> { record };

        string json = MiniJson.Serialize(dataList, pretty: true);
        Console.WriteLine("--- serialized ---");
        Console.WriteLine(json);

        var back = MiniJson.Deserialize(json) as List<object>;
        if (back == null) { Console.Error.WriteLine("FAIL: Deserialize not List<object>"); return 1; }

        var r = (Dictionary<string, object>)back[0];
        if ((string)r["service_name"] != "InventoryService") { Console.Error.WriteLine("FAIL: service_name"); return 1; }

        var cats = (Dictionary<string, object>)r["categories"];
        var pot  = (Dictionary<string, object>)cats["potions"];
        if ((long)pot["hp"] != 100)                             { Console.Error.WriteLine("FAIL: hp"); return 1; }
        if ((string)pot["name"] != "Potion<x>\"ok\"\n/line")    { Console.Error.WriteLine("FAIL: name escape"); return 1; }
        if ((double)pot["weight"] != 0.5)                       { Console.Error.WriteLine("FAIL: weight"); return 1; }
        if (pot["nullField"] != null)                           { Console.Error.WriteLine("FAIL: null not preserved"); return 1; }
        if (((Dictionary<string, object>)cats["empty"]).Count != 0) { Console.Error.WriteLine("FAIL: empty dict"); return 1; }

        string compact = MiniJson.Serialize(dataList, pretty: false);
        if (compact.Contains("\n")) { Console.Error.WriteLine("FAIL: compact has newline"); return 1; }

        Console.WriteLine("[OK] round-trip verified");
        return 0;
    }
}
'@ | Out-File -FilePath $ProgCs -Encoding utf8

# 2) runtimeconfig so dotnet can pick .NET 6 runtime
@'
{
  "runtimeOptions": {
    "tfm": "net6.0",
    "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.0" }
  }
}
'@ | Out-File -FilePath $RtCfg -Encoding utf8

# 3) Compile to exe
Write-Host "[*] Compiling test exe..." -ForegroundColor Cyan
& $DotNet $Csc `
    -target:exe -nostdlib+ -noconfig -langversion:9 -deterministic -debug- `
    "-out:$TestExe" `
    "-reference:$NetStd" `
    $SrcFile $ProgCs 2>&1 | Out-Host

if ($LASTEXITCODE -ne 0) {
    Write-Host "[FAIL] compile error" -ForegroundColor Red
    exit 1
}

# 4) Run via dotnet
Write-Host "[*] Running..." -ForegroundColor Cyan
& $DotNet $TestExe 2>&1 | Out-Host
$rc = $LASTEXITCODE
Write-Host ""
if ($rc -eq 0) { Write-Host "[OK] MiniJson round-trip test PASSED" -ForegroundColor Green }
else           { Write-Host "[FAIL] test exit=$rc" -ForegroundColor Red }
exit $rc
