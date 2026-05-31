#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace EssSystem.Core.Foundation.ResourceManager.Editor
{
    /// <summary>
    /// 资源 Manifest 生成工具。
    ///
    /// 扫描 Assets/Resources/ 下所有支持的资产，输出 Resources/ResourceManifest.json。
    /// 运行时 ResourceService 读取该文件构建路径索引，启动时无需全量加载所有资产。
    ///
    /// 有两种触发方式：
    ///   1. 自动：每次 BuildPipeline.BuildPlayer 时由 EssSystemBuildPreprocessor 自动调用
    ///   2. 手动：菜单 Tools > EssSystem > Generate Resource Manifest
    /// </summary>
    public static class ResourceManifestGenerator
    {
        private const string ResourcesRoot = "Assets/Resources";
        private const string OutputPath    = "Assets/Resources/ResourceManifest.json";

        // ============================================================
        // 公开 API（由 EssSystemBuildPreprocessor 和菜单项调用）
        // ============================================================

        [MenuItem("Tools/EssSystem/Generate Resource Manifest")]
        public static void GenerateFromMenu()
        {
            Generate();
            EditorUtility.DisplayDialog(
                "Resource Manifest",
                $"生成完成 → {OutputPath}",
                "OK");
        }

        /// <summary>
        /// 扫描 Resources/ 目录，生成路径索引 JSON。
        /// 构建流程中自动调用此方法，项目无需手动触发。
        /// </summary>
        public static void Generate()
        {
            var entries = new List<ResourceManifestEntry>();
            var guids   = AssetDatabase.FindAssets("", new[] { ResourcesRoot });

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(assetPath)) continue;

                var resourcePath = ToResourcePath(assetPath);
                if (string.IsNullOrEmpty(resourcePath)) continue;

                // 跳过 Manifest 文件本身，避免自引用
                if (resourcePath == "ResourceManifest") continue;

                var type = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
                if (type == null || !IsSupportedType(type)) continue;

                // 精灵图集：枚举所有子精灵，每张分别建立索引条目
                if (type == typeof(Texture2D) || type == typeof(Sprite))
                {
                    var allAtPath = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    var atlasName = Path.GetFileNameWithoutExtension(assetPath);
                    bool hasSubs  = false;

                    foreach (var obj in allAtPath)
                    {
                        // 只收集与文件名不同的 Sprite（子精灵）
                        if (obj is Sprite sprite && sprite.name != atlasName)
                        {
                            entries.Add(new ResourceManifestEntry
                            {
                                key        = sprite.name,
                                path       = resourcePath,
                                spriteName = sprite.name
                            });
                            hasSubs = true;
                        }
                    }

                    // 图集 / 单张精灵都注册一条以文件名为 key 的条目，spriteName 始终留空
                    // → 进入 _pathIndex，可通过 GetSpriteAsync("atlasName") 加载整张纹理
                    entries.Add(new ResourceManifestEntry
                    {
                        key        = atlasName,
                        path       = resourcePath,
                        spriteName = ""
                    });

                    if (!hasSubs) continue;  // 单张精灵不需要额外处理，已注册完毕
                    continue;                // 图集：子精灵已在循环中注册
                }

                // 普通资产：直接以文件名为 key
                entries.Add(new ResourceManifestEntry
                {
                    key  = Path.GetFileNameWithoutExtension(assetPath),
                    path = resourcePath
                });
            }

            WriteManifest(entries);
            Debug.Log($"[ResourceManifestGenerator] 生成完成：{entries.Count} 条记录 → {OutputPath}");
        }

        // ============================================================
        // 内部工具方法
        // ============================================================

        private static void WriteManifest(List<ResourceManifestEntry> entries)
        {
            var dir = Path.GetDirectoryName(OutputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = new ResourceManifestData { entries = entries };
            File.WriteAllText(OutputPath, JsonUtility.ToJson(data, prettyPrint: true),
                System.Text.Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        /// <summary>将完整 Asset 路径转换为 Resources/ 相对路径（不含扩展名）。</summary>
        private static string ToResourcePath(string assetPath)
        {
            const string prefix = "Assets/Resources/";
            if (!assetPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            var rel = assetPath.Substring(prefix.Length);
            var ext = Path.GetExtension(rel);
            return string.IsNullOrEmpty(ext) ? rel : rel.Substring(0, rel.Length - ext.Length);
        }

        private static bool IsSupportedType(Type type) =>
            type == typeof(GameObject)
         || type == typeof(Sprite)
         || type == typeof(Texture2D)
         || type == typeof(AudioClip)
         || type == typeof(Material)
         || type == typeof(AnimationClip)
         || type == typeof(RuleTile)
         || type == typeof(TextAsset);
    }
}
#endif
