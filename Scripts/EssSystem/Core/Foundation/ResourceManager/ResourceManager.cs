using System.Collections.Generic;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using UnityEngine;

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
            EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_DATA_LOADED, new List<object>());
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


        // ===== Async getters =====
        [Event(EVT_GET_PREFAB_ASYNC)]         public List<object> GetPrefabAsync(List<object> d)        => GetAsyncFwd(d, "Prefab");
        [Event(EVT_GET_SPRITE_ASYNC)]         public List<object> GetSpriteAsync(List<object> d)        => GetAsyncFwd(d, "Sprite");
        [Event(EVT_GET_AUDIO_CLIP_ASYNC)]     public List<object> GetAudioClipAsync(List<object> d)     => GetAsyncFwd(d, "AudioClip");
        [Event(EVT_GET_TEXTURE_ASYNC)]        public List<object> GetTextureAsync(List<object> d)       => GetAsyncFwd(d, "Texture");
        [Event(EVT_GET_MATERIAL_ASYNC)]       public List<object> GetMaterialAsync(List<object> d)      => GetAsyncFwd(d, "Material");
        [Event(EVT_GET_RULE_TILE_ASYNC)]      public List<object> GetRuleTileAsync(List<object> d)      => GetAsyncFwd(d, "RuleTile");
        [Event(EVT_GET_ANIMATION_CLIP_ASYNC)] public List<object> GetAnimationClipAsync(List<object> d) => GetAsyncFwd(d, "AnimationClip");

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
        [Event(EVT_LOAD_PREFAB_ASYNC)]    public List<object> LoadPrefabAsync(List<object> d)    => LoadAsyncFwd(d, "Prefab");
        [Event(EVT_LOAD_SPRITE_ASYNC)]    public List<object> LoadSpriteAsync(List<object> d)    => LoadAsyncFwd(d, "Sprite");
        [Event(EVT_LOAD_RULE_TILE_ASYNC)] public List<object> LoadRuleTileAsync(List<object> d)  => LoadAsyncFwd(d, "RuleTile");

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
