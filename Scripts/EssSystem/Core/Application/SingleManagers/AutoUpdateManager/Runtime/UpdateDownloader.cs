using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace EssSystem.Core.Application.SingleManagers.AutoUpdateManager.Runtime
{
    /// <summary>
    /// 轻量 HTTP 下载 —— 基于 <see cref="UnityWebRequest"/>。
    /// 跨平台（Standalone / Mobile / Editor 都跑同一份），无第三方依赖。
    /// </summary>
    public static class UpdateDownloader
    {
        private const int TIMEOUT_SECONDS = 30;

        /// <summary>下载文本（manifest 这类小文件）</summary>
        public static Task<string> DownloadStringAsync(string url)
        {
            var tcs = new TaskCompletionSource<string>();
            var req = UnityWebRequest.Get(url);
            req.timeout = TIMEOUT_SECONDS;

            var op = req.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (req.result == UnityWebRequest.Result.Success)
                        tcs.SetResult(req.downloadHandler.text);
                    else
                        tcs.SetException(new Exception($"HTTP {req.responseCode} {req.error} ({url})"));
                }
                catch (Exception e) { tcs.SetException(e); }
                finally { req.Dispose(); }
            };
            return tcs.Task;
        }

        /// <summary>
        /// 下载大文件到本地（边下边写到磁盘，峰值内存≈buffer 大小而非整个包）。
        /// <paramref name="onProgress"/> 每帧调一次，参数为 0..1。
        /// </summary>
        public static async Task DownloadFileAsync(string url, string savePath, Action<float> onProgress)
        {
            var req = UnityWebRequest.Get(url);
            req.timeout = 0;   // 大文件不限超时（业务可另传超时参数）
            var fileHandler = new DownloadHandlerFile(savePath) { removeFileOnAbort = true };
            req.downloadHandler = fileHandler;

            var op = req.SendWebRequest();
            // 进度循环（避免 SendWebRequest 单次 isDone 失去中间帧）
            while (!op.isDone)
            {
                onProgress?.Invoke(req.downloadProgress);
                await Task.Yield();
            }
            onProgress?.Invoke(1f);

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception($"HTTP {req.responseCode} {req.error} ({url})");

            req.Dispose();
        }
    }
}
