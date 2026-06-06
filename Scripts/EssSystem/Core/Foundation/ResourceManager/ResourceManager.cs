using System.Collections.Generic;
using System.IO;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Sprite;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EssSystem.Core.Foundation.ResourceManager
{
    /// <summary>
    /// 资源管理器 façade —— 仅做参数转发到 ResourceService，零业务逻辑。
    /// </summary>
    [Manager(0)]
    public class ResourceManager : Manager<ResourceManager>
    {
        // ===== Event Constants (façade, public API) =====
        public const string EVT_GET_PREFAB                = "GetPrefab";
        public const string EVT_GET_SPRITE                = "GetSprite";
        public const string EVT_GET_AUDIO_CLIP            = "GetAudioClip";
        public const string EVT_GET_TEXTURE               = "GetTexture";
        public const string EVT_GET_MATERIAL              = "GetMaterial";
        public const string EVT_GET_RULE_TILE             = "GetRuleTile";
        public const string EVT_GET_ANIMATION_CLIP        = "GetAnimationClip";
        public const string EVT_GET_PREFAB_ASYNC          = "GetPrefabAsync";
        public const string EVT_GET_SPRITE_ASYNC          = "GetSpriteAsync";
        public const string EVT_GET_AUDIO_CLIP_ASYNC      = "GetAudioClipAsync";
        public const string EVT_GET_TEXTURE_ASYNC         = "GetTextureAsync";
        public const string EVT_GET_MATERIAL_ASYNC        = "GetMaterialAsync";
        public const string EVT_GET_RULE_TILE_ASYNC       = "GetRuleTileAsync";
        public const string EVT_GET_ANIMATION_CLIP_ASYNC  = "GetAnimationClipAsync";
        // §4.1 下跨模块调用走 bare-string，原 3 个 façade alias 已删除：
        //   EVT_GET_MODEL_CLIPS / EVT_GET_ALL_MODEL_PATHS / EVT_RESOURCES_LOADED
        // 调用侧直接传字符串（"GetModelClips" / "GetAllModelPaths" / "OnResourcesLoaded"）
        // 以 ResourceService.EVT_X 为定义方唯一权威。
        public const string EVT_GET_EXTERNAL_SPRITE       = "GetExternalSprite";
        public const string EVT_GET_EXTERNAL_SPRITE_ASYNC = "GetExternalSpriteAsync";
        public const string EVT_LOAD_PREFAB_ASYNC         = "LoadPrefabAsync";
        public const string EVT_LOAD_SPRITE_ASYNC         = "LoadSpriteAsync";
        public const string EVT_LOAD_RULE_TILE_ASYNC      = "LoadRuleTileAsync";
        public const string EVT_LOAD_EXTERNAL_SPRITE_ASYNC = "LoadExternalSpriteAsync";
        public const string EVT_ADD_PRELOAD_CONFIG        = "AddPreloadConfig";
        // R2: 与 ResourceService.EVT_UNLOAD_RESOURCE/EVT_UNLOAD_ALL_RESOURCES 同名 — 走 Service 上的实现，
        // 这里只保留常量作公开 API；façade 上原本的 [Event] 方法是 dead code 已删除。
        public const string EVT_UNLOAD_RESOURCE           = "UnloadResource";
        public const string EVT_UNLOAD_ALL_RESOURCES      = "UnloadAllResources";
        /// <summary>主动注册一张多精灵图集，将子精灵按名缓存。data: [string sheetResourcePath]</summary>
        public const string EVT_REGISTER_SPRITE_SHEET     = ResourceService.EVT_REGISTER_SPRITE_SHEET;

        private ResourceService _resourceService;

        // 上次刷新 Inspector 时记录的资源数量 — 仅在数量变化（启动期预加载阶段）时刷新一次，
        // 稳定后零分配；不再做每帧/节流刷新。
        private int _lastInspectorResourceCount = -1;

        #region Inspector Debug Fields

        [Header("Debug Information")]
        [SerializeField] private int _loadedResourceCount = 0;
        [SerializeField] private string[] _loadedResourcePaths = System.Array.Empty<string>();

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            _resourceService = ResourceService.Instance;
            if (_resourceService != null) _serviceEnableLogging = _resourceService.EnableLogging;
            Log("ResourceManager 初始化完成", Color.green);
        }

        protected override void Update()
        {
            // Inspector 同步节流 — 仅在 Editor 模式下运行，避免 Build 模式下的无谓开销
            // Inspector 信息仅用于 Editor 调试，Build 模式下无意义
            // 日志开关同步已在 Awake 中调用一次，无需每帧重复
#if UNITY_EDITOR
            if (!_showServiceDataInInspector || _resourceService == null) return;

            var loadedResources = _resourceService.GetLoadedResources();
            var count = loadedResources.Count;
            if (count == _lastInspectorResourceCount) return;

            _lastInspectorResourceCount = count;
            UpdateServiceInspectorInfo();
            UpdateDebugInfo(loadedResources);
#endif
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (_resourceService == null) return;
            _resourceService.UpdateInspectorInfo();
            _serviceInspectorInfo = _resourceService.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (_resourceService != null) _resourceService.EnableLogging = _serviceEnableLogging;
        }

        private void UpdateDebugInfo(Dictionary<ResourceKey, UnityEngine.Object> loadedResources)
        {
            if (_resourceService == null) return;
            _loadedResourceCount = loadedResources.Count;
            // 只在容量变化时重建数组，避免每次都 new string[]。
            if (_loadedResourcePaths == null || _loadedResourcePaths.Length != loadedResources.Count)
                _loadedResourcePaths = new string[loadedResources.Count];
            int index = 0;
            foreach (var kvp in loadedResources)
            {
                var resourceId = _resourceService.GetResourceId(kvp.Key);
                _loadedResourcePaths[index++] = $"{resourceId} ({kvp.Value?.GetType().Name})";
            }
        }

        private void Start()
        {
            // 通过 Event 触发数据加载
            var sample = System.Diagnostics.Stopwatch.StartNew();
            EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_DATA_LOADED, new List<object>());
            sample.Stop();
            var elapsed = sample.ElapsedMilliseconds;
            var message = $"[StartupTiming] ResourceManager.Start.TriggerDataLoaded: {elapsed} ms";
            if (elapsed >= 16) Debug.LogWarning(message);
            else Debug.Log(message);
        }

        protected override void OnManagerDestroy()
        {
            if (_resourceService != null)
            {
                _resourceService.UnloadAll(new List<object>());
                Log("ResourceManager 销毁时已清理所有资源", Color.yellow);
            }
        }

        // ============================================================
        // R1: façade 转发 helpers — 把异步方法转发到对应的 Service
        // ============================================================

        /// <summary>异步获取资源：转发到对应素材类型的 Service。</summary>
        private List<object> GetAsyncFwd(List<object> data, string typeStr)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");
            
            // 转发到对应素材类型的 Service 的 GetAsync 事件
            string eventName = $"Get{typeStr}Async";
            return EventProcessor.Instance.TriggerEventMethod(eventName, data);
        }

        /// <summary>异步加载：转发到对应素材类型的 Service。</summary>
        private List<object> LoadAsyncFwd(List<object> data, string typeStr)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");
            
            // 转发到对应素材类型的 Service 的 LoadAsync 事件
            string eventName = $"Load{typeStr}Async";
            return EventProcessor.Instance.TriggerEventMethod(eventName, data);
        }


        private List<object> GetSync<T>(List<object> data, string typeTag) where T : UnityEngine.Object
        {
            if (data == null || data.Count < 1) return ResultCode.Fail("参数不足");
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, typeTag);
            if (_resourceService != null && _resourceService.TryGetResource(key, out var cached))
                return ResultCode.Ok(cached);

            var asset = Resources.Load<T>(path);
#if UNITY_EDITOR
            if (asset == null)
                asset = LoadFrameworkResourceSync<T>(path);
#endif
            if (asset == null)
                asset = LoadAddressableSync<T>(path, typeTag);
            if (asset == null) return ResultCode.Fail($"{typeTag} 加载失败: {path}");
            _resourceService?.CacheResource(key, asset);
            return ResultCode.Ok(asset);
        }

        private static T LoadAddressableSync<T>(string path, string typeTag) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            return null;
#else
            try
            {
                var handle = Addressables.LoadAssetAsync<T>(path);
                return handle.WaitForCompletion();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ResourceManager] Addressable {typeTag} load failed: {path}, {ex.Message}");
                return null;
            }
#endif
        }

#if UNITY_EDITOR
        private static T LoadFrameworkResourceSync<T>(string path) where T : UnityEngine.Object
        {
            if (string.IsNullOrEmpty(path)) return null;

            var normalized = path.Replace('\\', '/').Trim('/');
            const string frameworkPrefix = "Assets/FrameworkResources/";
            if (normalized.StartsWith(frameworkPrefix, System.StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(frameworkPrefix.Length);

            var assetPath = "Assets/FrameworkResources/" + normalized;
            var resolvedPath = ResolveFrameworkAssetPath(assetPath);
            return string.IsNullOrEmpty(resolvedPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<T>(resolvedPath);
        }

        private static string ResolveFrameworkAssetPath(string pathWithoutExtension)
        {
            if (File.Exists(pathWithoutExtension)) return pathWithoutExtension;

            string[] extensions =
            {
                ".prefab", ".asset", ".png", ".jpg", ".jpeg", ".wav", ".mp3", ".ogg", ".mat", ".controller"
            };
            foreach (var ext in extensions)
            {
                var candidate = pathWithoutExtension + ext;
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
#endif

        // ===== Sync getters =====
        [Event(EVT_GET_PREFAB)]         public List<object> GetPrefab(List<object> d)        => GetSync<GameObject>(d, "Prefab");
        [Event(EVT_GET_AUDIO_CLIP)]     public List<object> GetAudioClip(List<object> d)     => GetSync<AudioClip>(d, "AudioClip");
        [Event(EVT_GET_TEXTURE)]        public List<object> GetTexture(List<object> d)       => GetSync<Texture2D>(d, "Texture");
        [Event(EVT_GET_MATERIAL)]       public List<object> GetMaterial(List<object> d)      => GetSync<Material>(d, "Material");
        [Event(EVT_GET_RULE_TILE)]      public List<object> GetRuleTile(List<object> d)      => GetSync<RuleTile>(d, "RuleTile");
        [Event(EVT_GET_ANIMATION_CLIP)] public List<object> GetAnimationClip(List<object> d) => GetSync<AnimationClip>(d, "AnimationClip");

        [Event(EVT_GET_EXTERNAL_SPRITE)]
        public List<object> GetExternalSprite(List<object> data)
        {
            if (data == null || data.Count < 1) return ResultCode.Fail("参数不足");
            string filePath = data[0] as string;
            if (string.IsNullOrEmpty(filePath)) return ResultCode.Fail("路径为空");
            var key = new ResourceKey(filePath, true, "Sprite");
            return SpriteService.Instance.TryGetResource(key, out var cached)
                ? ResultCode.Ok(cached)
                : ResultCode.Fail("外部 Sprite 未加载");
        }

        // ===== Async getters =====
        public List<object> GetPrefabAsync(List<object> d)        => GetAsyncFwd(d, "Prefab");
        public List<object> GetSpriteAsync(List<object> d)        => GetAsyncFwd(d, "Sprite");
        public List<object> GetAudioClipAsync(List<object> d)     => GetAsyncFwd(d, "AudioClip");
        public List<object> GetTextureAsync(List<object> d)       => GetAsyncFwd(d, "Texture");
        public List<object> GetMaterialAsync(List<object> d)      => GetAsyncFwd(d, "Material");
        public List<object> GetRuleTileAsync(List<object> d)      => GetAsyncFwd(d, "RuleTile");
        public List<object> GetAnimationClipAsync(List<object> d) => GetAsyncFwd(d, "AnimationClip");

        /// <summary>外部 Sprite 异步获取走 ExternalImageService。</summary>
        [Event(EVT_GET_EXTERNAL_SPRITE_ASYNC)]
        public List<object> GetExternalSpriteAsync(List<object> data)
        {
            string filePath = data[0] as string;
            if (string.IsNullOrEmpty(filePath)) return ResultCode.Fail("路径为空");
            
            return EventProcessor.Instance.TriggerEventMethod(
                ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC,
                new List<object> { filePath });
        }

        // ===== Async loaders =====
        public List<object> LoadPrefabAsync(List<object> d)    => LoadAsyncFwd(d, "Prefab");
        public List<object> LoadSpriteAsync(List<object> d)    => LoadAsyncFwd(d, "Sprite");
        public List<object> LoadRuleTileAsync(List<object> d)  => LoadAsyncFwd(d, "RuleTile");

        /// <summary>外部图片异步加载走 EVT_LOAD_EXTERNAL_IMAGE_ASYNC。</summary>
        [Event(EVT_LOAD_EXTERNAL_SPRITE_ASYNC)]
        public List<object> LoadExternalSpriteAsync(List<object> data)
        {
            string filePath = data[0] as string;
            return EventProcessor.Instance.TriggerEventMethod(
                ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC,
                new List<object> { filePath });
        }

        /// <summary>添加预加载配置 — 转发到 ResourceService.EVT_ADD_RESOURCE_CONFIG。</summary>
        [Event(EVT_ADD_PRELOAD_CONFIG)]
        public List<object> AddPreloadConfig(List<object> data)
        {
            return EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_ADD_RESOURCE_CONFIG, data);
        }

        // R2: 原 EVT_UNLOAD_RESOURCE / EVT_UNLOAD_ALL_RESOURCES 的 [Event] 方法已删除（dead code，
        // 字符串 key 与 ResourceService 同名，扫描期被 Service 覆盖永不被调用）。常量保留作公开 API。
    }
}
