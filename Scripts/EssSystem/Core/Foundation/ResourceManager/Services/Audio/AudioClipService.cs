using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;

namespace EssSystem.Core.Foundation.ResourceManager.Services.Audio
{
    /// <summary>AudioClip 资源服务 — 管理 AudioClip 资源的加载、缓存、卸载。</summary>
    public class AudioClipService : ResourceServiceBase<AudioClipService>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_GET_AUDIO_CLIP_ASYNC = "GetAudioClipAsync";
        public const string EVT_LOAD_AUDIO_CLIP_ASYNC = "LoadAudioClipAsync";

        // ============================================================
        // 异步获取 AudioClip
        // ============================================================
        [Event(EVT_GET_AUDIO_CLIP_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("AudioClip"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(AudioClip), "AudioClip");
            return ResultCode.Fail("加载中");
        }

        // ============================================================
        // 异步加载 AudioClip
        // ============================================================
        [Event(EVT_LOAD_AUDIO_CLIP_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("AudioClip"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoad(path, typeof(AudioClip), "AudioClip");
            return ResultCode.Fail("加载中");
        }
    }
}
