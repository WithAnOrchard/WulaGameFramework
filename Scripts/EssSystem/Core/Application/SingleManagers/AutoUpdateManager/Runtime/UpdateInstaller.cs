using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Runtime
{
    /// <summary>
    /// 跨平台自替换安装器。
    ///
    /// Windows 流程（"运行时换皮"）：
    ///   1) 把 ZIP 路径 + 安装目录 + 进程 PID 写进 PowerShell stub
    ///   2) 启动 powershell.exe 跑 stub（隐藏窗口）
    ///   3) 调用方立刻 <c>Application.Quit()</c>（让 stub 拿到进程退出的时机）
    ///   4) stub：循环 Get-Process -Id 等进程退出 → 等待 1s 释放文件锁 →
    ///          [System.IO.Compression.ZipFile]::ExtractToDirectory 覆盖安装 →
    ///          Start-Process 重启新版本
    ///
    /// Mac / Linux：当前抛 NotSupportedException，业务侧自己挂 .sh / .app updater。
    /// </summary>
    public static class UpdateInstaller
    {
        public static void InstallFromZip(string zipPath)
        {
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
                throw new FileNotFoundException("下载的 ZIP 找不到", zipPath);

#if UNITY_STANDALONE_WIN
            InstallWindows(zipPath);
#elif UNITY_STANDALONE_OSX
            throw new PlatformNotSupportedException("Mac 自替换安装器未实现（需要单独 updater .app）。ZIP 路径：" + zipPath);
#elif UNITY_STANDALONE_LINUX
            throw new PlatformNotSupportedException("Linux 自替换安装器未实现（需要单独 updater .sh）。ZIP 路径：" + zipPath);
#else
            throw new PlatformNotSupportedException("AutoUpdate 仅支持 Standalone 平台；当前平台 ZIP 路径：" + zipPath);
#endif
        }

#if UNITY_STANDALONE_WIN
        private static void InstallWindows(string zipPath)
        {
            // Unity player 进程退出时 Application.dataPath 仍指向安装目录（_Data 子文件夹的父级）
            // 例如：C:\Games\WulaGame\WulaGame_Data  →  installDir = C:\Games\WulaGame
            string dataPath = UnityEngine.Application.dataPath.Replace('/', '\\');
            string installDir = Path.GetDirectoryName(dataPath);
            string exeName   = GetPlayerExecutableName(dataPath);   // e.g. "WulaGame.exe"
            int    gamePid   = Process.GetCurrentProcess().Id;

            // stub 写到 temp（不在游戏目录，避免被覆盖锁住）
            string stubDir  = Path.Combine(UnityEngine.Application.temporaryCachePath, "AutoUpdate");
            Directory.CreateDirectory(stubDir);
            string stubPath = Path.Combine(stubDir, "updater.ps1");

            File.WriteAllText(stubPath, BuildPowerShellStub(gamePid, zipPath, installDir, exeName));
            Debug.Log($"[AutoUpdate] PowerShell stub 写入 {stubPath}（等待 PID={gamePid} 退出）");

            // 启动 stub
            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-ExecutionPolicy Bypass -NoProfile -WindowStyle Hidden -File \"{stubPath}\"",
                UseShellExecute = false,
                CreateNoWindow  = true,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
            };
            Process.Start(psi);
        }

        /// <summary>
        /// PowerShell 5.1 兼容的 stub（用 .NET ZipFile API）。
        /// 关键点：游戏进程是 .exe + 多个 .dll，Windows 锁住的是 .exe + UnityPlayer.dll 等；
        /// 解压时只覆盖到 installDir（不动 .exe 之外的目录），所以"覆盖自己"在退出后是安全的。
        /// </summary>
        private static string BuildPowerShellStub(int gamePid, string zipPath, string installDir, string exeName)
        {
            // 单引号字符串中要把嵌入的 ' 转成 ''（PowerShell 转义）
            string Q(string s) => s.Replace("'", "''");
            return string.Join("\r\n", new[]
            {
                "Add-Type -AssemblyName System.IO.Compression.FileSystem",
                "$ErrorActionPreference = 'Stop'",
                $"$zip   = '{Q(zipPath)}'",
                $"$dir   = '{Q(installDir)}'",
                $"$exe   = '{Q(exeName)}'",
                $"$pid   = {gamePid}",
                "$log   = Join-Path $dir 'auto_update.log'",
                "",
                "function Write-Log($msg) {",
                "    $line = (Get-Date -Format 'o') + ' ' + $msg",
                "    Write-Host $line",
                "    Add-Content -Path $log -Value $line -Encoding UTF8",
                "}",
                "",
                "Write-Log (\"Waiting for game PID {0} to exit...\" -f $pid)",
                "$waited = 0",
                "while ($waited -lt 30) {",
                "    $p = Get-Process -Id $pid -ErrorAction SilentlyContinue",
                "    if (-not $p) { break }",
                "    Start-Sleep -Seconds 1",
                "    $waited++",
                "}",
                "Start-Sleep -Seconds 2  # grace: 等文件系统释放句柄",
                "Write-Log (\"Game exited after {0}s. Extracting...\" -f $waited)",
                "",
                "try {",
                "    [System.IO.Compression.ZipFile]::ExtractToDirectory($zip, $dir)",
                "    Write-Log 'Extraction OK'",
                "} catch {",
                "    Write-Log (\"Extract FAILED: {0}\" -f $_.Exception.Message)",
                "    exit 1",
                "}",
                "",
                "# 清理：删除 ZIP（成功后才删，失败留作排查）",
                "Remove-Item $zip -Force -ErrorAction SilentlyContinue",
                "",
                "# 重启新版本",
                "$exePath = Join-Path $dir $exe",
                "if (Test-Path $exePath) {",
                "    Write-Log (\"Restarting {0}...\" -f $exePath)",
                "    Start-Process -FilePath $exePath",
                "} else {",
                "    Write-Log (\"Exe not found at {0}\" -f $exePath)",
                "}",
            });
        }

        private static string GetPlayerExecutableName(string dataPath)
        {
            var dataDir = Path.GetFileName(dataPath);
            const string suffix = "_Data";
            if (!string.IsNullOrEmpty(dataDir) && dataDir.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return dataDir.Substring(0, dataDir.Length - suffix.Length) + ".exe";

            return UnityEngine.Application.productName + ".exe";
        }
#endif
    }
}
