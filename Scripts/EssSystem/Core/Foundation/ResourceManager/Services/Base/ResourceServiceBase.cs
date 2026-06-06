using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Base
{
    /// <summary>资源服务基类 — 提供通用的缓存、加载、卸载逻辑。</summary>
    public abstract class ResourceServiceBase<T> : Service<T> where T : class, new()
    {
        // ============================================================
        // 常量
        // ============================================================
        protected static readonly string[] SubfolderHints = { "", "UI", "Sprites", "Items", "Effects" };

        // ============================================================
        // 状态
        // ============================================================
        protected Dictionary<ResourceKey, UnityEngine.Object> LoadedResources
            = new Dictionary<ResourceKey, UnityEngine.Object>();

        // ============================================================
        // 缓存操作
        // ============================================================
        public void CacheResource(ResourceKey key, UnityEngine.Object resource)
        {
            if (resource != null)
            {
                LoadedResources[key] = resource;
                ResourceService.Instance?.TrackResource(key, resource);
            }
        }

        public bool TryGetResource(ResourceKey key, out UnityEngine.Object resource)
        {
            return LoadedResources.TryGetValue(key, out resource);
        }

        protected void RemoveResource(ResourceKey key)
        {
            if (LoadedResources.TryGetValue(key, out var resource))
            {
                Resources.UnloadAsset(resource);
                LoadedResources.Remove(key);
            }
        }

        public Dictionary<ResourceKey, UnityEngine.Object> GetLoadedResources() => LoadedResources;

        public int GetLoadedResourceCount() => LoadedResources.Count;

        public void ForgetResource(ResourceKey key)
        {
            LoadedResources.Remove(key);
        }

        public void ClearCache()
        {
            LoadedResources.Clear();
        }

        // ============================================================
        // 异步加载通用逻辑
        // ============================================================
        protected void StartAsyncLoad(string path, Type type, string typeTag)
        {
            var key = new ResourceKey(path, false, NormalizeTypeTag(typeTag));
            if (TryGetResource(key, out _)) return;

            var req = Resources.LoadAsync(path, type);
            req.completed += _ =>
            {
                if (req.asset != null)
                {
                    CacheResource(key, req.asset);
                    Log($"异步加载完成: {path} ({typeTag})", Color.green);
                }
                else
                {
                    Log($"异步加载失败: {path} ({typeTag})", Color.yellow);
                }
            };
        }

        protected virtual void StartAsyncLoadWithFallback(string path, Type type, string typeTag)
        {
            StartAsyncLoad(path, type, typeTag);
        }

        // ============================================================
        // 工具方法
        // ============================================================
        protected static string NormalizeTypeTag(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            switch (s)
            {
                case "Prefab":  case "GameObject": return "Prefab";
                case "Texture": case "Texture2D":  return "Texture";
                default:                           return s;
            }
        }

        // ============================================================
        // 卸载
        // ============================================================
        public void UnloadResource(ResourceKey key)
        {
            RemoveResource(key);
        }

        public void UnloadAll()
        {
            foreach (var resource in LoadedResources.Values)
            {
                Resources.UnloadAsset(resource);
            }
            LoadedResources.Clear();
        }
    }
}
