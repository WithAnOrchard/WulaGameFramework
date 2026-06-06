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
        public const string EVT_SET_BULK_LOAD_PATHS        = "SetResourceBulkLoadPaths";
        public const string EVT_ADD_BULK_LOAD_PATH         = "AddResourceBulkLoadPath";
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


        // ============================================================
        // 批量索引类型（预加载 + Resources.LoadAll 都按此顺序遍历）
        // ============================================================
        private static readonly (string tag, Type type)[] _bulkTypes =
        {
            ("Prefab",        typeof(GameObject)),
            ("Sprite",        typeof(Sprite)),
            ("AudioClip",     typeof(AudioClip)),
            ("Texture",       typeof(Texture2D)),
            ("Material",      typeof(Material)),
            ("RuleTile",      typeof(RuleTile)),
            ("AnimationClip", typeof(AnimationClip)),
        };

        // 调用链上的子文件夹提示（尝试 Resources.LoadAll 时按此顺序追加路径）
        private static readonly string[] _subfolderHints =
        {
            "", "UI", "Sprites", "Sprites/Tiles", "Sprites/UI", "Sprites/Characters",
            "Prefabs", "Audio", "Sound", "Models", "Models/Characters3D",
        };

        // ============================================================
        // 状态
        // ============================================================
        private readonly Dictionary<ResourceKey, UnityEngine.Object> _loadedResources
            = new Dictionary<ResourceKey, UnityEngine.Object>();

        private readonly List<string> _bulkLoadPaths = new List<string>();
        private bool _useConfiguredBulkLoadPaths;

        // Phase 1 优化：Unload 方法的静态缓存列表，避免每次分配
        private static readonly List<ResourceKey> _toRemoveCache = new List<ResourceKey>();

        // Phase 1 优化：外部图片加载结果的静态字典缓存，避免每次分配

        /// <summary>FBX/Model 路径（Resources 相对、不含扩展名）→ 内含 clip 名列表。</summary>

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
            
            // 强制初始化各个素材类型的 Service，确保它们的事件处理方法被注册到 EventProcessor
            var _0 = Services.Sprite.SpriteService.Instance;
            var _1 = Services.Sprite.SpriteSheetService.Instance;
            var _2 = Services.Prefab.PrefabService.Instance;
            var _3 = Services.Audio.AudioClipService.Instance;
            var _4 = Services.Texture.TextureService.Instance;
            var _5 = Services.Material.MaterialService.Instance;
            var _6 = Services.RuleTile.RuleTileService.Instance;
            var _7 = Services.Animation.AnimationClipService.Instance;
            var _8 = Services.Animation.ModelAnimationService.Instance;
            var _9 = Services.External.ExternalImageService.Instance;
            
            Log("ResourceService 初始化完成", Color.green);
        }

        public Dictionary<ResourceKey, UnityEngine.Object> GetLoadedResources() => _loadedResources;
        public string GetResourceId(ResourceKey key) => key.FileName;
        public bool TryGetResource(ResourceKey key, out UnityEngine.Object resource) =>
            _loadedResources.TryGetValue(key, out resource);

        public void CacheResource(ResourceKey key, UnityEngine.Object resource)
        {
            if (resource == null) return;
            _loadedResources[key] = resource;
            SyncToService(key.TypeTag, key, resource);
        }

        public void TrackResource(ResourceKey key, UnityEngine.Object resource)
        {
            if (resource == null) return;
            _loadedResources[key] = resource;
        }

        public void SetBulkLoadPaths(IEnumerable<string> paths)
        {
            if (_dataLoaded)
            {
                Debug.LogWarning("[ResourceService] Bulk load paths changed after data load; current load will not be rebuilt.");
            }

            _bulkLoadPaths.Clear();
            _useConfiguredBulkLoadPaths = true;
            if (paths == null) return;

            foreach (var path in paths)
                AddBulkLoadPath(path);
        }

        public void AddBulkLoadPath(string path)
        {
            if (_dataLoaded)
            {
                Debug.LogWarning("[ResourceService] Bulk load path added after data load; current load will not be rebuilt.");
            }

            _useConfiguredBulkLoadPaths = true;
            var normalized = NormalizeResourceLoadPath(path);
            if (_bulkLoadPaths.Contains(normalized)) return;
            _bulkLoadPaths.Add(normalized);
        }

        [Event(EVT_SET_BULK_LOAD_PATHS)]
        public List<object> SetBulkLoadPathsEvent(List<object> data)
        {
            var paths = new List<string>();
            if (data != null)
            {
                foreach (var item in data)
                {
                    if (item is string path)
                    {
                        paths.Add(path);
                    }
                    else if (item is IEnumerable<string> pathList)
                    {
                        paths.AddRange(pathList);
                    }
                }
            }

            SetBulkLoadPaths(paths);
            return ResultCode.Ok(paths.Count);
        }

        [Event(EVT_ADD_BULK_LOAD_PATH)]
        public List<object> AddBulkLoadPathEvent(List<object> data)
        {
            if (data == null || data.Count == 0 || !(data[0] is string path))
                return ResultCode.Fail("Resource bulk load path is empty.");
            AddBulkLoadPath(path);
            return ResultCode.Ok(path);
        }

        private static string NormalizeResourceLoadPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            var normalized = path.Replace('\\', '/').Trim('/');
            const string resourcesPrefix = "Resources/";
            var resourcesIndex = normalized.IndexOf(resourcesPrefix, StringComparison.OrdinalIgnoreCase);
            if (resourcesIndex >= 0)
                normalized = normalized.Substring(resourcesIndex + resourcesPrefix.Length);
            return normalized;
        }

        // ============================================================
        // 数据加载完成 → 预加载 + 全量索引 + 广播
        // ============================================================
        [Event(EVT_DATA_LOADED)]
        public List<object> OnDataLoaded(List<object> data)
        {
            if (_dataLoaded) return ResultCode.Ok(true);
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            _dataLoaded = true;

            var sample = System.Diagnostics.Stopwatch.StartNew();
            PreloadConfiguredResources();
            LogStartupTiming("ResourceService.PreloadConfiguredResources", sample);
            sample = System.Diagnostics.Stopwatch.StartNew();
            IndexAllResources();
            LogStartupTiming("ResourceService.IndexAllResources", sample);

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
            var loadPaths = GetBulkLoadPaths();
            Debug.Log(_useConfiguredBulkLoadPaths
                ? $"[StartupTiming] ResourceService.BulkLoadScope: configured paths={loadPaths.Count}"
                : $"[StartupTiming] ResourceService.BulkLoadScope: legacy paths={loadPaths.Count}");
            if (loadPaths.Count == 0)
            {
                Debug.Log("[StartupTiming] ResourceService.IndexAllResources skipped: no bulk load paths configured.");
                return;
            }
            // 加载 FBX Manifest
            
            // 批量预加载所有 Resources 下的资源
            foreach (var (tag, type) in _bulkTypes)
            {
                var sample = System.Diagnostics.Stopwatch.StartNew();
                ResourcesLoadAllInto(type, tag, loadPaths);
                LogStartupTiming($"ResourceService.IndexAllResources.{tag}", sample);
            }
        }
        
        /// <summary>
        /// 批量将 Resources/{subdir}/ 下的指定类型资产全部加载进缓存。
        /// </summary>
        private IReadOnlyList<string> GetBulkLoadPaths() =>
            _useConfiguredBulkLoadPaths ? _bulkLoadPaths : _subfolderHints;

        private void ResourcesLoadAllInto(Type type, string tag, IReadOnlyList<string> loadPaths)
        {
            // 按子目录遍历
            foreach (var path in loadPaths)
            {
                try
                {
                    var sample = System.Diagnostics.Stopwatch.StartNew();
                    var resources = Resources.LoadAll(path, type);
                    var loadMs = sample.ElapsedMilliseconds;
                    if (resources == null || resources.Length == 0)
                    {
                        if (loadMs >= 10)
                            Debug.LogWarning($"[StartupTiming] Resources.LoadAll<{tag}>(\"{path}\"): count=0, {loadMs} ms");
                        continue;
                    }

                    int added = 0;
                    foreach (var r in resources)
                    {
                        if (r == null) continue;
                        var key = new ResourceKey(r.name, false, tag);
                        if (!_loadedResources.ContainsKey(key))
                        {
                            CacheResource(key, r);
                            added++;
                        }
                    }
                    if (added > 0)
                    {
                        Log($"批量加载 {tag} from {path}: +{added} 个（总计 {resources.Length}）", Color.green);
                    }
                    if (loadMs >= 10 || added > 0)
                        Debug.Log($"[StartupTiming] Resources.LoadAll<{tag}>(\"{path}\"): count={resources.Length}, added={added}, {loadMs} ms");
                }
                catch (Exception ex)
                {
                    Log($"批量加载 {tag} from {path} 失败: {ex.Message}", Color.yellow);
                }
            }
        }
        
        /// <summary>
        /// 将资源同步到对应的 Service 缓存中。
        /// </summary>
        private static void LogStartupTiming(string label, System.Diagnostics.Stopwatch stopwatch)
        {
            if (stopwatch == null) return;
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            var message = $"[StartupTiming] {label}: {elapsed} ms";
            if (elapsed >= 16) Debug.LogWarning(message);
            else Debug.Log(message);
        }

        private void SyncToService(string tag, ResourceKey key, UnityEngine.Object resource)
        {
            try
            {
                switch (tag)
                {
                    case "Sprite":
                        Services.Sprite.SpriteService.Instance?.CacheResource(key, resource);
                        break;
                    case "Prefab":
                        Services.Prefab.PrefabService.Instance?.CacheResource(key, resource);
                        break;
                    case "AudioClip":
                        Services.Audio.AudioClipService.Instance?.CacheResource(key, resource);
                        break;
                    case "Texture":
                        Services.Texture.TextureService.Instance?.CacheResource(key, resource);
                        break;
                    case "Material":
                        Services.Material.MaterialService.Instance?.CacheResource(key, resource);
                        break;
                    case "RuleTile":
                        Services.RuleTile.RuleTileService.Instance?.CacheResource(key, resource);
                        break;
                    case "AnimationClip":
                        Services.Animation.AnimationClipService.Instance?.CacheResource(key, resource);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"同步资源到 Service 失败: {tag}/{key.FileName}: {ex.Message}", Color.yellow);
            }
        }

        private void ForgetFromService(ResourceKey key)
        {
            try
            {
                switch (key.TypeTag)
                {
                    case "Sprite":
                        Services.Sprite.SpriteService.Instance?.ForgetResource(key);
                        break;
                    case "Prefab":
                        Services.Prefab.PrefabService.Instance?.ForgetResource(key);
                        break;
                    case "AudioClip":
                        Services.Audio.AudioClipService.Instance?.ForgetResource(key);
                        break;
                    case "Texture":
                        Services.Texture.TextureService.Instance?.ForgetResource(key);
                        break;
                    case "Material":
                        Services.Material.MaterialService.Instance?.ForgetResource(key);
                        break;
                    case "RuleTile":
                        Services.RuleTile.RuleTileService.Instance?.ForgetResource(key);
                        break;
                    case "AnimationClip":
                        Services.Animation.AnimationClipService.Instance?.ForgetResource(key);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"从类型 Service 移除资源失败: {key}: {ex.Message}", Color.yellow);
            }
        }

        private void ClearServiceCaches()
        {
            Services.Sprite.SpriteService.Instance?.ClearCache();
            Services.Prefab.PrefabService.Instance?.ClearCache();
            Services.Audio.AudioClipService.Instance?.ClearCache();
            Services.Texture.TextureService.Instance?.ClearCache();
            Services.Material.MaterialService.Instance?.ClearCache();
            Services.RuleTile.RuleTileService.Instance?.ClearCache();
            Services.Animation.AnimationClipService.Instance?.ClearCache();
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
#if false
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
                        CacheResource(key, sprite);
                        Log($"外部 Sprite 加载成功: {filePath}");
                        callback?.Invoke(sprite);
                        _externalLoadResultDict.Clear();
                        _externalLoadResultDict["path"] = filePath;
                        _externalLoadResultDict["sprite"] = sprite;
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOADED,
                            new List<object> { _externalLoadResultDict });
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(tex);
                        callback?.Invoke(null);
                        _externalLoadResultDict.Clear();
                        _externalLoadResultDict["path"] = filePath;
                        _externalLoadResultDict["error"] = "加载失败";
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOAD_FAILED,
                            new List<object> { _externalLoadResultDict });
                    }
                });
            });
        }

        // ============================================================
        // 卸载
        // ============================================================
#endif
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
                ReleaseOne(_loadedResources[k]);
                _loadedResources.Remove(k);
                ForgetFromService(k);
            }
            return ResultCode.Ok();
        }

        [Event(ResourceManager.EVT_UNLOAD_ALL_RESOURCES)]
        public List<object> UnloadAll(List<object> data)
        {
            foreach (var resource in _loadedResources.Values)
                ReleaseOne(resource);
            _loadedResources.Clear();
            ClearServiceCaches();
            return ResultCode.Ok();
        }

        /// <summary>
        /// 按资源类型分派释放策略：
        ///   GameObject / Component（运行时实例，scene 内） → Object.Destroy / DestroyImmediate
        ///   GameObject / Component（prefab asset，persistent）→ 不 Destroy，只从 cache 移除
        ///   AssetBundle                                     → bundle.Unload(true)
        ///   其他 (Texture / AudioClip / Material / Mesh / TextAsset / 等) → Resources.UnloadAsset
        /// null 直接跳过。
        /// </summary>
        private static void ReleaseOne(UnityEngine.Object resource)
        {
            if (resource == null) return;

            if (resource is GameObject go)
            {
                // 区分 prefab asset（persistent）和 Instantiate 出来的实例
                //   prefab asset 的 go.scene.IsValid() == false（不在任何 scene）
                //   scene 内实例的 go.scene.IsValid() == true
                // —— Destroy 不允许销毁 asset，会抛 "Destroying assets is not permitted"
                if (go.scene.IsValid())
                {
                    // 必须 UnityEngine.Application 全限定 —— 项目存在 EssSystem.Core.Application 命名空间会遮蔽
                    if (UnityEngine.Application.isPlaying)
                        UnityEngine.Object.Destroy(go);
                    else
                        UnityEngine.Object.DestroyImmediate(go, allowDestroyingAssets: true);
                }
                // persistent asset：不销毁，仅从 cache 移除；内存由 Resources.UnloadUnusedAssets 后续清
            }
            else if (resource is Component comp)
            {
                // Component 走所属 GameObject 的归属判断
                var ownerGo = comp.gameObject;
                if (ownerGo != null && ownerGo.scene.IsValid())
                {
                    if (UnityEngine.Application.isPlaying)
                        UnityEngine.Object.Destroy(comp);
                    else
                        UnityEngine.Object.DestroyImmediate(comp, allowDestroyingAssets: true);
                }
            }
            else if (resource is AssetBundle bundle)
            {
                bundle.Unload(true);
            }
            else
            {
                Resources.UnloadAsset(resource);
            }
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
#if false
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

        public List<object> GetAllModelPaths(List<object> data)
        {
            var list = new List<string>(_modelClipNames.Count);
            foreach (var k in _modelClipNames.Keys) list.Add(k);
            return ResultCode.Ok(list);
        }

        // ============================================================
        // Inspector
        // ============================================================
#endif
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
#if false
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

#endif
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
