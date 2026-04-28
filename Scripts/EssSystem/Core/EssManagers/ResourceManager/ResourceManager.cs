using System.Collections.Generic;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.Core.EssManagers.Manager;
using UnityEngine;

namespace EssSystem.Core.EssManagers.ResourceManager
{
    /// <summary>
    /// 资源管理器 - 符合架构规范
    /// </summary>
    [Manager(0)]
    public class ResourceManager : Manager<ResourceManager>
    {
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

        protected override void Start()
        {
            base.Start();
            // 通过 Event 触发数据加载
            EventProcessor.Instance.TriggerEventMethod("OnResourceDataLoaded", new List<object>());
        }

        /// <summary>
        /// 获取 Prefab
        /// </summary>
        [Event("GetPrefab")]
        public List<object> GetPrefab(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("GetResource", new List<object> { path, "Prefab", false });
            if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
            {
                return new List<object> { "成功", result[1] };
            }
            return new List<object> { "获取失败" };
        }

        /// <summary>
        /// 获取 Sprite
        /// </summary>
        [Event("GetSprite")]
        public List<object> GetSprite(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("GetResource", new List<object> { path, "Sprite", false });
            if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
            {
                return new List<object> { "成功", result[1] };
            }
            return new List<object> { "获取失败" };
        }

        /// <summary>
        /// 获取 AudioClip
        /// </summary>
        [Event("GetAudioClip")]
        public List<object> GetAudioClip(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("GetResource", new List<object> { path, "AudioClip", false });
            if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
            {
                return new List<object> { "成功", result[1] };
            }
            return new List<object> { "获取失败" };
        }

        /// <summary>
        /// 获取 Texture
        /// </summary>
        [Event("GetTexture")]
        public List<object> GetTexture(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("GetResource", new List<object> { path, "Texture", false });
            if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
            {
                return new List<object> { "成功", result[1] };
            }
            return new List<object> { "获取失败" };
        }

        /// <summary>
        /// 获取外部 Sprite
        /// </summary>
        [Event("GetExternalSprite")]
        public List<object> GetExternalSprite(List<object> data)
        {
            string filePath = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("GetResource", new List<object> { filePath, "Sprite", true });
            if (result != null && result.Count >= 2 && result[0].ToString() == "成功")
            {
                return new List<object> { "成功", result[1] };
            }
            return new List<object> { "获取失败" };
        }

        /// <summary>
        /// 异步加载 Prefab
        /// </summary>
        [Event("LoadPrefabAsync")]
        public List<object> LoadPrefabAsync(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("LoadResourceAsync", new List<object> { path, "Prefab", false });
            return result;
        }

        /// <summary>
        /// 异步加载 Sprite
        /// </summary>
        [Event("LoadSpriteAsync")]
        public List<object> LoadSpriteAsync(List<object> data)
        {
            string path = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("LoadResourceAsync", new List<object> { path, "Sprite", false });
            return result;
        }

        /// <summary>
        /// 异步加载外部 Sprite
        /// </summary>
        [Event("LoadExternalSpriteAsync")]
        public List<object> LoadExternalSpriteAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var result = EventProcessor.Instance.TriggerEventMethod("LoadExternalImageAsync", new List<object> { filePath });
            return result;
        }

        /// <summary>
        /// 添加预加载配置
        /// </summary>
        [Event("AddPreloadConfig")]
        public List<object> AddPreloadConfig(List<object> data)
        {
            string id = data[0] as string;
            string path = data[1] as string;
            ResourceType type = (ResourceType)data[2];
            bool isExternal = data.Count > 3 ? (bool)data[3] : false;

            var result = EventProcessor.Instance.TriggerEventMethod("AddResourceConfig", 
                new List<object> { id, path, type, isExternal });
            return result;
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        [Event("UnloadResource")]
        public List<object> UnloadResource(List<object> data)
        {
            string path = data[0] as string;
            bool isExternal = data.Count > 1 ? (bool)data[1] : false;

            var result = EventProcessor.Instance.TriggerEventMethod("UnloadResource", 
                new List<object> { path, isExternal });
            return result;
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        [Event("UnloadAllResources")]
        public List<object> UnloadAllResources(List<object> data)
        {
            var result = EventProcessor.Instance.TriggerEventMethod("UnloadAllResources", new List<object>());
            return result;
        }
    }
}
