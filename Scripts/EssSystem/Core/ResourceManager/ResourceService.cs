using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Manager;

namespace EssSystem.Core.ResourceManager
{
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
    /// 资源服务 - 简化版资源管理
    /// </summary>
    public class ResourceService : Service<ResourceService>
    {
        private Dictionary<string, UnityEngine.Object> _loadedResources = new Dictionary<string, UnityEngine.Object>();
        private bool _dataLoaded = false;

        protected override void Initialize()
        {
            base.Initialize();
            // DataManager会自动加载数据，数据加载完成后会触发预加载
        }

        /// <summary>
        /// 当DataManager完成数据加载后调用此方法
        /// </summary>
        public void OnDataLoaded()
        {
            if (_dataLoaded) return;
            _dataLoaded = true;
            LoadAndPreloadResources();
        }

        /// <summary>
        /// 从存储加载配置并预加载所有资源
        /// </summary>
        private void LoadAndPreloadResources()
        {
            // 预加载Prefab
            var prefabKeys = GetKeys("Prefab");
            foreach (var key in prefabKeys)
            {
                var config = GetData<ResourceConfigItem>("Prefab", key);
                if (config != null)
                {
                    LoadPrefabAsync(config.path, (prefab) => {
                        if (prefab != null)
                        {
                            Debug.Log($"预加载Prefab: {config.path}");
                        }
                    });
                }
            }

            // 预加载Sprite
            var spriteKeys = GetKeys("Sprite");
            foreach (var key in spriteKeys)
            {
                var config = GetData<ResourceConfigItem>("Sprite", key);
                if (config != null)
                {
                    if (config.isExternal)
                    {
                        LoadExternalImageAsync(config.path, (sprite) => {
                            if (sprite != null)
                            {
                                Debug.Log($"预加载外部Sprite: {config.path}");
                            }
                        });
                    }
                    else
                    {
                        LoadAsync<Sprite>(config.path, (sprite) => {
                            if (sprite != null)
                            {
                                Debug.Log($"预加载Sprite: {config.path}");
                            }
                        });
                    }
                }
            }

            // 预加载AudioClip
            var audioKeys = GetKeys("AudioClip");
            foreach (var key in audioKeys)
            {
                var config = GetData<ResourceConfigItem>("AudioClip", key);
                if (config != null)
                {
                    LoadAudioClipAsync(config.path, (audio) => {
                        if (audio != null)
                        {
                            Debug.Log($"预加载AudioClip: {config.path}");
                        }
                    });
                }
            }

            // 预加载Texture
            var textureKeys = GetKeys("Texture");
            foreach (var key in textureKeys)
            {
                var config = GetData<ResourceConfigItem>("Texture", key);
                if (config != null)
                {
                    LoadAsync<Texture2D>(config.path, (texture) => {
                        if (texture != null)
                        {
                            Debug.Log($"预加载Texture: {config.path}");
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 获取已加载的资源（同步）
        /// </summary>
        public T Get<T>(string path, bool isExternal = false) where T : UnityEngine.Object
        {
            string key = isExternal ? $"external:{path}" : $"unity:{path}";
            
            if (_loadedResources.ContainsKey(key))
            {
                return _loadedResources[key] as T;
            }
            
            return null;
        }

        /// <summary>
        /// 添加预加载配置
        /// </summary>
        public void AddPreloadConfig(string id, string path, ResourceType type, bool isExternal = false)
        {
            string category = type.ToString();
            var config = new ResourceConfigItem { id = id, path = path, isExternal = isExternal, type = type };
            SetData(category, id, config);
        }

        /// <summary>
        /// 异步加载Unity内部资源
        /// </summary>
        public void LoadAsync<T>(string path, System.Action<T> callback) where T : UnityEngine.Object
        {
            string key = $"unity:{path}";
            
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
        public void LoadExternalImageAsync(string filePath, System.Action<Sprite> callback)
        {
            string key = $"external:{filePath}";
            
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
                
                UnityEngine.MainThreadDispatcher.Enqueue(() =>
                {
                    Texture2D texture = new Texture2D(2, 2);
                    if (texture.LoadImage(bytes))
                    {
                        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.zero);
                        _loadedResources[key] = sprite;
                        callback?.Invoke(sprite);
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(texture);
                        callback?.Invoke(null);
                    }
                });
            });
        }

        /// <summary>
        /// 异步加载AudioClip
        /// </summary>
        public void LoadAudioClipAsync(string path, System.Action<AudioClip> callback)
        {
            string key = $"unity:{path}";
            
            if (_loadedResources.ContainsKey(key))
            {
                callback?.Invoke(_loadedResources[key] as AudioClip);
                return;
            }

            LoadAsync<AudioClip>(path, callback);
        }

        /// <summary>
        /// 异步加载Prefab
        /// </summary>
        public void LoadPrefabAsync(string path, System.Action<GameObject> callback)
        {
            string key = $"unity:{path}";
            
            if (_loadedResources.ContainsKey(key))
            {
                callback?.Invoke(_loadedResources[key] as GameObject);
                return;
            }

            LoadAsync<GameObject>(path, callback);
        }

        /// <summary>
        /// 卸载资源
        /// </summary>
        public void Unload(string path, bool isExternal = false)
        {
            string key = isExternal ? $"external:{path}" : $"unity:{path}";
            
            if (_loadedResources.ContainsKey(key))
            {
                Resources.UnloadAsset(_loadedResources[key]);
                _loadedResources.Remove(key);
            }
        }

        /// <summary>
        /// 卸载所有资源
        /// </summary>
        public void UnloadAll()
        {
            foreach (var resource in _loadedResources.Values)
            {
                Resources.UnloadAsset(resource);
            }
            _loadedResources.Clear();
        }
    }
}
