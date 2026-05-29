using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Sprite;

namespace EssSystem.Core.Foundation.ResourceManager.Services.External
{
    /// <summary>外部图片服务 — 异步加载外部图片。</summary>
    public class ExternalImageService : Service<ExternalImageService>
    {
        public const string EVT_LOAD_EXTERNAL_IMAGE_ASYNC = "LoadExternalImageAsync";
        public const string EVT_EXTERNAL_IMAGE_LOADED = "OnExternalImageLoaded";
        public const string EVT_EXTERNAL_IMAGE_LOAD_FAILED = "OnExternalImageLoadFailed";

        [Event(EVT_LOAD_EXTERNAL_IMAGE_ASYNC)]
        public List<object> LoadExternalImageAsync(List<object> data)
        {
            string filePath = data[0] as string;
            var key = new ResourceKey(filePath, true, "Sprite");

            var spriteService = SpriteService.Instance;
            if (spriteService.TryGetResource(key, out var cached)) return ResultCode.Ok(cached);
            if (!File.Exists(filePath)) return ResultCode.Fail("文件不存在");

            LoadExternalImageAsyncInternal(filePath, null);
            return ResultCode.Fail("加载中");
        }

        private void LoadExternalImageAsyncInternal(string filePath, Action<UnityEngine.Sprite> callback)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var bytes = File.ReadAllBytes(filePath);
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(bytes))
                        {
                            var sprite = UnityEngine.Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.one * 0.5f);
                            var key = new ResourceKey(filePath, true, "Sprite");
                            SpriteService.Instance.CacheResource(key, sprite);
                            callback?.Invoke(sprite);
                            Log($"外部图片加载成功: {filePath}", Color.green);
                            
                            if (EventProcessor.Instance.HasListener(EVT_EXTERNAL_IMAGE_LOADED))
                                EventProcessor.Instance.TriggerEvent(EVT_EXTERNAL_IMAGE_LOADED,
                                    new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["sprite"] = sprite } });
                        }
                        else
                        {
                            Log($"外部图片加载失败: {filePath}", Color.yellow);
                            callback?.Invoke(null);
                            EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOAD_FAILED,
                                new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["error"] = "图片格式错误" } });
                        }
                    });
                }
                catch (Exception ex)
                {
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        Log($"外部图片加载异常: {filePath} - {ex.Message}", Color.red);
                        callback?.Invoke(null);
                        EventProcessor.Instance.TriggerEventMethod(EVT_EXTERNAL_IMAGE_LOAD_FAILED,
                            new List<object> { new Dictionary<string, object> { ["path"] = filePath, ["error"] = ex.Message } });
                    });
                }
            });
        }
    }
}
