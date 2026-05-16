// 此 Editor 脚本不可被 #if URP_INSTALLED 包裹 —— 它的职责就是维护这个符号。
#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace EssSystem.Core.Presentation.LightManager.Editor
{
    /// <summary>
    /// LightManager URP 安装检测器 —— 启动时自动检查 URP 包，同步 <c>URP_INSTALLED</c> 编译符号；
    /// 未装时弹一次对话框，引导玩家通过 Package Manager 自动安装。
    /// </summary>
    [InitializeOnLoad]
    public static class LightManagerInstaller
    {
        private const string URP_PACKAGE_ID    = "com.unity.render-pipelines.universal";
        private const string URP_DEFINE_SYMBOL = "URP_INSTALLED";
        private const string PROMPT_PREF_KEY   = "EssSystem.LightManager.URPPromptShown";

        private const string MENU_INSTALL = "Tools/EssSystem/LightManager/Install URP Package";
        private const string MENU_CHECK   = "Tools/EssSystem/LightManager/Recheck URP Status";

        private static AddRequest _addRequest;

        // ============================================================
        // 启动时检测
        // ============================================================
        static LightManagerInstaller()
        {
            // 用 EditorApplication.delayCall 确保 AssetDatabase 就绪后再检
            EditorApplication.delayCall += () =>
            {
                bool urpPresent = IsUrpInstalled();
                SyncDefineSymbol(urpPresent);

                if (!urpPresent && !EditorPrefs.GetBool(PROMPT_PREF_KEY, false))
                {
                    EditorPrefs.SetBool(PROMPT_PREF_KEY, true);   // 只弹一次（除非手动 Recheck）
                    PromptInstall();
                }
            };
        }

        // ============================================================
        // 菜单入口
        // ============================================================
        [MenuItem(MENU_INSTALL)]
        public static void MenuInstall()
        {
            if (IsUrpInstalled())
            {
                EditorUtility.DisplayDialog("URP 已安装",
                    "URP package 已存在于项目依赖中。\n\n如需启用 LightManager，请确保 URP_INSTALLED 编译符号已设置（菜单：Recheck URP Status）。",
                    "OK");
                return;
            }
            InstallUrp();
        }

        [MenuItem(MENU_CHECK)]
        public static void MenuRecheck()
        {
            EditorPrefs.DeleteKey(PROMPT_PREF_KEY);
            bool urpPresent = IsUrpInstalled();
            SyncDefineSymbol(urpPresent);
            EditorUtility.DisplayDialog(
                "LightManager URP 状态",
                $"URP package 安装状态：{(urpPresent ? "已安装 ✓" : "未安装 ✗")}\n" +
                $"URP_INSTALLED 编译符号：{(IsDefineSet() ? "已设置 ✓" : "未设置 ✗")}\n\n" +
                (urpPresent ? "LightManager 应已可用（重编译后）。" : "请通过 Install URP Package 菜单安装。"),
                "OK");
        }

        // ============================================================
        // 实际操作
        // ============================================================
        private static void PromptInstall()
        {
            const string title = "EssSystem · LightManager 需要 URP";
            const string msg =
                "Presentation/LightManager 依赖 URP（Universal Render Pipeline）包。\n\n" +
                "当前项目未安装 URP，LightManager 已自动降级为 stub（事件全部返回 Fail）。\n\n" +
                "是否现在通过 Package Manager 自动安装最新版 URP？\n\n" +
                "（也可以稍后通过菜单：Tools/EssSystem/LightManager/Install URP Package）";
            if (EditorUtility.DisplayDialog(title, msg, "立即安装", "稍后再说"))
                InstallUrp();
        }

        private static void InstallUrp()
        {
            if (_addRequest != null && !_addRequest.IsCompleted)
            {
                Debug.Log("[LightManagerInstaller] 已有安装任务在跑，等待中...");
                return;
            }
            Debug.Log($"[LightManagerInstaller] 通过 Package Manager 安装 {URP_PACKAGE_ID}...");
            _addRequest = Client.Add(URP_PACKAGE_ID);
            EditorApplication.update += OnAddRequestProgress;
        }

        private static void OnAddRequestProgress()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;
            EditorApplication.update -= OnAddRequestProgress;

            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[LightManagerInstaller] URP 安装成功：{_addRequest.Result.packageId}");
                SyncDefineSymbol(true);   // 触发重编译
                EditorUtility.DisplayDialog("URP 安装成功",
                    $"已添加 {_addRequest.Result.packageId}。\n\n" +
                    "Unity 即将重新编译，编译完成后 LightManager 即可启用。\n\n" +
                    "首次使用 URP 还需创建 URP Asset：\n" +
                    "  Assets → Create → Rendering → URP Asset (with Universal Renderer)\n" +
                    "并把它指派到 Project Settings → Graphics → Scriptable Render Pipeline Settings。",
                    "OK");
            }
            else
            {
                Debug.LogError($"[LightManagerInstaller] URP 安装失败: {_addRequest.Error?.message}");
                EditorUtility.DisplayDialog("URP 安装失败",
                    $"错误：{_addRequest.Error?.message}\n\n" +
                    "请尝试在 Window → Package Manager 中手动安装 'Universal RP'。",
                    "OK");
            }
            _addRequest = null;
        }

        // ============================================================
        // 包检测 & define 符号管理
        // ============================================================
        private static bool IsUrpInstalled()
        {
            // 读 Packages/manifest.json，避免依赖 Client.List 的异步性
            try
            {
                // 注：必须 UnityEngine.Application 全限定 —— 项目存在 EssSystem.Core.Application 命名空间会遮蔽
                var manifestPath = Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "Packages", "manifest.json");
                if (!File.Exists(manifestPath)) return false;
                var content = File.ReadAllText(manifestPath);
                return content.Contains($"\"{URP_PACKAGE_ID}\"");
            }
            catch { return false; }
        }

        private static bool IsDefineSet()
        {
            var defines = GetCurrentDefines();
            return defines.Contains(URP_DEFINE_SYMBOL);
        }

        private static void SyncDefineSymbol(bool shouldDefine)
        {
            var defines = GetCurrentDefines().ToList();
            bool present = defines.Contains(URP_DEFINE_SYMBOL);

            if (shouldDefine && !present)
            {
                defines.Add(URP_DEFINE_SYMBOL);
                SetCurrentDefines(defines.ToArray());
                Debug.Log($"[LightManagerInstaller] 已添加编译符号：{URP_DEFINE_SYMBOL}");
            }
            else if (!shouldDefine && present)
            {
                defines.Remove(URP_DEFINE_SYMBOL);
                SetCurrentDefines(defines.ToArray());
                Debug.Log($"[LightManagerInstaller] 已移除编译符号：{URP_DEFINE_SYMBOL}");
            }
        }

        // 兼容 Unity 2021.2+ 的新 API；旧 API fallback
        private static string[] GetCurrentDefines()
        {
#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            return PlayerSettings.GetScriptingDefineSymbols(target).Split(';');
#else
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(group).Split(';');
#endif
        }

        private static void SetCurrentDefines(string[] defines)
        {
#if UNITY_2021_2_OR_NEWER
            var target = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
#else
            var group = EditorUserBuildSettings.selectedBuildTargetGroup;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", defines));
#endif
        }
    }
}
#endif
