using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;

namespace EssSystem.Core.Foundation.ResourceManager
{
    /// <summary>资源缓存键（FileName + IsExternal + TypeTag），避免同名不同类型碰撞。</summary>
    public struct ResourceKey : IEquatable<ResourceKey>
    {
        public readonly string FileName;
        public readonly bool   IsExternal;
        public readonly string TypeTag;

        public ResourceKey(string path, bool isExternal, string typeTag = null)
        {
            var name = string.IsNullOrEmpty(path) ? path : Path.GetFileNameWithoutExtension(path);
            FileName   = string.IsNullOrEmpty(name) ? path : name;
            IsExternal = isExternal;
            TypeTag    = typeTag ?? "";
        }

        public bool Equals(ResourceKey other) =>
            FileName == other.FileName && IsExternal == other.IsExternal && TypeTag == other.TypeTag;

        public override bool Equals(object obj) => obj is ResourceKey k && Equals(k);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (FileName?.GetHashCode() ?? 0);
                h = h * 31 + IsExternal.GetHashCode();
                h = h * 31 + (TypeTag?.GetHashCode() ?? 0);
                return h;
            }
        }

        public override string ToString() =>
            string.IsNullOrEmpty(TypeTag)
                ? $"{(IsExternal ? "external" : "unity")}:{FileName}"
                : $"{(IsExternal ? "external" : "unity")}:{TypeTag}:{FileName}";
    }

    public enum ResourceType { Prefab, Sprite, AudioClip, Texture, RuleTile, AnimationClip }

    [Serializable]
    public class ResourceConfigItem
    {
        public string id;
        public string path;
        public bool   isExternal;
        public ResourceType type;
    }

    /// <summary>
    /// 资源服务（门面） —— 协调各个素材类型的 Service，提供统一的资源管理接口。
    /// 具体的异步加载由各个素材类型的 Service 处理（PrefabService、SpriteService 等）。
    /// </summary>
    public class ResourceService : Service<ResourceService>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_DATA_LOADED                = "OnResourceDataLoaded";
        public const string EVT_ADD_RESOURCE_CONFIG        = "AddResourceConfig";
        public const string EVT_LOAD_EXTERNAL_IMAGE_ASYNC  = "LoadExternalImageAsync";
        public const string EVT_GET_MODEL_CLIPS            = "GetModelClips";
        public const string EVT_GET_ALL_MODEL_PATHS        = "GetAllModelPaths";
        public const string EVT_RESOURCES_LOADED           = "OnResourcesLoaded";
        public const string EVT_EXTERNAL_IMAGE_LOADED      = "OnExternalImageLoaded";
        public const string EVT_EXTERNAL_IMAGE_LOAD_FAILED = "OnExternalImageLoadFailed";
        /// <summary>业务侧主动声明一张多精灵图集，将其所有子精灵按名入缓存。
        /// data: [string sheetResourcePath]  ―  Resources/ 相对路径，不含扩展名，如 "Plants/Plants"
        /// 返回 Ok(int addedCount) / Fail(msg)。</summary>
        public const string EVT_REGISTER_SPRITE_SHEET = "RegisterSpriteSheet";
        /// <summary>获取资源引用计数统计信息。返回 Ok(Dictionary&lt;string, object&gt;)。</summary>
        public const string EVT_GET_REFCOUNT_STATS = "GetRefCountStats";
        /// <summary>清理超时未使用的资源。返回 Ok()。</summary>
        public const string EVT_CLEANUP_UNUSED_ASSETS = "CleanupUnusedAssets";

        private const string FBXManifestResourcePath = "CharacterFBXManifest";

        // ============================================================
        // 状态
        // ============================================================
        private readonly Dictionary<ResourceKey, UnityEngine.Object> _loadedResources
            = new Dictionary<ResourceKey, UnityEngine.Object>();

        // Phase 1 优化：Unload 方法的静态缓存列表，避免每次分配
        private static readonly List<ResourceKey> _toRemoveCache = new List<ResourceKey>();

        /// <summary>FBX/Model 路径（Resources 相对、不含扩展名）→ 内含 clip 名列表。</summary>
        private readonly Dictionary<string, List<string>> _modelClipNames
            = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>资源引用计数管理器（Phase 1 优化）。</summary>
        private ResourceRefCounter _refCounter;

        private bool _dataLoaded;

        // ============================================================
        // 公开访问
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            _refCounter = new ResourceRefCounter(300f);
            Log("ResourceService 初始化完成", Color.green);
        }

        public Dictionary<ResourceKey, UnityEngine.Object> GetLoadedResources() => _loadedResources;
        public string GetResourceId(ResourceKey key) => key.FileName;

        // ============================================================
        // 数据加载完成 → 预加载 + 全量索引 + 广播
        // ============================================================
        [Event(EVT_DATA_LOADED)]
        public List<object> OnDataLoaded(List<object> data)
        {
            if (_dataLoaded) return ResultCode.Ok(true);
            _dataLoaded = true;

            PreloadConfiguredResources();
            IndexAllResources();

            try
            {
                if (EventProcessor.Instance.HasListener(EVT_RESOURCES_LOADED))
                    EventProcessor.Instance.TriggerEvent(EVT_RESOURCES_LOADED, new List<object>());
            }
            catch (Exception ex) { Log($"广播 EVT_RESOURCES_LOADED 异常：{ex.Message}", Color.yellow); }

            return ResultCode.Ok();
        }

        /// <summary>数据加载完成时的初始化逻辑。</summary>
        private void PreloadConfiguredResources()
        {
            // 预加载逻辑由各个素材类型的 Service 处理
        }

        /// <summary>资源索引逻辑。</summary>
        private void IndexAllResources()
        {
            // 资源索引由各个素材类型的 Service 处理
            LoadFBXManifestIfPresent();
        }




        // 添加预加载配置
        // ============================================================
        [Event(EVT_ADD_RESOURCE_CONFIG)]
        public List<object> AddPreloadConfig(List<object> data)
        {
            string       id         = data[0] as string;
            string       path       = data[1] as string;
            ResourceType type       = (ResourceType)data[2];
            bool         isExternal = data.Count > 3 && (bool)data[3];

            if (string.IsNullOrEmpty(id)) id = new ResourceKey(path, isExternal).FileName;

            SetData(type.ToString(), id,
                new ResourceConfigItem { id = id, path = path, isExternal = isExternal, type = type });
            return ResultCode.Ok();
        }

        // ============================================================
        // 外部图片加载（异步、走 MainThreadDispatcher）
        // ============================================================
        [Event(EVT_LOAD_EXTERNAL_IMAGE_ASYNC)]
        public List<object> LoadExternalImageAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var key = new ResourceKey(filePath, true, "Sprite");

            if (_loadedResources.TryGetValue(key, out var cached)) return ResultCode.Ok(cached);
            if (!File.Exists(filePath))                            return ResultCode.Fail("文件不存在");

            LoadExternalImageAsync(filePath, null);
            return ResultCode.Fail("加载中");
        }

        private void LoadExternalImageAsync(string filePath, Action<Sprite> callback)
        {
            var key = new ResourceKey(filePath, true, "Sprite");
            if (_loadedResources.TryGetValue(key, out var cached)) { callback?.Invoke(cached as Sprite); return; }
            if (!File.Exists(filePath))                            { callback?.Invoke(null);             return; }

            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                MainThreadDispatcher.Enqueue(() =>
                {
                    var tex = new Texture2D(2, 2);
                    if (tex.LoadImage(bytes))
                    {
                        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero);
                        _loadedResources[key] = sprite;
                        Log($"外部 Sprite 加载成功: {filePath}");
                        callback?.Invoke(sprite);
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOADED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["sprite"] = sprite } });
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(tex);
                        callback?.Invoke(null);
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOAD_FAILED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["error"] = "加载失败" } });
                    }
                });
            });
        }

        // ============================================================
        // 卸载
        // ============================================================
        [Event(ResourceManager.EVT_UNLOAD_RESOURCE)]
        public List<object> Unload(List<object> data)
        {
            string path       = data[0] as string;
            bool   isExternal = data.Count > 1 && (bool)data[1];
            string typeStr    = data.Count > 2 ? data[2] as string : null;
            string targetTag  = string.IsNullOrEmpty(typeStr) ? null : NormalizeTypeTag(typeStr);
            string targetName = new ResourceKey(path, isExternal).FileName;

            // Phase 1 优化：使用静态缓存列表，避免每次分配
            _toRemoveCache.Clear();
            foreach (var k in _loadedResources.Keys)
            {
                if (k.FileName != targetName || k.IsExternal != isExternal) continue;
                if (targetTag != null && k.TypeTag != targetTag)            continue;
                _toRemoveCache.Add(k);
            }
            if (_toRemoveCache.Count == 0) return ResultCode.Fail("资源未加载");

            foreach (var k in _toRemoveCache)
            {
                Resources.UnloadAsset(_loadedResources[k]);
                _loadedResources.Remove(k);
            }
            return ResultCode.Ok();
        }

        [Event(ResourceManager.EVT_UNLOAD_ALL_RESOURCES)]
        public List<object> UnloadAll(List<object> data)
        {
            foreach (var resource in _loadedResources.Values) Resources.UnloadAsset(resource);
            _loadedResources.Clear();
            return ResultCode.Ok();
        }

        // ============================================================
        // 引用计数统计 + 自动清理（Phase 1 优化）
        // ============================================================
        [Event(EVT_GET_REFCOUNT_STATS)]
        public List<object> GetRefCountStats(List<object> data)
        {
            if (_refCounter == null) return ResultCode.Fail("引用计数管理器未初始化");
            var stats = _refCounter.GetStats();
            return ResultCode.Ok(stats);
        }

        [Event(EVT_CLEANUP_UNUSED_ASSETS)]
        public List<object> CleanupUnusedAssets(List<object> data)
        {
            if (_refCounter == null) return ResultCode.Fail("引用计数管理器未初始化");
            _refCounter.CleanupUnusedAssets();
            return ResultCode.Ok();
        }

        // ============================================================
        // Model clips（FBX 容器内 AnimationClip 查询）
        // ============================================================
        [Event(EVT_GET_MODEL_CLIPS)]
        public List<object> GetModelClips(List<object> data)
        {
            string modelPath = data != null && data.Count > 0 ? data[0] as string : null;
            var result = new List<AnimationClip>();
            if (string.IsNullOrEmpty(modelPath)) return ResultCode.Ok(result);

            // 1) manifest / Editor 预烘命中 → 反查 AnimationClip 缓存
            if (_modelClipNames.TryGetValue(NormalizeModelKey(modelPath), out var names) && names != null)
            {
                foreach (var name in names)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (_loadedResources.TryGetValue(new ResourceKey(name, false, "AnimationClip"), out var o)
                        && o is AnimationClip clip)
                        result.Add(clip);
                }
                if (result.Count > 0) return ResultCode.Ok(result);
            }

#if UNITY_EDITOR
            // 2) Editor fallback：由 ModelAnimationService 处理
#endif
            return ResultCode.Ok(result);
        }

        [Event(EVT_GET_ALL_MODEL_PATHS)]
        public List<object> GetAllModelPaths(List<object> data)
        {
            var list = new List<string>(_modelClipNames.Count);
            foreach (var k in _modelClipNames.Keys) list.Add(k);
            return ResultCode.Ok(list);
        }

        // ============================================================
        // Inspector
        // ============================================================
        public override void UpdateInspectorInfo()
        {
#if UNITY_EDITOR
            base.UpdateInspectorInfo();
            if (InspectorInfo == null) InspectorInfo = new ServiceDataInspectorInfo();

            InspectorInfo.Categories.Clear();  // 清空旧数据
            
            var cat = new ServiceDataInspectorInfo.CategoryInfo
            {
                CategoryName = "已加载资源",
                DataCount    = _loadedResources.Count,
                DataItems    = new List<ServiceDataInspectorInfo.DataInfo>(_loadedResources.Count)  // 预分配
            };
            foreach (var kvp in _loadedResources)
            {
                cat.DataItems.Add(new ServiceDataInspectorInfo.DataInfo
                {
                    Key          = kvp.Key.ToString(),
                    TypeName     = kvp.Value?.GetType().Name ?? "null",
                    ValueSummary = kvp.Value?.name ?? "null",
                });
            }
            InspectorInfo.Categories.Add(cat);
#endif
        }

        // ============================================================
        // 工具方法
        // ============================================================

        /// <summary>读取 Resources/CharacterFBXManifest.json，把记录的 modelPath → clip 名列表写进 _modelClipNames。</summary>
        private void LoadFBXManifestIfPresent()
        {
            var asset = Resources.Load<TextAsset>(FBXManifestResourcePath);
            if (asset == null) return;
            try
            {
                var w = JsonUtility.FromJson<FBXManifest>(asset.text);
                if (w?.entries == null) return;
                int added = 0;
                foreach (var e in w.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.path)) continue;
                    var key = NormalizeModelKey(e.path);
                    if (_modelClipNames.ContainsKey(key)) continue;
                    _modelClipNames[key] = e.clips != null ? new List<string>(e.clips) : new List<string>();
                    added++;
                }
                Log($"FBX manifest 加载：+{added} 条记录（总 {_modelClipNames.Count}）");
            }
            catch (Exception ex) { Log($"FBX manifest 解析失败：{ex.Message}", Color.yellow); }
        }

        private static string NormalizeTypeTag(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            switch (s)
            {
                case "Prefab":  case "GameObject": return "Prefab";
                case "Texture": case "Texture2D":  return "Texture";
                default:                           return s;
            }
        }


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


        // ============================================================
        // FBX manifest 数据载体
        // ============================================================
        [Serializable] private class FBXManifest      { public FBXManifestEntry[] entries; }
        [Serializable] private class FBXManifestEntry { public string path; public string[] clips; }
    }
}
