#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.EditorTools
{
    /// <summary>
    /// 生成 <c>Assets/Resources/CharacterFBXManifest.json</c> —— 记录 Resources/ 下每个 FBX 的
    /// path（不含扩展名）+ 内含 AnimationClip 名列表。
    /// <para><b>用途</b>：Build 期 <c>AssetDatabase</c> 不可用时，由 <c>ResourceService.LoadFBXManifestIfPresent</c>
    /// 读取此 manifest 填充 <c>_modelClipNames</c>，让 <c>CharacterConfigFactory.RegisterAllFBXInResources</c> 在 Build 也能跑。</para>
    /// <para><b>菜单</b>：<c>Tools/Character/Rebuild FBX Manifest</c>（手动重建）。</para>
    /// <para><b>Build 预处理</b>：实现 <see cref="IPreprocessBuildWithReport"/>，每次 Build 自动重建。</para>
    /// </summary>
    public class FBXManifestBuilder : IPreprocessBuildWithReport
    {
        public const string ManifestAssetPath = "Assets/Resources/CharacterFBXManifest.json";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildManifest(silent: true);
        }

        [MenuItem("Tools/Character/Rebuild FBX Manifest")]
        public static void RebuildMenu()
        {
            int n = BuildManifest(silent: false);
            EditorUtility.DisplayDialog("FBX Manifest",
                $"已重建 {ManifestAssetPath}\n收录 FBX：{n} 个", "OK");
        }

        public static int BuildManifest(bool silent)
        {
            var entries = new List<Entry>();
            var guids = AssetDatabase.FindAssets("t:Model");
            if (guids != null)
            {
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    int idx = path.IndexOf("/Resources/", System.StringComparison.Ordinal);
                    if (idx < 0) continue;

                    // Resources 相对 + 去扩展名：Assets/Resources/Models/zombie.fbx → Models/zombie
                    var rel = path.Substring(idx + "/Resources/".Length);
                    var ext = Path.GetExtension(rel);
                    if (!string.IsNullOrEmpty(ext)) rel = rel.Substring(0, rel.Length - ext.Length);

                    var clips = new List<string>();
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    if (subAssets != null)
                    {
                        foreach (var a in subAssets)
                        {
                            if (a is AnimationClip c
                                && !c.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                                clips.Add(c.name);
                        }
                    }

                    entries.Add(new Entry { path = rel.Replace('\\', '/'), clips = clips.ToArray() });
                }
            }

            // 写入 JSON（用 JsonUtility 简单包装）
            var wrapper = new Wrapper { entries = entries.ToArray() };
            var json = JsonUtility.ToJson(wrapper, prettyPrint: true);

            var dir = Path.GetDirectoryName(ManifestAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(ManifestAssetPath, json);
            AssetDatabase.ImportAsset(ManifestAssetPath);

            if (!silent)
                Debug.Log($"[FBXManifestBuilder] 写入 {ManifestAssetPath}（{entries.Count} 个 FBX）");
            return entries.Count;
        }

        [System.Serializable] private class Wrapper { public Entry[] entries; }
        [System.Serializable] private class Entry
        {
            public string path;
            public string[] clips;
        }
    }
}
#endif
