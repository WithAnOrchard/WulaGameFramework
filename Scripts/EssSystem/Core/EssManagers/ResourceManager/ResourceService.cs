using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Util;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;

namespace EssSystem.Core.EssManagers.ResourceManager
{
    /// <summary>
    /// 资源缓存键（性能优化）
    /// </summary>
    public struct ResourceKey : IEquatable<ResourceKey>
    {
        public readonly string Path;
        public readonly bool IsExternal;

        public ResourceKey(string path, bool isExternal)
        {
            Path = path;
            IsExternal = isExternal;
        }

        public bool Equals(ResourceKey other)
        {
            return Path == other.Path && IsExternal == other.IsExternal;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (Path?.GetHashCode() ?? 0);
                hash = hash * 31 + IsExternal.GetHashCode();
                return hash;
            }
        }

        public override string ToString()
        {
            return IsExternal ? $"external:{Path}" : $"unity:{Path}";
        }
    }

    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        Prefab,
        Sprite,
        AudioClip,
        Texture
    }

    /// <summary>
    /// 资源配置项
    /// </summary>
    [Serializable]
    public class ResourceConfigItem
    {
        public string id;
        public string path;
        public bool isExternal;
        public ResourceType type;
    }

    /// <summary>
    /// 资源服务 - 符合架构规范
    /// </summary>
    public class ResourceService : Service<ResourceService>
    {
        private Dictionary<ResourceKey, UnityEngine.Object> _loadedResources = new Dictionary<ResourceKey, UnityEngine.Object>();
        private bool _dataLoaded = false;

        protected override void Initialize()
        {
            base.Initialize();
            Log("ResourceService 初始化完成", Color.green);
        }

        /// <summary>
        /// 获取已加载的资源（用于调试）
        /// </summary>
        public Dictionary<ResourceKey, UnityEngine.Object> GetLoadedResources()
        {
            return _loadedResources;
        }

        /// <summary>
        /// 当 DataManager 完成数据加载后调用此方法
        /// </summary>
        [Event("OnResourceDataLoaded")]
        public List<object> OnDataLoaded(List<object> data)
        {
            if (_dataLoaded) return new List<object> { "已加载" };
            _dataLoaded = true;
            LoadAndPreloadResources();
            return new List<object> { "成功" };
        }

        /// <summary>
        /// 从存储加载配置并预加载所有资源
        /// </summary>
        private void LoadAndPreloadResources()
        {
            PreloadCategory<GameObject>("Prefab");
            PreloadCategory<AudioClip>("AudioClip");
            PreloadCategory<Texture2D>("Texture");

            // Sprite 特殊：外部文件走 LoadExternalImageAsync
            foreach (var key in GetKeys("Sprite"))
            {
                var config = GetData<ResourceConfigItem>("Sprite", key);
                if (config == null) continue;

                if (config.isExternal)
                    LoadExternalImageAsync(config.path, s => { if (s != null) Debug.Log($"预加载外部Sprite: {config.path}"); });
                else
                    LoadAsync<Sprite>(config.path, s => { if (s != null) Debug.Log($"预加载Sprite: {config.path}"); });
            }
        }

        /// <summary>
        /// 通用预加载 helper — 按类别 key 异步加载 Unity 资源
        /// </summary>
        private void PreloadCategory<T>(string category) where T : UnityEngine.Object
        {
            foreach (var key in GetKeys(category))
            {
                var config = GetData<ResourceConfigItem>(category, key);
                if (config == null) continue;
                LoadAsync<T>(config.path, asset => { if (asset != null) Debug.Log($"预加载{category}: {config.path}"); });
            }
        }

        /// <summary>
        /// 获取已加载的资源（同步）
        /// </summary>
        [Event("GetResource")]
        public List<object> Get(List<object> data)
        {
            string path = data[0] as string;
            string typeStr = data[1] as string;
            bool isExternal = data.Count > 2 ? (bool)data[2] : false;

            var key = new ResourceKey(path, isExternal);

            if (_loadedResources.ContainsKey(key))
            {
                return new List<object> { "成功", _loadedResources[key] };
            }

            return new List<object> { "资源未加载" };
        }

        /// <summary>
        /// 添加预加载配置
        /// </summary>
        [Event("AddResourceConfig")]
        public List<object> AddPreloadConfig(List<object> data)
        {
            string id = data[0] as string;
            string path = data[1] as string;
            ResourceType type = (ResourceType)data[2];
            bool isExternal = data.Count > 3 ? (bool)data[3] : false;

            string category = type.ToString();
            var config = new ResourceConfigItem { id = id, path = path, isExternal = isExternal, type = type };
            SetData(category, id, config);

            return new List<object> { "成功" };
        }

        /// <summary>
        /// 异步加载 Unity 内部资源
        /// </summary>
        [Event("LoadResourceAsync")]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;
            string typeStr = data[1] as string;
            bool isExternal = data.Count > 2 ? (bool)data[2] : false;

            if (isExternal)
            {
                return new List<object> { "外部资源请使用 LoadExternalImageAsync" };
            }

            var key = new ResourceKey(path, false);

            if (_loadedResources.ContainsKey(key))
            {
                return new List<object> { "成功", _loadedResources[key] };
            }

            // 根据类型加载
            switch (typeStr)
            {
                case "Prefab":
                    LoadAsync<GameObject>(path, obj => { });
                    break;
                case "Sprite":
                    LoadAsync<Sprite>(path, obj => { });
                    break;
                case "AudioClip":
                    LoadAsync<AudioClip>(path, obj => { });
                    break;
                case "Texture":
                    LoadAsync<Texture2D>(path, obj => { });
                    break;
                default:
                    return new List<object> { "不支持的资源类型" };
            }

            // 异步加载，返回加载中状态
            return new List<object> { "加载中" };
        }

        /// <summary>
        /// 内部异步加载方法
        /// </summary>
        private void LoadAsync<T>(string path, System.Action<T> callback) where T : UnityEngine.Object
        {
            var key = new ResourceKey(path, false);

            if (_loadedResources.ContainsKey(key))
            {
                callback?.Invoke(_loadedResources[key] as T);
                return;
            }

            ResourceRequest request = Resources.LoadAsync<T>(path);
            request.completed += (operation) =>
            {
                T resource = request.asset as T;
                if (resource != null)
                {
                    _loadedResources[key] = resource;
                }
                callback?.Invoke(resource);
            };
        }

        /// <summary>
        /// 异步加载外部文件图片
        /// </summary>
        [Event("LoadExternalImageAsync")]
        public List<object> LoadExternalImageAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var key = new ResourceKey(filePath, true);

            if (_loadedResources.ContainsKey(key))
            {
                return new List<object> { "成功", _loadedResources[key] };
            }

            if (!System.IO.File.Exists(filePath))
            {
                return new List<object> { "文件不存在" };
            }

            LoadExternalImageAsync(filePath, null);
            return new List<object> { "加载中" };
        }

        /// <summary>
        /// 内部异步加载外部文件图片（带回调）
        /// </summary>
        private void LoadExternalImageAsync(string filePath, System.Action<Sprite> callback)
        {
            var key = new ResourceKey(filePath, true);

            if (_loadedResources.ContainsKey(key))
            {
                callback?.Invoke(_loadedResources[key] as Sprite);
                return;
            }

            if (!System.IO.File.Exists(filePath))
            {
                callback?.Invoke(null);
                return;
            }

            System.Threading.Tasks.Task.Run(() =>
            {
                byte[] bytes = System.IO.File.ReadAllBytes(filePath);

                MainThreadDispatcher.Enqueue(() =>
                {
                    Texture2D texture = new Texture2D(2, 2);
                    if (texture.LoadImage(bytes))
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                        _loadedResources[key] = sprite;
                        Debug.Log($"外部 Sprite 加载成功: {filePath}");

                        callback?.Invoke(sprite);

                        // 触发加载完成事件
                        EventProcessor.Instance.TriggerEventMethod("OnExternalImageLoaded",
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["sprite"] = sprite } });
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);

                        callback?.Invoke(null);

                        // 触发加载失败事件
                        EventProcessor.Instance.TriggerEventMethod("OnExternalImageLoadFailed",
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["error"] = "加载失败" } });
                    }
                });
            });
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        [Event("UnloadResource")]
        public List<object> Unload(List<object> data)
        {
            string path = data[0] as string;
            bool isExternal = data.Count > 1 ? (bool)data[1] : false;

            var key = new ResourceKey(path, isExternal);

            if (_loadedResources.ContainsKey(key))
            {
                Resources.UnloadAsset(_loadedResources[key]);
                _loadedResources.Remove(key);
                return new List<object> { "成功" };
            }

            return new List<object> { "资源未加载" };
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        [Event("UnloadAllResources")]
        public List<object> UnloadAll(List<object> data)
        {
            foreach (var resource in _loadedResources.Values)
            {
                Resources.UnloadAsset(resource);
            }
            _loadedResources.Clear();
            return new List<object> { "成功" };
        }
    }
}
