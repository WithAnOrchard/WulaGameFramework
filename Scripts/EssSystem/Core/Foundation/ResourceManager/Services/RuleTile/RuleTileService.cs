using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.RuleTile
{
    /// <summary>RuleTile 资源服务 — 管理 RuleTile 资源的加载、缓存、卸载。</summary>
    public class RuleTileService : ResourceServiceBase<RuleTileService>
    {
        public const string EVT_GET_RULE_TILE_ASYNC = "GetRuleTileAsync";
        public const string EVT_LOAD_RULE_TILE_ASYNC = "LoadRuleTileAsync";

        [Event(EVT_GET_RULE_TILE_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("RuleTile"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            var ruleTileType = Type.GetType("UnityEngine.Tilemaps.RuleTile, UnityEngine.Tilemaps");
            if (ruleTileType == null) ruleTileType = Type.GetType("UnityEngine.Tilemaps.RuleTile, UnityEngine");
            if (ruleTileType != null)
                StartAsyncLoad(path, ruleTileType, "RuleTile");
            return ResultCode.Fail("加载中");
        }

        [Event(EVT_LOAD_RULE_TILE_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("RuleTile"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            var ruleTileType = Type.GetType("UnityEngine.Tilemaps.RuleTile, UnityEngine.Tilemaps");
            if (ruleTileType == null) ruleTileType = Type.GetType("UnityEngine.Tilemaps.RuleTile, UnityEngine");
            if (ruleTileType != null)
                StartAsyncLoad(path, ruleTileType, "RuleTile");
            return ResultCode.Fail("加载中");
        }
    }
}
