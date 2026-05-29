using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Animation
{
    /// <summary>AnimationClip 资源服务 — 管理 AnimationClip 资源的加载、缓存、卸载。</summary>
    public class AnimationClipService : ResourceServiceBase<AnimationClipService>
    {
        public const string EVT_GET_ANIMATION_CLIP_ASYNC = "GetAnimationClipAsync";
        public const string EVT_LOAD_ANIMATION_CLIP_ASYNC = "LoadAnimationClipAsync";

        [Event(EVT_GET_ANIMATION_CLIP_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("AnimationClip"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(AnimationClip), "AnimationClip");
            return ResultCode.Fail("加载中");
        }

        [Event(EVT_LOAD_ANIMATION_CLIP_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("AnimationClip"));
            if (TryGetResource(key, out var cached)) return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(AnimationClip), "AnimationClip");
            return ResultCode.Fail("加载中");
        }
    }
}
