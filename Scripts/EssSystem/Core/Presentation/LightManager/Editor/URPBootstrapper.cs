#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EssSystem.Core.Presentation.LightManager.Editor
{
    /// <summary>
    /// Creates or refreshes URP renderer assets and assigns them to Graphics/Quality settings.
    /// </summary>
    public static class URPBootstrapper
    {
        private const string MENU_PREFIX = "Tools/WulaSystem/Presentation/LightManager/URP/";
        public const string URP_DIR = "Assets/Settings/URP";
        public const string RENDERER_NAME = "URP-ForwardRenderer.asset";
        public const string RENDERER_2D_NAME = "URP-2DRenderer.asset";
        public const string URP_ASSET_NAME = "URP-Default.asset";
        public const string VOLUME_NAME = "URP-VolumeProfile.asset";
        public const string URP_PACKAGE_ID = "com.unity.render-pipelines.universal";

        [MenuItem(MENU_PREFIX + "Bootstrap URP Project (3D/Forward)", priority = 200)]
        public static void MenuBootstrap3D() => Bootstrap(is2D: false);

        [MenuItem(MENU_PREFIX + "Bootstrap URP Project (2D)", priority = 201)]
        public static void MenuBootstrap2D() => Bootstrap(is2D: true);

        public static void Bootstrap(bool is2D)
        {
            try
            {
                if (!IsUrpInstalled())
                {
                    EditorUtility.DisplayDialog("URP is not installed",
                        "Please install URP from Tools/WulaSystem/Presentation/LightManager/URP/Install URP Package first.",
                        "OK");
                    return;
                }

                EnsureFolder(URP_DIR);
                int changed = 0;

                string rendererName = is2D ? RENDERER_2D_NAME : RENDERER_NAME;
                string rendererPath = $"{URP_DIR}/{rendererName}";
                var rendererData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(rendererPath);
                if (rendererData == null)
                {
                    var rendererType = ResolveRendererType(is2D);
                    if (rendererType == null)
                        throw new Exception("Unable to resolve URP renderer data type.");

                    rendererData = ScriptableObject.CreateInstance(rendererType);
                    AssetDatabase.CreateAsset(rendererData, rendererPath);
                    changed++;
                }

                string urpPath = $"{URP_DIR}/{URP_ASSET_NAME}";
                var urpAsset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(urpPath);
                var urpAssetType = FindType("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
                if (urpAssetType == null)
                    throw new Exception("Unable to resolve UniversalRenderPipelineAsset type.");

                var wantedRendererType = ResolveRendererType(is2D);
                if (urpAsset == null)
                {
                    urpAsset = ScriptableObject.CreateInstance(urpAssetType);
                    AttachRendererData(urpAsset, urpAssetType, rendererData);
                    AssetDatabase.CreateAsset(urpAsset, urpPath);
                    changed++;
                }
                else if (NeedsRendererSwap(urpAsset, urpAssetType, wantedRendererType))
                {
                    var existingName = GetCurrentRendererName(urpAsset, urpAssetType);
                    AttachRendererData(urpAsset, urpAssetType, rendererData);
                    EditorUtility.SetDirty(urpAsset);
                    changed++;
                    Debug.Log($"[URPBootstrapper] Swapped renderer data: {existingName} -> {wantedRendererType?.Name}.");
                }

                var renderPipelineAsset = urpAsset as RenderPipelineAsset;
                if (renderPipelineAsset == null)
                    throw new Exception("URP asset can not be cast to RenderPipelineAsset.");

                if (TrySetGraphicsDefaultRenderPipeline(renderPipelineAsset))
                    changed++;

                ApplyQualityRenderPipeline(renderPipelineAsset);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"[URPBootstrapper] URP {(is2D ? "2D" : "3D")} bootstrap complete. Changed {changed} assets/settings.");
                EditorUtility.DisplayDialog("URP Bootstrap Complete",
                    $"Updated URP assets:\n{rendererPath}\n{urpPath}\n\nGraphics and Quality settings were updated.",
                    "OK");
            }
            catch (Exception e)
            {
                Debug.LogError($"[URPBootstrapper] Bootstrap failed: {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("URP Bootstrap Failed",
                    $"Error: {e.Message}\n\nSee Console for details.",
                    "OK");
            }
        }

        private static Type ResolveRendererType(bool is2D)
        {
            if (is2D)
                return FindType("UnityEngine.Rendering.Universal.Renderer2DData");

            return FindType("UnityEngine.Rendering.Universal.UniversalRendererData")
                ?? FindType("UnityEngine.Rendering.Universal.ForwardRendererData");
        }

        private static bool NeedsRendererSwap(ScriptableObject urpAsset, Type urpAssetType, Type wantedRendererType)
        {
            if (wantedRendererType == null) return false;

            var listField = urpAssetType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField == null) return false;

            var list = listField.GetValue(urpAsset) as IList;
            if (list == null || list.Count == 0) return true;

            var first = list[0] as UnityEngine.Object;
            return first == null || first.GetType() != wantedRendererType;
        }

        private static string GetCurrentRendererName(ScriptableObject urpAsset, Type urpAssetType)
        {
            var listField = urpAssetType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            var list = listField?.GetValue(urpAsset) as IList;
            if (list == null || list.Count == 0) return "<empty>";

            var first = list[0] as UnityEngine.Object;
            return first == null ? "<null>" : first.GetType().Name;
        }

        private static void AttachRendererData(object urpAsset, Type urpAssetType, ScriptableObject rendererData)
        {
            var listField = urpAssetType.GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField != null)
            {
                var fieldType = listField.FieldType;
                if (fieldType.IsArray)
                {
                    var elemType = fieldType.GetElementType();
                    var arr = Array.CreateInstance(elemType, 1);
                    arr.SetValue(rendererData, 0);
                    listField.SetValue(urpAsset, arr);
                }
                else if (fieldType.IsGenericType)
                {
                    var list = (IList)Activator.CreateInstance(fieldType);
                    list.Add(rendererData);
                    listField.SetValue(urpAsset, list);
                }
                else
                {
                    Debug.LogWarning($"[URPBootstrapper] Unsupported m_RendererDataList field type: {fieldType}.");
                }
            }

            var indexField = urpAssetType.GetField("m_DefaultRendererIndex", BindingFlags.NonPublic | BindingFlags.Instance);
            if (indexField != null)
                indexField.SetValue(urpAsset, 0);
        }

        private static bool TrySetGraphicsDefaultRenderPipeline(RenderPipelineAsset renderPipelineAsset)
        {
            var prop = typeof(GraphicsSettings).GetProperty("defaultRenderPipeline", BindingFlags.Public | BindingFlags.Static);
            if (prop != null && prop.CanWrite)
            {
                var current = prop.GetValue(null);
                if (Equals(current, renderPipelineAsset)) return false;

                prop.SetValue(null, renderPipelineAsset);
                return true;
            }

            var legacyProp = typeof(GraphicsSettings).GetProperty("renderPipelineAsset", BindingFlags.Public | BindingFlags.Instance);
            if (legacyProp != null && legacyProp.CanWrite)
            {
                var settingsObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("ProjectSettings/GraphicsSettings.asset");
                if (settingsObj == null) return false;

                legacyProp.SetValue(settingsObj, renderPipelineAsset);
                EditorUtility.SetDirty(settingsObj);
                return true;
            }

            Debug.LogWarning("[URPBootstrapper] Unable to find a writable GraphicsSettings render pipeline property.");
            return false;
        }

        private static void ApplyQualityRenderPipeline(RenderPipelineAsset renderPipelineAsset)
        {
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
                    setAt.Invoke(null, new object[] { i, renderPipelineAsset });

                Debug.Log($"[URPBootstrapper] Bound render pipeline to {names.Length} quality levels.");
                return;
            }

            var renderPipelineProp = typeof(QualitySettings).GetProperty("renderPipeline", BindingFlags.Public | BindingFlags.Static);
            if (renderPipelineProp != null && renderPipelineProp.CanWrite)
            {
                var names = QualitySettings.names;
                int original = QualitySettings.GetQualityLevel();
                try
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                        renderPipelineProp.SetValue(null, renderPipelineAsset);
                    }

                    Debug.Log($"[URPBootstrapper] Bound render pipeline to {names.Length} quality levels.");
                }
                finally
                {
                    QualitySettings.SetQualityLevel(original, applyExpensiveChanges: false);
                }

                return;
            }

            ApplyQualityRenderPipelineSerialized(renderPipelineAsset);
        }

        private static void ApplyQualityRenderPipelineSerialized(RenderPipelineAsset renderPipelineAsset)
        {
            const string assetPath = "ProjectSettings/QualitySettings.asset";
            var objs = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            UnityEngine.Object qualityAsset = null;

            foreach (var obj in objs)
            {
                if (obj == null) continue;
                if (obj.GetType().Name == "QualitySettings")
                {
                    qualityAsset = obj;
                    break;
                }
            }

            if (qualityAsset == null)
            {
                Debug.LogWarning("[URPBootstrapper] Unable to find QualitySettings asset. Please bind the URP asset manually.");
                return;
            }

            var serializedObject = new SerializedObject(qualityAsset);
            var levels = serializedObject.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                Debug.LogWarning("[URPBootstrapper] m_QualitySettings is not an array.");
                return;
            }

            int count = 0;
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                var rp = level.FindPropertyRelative("customRenderPipeline");
                if (rp == null) continue;

                rp.objectReferenceValue = renderPipelineAsset;
                count++;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(qualityAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[URPBootstrapper] Bound render pipeline to {count}/{levels.arraySize} quality levels via SerializedObject.");
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
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
                var manifestPath = Path.Combine(
                    Path.GetDirectoryName(UnityEngine.Application.dataPath),
                    "Packages",
                    "manifest.json");

                if (!File.Exists(manifestPath)) return false;

                var content = File.ReadAllText(manifestPath);
                return content.Contains($"\"{URP_PACKAGE_ID}\"");
            }
            catch
            {
                return false;
            }
        }
    }
}
#endif
