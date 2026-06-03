// URPBootstrapper.cs  ——  Editor only
// 一键初始化 URP 项目资源 + Graphics / Quality 设置。
// 通过反射访问 URP 类型，文件本身不依赖 URP_INSTALLED 编译符号（避免 URP 未装时编译失败）。
//
// 菜单：
//   Tools/EssSystem/LightManager/Bootstrap URP Project (3D/Forward)
//   Tools/EssSystem/LightManager/Bootstrap URP Project (2D)
//
// 自动触发：LightManagerInstaller 在 URP 包安装成功 + 编译完成后，会调本类的 Bootstrap(false)。

#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EssSystem.Core.Presentation.LightManager.Editor
{
    /// <summary>
    /// URP 项目一键初始化。创建 URP Asset / Renderer Data / Volume Profile 等资源，
    /// 并指派到 Project Settings → Graphics / Quality。
    /// </summary>
    public static class URPBootstrapper
    {
        // ─── 输出路径 ─────────────────────────────────────────
        public const string URP_DIR         = "Assets/Settings/URP";
        public const string RENDERER_NAME   = "URP-ForwardRenderer.asset";
        public const string RENDERER_2D_NAME = "URP-2DRenderer.asset";
        public const string URP_ASSET_NAME  = "URP-Default.asset";
        public const string VOLUME_NAME     = "URP-VolumeProfile.asset";

        public const string URP_PACKAGE_ID  = "com.unity.render-pipelines.universal";

        // ─── 菜单 ─────────────────────────────────────────────
        [MenuItem("Tools/EssSystem/LightManager/Bootstrap URP Project (3D/Forward)", priority = 200)]
        public static void MenuBootstrap3D() => Bootstrap(is2D: false);

        [MenuItem("Tools/EssSystem/LightManager/Bootstrap URP Project (2D)", priority = 201)]
        public static void MenuBootstrap2D() => Bootstrap(is2D: true);

        // ─── 入口 ─────────────────────────────────────────────
        public static void Bootstrap(bool is2D)
        {
            try
            {
                if (!IsUrpInstalled())
                {
                    EditorUtility.DisplayDialog("URP 未安装",
                        "请先通过菜单 Tools/EssSystem/LightManager/Install URP Package 安装 URP 包。",
                        "OK");
                    return;
                }

                EnsureFolder(URP_DIR);
                int created = 0;

                // 1) Renderer Data（ForwardRendererData / UniversalRendererData / Renderer2DData）
                string rendererName = is2D ? RENDERER_2D_NAME : RENDERER_NAME;
                string rendererPath = $"{URP_DIR}/{rendererName}";
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererPath);
                if (rendererData == null)
                {
                    var rendererType = ResolveRendererType(is2D);
                    if (rendererType == null) throw new Exception("找不到 URP RendererData 类型");
                    rendererData = ScriptableObject.CreateInstance(rendererType);
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                    created++;
                }

                // 2) URP Asset
                string urpPath = $"{URP_DIR}/{URP_ASSET_NAME}";
                var urpAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(urpPath);
                var urpAssetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
                if (urpAssetType == null) throw new Exception("找不到 UniversalRenderPipelineAsset 类型");
                var wantedRendererType = ResolveRendererType(is2D);

                if (urpAsset == null)
                {
                    urpAsset = ScriptableObject.CreateInstance(urpAssetType);
                    AttachRendererData(urpAsset, urpAssetType, rendererData);
                    AssetDatabase.CreateAsset(urpAsset, urpPath);
                    created++;
                }
                else
                {
                    // URP-Default 已存在：检查当前 RendererData 是否就是想要的类型
                    // —— 之前的 Bootstrap 2D 把 Renderer2DData 写进去了，现在重跑 3D 要换回 UniversalRendererData
                    if (NeedsRendererSwap(urpAsset, urpAssetType, wantedRendererType))
                    {
                        AttachRendererData(urpAsset, urpAssetType, rendererData);
                        EditorUtility.SetDirty(urpAsset);
                        AssetDatabase.SaveAssetIfDirty(urpAsset);
                        created++;
                        var existingName = GetCurrentRendererName(urpAsset, urpAssetType);
                        Debug.Log($"[URPBootstrapper] URP-Default RendererData 切换：{existingName} → {wantedRendererType?.Name}（{(is2D ? "2D" : "3D")} 模式）");
                    }
                }

                // 3) Graphics.defaultRenderPipeline
                var rpAsset = urpAsset as RenderPipelineAsset;
                if (rpAsset == null) throw new Exception("URP Asset 类型转换 RenderPipelineAsset 失败");
                if (TrySetGraphicsDefaultRenderPipeline(rpAsset)) created++;

                // 4) 所有 Quality Level 都指派该 URP Asset（反射 + 回退）
                ApplyQualityRenderPipeline(rpAsset);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[URPBootstrapper] URP {(is2D ? "2D" : "3D")} 初始化完成，新建/更新 {created} 个资源。");
                EditorUtility.DisplayDialog("URP 初始化完成",
                    $"已创建/更新 URP 资源：\n" +
                    $"  • {rendererPath}\n" +
                    $"  • {urpPath}\n\n" +
                    "并已设置到 Project Settings → Graphics / Quality。\n\n" +
                    "现在可以：\n" +
                    "  • 场景里建 Camera（自动用 URP 渲染）\n" +
                    "  • LightManager 节点挂 Volume 引用 post-FX\n" +
                    "  • 重启 Play 看到 URP 接管渲染",
                    "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[URPBootstrapper] 初始化失败: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("URP 初始化失败",
                    $"错误：{e.Message}\n\n详细堆栈见 Console。",
                    "OK");
            }
        }

        // ─── 反射辅助 ─────────────────────────────────────────
        private static Type ResolveRendererType(bool is2D)
        {
            // URP 17+: UniversalRendererData
            // URP 14-16: UniversalRendererData
            // URP 12-13: ForwardRendererData
            // URP 2D: Renderer2DData
            if (is2D)
            {
                return FindType("UnityEngine.Rendering.Universal.Renderer2DData");
            }
            return FindType("UnityEngine.Rendering.Universal.UniversalRendererData")
                ?? FindType("UnityEngine.Rendering.Universal.ForwardRendererData");
        }

        /// <summary>
        /// 检查 URP-Default.m_RendererDataList[0] 是不是 wantedRendererType
        /// 不匹配 / 列表为空 → 需要替换。
        /// </summary>
        private static bool NeedsRendererSwap(ScriptableObject urpAsset, Type urpAssetType, Type wantedRendererType)
        {
            if (wantedRendererType == null) return false;
            var listField = urpAssetType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField == null) return false;
            var list = listField.GetValue(urpAsset) as System.Collections.IList;
            if (list == null || list.Count == 0) return true;
            var first = list[0] as UnityEngine.Object;
            if (first == null) return true;
            return first.GetType() != wantedRendererType;
        }

        private static string GetCurrentRendererName(ScriptableObject urpAsset, Type urpAssetType)
        {
            var listField = urpAssetType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = listField?.GetValue(urpAsset) as System.Collections.IList;
            if (list == null || list.Count == 0) return "<empty>";
            var first = list[0] as UnityEngine.Object;
            return first == null ? "<null>" : first.GetType().Name;
        }

        private static void AttachRendererData(object urpAsset, Type urpAssetType, ScriptableObject rendererData)
        {
            // URP 内部字段：
            //   m_RendererDataList  在老 URP 是 List<ScriptableRendererData>
            //                       在新 URP (Unity 6) 是 ScriptableRendererData[]
            //   m_DefaultRendererIndex : int
            // 全部用反射拿 FieldType 后再判断，避免直接引用 ScriptableRendererData
            // （URP 未装时该类型不可见，直接 typeof 会 CS0246）
            var listField = urpAssetType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField != null)
            {
                var fieldType = listField.FieldType;
                if (fieldType.IsArray)
                {
                    // 数组版本：ScriptableRendererData[]
                    var elemType = fieldType.GetElementType();
                    var arr = Array.CreateInstance(elemType, 1);
                    arr.SetValue(rendererData, 0);
                    listField.SetValue(urpAsset, arr);
                }
                else if (fieldType.IsGenericType)
                {
                    // List 版本：List<ScriptableRendererData>
                    var list = (IList)Activator.CreateInstance(fieldType);
                    list.Add(rendererData);
                    listField.SetValue(urpAsset, list);
                }
                else
                {
                    Debug.LogWarning($"[URPBootstrapper] m_RendererDataList 字段类型 {fieldType} 既不是数组也不是 List，跳过。");
                }
            }
            var indexField = urpAssetType.GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            if (indexField != null) indexField.SetValue(urpAsset, 0);
        }

        /// <summary>
        /// 设 GraphicsSettings.defaultRenderPipeline（2021.2+ API），老版本走 Instance.renderPipelineAsset。
        /// </summary>
        private static bool TrySetGraphicsDefaultRenderPipeline(RenderPipelineAsset rpAsset)
        {
            // 新 API（2021.2+）
            var prop = typeof(GraphicsSettings).GetProperty(
                "defaultRenderPipeline", BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite)
            {
                var current = prop.GetValue(null);
                // RenderPipelineAsset 是 UnityEngine.Object，== 会触发 Unity 的"对象销毁但引用还在"假相等
                // 用 Equals 做值比较（或 (Object)current == rpAsset 显式声明走引用比较）
                if (Equals(current, rpAsset)) return false;
                prop.SetValue(null, rpAsset);
                return true;
            }
            // 老 API：GraphicsSettings.renderPipelineAsset
            var legacyProp = typeof(GraphicsSettings).GetProperty(
                "renderPipelineAsset", BindingFlags.Public | BindingFlags.Instance);
            if (legacyProp != null && legacyProp.CanWrite)
            {
                // 注意：老 API 是 instance 属性（单例 GraphicsSettings）
                var settingsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    "ProjectSettings/GraphicsSettings.asset");
                legacyProp.SetValue(settingsObj, rpAsset);
                EditorUtility.SetDirty(settingsObj);
                return true;
            }
            Debug.LogWarning("[URPBootstrapper] 找不到 GraphicsSettings 的 RenderPipeline 属性（API Compat Level 太旧？）");
            return false;
        }

        /// <summary>
        /// 把 RP asset 套到所有 Quality Level。三层兜底：
        ///   1) SetRenderPipelineAssetAt（2021.2+ 反射，per-level）
        ///   2) 循环 SetQualityLevel + renderPipeline 属性（适用于老 API 或 API 缺失）
        ///   3) SerializedObject 直接改 QualitySettings.asset 的 m_QualitySettings[*].customRenderPipeline
        /// </summary>
        private static void ApplyQualityRenderPipeline(RenderPipelineAsset rpAsset)
        {
            // ── 1) SetRenderPipelineAssetAt（per-level，最干净） ──
            var setAt = typeof(QualitySettings).GetMethod(
                "SetRenderPipelineAssetAt",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: new[] { typeof(int), typeof(RenderPipelineAsset) },
                modifiers: null);
            if (setAt != null)
            {
                var names = QualitySettings.names;
                for (int i = 0; i < names.Length; i++)
                    setAt.Invoke(null, new object[] { i, rpAsset });
                Debug.Log($"[URPBootstrapper] SetRenderPipelineAssetAt: 绑定 {names.Length} 个 quality level");
                return;
            }

            // ── 2) 循环切 level + renderPipeline 属性 ──
            var rpProp = typeof(QualitySettings).GetProperty(
                "renderPipeline", BindingFlags.Public | BindingFlags.Static);
            if (rpProp != null && rpProp.CanWrite)
            {
                var names = QualitySettings.names;
                int original = QualitySettings.GetQualityLevel();
                try
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                        rpProp.SetValue(null, rpAsset);
                    }
                    Debug.Log($"[URPBootstrapper] 循环切 level 方式绑定 {names.Length} 个 quality level");
                }
                finally
                {
                    // 恢复用户原本选的 level
                    QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
                }
                return;
            }

            // ── 3) 终极兜底：SerializedObject 直接改 YAML ──
            ApplyQualityRenderPipelineSerialized(rpAsset);
        }

        /// <summary>
        /// 直接编辑 ProjectSettings/QualitySettings.asset 的 m_QualitySettings[*].customRenderPipeline。
        /// 适用于上述 API 全部不可用的情况（如某些 Unity 版本 / 自定义 QualitySettings）。
        /// </summary>
        private static void ApplyQualityRenderPipelineSerialized(RenderPipelineAsset rpAsset)
        {
            const string assetPath = "ProjectSettings/QualitySettings.asset";
            // ProjectSettings 下的资产不能用 AssetDatabase.LoadAssetAtPath，要走 LoadAllAssetsAtPath
            var objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            UnityEngine.Object qualityAsset = null;
            foreach (var o in objs)
            {
                if (o == null) continue;
                if (o.GetType().Name == "QualitySettings")
                {
                    qualityAsset = o;
                    break;
                }
            }
            if (qualityAsset == null)
            {
                // 退化：直接走 Resources / 反射 QualitySettings.GetSerializedObject()
                Debug.LogWarning("[URPBootstrapper] SerializedObject 兜底：找不到 QualitySettings 资产对象，请手动在 Project Settings → Quality 里绑定 RP。");
                return;
            }

            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                Debug.LogWarning("[URPBootstrapper] SerializedObject 兜底：m_QualitySettings 不是数组");
                return;
            }

            int count = 0;
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var rp = level.FindPropertyRelative("customRenderPipeline");
                if (rp != null)
                {
                    rp.objectReferenceValue = rpAsset;
                    count++;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qualityAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[URPBootstrapper] SerializedObject 兜底：绑定 {count}/{levels.arraySize} 个 quality level");
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static void EnsureFolder(string assetPath)
        {
            var parts = assetPath.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        internal static bool IsUrpInstalled()
        {
            try
            {
                // 必须 UnityEngine.Application 全限定 —— 项目存在 EssSystem.Core.Application 命名空间会遮蔽
                var manifestPath = Path.Combine(
                    Path.GetDirectoryName(UnityEngine.Application.dataPath),
                    "Packages", "manifest.json");
                if (!File.Exists(manifestPath)) return false;
                var content = File.ReadAllText(manifestPath);
                return content.Contains($"\"{URP_PACKAGE_ID}\"");
            }
            catch { return false; }
        }
    }
}
#endif
