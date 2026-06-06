#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Dao;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Editor
{
    /// <summary>
    /// Unity Build output -> AutoUpdate package + latest.json
    /// Menu: <b>Build/WulaSystem/Foundation/AutoUpdate/Build Update Package</b>.
    /// </summary>
    public static class AutoUpdateBuilder
    {
        public const string MENU_PATH = "Build/WulaSystem/Foundation/AutoUpdate/Build Update Package";
        public const string DEFAULT_OUTPUT_DIR = "Build/Updates";
        public const string DEFAULT_BASE_URL = "https://your-cdn.example.com/updates";
        public const string MANIFEST_FILE_NAME = "latest.json";

        [MenuItem(MENU_PATH, priority = 10)]
        public static void BuildUpdatePackage()
        {
            string playerDir = EditorUtility.OpenFolderPanel(
                "Choose Unity Player output folder (eg. Builds/Win64/WulaGame)",
                Path.Combine(UnityEngine.Application.dataPath, "..", "Builds"),
                "");

            if (string.IsNullOrEmpty(playerDir))
            {
                Debug.Log("[AutoUpdateBuilder] User cancelled folder select.");
                return;
            }

            BuildFromBuildOutput(playerDir, DEFAULT_BASE_URL, silent: false);
        }

        /// <summary>
        /// Build auto-update artifacts from a build output path (called by menu and BuildSystem).
        /// playerOutputPath can be:
        /// - xxx\Game.exe
        /// - xxx\Win64\Game
        /// - xxx\Win64\Game.app
        /// </summary>
        public static bool BuildFromBuildOutput(string playerOutputPath, string baseUrl = DEFAULT_BASE_URL, bool silent = true)
        {
            var playerDir = ResolvePlayerBuildDirectory(playerOutputPath);
            if (string.IsNullOrEmpty(playerDir))
            {
                var msg = $"Invalid player output path: {playerOutputPath}";
                if (!silent) EditorUtility.DisplayDialog("AutoUpdateBuilder", msg, "OK");
                Debug.LogError($"[AutoUpdateBuilder] {msg}");
                return false;
            }

            var version = PlayerSettings.bundleVersion;
            if (string.IsNullOrWhiteSpace(version))
            {
                const string versionMsg = "PlayerSettings.bundleVersion is empty. Please set version before building update package.";
                if (!silent) EditorUtility.DisplayDialog("AutoUpdateBuilder", versionMsg, "OK");
                Debug.LogError("[AutoUpdateBuilder] " + versionMsg);
                return false;
            }

            var normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DEFAULT_BASE_URL : baseUrl.Trim().TrimEnd('/');

            // Output directory
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string outputDir = Path.Combine(projectRoot, DEFAULT_OUTPUT_DIR);
            Directory.CreateDirectory(outputDir);

            string zipPath = Path.Combine(outputDir, $"v{version}.zip");
            string manifestPath = Path.Combine(outputDir, MANIFEST_FILE_NAME);

            try
            {
                if (File.Exists(zipPath)) File.Delete(zipPath);
                var sw = Stopwatch.StartNew();
                Debug.Log($"[AutoUpdateBuilder] Zipping {playerDir} -> {zipPath}");
                ZipFile.CreateFromDirectory(playerDir, zipPath, System.IO.Compression.CompressionLevel.Optimal, includeBaseDirectory: true);
                sw.Stop();
                long size = new FileInfo(zipPath).Length;
                Debug.Log($"[AutoUpdateBuilder] Zip done: {size / 1024f / 1024f:F2} MB, elapsed {sw.Elapsed.TotalSeconds:F1}s");
            }
            catch (Exception e)
            {
                var err = "Build auto-update package failed: " + e.Message;
                if (!silent) EditorUtility.DisplayDialog("AutoUpdateBuilder", err, "OK");
                Debug.LogError(e);
                return false;
            }

            var manifest = new UpdateManifest
            {
                version = version,
                releaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                downloadUrl = $"{normalizedBaseUrl}/v{version}.zip",
                checksumSha256 = ComputeSha256(zipPath),
                changelog = "Set changelog in latest.json before publishing.",
                minVersion = "",
                mandatory = false,
                packageSize = new FileInfo(zipPath).Length,
                extra = $"zipPath={zipPath}",
            };
            File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, prettyPrint: true), Encoding.UTF8);
            Debug.Log($"[AutoUpdateBuilder] Manifest written: {manifestPath}");

            if (!silent)
            {
                var msg = $"Done\n\nversion: {version}\nzip: {zipPath}\nmanifest: {manifestPath}\nsize: {manifest.packageSize / 1024f / 1024f:F2} MB\nsha256: {manifest.checksumSha256}\n\n" +
                          "Update buildUrl in latest.json if needed, then upload the Build/Updates folder to CDN.";
                EditorUtility.DisplayDialog("AutoUpdateBuilder", msg, "OK");
            }

            return true;
        }

        private static string ResolvePlayerBuildDirectory(string playerOutputPath)
        {
            if (string.IsNullOrWhiteSpace(playerOutputPath)) return null;

            var normalized = playerOutputPath.Replace('\\', '/').Trim();

            if (Directory.Exists(normalized)) return normalized;

            if (File.Exists(normalized))
            {
                return Path.GetDirectoryName(normalized);
            }

            if (!string.IsNullOrEmpty(Path.GetExtension(normalized)))
            {
                var fallbackDir = Path.GetDirectoryName(normalized);
                if (!string.IsNullOrEmpty(fallbackDir) && Directory.Exists(fallbackDir))
                {
                    return fallbackDir;
                }
            }

            return null;
        }

        private static string ComputeSha256(string filePath)
        {
            try
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                using var fs = File.OpenRead(filePath);
                var hash = sha.ComputeHash(fs);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AutoUpdateBuilder] SHA256 计算失败: " + e.Message);
                return "";
            }
        }

        [MenuItem("Build/WulaSystem/Foundation/AutoUpdate/Set Update Base URL...", priority = 11)]
        public static void SetBaseUrl()
        {
            string projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
            string manifestPath = Path.Combine(projectRoot, DEFAULT_OUTPUT_DIR, MANIFEST_FILE_NAME);
            if (!File.Exists(manifestPath))
            {
                EditorUtility.DisplayDialog("AutoUpdateBuilder", $"No latest.json found at: {manifestPath}, please run Build Update Package first.", "OK");
                return;
            }

            string current = File.ReadAllText(manifestPath);
            string currentUrl = ExtractDownloadUrl(current);
            EditorUtility.DisplayDialog("Set Update Base URL",
                "Unity 6 removes EditorUtility.InputDialog.\n" +
                "Please replace downloadUrl manually in latest.json.\n\n" +
                $"Current: {currentUrl}\n\n" +
                "After replace, open latest.json from folder.",
                "Open File");
            EditorUtility.RevealInFinder(manifestPath);
            Debug.Log($"[AutoUpdateBuilder] Current baseUrl: {currentUrl}, edit latest.json manually for CI.");
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
