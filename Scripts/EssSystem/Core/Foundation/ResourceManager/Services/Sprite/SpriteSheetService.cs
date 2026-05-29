using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Sprite
{
    /// <summary>精灵图集服务 — 管理精灵图集和子精灵。</summary>
    public class SpriteSheetService : Service<SpriteSheetService>
    {
        public const string EVT_REGISTER_SPRITE_SHEET = "RegisterSpriteSheet";

        [Event(EVT_REGISTER_SPRITE_SHEET)]
        public List<object> RegisterSpriteSheet(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string sheetPath) || string.IsNullOrEmpty(sheetPath))
                return ResultCode.Fail("参数错误：需要 [string sheetResourcePath]");

            var sprites = Resources.LoadAll<UnityEngine.Sprite>(sheetPath);
            if (sprites == null || sprites.Length == 0)
                return ResultCode.Fail($"找不到精灵图集: {sheetPath}");

            var spriteService = SpriteService.Instance;
            int added = 0;
            foreach (var s in sprites)
            {
                if (s == null) continue;
                var key = new ResourceKey(s.name, false, "Sprite");
                if (!spriteService.TryGetResource(key, out _))
                {
                    spriteService.CacheResource(key, s);
                    added++;
                }
            }
            Log($"图集注册 '{sheetPath}': +{added} 个子精灵（总 {sprites.Length}）");
            return ResultCode.Ok(added);
        }

        // ============================================================
        // 公开接口
        // ============================================================
        public void CacheResource(ResourceKey key, UnityEngine.Object resource)
        {
            SpriteService.Instance.CacheResource(key, resource);
        }

        public bool TryGetResource(ResourceKey key, out UnityEngine.Object resource)
        {
            return SpriteService.Instance.TryGetResource(key, out resource);
        }
    }
}
