using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Foundation.ResourceManager
{
    /// <summary>
    /// 资源引用计数管理器 —— 支持精细的资源加载/卸载控制。
    /// 每个资源维护引用计数，当计数归零时可自动卸载。
    /// 
    /// 用途：
    /// - 追踪资源的引用关系
    /// - 自动卸载长期未使用的资源
    /// - 提供资源统计信息用于调试和监控
    /// </summary>
    public class ResourceRefCounter
    {
        private class ResourceRef
        {
            public UnityEngine.Object Asset;
            public int RefCount;
            public float LastAccessTime;
        }

        private readonly Dictionary<ResourceKey, ResourceRef> _resources = new();
        private readonly float _unloadTimeoutSeconds;
        private float _lastCleanupTime;
        private const float CLEANUP_INTERVAL = 60f;

        public ResourceRefCounter(float unloadTimeoutSeconds = 300f)
        {
            _unloadTimeoutSeconds = unloadTimeoutSeconds;
            _lastCleanupTime = Time.realtimeSinceStartup;
        }

        /// <summary>加载资源（增加引用计数）。</summary>
        public T Load<T>(ResourceKey key, T asset) where T : UnityEngine.Object
        {
            if (asset == null) return null;

            if (_resources.TryGetValue(key, out var rf))
            {
                rf.RefCount++;
                rf.LastAccessTime = Time.realtimeSinceStartup;
                return rf.Asset as T;
            }

            _resources[key] = new ResourceRef
            {
                Asset = asset,
                RefCount = 1,
                LastAccessTime = Time.realtimeSinceStartup
            };
            return asset;
        }

        /// <summary>卸载资源（减少引用计数）。</summary>
        public bool Unload(ResourceKey key)
        {
            if (!_resources.TryGetValue(key, out var rf)) return false;

            rf.RefCount--;
            if (rf.RefCount <= 0)
            {
                Resources.UnloadAsset(rf.Asset);
                _resources.Remove(key);
                return true;
            }
            return false;
        }

        /// <summary>获取资源（不改变引用计数，仅用于查询）。</summary>
        public T Get<T>(ResourceKey key) where T : UnityEngine.Object
        {
            if (_resources.TryGetValue(key, out var rf))
            {
                rf.LastAccessTime = Time.realtimeSinceStartup;
                return rf.Asset as T;
            }
            return null;
        }

        /// <summary>定期清理超时未使用的资源。</summary>
        public void CleanupUnusedAssets()
        {
            var now = Time.realtimeSinceStartup;
            if (now - _lastCleanupTime < CLEANUP_INTERVAL) return;

            _lastCleanupTime = now;
            var keysToRemove = new List<ResourceKey>();

            foreach (var kvp in _resources)
            {
                if (kvp.Value.RefCount == 0 && now - kvp.Value.LastAccessTime > _unloadTimeoutSeconds)
                    keysToRemove.Add(kvp.Key);
            }

            foreach (var key in keysToRemove)
            {
                Resources.UnloadAsset(_resources[key].Asset);
                _resources.Remove(key);
            }
        }

        /// <summary>获取资源统计信息。</summary>
        public Dictionary<string, object> GetStats()
        {
            var stats = new Dictionary<string, object>
            {
                { "TotalCount", _resources.Count },
                { "ActiveCount", 0 },
                { "InactiveCount", 0 },
                { "TotalRefCount", 0 }
            };

            int activeCount = 0;
            int inactiveCount = 0;
            int totalRefCount = 0;

            foreach (var kvp in _resources)
            {
                totalRefCount += kvp.Value.RefCount;
                if (kvp.Value.RefCount > 0) activeCount++;
                else inactiveCount++;
            }

            stats["ActiveCount"] = activeCount;
            stats["InactiveCount"] = inactiveCount;
            stats["TotalRefCount"] = totalRefCount;
            return stats;
        }

        /// <summary>强制卸载所有资源。</summary>
        public void UnloadAll()
        {
            foreach (var rf in _resources.Values)
            {
                Resources.UnloadAsset(rf.Asset);
            }
            _resources.Clear();
        }

        /// <summary>获取资源数量。</summary>
        public int GetResourceCount() => _resources.Count;
    }
}
