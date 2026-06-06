#if UNITY_EDITOR
using EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Editor;
using EssSystem.Core.Foundation.ResourceManager.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EssSystem.Core.Foundation.BuildSystem.Editor
{
    /// <summary>
    /// 统一的构建前后处理入口。
    /// </summary>
    public class EssSystemBuildPreprocessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string AUTOUPDATE_POSTBUILD_PREF = "EssSystem.BuildSystem.AutoUpdate.PostBuild.Enabled";
        private const string AUTOUPDATE_MENU_PATH = "Build/WulaSystem/Foundation/AutoUpdate/Auto Generate Update Package After Build";

        private static bool AutoUpdatePostBuildEnabled
        {
            get => EditorPrefs.GetBool(AUTOUPDATE_POSTBUILD_PREF, true);
            set => EditorPrefs.SetBool(AUTOUPDATE_POSTBUILD_PREF, value);
        }

        /// <summary>优先级较低，先跑框架资源清单，再执行普通项目预处理器。</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[EssSystem Build] 开始执行构建前预处理...");

            RunStep("生成 ResourceManifest", ResourceManifestGenerator.Generate);

            Debug.Log("[EssSystem Build] 构建前预处理完成");
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            Debug.Log("[EssSystem Build] 开始执行构建后处理...");
            RunStep("生成 AutoUpdate 更新包", () => GenerateAutoUpdatePackage(report));
            Debug.Log("[EssSystem Build] 构建后处理完成");
        }

        [MenuItem(AUTOUPDATE_MENU_PATH, priority = 20)]
        public static void ToggleAutoUpdatePostBuild()
        {
            AutoUpdatePostBuildEnabled = !AutoUpdatePostBuildEnabled;
            Debug.Log($"[EssSystem Build] AutoUpdate 构建后自动生成：{AutoUpdatePostBuildEnabled}");
        }

        [MenuItem(AUTOUPDATE_MENU_PATH, true)]
        public static bool ToggleAutoUpdatePostBuildValidate()
        {
            Menu.SetChecked(AUTOUPDATE_MENU_PATH, AutoUpdatePostBuildEnabled);
            return true;
        }

        private static void RunStep(string stepName, System.Action step)
        {
            try
            {
                Debug.Log($"[EssSystem Build]   -> {stepName}");
                step();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[EssSystem Build] 步骤 {stepName} 失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static void GenerateAutoUpdatePackage(BuildReport report)
        {
            string outputPath = report?.summary.outputPath;
            if (!AutoUpdatePostBuildEnabled)
            {
                Debug.Log("[EssSystem Build] 已关闭 AutoUpdate 构建后自动生成。");
                return;
            }

            if (string.IsNullOrEmpty(outputPath))
            {
                Debug.LogWarning("[EssSystem Build] Build 输出路径为空，跳过 AutoUpdate 更新包生成。");
                return;
            }

            if (report.summary.platform != BuildTarget.StandaloneWindows && report.summary.platform != BuildTarget.StandaloneWindows64)
            {
                Debug.Log($"[EssSystem Build] 当前平台 {report.summary.platform} 暂不生成 AutoUpdate 包（仅 Windows）。");
                return;
            }

            var success = AutoUpdateBuilder.BuildFromBuildOutput(outputPath, AutoUpdateBuilder.DEFAULT_BASE_URL, silent: true);
            if (!success)
            {
                Debug.LogError($"[EssSystem Build] AutoUpdate 包生成失败: {outputPath}");
                return;
            }

            string version = PlayerSettings.bundleVersion;
            string outputDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath), AutoUpdateBuilder.DEFAULT_OUTPUT_DIR);
            string zipPath = System.IO.Path.Combine(outputDir, $"v{version}.zip");
            string manifestPath = System.IO.Path.Combine(outputDir, AutoUpdateBuilder.MANIFEST_FILE_NAME);
            Debug.Log($"[EssSystem Build] AutoUpdate 包已生成：{zipPath} / {manifestPath}");
        }
    }
}
#endif
