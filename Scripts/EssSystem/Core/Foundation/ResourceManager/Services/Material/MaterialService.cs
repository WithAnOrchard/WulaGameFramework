using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Material
{
    /// <summary>Material 资源服务 — 管理 Material 资源的加载、缓存、卸载。</summary>
    public class MaterialService : ResourceServiceBase<MaterialService>
    {
        public const string EVT_GET_MATERIAL_ASYNC = "GetMaterialAsync";
        public const string EVT_LOAD_MATERIAL_ASYNC = "LoadMaterialAsync";

        [Event(EVT_GET_MATERIAL_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Material"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(UnityEngine.Material), "Material");
            return ResultCode.Fail("加载中");
        }

        [Event(EVT_LOAD_MATERIAL_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Material"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(UnityEngine.Material), "Material");
            return ResultCode.Fail("加载中");
        }
    }
}
