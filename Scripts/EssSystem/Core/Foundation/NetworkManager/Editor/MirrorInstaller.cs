// MirrorInstaller —— Editor only
// 监听编辑器加载，自动确保 Mirror 已通过 OpenUPM 安装。
// 流程：检测 Packages/manifest.json → 缺则注入 OpenUPM scoped registry +
//       Client.Add(com.mirror-networking.mirror) → 安装成功后设置 MIRROR_INSTALLED 宏。
//
// 用户可通过菜单：
//   Tools/WulaSystem/Foundation/Network/Mirror/Install Mirror Now
//   Tools/WulaSystem/Foundation/Network/Mirror/Uninstall Mirror
//   Tools/WulaSystem/Foundation/Network/Mirror/Toggle Auto-Install
//
// 配置常量集中在文件顶部，OpenUPM 包名/作用域有变时改这里即可。

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EssSystem.Core.Base.Util;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace EssSystem.Core.Foundation.NetworkManager.EditorTools
{
    /// <summary>
    /// 在编辑器启动时检查并安装 Mirror（OpenUPM）。
    /// </summary>
    [InitializeOnLoad]
    public static class MirrorInstaller
    {
        private const string MENU_PREFIX = "Tools/WulaSystem/Foundation/Network/Mirror/";
        // ─── 配置 ───────────────────────────────────────────────
        public const string MirrorPackageId = "com.mirrornetworking.mirror";
        public const string OpenUpmRegistryName = "OpenUPM";
        public const string OpenUpmRegistryUrl = "https://package.openupm.com";
        public static readonly string[] OpenUpmScopes = { "com.mirrornetworking", "com.cysharp" };
        public const string ScriptingDefineMirrorInstalled = "MIRROR_INSTALLED";

        // EditorPrefs 键
        private const string EpAutoInstall = "WulaFw.Network.AutoInstallMirror";
        private const string EpInstallInProgress = "WulaFw.Network.InstallInProgress";

        // ─── 静态状态 ───────────────────────────────────────────
        private static AddRequest _addRequest;
        private static ListRequest _listRequest;
        private static RemoveRequest _removeRequest;
        private static bool _checkScheduled;

        public static bool AutoInstall
        {
            get => EditorPrefs.GetBool(EpAutoInstall, true);
            set => EditorPrefs.SetBool(EpAutoInstall, value);
        }

        // ─── 入口：编辑器启动时延迟一帧检查 ──────────────────
        static MirrorInstaller()
        {
            // 安装中（重启后）回调用，跳过自动检查
            if (EditorPrefs.GetBool(EpInstallInProgress, false))
            {
                EditorPrefs.SetBool(EpInstallInProgress, false);
                EditorApplication.delayCall += () =>
                {
                    EnsureScriptingDefine(true);
                    Debug.Log("[MirrorInstaller] Mirror 安装完成，已设置 MIRROR_INSTALLED 宏。");
                };
                return;
            }

            if (!AutoInstall) return;
            if (_checkScheduled) return;
            _checkScheduled = true;
            EditorApplication.delayCall += DoStartupCheck;
        }

        private static void DoStartupCheck()
        {
            // 已安装 → 仅同步 define
            if (IsMirrorInstalled())
            {
                EnsureScriptingDefine(true);
                return;
            }
            // 未安装且开启了自动安装 → 弹窗确认（不阻塞）
            if (EditorUtility.DisplayDialog(
                "WulaFramework · Mirror 未安装",
                "检测到挂载的 NetworkManager 需要 Mirror，但 Mirror 尚未安装。\n\n" +
                "点击「安装」将自动：\n" +
                "  1. 注入 OpenUPM Scoped Registry\n" +
                "  2. 安装包 " + MirrorPackageId + "\n" +
                "  3. 设置 MIRROR_INSTALLED 编译宏\n\n" +
                "首次安装可能触发包重启编译，约 30-60 秒。",
                "立即安装", "稍后再说"))
            {
                InstallMirror();
            }
            else
            {
                EnsureScriptingDefine(false);
                Debug.LogWarning("[MirrorInstaller] 已跳过 Mirror 安装。可通过菜单 Tools/WulaSystem/Foundation/Network/Mirror/Install Mirror Now 重新触发。");
            }
        }

        // ─── 菜单 ───────────────────────────────────────────────
        [MenuItem(MENU_PREFIX + "Install Mirror Now", priority = 100)]
        public static void InstallMirror()
        {
            EnsureOpenUpmRegistry();
            EditorPrefs.SetBool(EpInstallInProgress, true);
            Debug.Log("[MirrorInstaller] 开始安装 Mirror...");
            _addRequest = Client.Add(MirrorPackageId);
            EditorApplication.update += OnAddProgress;
        }

        [MenuItem(MENU_PREFIX + "Uninstall Mirror", priority = 101)]
        public static void UninstallMirror()
        {
            if (!IsMirrorInstalled())
            {
                Debug.Log("[MirrorInstaller] Mirror 未安装。");
                return;
            }
            _removeRequest = Client.Remove(MirrorPackageId);
            EditorApplication.update += OnRemoveProgress;
        }

        [MenuItem(MENU_PREFIX + "Toggle Auto-Install", priority = 200)]
        public static void ToggleAutoInstall()
        {
            AutoInstall = !AutoInstall;
            Debug.Log($"[MirrorInstaller] AutoInstall = {AutoInstall}");
        }

        [MenuItem(MENU_PREFIX + "Repair OpenUPM Scopes", priority = 150)]
        public static void RepairOpenUpmScopes()
        {
            var manifestPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifestPath)) { Debug.LogError("[MirrorInstaller] manifest.json 不存在"); return; }
            var raw = File.ReadAllText(manifestPath);
            var root = MiniJson.Deserialize(raw) as Dictionary<string, object>;
            if (root == null) { Debug.LogError("[MirrorInstaller] manifest.json 解析失败"); return; }

            // 移除旧的错误 scope / dependency
            if (root.TryGetValue("scopedRegistries", out var rs) && rs is List<object> regs)
            {
                foreach (var r in regs.OfType<Dictionary<string, object>>())
                {
                    if (r.TryGetValue("scopes", out var s) && s is List<object> sl)
                    {
                        sl.RemoveAll(x => x is string xs && (xs == "com.mirror-networking" || xs == "com.vis2k"));
                        r["scopes"] = sl;
                    }
                }
            }
            if (root.TryGetValue("dependencies", out var dep) && dep is Dictionary<string, object> deps)
            {
                deps.Remove("com.mirror-networking.mirror");
                deps.Remove("com.vis2k.mirror");
            }
            File.WriteAllText(manifestPath, MiniJson.Serialize(root, pretty: true));
            Debug.Log("[MirrorInstaller] 已清理旧的错误 scope/依赖。请重新触发 Install Mirror Now。");
            Client.Resolve();
        }

        [MenuItem(MENU_PREFIX + "Check Mirror Status", priority = 201)]
        public static void CheckStatus()
        {
            var installed = IsMirrorInstalled();
            EditorUtility.DisplayDialog("Mirror Status",
                $"Installed: {installed}\nAutoInstall: {AutoInstall}\nMIRROR_INSTALLED define: {HasDefine(ScriptingDefineMirrorInstalled)}",
                "OK");
        }

        // ─── Package 安装回调 ───────────────────────────────────
        private static void OnAddProgress()
        {
            if (_addRequest == null) { EditorApplication.update -= OnAddProgress; return; }
            if (!_addRequest.IsCompleted) return;

            EditorApplication.update -= OnAddProgress;
            if (_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[MirrorInstaller] 安装成功: {_addRequest.Result.packageId}");
                EnsureScriptingDefine(true);
            }
            else
            {
                EditorPrefs.SetBool(EpInstallInProgress, false);
                Debug.LogError($"[MirrorInstaller] 安装失败: {_addRequest.Error?.message}\n" +
                               "请检查网络或手动通过 Package Manager / Asset Store 安装 Mirror。");
            }
            _addRequest = null;
        }

        private static void OnRemoveProgress()
        {
            if (_removeRequest == null) { EditorApplication.update -= OnRemoveProgress; return; }
            if (!_removeRequest.IsCompleted) return;
            EditorApplication.update -= OnRemoveProgress;
            if (_removeRequest.Status == StatusCode.Success)
            {
                Debug.Log("[MirrorInstaller] Mirror 已卸载。");
                EnsureScriptingDefine(false);
            }
            else
            {
                Debug.LogError($"[MirrorInstaller] 卸载失败: {_removeRequest.Error?.message}");
            }
            _removeRequest = null;
        }

        // ─── 检测：包是否在 manifest.json 里 ────────────────────
        public static bool IsMirrorInstalled()
        {
            try
            {
                var manifestPath = Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "manifest.json");
                manifestPath = Path.GetFullPath(manifestPath);
                if (!File.Exists(manifestPath)) return false;
                var json = File.ReadAllText(manifestPath);
                // 简单字串包含即可，性能优先；OpenUPM 解析也走这条
                return json.Contains($"\"{MirrorPackageId}\"");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MirrorInstaller] IsMirrorInstalled 检测异常: {ex.Message}");
                return false;
            }
        }

        // ─── manifest.json 注入 OpenUPM scoped registry ─────────
        private static void EnsureOpenUpmRegistry()
        {
            var manifestPath = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifestPath))
            {
                Debug.LogError("[MirrorInstaller] manifest.json 不存在，无法注入 OpenUPM registry。");
                return;
            }

            var raw = File.ReadAllText(manifestPath);
            var root = MiniJson.Deserialize(raw) as Dictionary<string, object>;
            if (root == null)
            {
                Debug.LogError("[MirrorInstaller] manifest.json 解析失败。");
                return;
            }

            var registries = root.TryGetValue("scopedRegistries", out var raw2) && raw2 is List<object> lst
                ? lst
                : new List<object>();

            // 已有 OpenUPM？补全 scope 即可
            Dictionary<string, object> openUpm = null;
            foreach (var r in registries)
            {
                if (r is Dictionary<string, object> d &&
                    d.TryGetValue("url", out var u) && (u as string) == OpenUpmRegistryUrl)
                {
                    openUpm = d;
                    break;
                }
            }
            var changed = false;
            if (openUpm == null)
            {
                openUpm = new Dictionary<string, object>
                {
                    {"name", OpenUpmRegistryName},
                    {"url", OpenUpmRegistryUrl},
                    {"scopes", new List<object>(OpenUpmScopes)}
                };
                registries.Add(openUpm);
                changed = true;
            }
            else
            {
                var scopes = openUpm.TryGetValue("scopes", out var s) && s is List<object> sl
                    ? sl
                    : new List<object>();
                foreach (var need in OpenUpmScopes)
                {
                    if (!scopes.Contains(need))
                    {
                        scopes.Add(need);
                        changed = true;
                    }
                }
                openUpm["scopes"] = scopes;
            }

            if (!changed)
            {
                root["scopedRegistries"] = registries;
                return;
            }

            root["scopedRegistries"] = registries;
            File.WriteAllText(manifestPath, MiniJson.Serialize(root, pretty: true));
            Debug.Log("[MirrorInstaller] 已写入 OpenUPM scoped registry 到 Packages/manifest.json");
        }

        // ─── Scripting Define 管理 ──────────────────────────────
        private static bool HasDefine(string symbol)
        {
            var grp = EditorUserBuildSettings.selectedBuildTargetGroup;
            var named = NamedBuildTarget.FromBuildTargetGroup(grp);
            PlayerSettings.GetScriptingDefineSymbols(named, out var defs);
            return defs.Any(s => s.Trim() == symbol);
        }

        private static void EnsureScriptingDefine(bool desired)
        {
            // 同步 Standalone / iOS / Android 三个常用平台；其余按需扩展
            var groups = new[]
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.Android,
                BuildTargetGroup.iOS,
                BuildTargetGroup.WebGL,
            };
            foreach (var grp in groups)
            {
                var named = NamedBuildTarget.FromBuildTargetGroup(grp);
                PlayerSettings.GetScriptingDefineSymbols(named, out var defsArr);
                var set = new HashSet<string>(defsArr.Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)));
                var dirty = false;
                if (desired && !set.Contains(ScriptingDefineMirrorInstalled))
                {
                    set.Add(ScriptingDefineMirrorInstalled); dirty = true;
                }
                else if (!desired && set.Contains(ScriptingDefineMirrorInstalled))
                {
                    set.Remove(ScriptingDefineMirrorInstalled); dirty = true;
                }
                if (dirty)
                    PlayerSettings.SetScriptingDefineSymbols(named, set.ToArray());
            }
        }
    }
}
#endif
