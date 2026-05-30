using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Sprite
{
    /// <summary>Sprite 资源服务 — 管理 Sprite 资源的加载、缓存、卸载、子图兜底。</summary>
    public class SpriteService : ResourceServiceBase<SpriteService>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_GET_SPRITE = "GetSprite";
        public const string EVT_GET_SPRITE_ASYNC = "GetSpriteAsync";
        public const string EVT_LOAD_SPRITE_ASYNC = "LoadSpriteAsync";
        public const string EVT_REGISTER_SPRITE_TO_CACHE = "RegisterSpriteToCache";
        
        // ============================================================
        // 同步获取 Sprite（从缓存）
        // ============================================================
        [Event(EVT_GET_SPRITE)]
        public List<object> GetFromCache(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Sprite"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            return ResultCode.Fail("Sprite 未加载");
        }

        // ============================================================
        // 异步获取 Sprite
        // ============================================================
        [Event(EVT_GET_SPRITE_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Sprite"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoadWithFallback(path, typeof(UnityEngine.Sprite), "Sprite");
            return ResultCode.Fail("加载中");
        }

        // ============================================================
        // 异步加载 Sprite
        // ============================================================
        [Event(EVT_LOAD_SPRITE_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Sprite"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoadWithFallback(path, typeof(UnityEngine.Sprite), "Sprite");
            return ResultCode.Fail("加载中");
        }

        // ============================================================
        // 注册 Sprite 到缓存
        // ============================================================
        [Event(EVT_REGISTER_SPRITE_TO_CACHE)]
        public List<object> RegisterSpriteToCache(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数错误");
            
            string spriteId = data[0] as string;
            var sprite = data[1] as UnityEngine.Sprite;
            
            if (string.IsNullOrEmpty(spriteId) || sprite == null)
                return ResultCode.Fail("Sprite ID 或 Sprite 为空");
            
            var key = new ResourceKey(spriteId, false, NormalizeTypeTag("Sprite"));
            CacheResource(key, sprite);
            Log($"注册 Sprite 到缓存: {spriteId}", Color.green);
            return ResultCode.Ok();
        }

        // ============================================================
        // Fallback 异步加载 — 尝试多个候选路径
        // ============================================================
        protected override void StartAsyncLoadWithFallback(string path, System.Type type, string typeTag)
        {
            var key = new ResourceKey(path, false, NormalizeTypeTag(typeTag));
            if (TryGetResource(key, out _)) return;

            // 先尝试直接路径
            StartAsyncLoad(path, type, typeTag);

            // 最后尝试子图加载
            StartAsyncLoadSpriteByName(path);
        }

        /// <summary>异步加载 Sprite 子图 — 尝试从各个子目录加载。</summary>
        private void StartAsyncLoadSpriteByName(string path)
        {
            var spriteName = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
            if (string.IsNullOrEmpty(spriteName)) spriteName = path;

            // 异步尝试各个子目录
            foreach (var hint in SubfolderHints)
            {
                var candidate = string.IsNullOrEmpty(hint) ? spriteName : $"{hint}/{spriteName}";
                var req = Resources.LoadAsync<UnityEngine.Sprite>(candidate);
                req.completed += _ =>
                {
                    if (req.asset != null)
                    {
                        var spriteKey = new ResourceKey(spriteName, false, NormalizeTypeTag("Sprite"));
                        if (!TryGetResource(spriteKey, out var existing))
                        {
                            CacheResource(spriteKey, req.asset);
                            Log($"异步加载 Sprite 子图成功: {spriteName}", Color.green);
                        }
                    }
                };
            }
        }
    }
}
