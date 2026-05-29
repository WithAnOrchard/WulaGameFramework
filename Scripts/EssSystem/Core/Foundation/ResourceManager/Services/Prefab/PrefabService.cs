using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Prefab
{
    /// <summary>Prefab 资源服务 — 管理 Prefab 资源的加载、缓存、卸载。</summary>
    public class PrefabService : ResourceServiceBase<PrefabService>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_GET_PREFAB_ASYNC = "GetPrefabAsync";
        public const string EVT_LOAD_PREFAB_ASYNC = "LoadPrefabAsync";

        // ============================================================
        // 异步获取 Prefab
        // ============================================================
        [Event(EVT_GET_PREFAB_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Prefab"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(GameObject), "Prefab");
            return ResultCode.Fail("加载中");
        }

        // ============================================================
        // 异步加载 Prefab
        // ============================================================
        [Event(EVT_LOAD_PREFAB_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Prefab"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(GameObject), "Prefab");
            return ResultCode.Fail("加载中");
        }
    }
}
