#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Dao;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Editor
{
    /// <summary>
    /// 一键把 Unity Build 输出打成 AutoUpdate 用的 ZIP + latest.json。
    ///
    /// 菜单 <b>Build / EssSystem / Build Update Package</b>：
    ///   1. 弹文件夹选择框，让你选刚才 Build 出来的 Player 目录（例如 <c>Builds/Win64/WulaGame</c>）
    ///   2. 读 <c>PlayerSettings.bundleVersion</c> 作为 version
    ///   3. 写 manifest（latest.json）→ <c>Build/Updates/latest.json</c>
    ///   4. 把整个 Player 目录 ZIP → <c>Build/Updates/v{version}.zip</c>
    ///   5. 把 manifest 内 <c>downloadUrl</c> 用占位 <c>{baseUrl}/v{version}.zip</c> 写（业务侧用 sed/脚本替换）
    ///
    /// 之后把 <c>Build/Updates/</c> 整个 rsync / scp 到 CDN / Nginx 静态目录即可。
    /// </summary>
    public static class AutoUpdateBuilder
    {
        public const string MENU_PATH = "Build/EssSystem/Build Update Package";
        public const string DEFAULT_OUTPUT_DIR = "Build/Updates";
        public const string DEFAULT_BASE_URL = "https://your-cdn.example.com/updates";

        [MenuItem(MENU_PATH, priority = 10)]
        public static void BuildUpdatePackage()
        {
            // 1) 选 Player 目录
            string playerDir = EditorUtility.OpenFolderPanel(
                "选择 Unity Build 出的 Player 目录（例如 Builds/Win64/WulaGame）",
                Path.Combine(UnityEngine.Application.dataPath, "..", "Builds"),
                "");
            if (string.IsNullOrEmpty(playerDir))
            {
                Debug.Log("[AutoUpdateBuilder] 用户取消选择");
                return;
            }
            if (!File.Exists(Path.Combine(playerDir, Path.GetFileName(playerDir) + ".exe")))
            {
                // 不强制 .exe 存在（Mac .app 不会有），只警告
                Debug.LogWarning($"[AutoUpdateBuilder] 警告：{playerDir} 下没找到同名 .exe，Mac/Linux 可忽略");
            }

            // 2) 读 version（PlayerSettings.bundleVersion）
            string version = PlayerSettings.bundleVersion;
            if (string.IsNullOrEmpty(version))
            {
                EditorUtility.DisplayDialog("AutoUpdateBuilder",
                    "PlayerSettings.bundleVersion 为空，请先在 Player Settings 设置版本号。", "OK");
                return;
            }

            // 3) 输出目录
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string outputDir = Path.Combine(projectRoot, DEFAULT_OUTPUT_DIR);
            Directory.CreateDirectory(outputDir);

            string zipPath = Path.Combine(outputDir, $"v{version}.zip");
            string manifestPath = Path.Combine(outputDir, "latest.json");

            // 4) ZIP 打包（覆盖式）
            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                Debug.Log($"[AutoUpdateBuilder] 开始打包 {playerDir} → {zipPath}");
                var sw = Stopwatch.StartNew();
                ZipFile.CreateFromDirectory(playerDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
                sw.Stop();
                long size = new FileInfo(zipPath).Length;
                Debug.Log($"[AutoUpdateBuilder] ZIP 完成：{size / 1024f / 1024f:F2} MB，用时 {sw.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("AutoUpdateBuilder 失败", "ZIP 失败：" + e.Message, "OK");
                Debug.LogError(e);
                return;
            }

            // 5) 写 manifest（占位 {baseUrl}，业务侧部署前 sed 替换）
            var manifest = new UpdateManifest
            {
                version        = version,
                releaseDate    = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                downloadUrl    = $"{DEFAULT_BASE_URL}/v{version}.zip",
                checksumSha256 = ComputeSha256(zipPath),
                changelog      = "（在 Inspector / 打包脚本里填更新说明）",
                minVersion     = "",
                mandatory      = false,
                packageSize    = new FileInfo(zipPath).Length,
                extra          = $"zipPath={zipPath}",
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, prettyPrint: true), Encoding.UTF8);
            Debug.Log($"[AutoUpdateBuilder] manifest 写入 {manifestPath}");

            // 6) 弹结果
            string msg =
                $"打包完成\n\n" +
                $"version: {version}\n" +
                $"zip    : {zipPath}\n" +
                $"manifest: {manifestPath}\n" +
                $"size   : {manifest.packageSize / 1024f / 1024f:F2} MB\n" +
                $"sha256 : {manifest.checksumSha256}\n\n" +
                $"接下来把这两个文件 scp / rsync 到 CDN，\n" +
                $"记得把 manifest.downloadUrl 里的 {DEFAULT_BASE_URL} 替换成实际 CDN 地址。";
            EditorUtility.DisplayDialog("AutoUpdateBuilder", msg, "OK");
        }

        // ─── 工具 ─────────────────────────────────────────

        private static string ComputeSha256(string filePath)
        {
            try
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                using var fs  = File.OpenRead(filePath);
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AutoUpdateBuilder] SHA256 计算失败：" + e.Message);
                return "";
            }
        }

        // ─── 辅助：手动把 latest.json 的 {baseUrl} 占位换成真实 CDN（CI 用） ─────

        [MenuItem("Build/EssSystem/Set Update Base URL...", priority = 11)]
        public static void SetBaseUrl()
        {
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string manifestPath = Path.Combine(projectRoot, DEFAULT_OUTPUT_DIR, "latest.json");
            if (!File.Exists(manifestPath))
            {
                EditorUtility.DisplayDialog("AutoUpdateBuilder", $"没找到 {manifestPath}，先跑一遍 Build Update Package。", "OK");
                return;
            }

            string current = File.ReadAllText(manifestPath);
            string currentUrl = ExtractDownloadUrl(current);
            // Unity 6 的 EditorUtility 移除了 InputDialog（带输入框的对话框）
            // 改成 DisplayDialog 提示用户手动改 + RevealInFinder 唤起资源管理器
            EditorUtility.DisplayDialog("Set Update Base URL",
                "Unity 6 的 EditorUtility.InputDialog 已移除，请手动改 latest.json 里的 downloadUrl：\n\n" +
                $"当前：\n{currentUrl}\n\n" +
                "点 OK 打开 latest.json 所在文件夹。",
                "打开文件夹");
            EditorUtility.RevealInFinder(manifestPath);
            Debug.Log($"[AutoUpdateBuilder] 当前 baseUrl：{currentUrl}（手动改 latest.json 后用 CI sed 也可）");
        }

        private static string ExtractDownloadUrl(string json)
        {
            const string key = "\"downloadUrl\":";
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return "";
            i += key.Length;
            while (i < json.Length && (json[i] == ' ' || json[i] == '\t')) i++;
            if (i >= json.Length || json[i] != '"') return "";
            int j = json.IndexOf('"', i + 1);
            return j < 0 ? "" : json.Substring(i + 1, j - i - 1);
        }
    }
}
#endif
