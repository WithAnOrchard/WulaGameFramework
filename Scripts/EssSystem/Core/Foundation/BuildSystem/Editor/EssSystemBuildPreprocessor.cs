#if UNITY_EDITOR
using EssSystem.Core.Foundation.ResourceManager.Editor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EssSystem.Core.Foundation.BuildSystem.Editor
{
    /// <summary>
    /// EssSystem 框架级构建前处理器。
    ///
    /// 凡是使用 EssSystem 的项目，每次执行 BuildPipeline.BuildPlayer 时都会自动触发本处理器，
    /// 无需在各自的 BuildMenu 中手动调用。
    ///
    /// 当前内置步骤：
    ///   1. 生成 ResourceManifest.json（让 ResourceService 在运行时按需加载而不全量预热）
    ///
    /// 如需添加新的框架级预处理步骤，在 OnPreprocessBuild 中追加即可，所有项目自动受益。
    /// </summary>
    public class EssSystemBuildPreprocessor : IPreprocessBuildWithReport
    {
        /// <summary>优先级 -100：早于项目自定义预处理器执行，确保框架资源就绪后再进行项目层处理。</summary>
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[EssSystem Build] ▶ 开始框架级构建前处理...");

            RunStep("生成 ResourceManifest", ResourceManifestGenerator.Generate);

            Debug.Log("[EssSystem Build] ✓ 框架级构建前处理完成");
        }

        private static void RunStep(string stepName, System.Action step)
        {
            try
            {
                Debug.Log($"[EssSystem Build]   → {stepName}");
                step();
            }
            catch (System.Exception ex)
            {
                // 预处理失败记录错误但不中止构建，避免工具问题阻塞正常构建流程
                Debug.LogError($"[EssSystem Build] 预处理步骤「{stepName}」失败: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif
