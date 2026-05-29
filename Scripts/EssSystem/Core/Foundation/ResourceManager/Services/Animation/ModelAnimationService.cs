using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Animation
{
    /// <summary>模型动画服务 — 管理 FBX 模型和 AnimationClip。</summary>
    public class ModelAnimationService : Service<ModelAnimationService>
    {
        public const string EVT_GET_MODEL_CLIPS = "GetModelClips";
        public const string EVT_GET_ALL_MODEL_PATHS = "GetAllModelPaths";

        private const string FBXManifestResourcePath = "CharacterFBXManifest";
        private readonly Dictionary<string, List<string>> _modelClipNames
            = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        protected override void Initialize()
        {
            base.Initialize();
            LoadFBXManifestIfPresent();
#if UNITY_EDITOR
            EditorIndexModelClipNames();
#endif
        }

        [Event(EVT_GET_MODEL_CLIPS)]
        public List<object> GetModelClips(List<object> data)
        {
            string modelPath = data != null && data.Count > 0 ? data[0] as string : null;
            var result = new List<AnimationClip>();
            if (string.IsNullOrEmpty(modelPath)) return ResultCode.Ok(result);

            if (_modelClipNames.TryGetValue(NormalizeModelKey(modelPath), out var names) && names != null)
            {
                var animService = AnimationClipService.Instance;
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (animService.GetLoadedResources().TryGetValue(
                        new ResourceKey(name, false, "AnimationClip"), out var o) && o is AnimationClip clip)
                        result.Add(clip);
                }
            }
            return ResultCode.Ok(result);
        }

        [Event(EVT_GET_ALL_MODEL_PATHS)]
        public List<object> GetAllModelPaths(List<object> data)
        {
            var list = new List<string>(_modelClipNames.Count);
            foreach (var k in _modelClipNames.Keys) list.Add(k);
            return ResultCode.Ok(list);
        }

        private void LoadFBXManifestIfPresent()
        {
            var manifest = Resources.Load<TextAsset>(FBXManifestResourcePath);
            if (manifest == null) return;

            try
            {
                var lines = manifest.text.Split(new[] { '\r', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var parts = line.Split('|');
                    if (parts.Length < 2) continue;
                    var modelKey = parts[0].Trim();
                    var clipNames = new List<string>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var clipName = parts[i].Trim();
                        if (!string.IsNullOrEmpty(clipName)) clipNames.Add(clipName);
                    }
                    _modelClipNames[modelKey] = clipNames;
                }
                Log($"FBX Manifest 加载成功: {_modelClipNames.Count} 个模型");
            }
            catch (Exception ex)
            {
                Log($"FBX Manifest 加载失败: {ex.Message}", Color.yellow);
            }
        }

#if UNITY_EDITOR
        private void EditorIndexModelClipNames()
        {
            var fbxGuids = UnityEditor.AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Resources" });
            foreach (var guid in fbxGuids)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var modelKey = NormalizeModelKey(path);
                if (string.IsNullOrEmpty(modelKey)) continue;

                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null) continue;

                if (!_modelClipNames.TryGetValue(modelKey, out var clips))
                {
                    clips = new List<string>();
                    _modelClipNames[modelKey] = clips;
                }
                if (!clips.Contains(clip.name)) clips.Add(clip.name);
            }
        }
#endif

        private static string NormalizeModelKey(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var p = path.Replace('\\', '/');
            const string prefix = "Assets/Resources/";
            if (p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) p = p.Substring(prefix.Length);
            var ext = Path.GetExtension(p);
            if (!string.IsNullOrEmpty(ext)) p = p.Substring(0, p.Length - ext.Length);
            return p;
        }
    }
}
