using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Texture
{
    /// <summary>Texture 资源服务 — 管理 Texture 资源的加载、缓存、卸载。</summary>
    public class TextureService : ResourceServiceBase<TextureService>
    {
        public const string EVT_GET_TEXTURE_ASYNC = "GetTextureAsync";
        public const string EVT_LOAD_TEXTURE_ASYNC = "LoadTextureAsync";

        [Event(EVT_GET_TEXTURE_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Texture"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(Texture2D), "Texture");
            return ResultCode.Fail("加载中");
        }

        [Event(EVT_LOAD_TEXTURE_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Texture"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(Texture2D), "Texture");
            return ResultCode.Fail("加载中");
        }
    }
}
