#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.EditorTools
{
    /// <summary>
    /// Generates Assets/Resources/CharacterFBXManifest.json for runtime FBX clip lookup.
    /// Menu: Tools/WulaSystem/Presentation/Character/3D/FBX/Rebuild FBX Manifest
    /// </summary>
    public class FBXManifestBuilder : IPreprocessBuildWithReport
    {
        public const string ManifestAssetPath = "Assets/Resources/CharacterFBXManifest.json";
        private const string MENU_PREFIX = "Tools/WulaSystem/Presentation/Character/3D/FBX/";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            BuildManifest(silent: true);
        }

        [MenuItem(MENU_PREFIX + "Rebuild FBX Manifest")]
        public static void RebuildMenu()
        {
            int count = BuildManifest(silent: false);
            EditorUtility.DisplayDialog("FBX Manifest",
                $"Rebuilt {ManifestAssetPath}\nFBX entries: {count}", "OK");
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

                    int resourcesIndex = path.IndexOf("/Resources/", System.StringComparison.Ordinal);
                    if (resourcesIndex < 0) continue;

                    var relativePath = path.Substring(resourcesIndex + "/Resources/".Length);
                    var ext = Path.GetExtension(relativePath);
                    if (!string.IsNullOrEmpty(ext))
                        relativePath = relativePath.Substring(0, relativePath.Length - ext.Length);

                    var clips = new List<string>();
                    var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
                    if (subAssets != null)
                    {
                        foreach (var asset in subAssets)
                        {
                            if (asset is AnimationClip clip
                                && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                                clips.Add(clip.name);
                        }
                    }

                    entries.Add(new Entry
                    {
                        path = relativePath.Replace('\\', '/'),
                        clips = clips.ToArray()
                    });
                }
            }

            var wrapper = new Wrapper { entries = entries.ToArray() };
            var json = JsonUtility.ToJson(wrapper, prettyPrint: true);

            var dir = Path.GetDirectoryName(ManifestAssetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(ManifestAssetPath, json);
            AssetDatabase.ImportAsset(ManifestAssetPath);

            if (!silent)
                Debug.Log($"[FBXManifestBuilder] Wrote {ManifestAssetPath} ({entries.Count} FBX entries).");

            return entries.Count;
        }

        [System.Serializable]
        private class Wrapper
        {
            public Entry[] entries;
        }

        [System.Serializable]
        private class Entry
        {
            public string path;
            public string[] clips;
        }
    }
}
#endif
