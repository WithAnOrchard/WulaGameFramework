using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;

namespace EssSystem.Core.ResourceManager
{
    /// <summary>
    /// 资源管理器 - 符合架构规范
    /// </summary>
    [Manager(0)]
    public class ResourceManager : Manager<ResourceManager>
    {
        private ResourceService _resourceService;

        protected override void Initialize()
        {
            base.Initialize();
            _resourceService = ResourceService.Instance;
            Log("ResourceManager 初始化完成", Color.green);
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
