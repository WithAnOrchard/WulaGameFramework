using System.Collections.Generic;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using UnityEngine;

namespace EssSystem.Core.EssManagers.ResourceManager
{
    /// <summary>
    /// 资源管理器 - 符合架构规范
    /// </summary>
    [Manager(0)]
    public class ResourceManager : Manager<ResourceManager>
    {
        // ===== Event Constants (façade, public API) =====
        public const string EVT_GET_PREFAB                = "GetPrefab";
        public const string EVT_GET_SPRITE                = "GetSprite";
        public const string EVT_GET_AUDIO_CLIP            = "GetAudioClip";
        public const string EVT_GET_TEXTURE               = "GetTexture";
        public const string EVT_GET_EXTERNAL_SPRITE       = "GetExternalSprite";
        public const string EVT_LOAD_PREFAB_ASYNC         = "LoadPrefabAsync";
        public const string EVT_LOAD_SPRITE_ASYNC         = "LoadSpriteAsync";
        public const string EVT_LOAD_EXTERNAL_SPRITE_ASYNC = "LoadExternalSpriteAsync";
        public const string EVT_ADD_PRELOAD_CONFIG        = "AddPreloadConfig";
        public const string EVT_UNLOAD_RESOURCE           = "UnloadResource";
        public const string EVT_UNLOAD_ALL_RESOURCES      = "UnloadAllResources";

        private ResourceService _resourceService;

        #region Inspector Debug Fields

        [Header("Debug Information")]
        [SerializeField]
        private int _loadedResourceCount = 0;

        [SerializeField]
        private string[] _loadedResourcePaths = new string[0];

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            _resourceService = ResourceService.Instance;

            // 从Service加载日志设置
            if (_resourceService != null)
            {
                _serviceEnableLogging = _resourceService.EnableLogging;
            }

            Log("ResourceManager 初始化完成", Color.green);
        }

        protected override void Update()
        {
            base.Update();
            UpdateDebugInfo();
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (_resourceService != null)
            {
                _resourceService.UpdateInspectorInfo();
                _serviceInspectorInfo = _resourceService.InspectorInfo;
            }
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (_resourceService != null)
            {
                _resourceService.EnableLogging = _serviceEnableLogging;
            }
        }

        private void UpdateDebugInfo()
        {
            if (_resourceService != null)
            {
                var loadedResources = _resourceService.GetLoadedResources();
                _loadedResourceCount = loadedResources.Count;
                _loadedResourcePaths = new string[loadedResources.Count];
                int index = 0;
                foreach (var kvp in loadedResources)
                {
                    var resourceId = _resourceService.GetResourceId(kvp.Key);
                    _loadedResourcePaths[index] = $"{resourceId} ({kvp.Value?.GetType().Name})";
                    index++;
                }
            }
        }

        private void Start()
        {
            // 通过 Event 触发数据加载
            EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_DATA_LOADED, new List<object>());
        }

        /// <summary>
        /// 获取 Prefab
        /// </summary>
        [Event(EVT_GET_PREFAB)]
        public List<object> GetPrefab(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_GET_RESOURCE, new List<object> { path, "Prefab", false });
            if (ResultCode.IsOk(result) && result.Count >= 2)
                return ResultCode.Ok(result[1]);
            return ResultCode.Fail("获取失败");
        }

        /// <summary>
        /// 获取 Sprite
        /// </summary>
        [Event(EVT_GET_SPRITE)]
        public List<object> GetSprite(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_GET_RESOURCE, new List<object> { path, "Sprite", false });
            if (ResultCode.IsOk(result) && result.Count >= 2)
                return ResultCode.Ok(result[1]);
            return ResultCode.Fail("获取失败");
        }

        /// <summary>
        /// 获取 AudioClip
        /// </summary>
        [Event(EVT_GET_AUDIO_CLIP)]
        public List<object> GetAudioClip(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_GET_RESOURCE, new List<object> { path, "AudioClip", false });
            if (ResultCode.IsOk(result) && result.Count >= 2)
                return ResultCode.Ok(result[1]);
            return ResultCode.Fail("获取失败");
        }

        /// <summary>
        /// 获取 Texture
        /// </summary>
        [Event(EVT_GET_TEXTURE)]
        public List<object> GetTexture(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_GET_RESOURCE, new List<object> { path, "Texture", false });
            if (ResultCode.IsOk(result) && result.Count >= 2)
                return ResultCode.Ok(result[1]);
            return ResultCode.Fail("获取失败");
        }

        /// <summary>
        /// 获取外部 Sprite
        /// </summary>
        [Event(EVT_GET_EXTERNAL_SPRITE)]
        public List<object> GetExternalSprite(List<object> data)
        {
            string filePath = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_GET_RESOURCE, new List<object> { filePath, "Sprite", true });
            if (ResultCode.IsOk(result) && result.Count >= 2)
                return ResultCode.Ok(result[1]);
            return ResultCode.Fail("获取失败");
        }

        /// <summary>
        /// 异步加载 Prefab
        /// </summary>
        [Event(EVT_LOAD_PREFAB_ASYNC)]
        public List<object> LoadPrefabAsync(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_LOAD_RESOURCE_ASYNC, new List<object> { path, "Prefab", false });
            return result;
        }

        /// <summary>
        /// 异步加载 Sprite
        /// </summary>
        [Event(EVT_LOAD_SPRITE_ASYNC)]
        public List<object> LoadSpriteAsync(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_LOAD_RESOURCE_ASYNC, new List<object> { path, "Sprite", false });
            return result;
        }

        /// <summary>
        /// 异步加载外部 Sprite
        /// </summary>
        [Event(EVT_LOAD_EXTERNAL_SPRITE_ASYNC)]
        public List<object> LoadExternalSpriteAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC, new List<object> { filePath });
            return result;
        }

        /// <summary>
        /// 添加预加载配置
        /// </summary>
        [Event(EVT_ADD_PRELOAD_CONFIG)]
        public List<object> AddPreloadConfig(List<object> data)
        {
            string id = data[0] as string;
            string path = data[1] as string;
            ResourceType type = (ResourceType)data[2];
            bool isExternal = data.Count > 3 ? (bool)data[3] : false;

            var result = EventProcessor.Instance.TriggerEventMethod(ResourceService.EVT_ADD_RESOURCE_CONFIG, 
                new List<object> { id, path, type, isExternal });
            return result;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        // NOTE: 与 ResourceService.Unload 同名（_eventMethods 字典 key 冲突，扫描期后注册者覆盖前者）。
        // 该 façade 在当前实现下实际上 dead-code，保留以便统一公开 API；常量与 Service 共用 EVT_UNLOAD_RESOURCE 字符串值。
        [Event(EVT_UNLOAD_RESOURCE)]
        public List<object> UnloadResource(List<object> data)
        {
            string path = data[0] as string;
            bool isExternal = data.Count > 1 ? (bool)data[1] : false;

            var result = EventProcessor.Instance.TriggerEventMethod(EVT_UNLOAD_RESOURCE, 
                new List<object> { path, isExternal });
            return result;
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        // NOTE: 与 ResourceService.UnloadAll 同名（同上注释）。
        [Event(EVT_UNLOAD_ALL_RESOURCES)]
        public List<object> UnloadAllResources(List<object> data)
        {
            var result = EventProcessor.Instance.TriggerEventMethod(EVT_UNLOAD_ALL_RESOURCES, new List<object>());
            return result;
        }
    }
}
