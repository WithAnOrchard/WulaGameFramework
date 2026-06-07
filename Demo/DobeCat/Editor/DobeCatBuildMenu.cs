using System.IO;
using Debug = UnityEngine.Debug;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Demo.DobeCat.Editor
{
    /// <summary>
    /// DobeCat 专用构建系统（全自动，无需任何手动配置）。
    ///
    /// <list type="bullet">
    /// <item><b>输出路径</b>：自动计算为 <c>{项目根}/Builds/DobeCat/DobeCat.exe</c>，无需设置。</item>
    /// <item><b>资源隔离</b>：通过 Addressable 组的 IncludeInBuild 标志控制框架资源是否入包。</item>
    /// <item><b>恢复保障</b>：<see cref="IPostprocessBuildWithReport"/> 确保构建后 Addressable 组状态复原。</item>
    /// <item><b>命令行</b>：支持 <c>-executeMethod Demo.DobeCat.Editor.DobeCatBuildMenu.BuildFromCommandLine</c>。</item>
    /// </list>
    /// </summary>
    public class DobeCatBuildMenu : IPostprocessBuildWithReport
    {
        // ─── 常量 ─────────────────────────────────────────────────
        private const string MENU_PREFIX            = "Build/WulaSystem/Demo/DobeCat/";
        private const string ScenePath              = "Assets/Demo/DobeCat/Scenes/DobeCat.unity";
        private const string FrameworkAddressableGroup = "Framework-Resources";

        private sealed class PlayerSettingsSnapshot
        {
            public bool UseFlipModelSwapchain;
            public bool PreserveFramebufferAlpha;
            public bool RunInBackground;
            public bool VisibleInBackground;
            public bool ResizableWindow;
            public FullScreenMode FullScreenMode;
            public GraphicsDeviceType[] GraphicsApis;
        }

        /// <summary>自动计算输出路径：{项目根}/Builds/DobeCat/DobeCat.exe</summary>
        private static string AutoOutputPath =>
            Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Builds", "DobeCat", "DobeCat.exe");

        // IPostprocessBuildWithReport 优先级（越小越先执行）
        public int callbackOrder => 0;

        // IPostprocessBuildWithReport 兜底（确保 Addressable 组状态恢复）
        public void OnPostprocessBuild(BuildReport report)
        {
            SetFrameworkGroupIncluded(true);
        }

        // ─── 菜单入口 ─────────────────────────────────────────────
        [MenuItem(MENU_PREFIX + "Build Windows x64", priority = 1)]
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64);

        [MenuItem(MENU_PREFIX + "Show Output Folder", priority = 20)]
        public static void ShowOutputFolder()
        {
            var dir = Path.GetDirectoryName(AutoOutputPath)!;
            if (Directory.Exists(dir))
                EditorUtility.RevealInFinder(dir);
            else
                EditorUtility.DisplayDialog("DobeCat Build", $"输出目录尚未生成:\n{dir}", "OK");
        }

        // ─── 命令行入口（-executeMethod Demo.DobeCat.Editor.DobeCatBuildMenu.BuildFromCommandLine）
        public static void BuildFromCommandLine()
        {
            Build(BuildTarget.StandaloneWindows64);
            // 命令行构建：结果已写入 Console / 日志，Unity 自动以退出码反映成败
        }

        // ─── 核心构建逻辑 ─────────────────────────────────────────
        private static void Build(BuildTarget target)
        {
            var outputPath = AutoOutputPath;
            var sceneFullPath = Path.Combine(Application.dataPath, "..", ScenePath).Replace('\\', '/');

            if (!File.Exists(sceneFullPath))
            {
                var msg = $"场景文件不存在:\n{ScenePath}";
                Debug.LogError($"[DobeCatBuild] {msg}");
                if (!Application.isBatchMode)
                    EditorUtility.DisplayDialog("DobeCat Build 失败", msg, "OK");
                return;
            }

            CleanOutputDirectory(outputPath);

            // 排除 Framework Addressable 组（迁移完成后 Assets/Resources 已不存在，此步确保组不入包）
            SetFrameworkGroupIncluded(false);
            // 构建 Addressable 内容（仅 DobeCat 相关组）
            BuildAddressables();

            // 阻止 AddressablesPlayerBuildProcessor 在 BuildPipeline.BuildPlayer 内部二次触发 BuildPlayerContent
            var addrSettings = AddressableAssetSettingsDefaultObject.Settings;
            var prevBuildOption = addrSettings != null
                ? addrSettings.BuildAddressablesWithPlayerBuild
                : AddressableAssetSettings.PlayerBuildOption.PreferencesValue;
            if (addrSettings != null)
                addrSettings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;

            BuildReport report = null;
            var playerSettings = ApplyTransparentOverlayPlayerSettings();
            try
            {
                Debug.Log($"[DobeCatBuild] ▶ 开始构建 → {outputPath}");
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes           = new[] { ScenePath },
                    locationPathName = outputPath,
                    target           = target,
                    options          = BuildOptions.None,
                });
            }
            finally
            {
                // 还原设置，避免影响其他构建目标
                if (addrSettings != null)
                    addrSettings.BuildAddressablesWithPlayerBuild = prevBuildOption;
                RestorePlayerSettings(playerSettings);
                SetFrameworkGroupIncluded(true);
                DeleteBuildDebugArtifacts(outputPath);
                DeleteStaleD3D12Artifacts(outputPath);
                LogBuildResult(report, outputPath);
            }
        }

        private static PlayerSettingsSnapshot ApplyTransparentOverlayPlayerSettings()
        {
            var snapshot = new PlayerSettingsSnapshot
            {
                UseFlipModelSwapchain     = PlayerSettings.useFlipModelSwapchain,
                PreserveFramebufferAlpha  = PlayerSettings.preserveFramebufferAlpha,
                RunInBackground           = PlayerSettings.runInBackground,
                VisibleInBackground       = PlayerSettings.visibleInBackground,
                ResizableWindow           = PlayerSettings.resizableWindow,
                FullScreenMode            = PlayerSettings.fullScreenMode,
                GraphicsApis              = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64)
            };

            PlayerSettings.useFlipModelSwapchain    = false;
            PlayerSettings.preserveFramebufferAlpha = true;
            PlayerSettings.runInBackground          = true;
            PlayerSettings.visibleInBackground      = true;
            PlayerSettings.resizableWindow          = false;
            PlayerSettings.fullScreenMode           = FullScreenMode.Windowed;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D11 });

            Debug.Log("[DobeCatBuild] 已应用透明桌面叠加 PlayerSettings：preserveFramebufferAlpha=true, useFlipModelSwapchain=false");
            return snapshot;
        }

        private static void RestorePlayerSettings(PlayerSettingsSnapshot snapshot)
        {
            if (snapshot == null) return;
            PlayerSettings.useFlipModelSwapchain    = snapshot.UseFlipModelSwapchain;
            PlayerSettings.preserveFramebufferAlpha = snapshot.PreserveFramebufferAlpha;
            PlayerSettings.runInBackground          = snapshot.RunInBackground;
            PlayerSettings.visibleInBackground      = snapshot.VisibleInBackground;
            PlayerSettings.resizableWindow          = snapshot.ResizableWindow;
            PlayerSettings.fullScreenMode           = snapshot.FullScreenMode;
            if (snapshot.GraphicsApis != null && snapshot.GraphicsApis.Length > 0)
                PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, snapshot.GraphicsApis);
        }

        // ─── Addressables 组控制 ──────────────────────────────────
        private static void CleanOutputDirectory(string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(dir)) return;

            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var buildsRoot = Path.Combine(projectRoot, "Builds");
            var fullDir = Path.GetFullPath(dir);
            var fullBuildsRoot = Path.GetFullPath(buildsRoot);
            if (!fullDir.StartsWith(fullBuildsRoot, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[DobeCatBuild] Refuse to clean output outside Builds: {fullDir}");
                return;
            }

            if (Directory.Exists(fullDir))
            {
                Directory.Delete(fullDir, recursive: true);
                Debug.Log($"[DobeCatBuild] Cleaned output directory: {fullDir}");
            }
            Directory.CreateDirectory(fullDir);
        }

        private static void DeleteBuildDebugArtifacts(string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            var productName = Path.GetFileNameWithoutExtension(outputPath);
            var doNotShip = Path.Combine(dir, $"{productName}_BurstDebugInformation_DoNotShip");
            if (!Directory.Exists(doNotShip)) return;

            try
            {
                Directory.Delete(doNotShip, recursive: true);
                Debug.Log($"[DobeCatBuild] Deleted build debug directory: {doNotShip}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DobeCatBuild] Failed to delete build debug directory: {ex.Message}");
            }
        }

        private static void DeleteStaleD3D12Artifacts(string outputPath)
        {
            var dir = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

            DeleteFileIfExists(Path.Combine(dir, "dstorage.dll"));
            DeleteFileIfExists(Path.Combine(dir, "dstoragecore.dll"));

            var d3d12Dir = Path.Combine(dir, "D3D12");
            if (!Directory.Exists(d3d12Dir)) return;

            try
            {
                Directory.Delete(d3d12Dir, recursive: true);
                Debug.Log($"[DobeCatBuild] Deleted stale D3D12 directory: {d3d12Dir}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DobeCatBuild] Failed to delete stale D3D12 directory: {ex.Message}");
            }
        }

        private static void DeleteFileIfExists(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                File.Delete(path);
                Debug.Log($"[DobeCatBuild] Deleted stale file: {path}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DobeCatBuild] Failed to delete stale file {path}: {ex.Message}");
            }
        }

        private static void SetFrameworkGroupIncluded(bool included)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return;
            var group = settings.FindGroup(FrameworkAddressableGroup);
            if (group == null) return;
            var schema = group.GetSchema<UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema>();
            if (schema == null) return;
            schema.IncludeInBuild = included;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"[DobeCatBuild] Addressable 组 [{FrameworkAddressableGroup}] IncludeInBuild={included}");
        }

        private static void BuildAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                Debug.Log("[DobeCatBuild] Addressable Settings 未配置，跳过 Addressable 构建");
                return;
            }
            Debug.Log("[DobeCatBuild] 构建 Addressable 内容...");
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (!string.IsNullOrEmpty(result?.Error))
                Debug.LogError($"[DobeCatBuild] Addressable 构建错误: {result.Error}");
            else
                Debug.Log("[DobeCatBuild] Addressable 内容构建完成");
        }

        // ─── 构建结果日志 ─────────────────────────────────────────
        private static void LogBuildResult(BuildReport report, string outputPath)
        {
            if (report == null) return;
            var s = report.summary;
            if (s.result == BuildResult.Succeeded)
                Debug.Log($"[DobeCatBuild] ✓ 构建成功  {s.totalSize / 1048576f:F1} MB → {outputPath}  耗时 {s.totalTime.TotalSeconds:F1}s");
            else
                Debug.LogError($"[DobeCatBuild] ✗ 构建失败: {s.result}  错误数={s.totalErrors}  警告数={s.totalWarnings}");
        }
    }
}
