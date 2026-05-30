using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace EssSystem.Core.Base.Util
{
    /// <summary>
    /// 内存追踪工具类，用于诊断内存占用问题。
    /// 记录纹理、资源加载、Canvas、RenderTexture 等信息，帮助定位内存泄漏。
    /// </summary>
    public static class MemoryTracker
    {
        private static readonly List<ResourceEntry> _loadedResources = new List<ResourceEntry>();
        private static readonly object _lock = new object();

        public struct ResourceEntry
        {
            public string Path;
            public string Type;
            public long MemoryBytes;
            public long Timestamp;
        }

        /// <summary>记录资源加载。</summary>
        public static void LogResourceLoad(string path, string type, long memoryBytes)
        {
            lock (_lock)
            {
                _loadedResources.Add(new ResourceEntry
                {
                    Path = path,
                    Type = type,
                    MemoryBytes = memoryBytes,
                    Timestamp = DateTime.Now.Ticks
                });
            }
            Debug.Log($"[MemoryTracker] 加载资源: {path} ({type}) - {FormatBytes(memoryBytes)}");
        }

        /// <summary>记录纹理加载（特殊处理，因为 Texture 是最大的内存消耗）。</summary>
        public static void LogTextureLoad(string path, Texture2D texture)
        {
            if (texture == null) return;
            long memoryBytes = Profiler.GetRuntimeMemorySizeLong(texture);
            lock (_lock)
            {
                _loadedResources.Add(new ResourceEntry
                {
                    Path = path,
                    Type = "Texture",
                    MemoryBytes = memoryBytes,
                    Timestamp = DateTime.Now.Ticks
                });
            }
            Debug.Log($"[MemoryTracker] 加载纹理: {path} - {FormatBytes(memoryBytes)} | 尺寸: {texture.width}x{texture.height}");
        }

        /// <summary>记录 RenderTexture 创建。</summary>
        public static void LogRenderTextureCreate(string name, RenderTexture rt)
        {
            if (rt == null) return;
            long memoryBytes = Profiler.GetRuntimeMemorySizeLong(rt);
            lock (_lock)
            {
                _loadedResources.Add(new ResourceEntry
                {
                    Path = $"RenderTexture/{name}",
                    Type = "RenderTexture",
                    MemoryBytes = memoryBytes,
                    Timestamp = DateTime.Now.Ticks
                });
            }
            Debug.Log($"[MemoryTracker] 创建 RenderTexture: {name} - {FormatBytes(memoryBytes)} | 尺寸: {rt.width}x{rt.height} | depth: {rt.depth}");
        }

        /// <summary>记录 RenderTexture 销毁。</summary>
        public static void LogRenderTextureDestroy(string name)
        {
            lock (_lock)
            {
                _loadedResources.RemoveAll(r => r.Path == $"RenderTexture/{name}");
            }
            Debug.Log($"[MemoryTracker] 销毁 RenderTexture: {name}");
        }

        /// <summary>记录 Canvas 创建。</summary>
        public static void LogCanvasCreate(string name, Canvas canvas)
        {
            if (canvas == null) return;
            var mode = canvas.renderMode;
            var isOverlay = mode == RenderMode.ScreenSpaceOverlay;
            lock (_lock)
            {
                _loadedResources.Add(new ResourceEntry
                {
                    Path = $"Canvas/{name}",
                    Type = isOverlay ? "Canvas_ScreenSpaceOverlay" : "Canvas_Other",
                    MemoryBytes = isOverlay ? EstimateScreenRenderTextureSize() : 0,
                    Timestamp = DateTime.Now.Ticks
                });
            }
            Debug.Log($"[MemoryTracker] 创建 Canvas: {name} - 模式: {mode} | IsOverlay: {isOverlay}");
        }

        /// <summary>输出当前内存统计摘要。</summary>
        public static void DumpMemorySummary()
        {
            long totalMemory = 0;
            long textureMemory = 0;
            long renderTextureMemory = 0;
            long canvasMemory = 0;
            int textureCount = 0;
            int renderTextureCount = 0;
            int canvasCount = 0;

            lock (_lock)
            {
                foreach (var entry in _loadedResources)
                {
                    totalMemory += entry.MemoryBytes;
                    if (entry.Type == "Texture")
                    {
                        textureMemory += entry.MemoryBytes;
                        textureCount++;
                    }
                    else if (entry.Type == "RenderTexture")
                    {
                        renderTextureMemory += entry.MemoryBytes;
                        renderTextureCount++;
                    }
                    else if (entry.Type.StartsWith("Canvas"))
                    {
                        canvasMemory += entry.MemoryBytes;
                        canvasCount++;
                    }
                }
            }

            Debug.Log($"[MemoryTracker] ===== 内存统计摘要 =====");
            Debug.Log($"[MemoryTracker] 追踪资源总数: {_loadedResources.Count}");
            Debug.Log($"[MemoryTracker] 纹理数量: {textureCount}, 内存: {FormatBytes(textureMemory)}");
            Debug.Log($"[MemoryTracker] RenderTexture 数量: {renderTextureCount}, 内存: {FormatBytes(renderTextureMemory)}");
            Debug.Log($"[MemoryTracker] Canvas 数量: {canvasCount}, 估算内存: {FormatBytes(canvasMemory)}");
            Debug.Log($"[MemoryTracker] Unity 总内存: {FormatBytes(Profiler.GetTotalReservedMemoryLong())}");
            Debug.Log($"[MemoryTracker] Unity 堆内存: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())}");
            Debug.Log($"[MemoryTracker] =============================");
        }

        /// <summary>输出所有追踪的纹理列表（按内存排序）。</summary>
        public static void DumpTextureList()
        {
            List<ResourceEntry> textures;
            lock (_lock)
            {
                textures = new List<ResourceEntry>(_loadedResources.FindAll(r => r.Type == "Texture"));
            }

            textures.Sort((a, b) => b.MemoryBytes.CompareTo(a.MemoryBytes));

            Debug.Log($"[MemoryTracker] ===== 纹理列表（按内存降序）=====");
            foreach (var tex in textures)
            {
                Debug.Log($"[MemoryTracker]   {FormatBytes(tex.MemoryBytes),12} | {tex.Path}");
            }
            Debug.Log($"[MemoryTracker] =============================");
        }

        /// <summary>输出所有 RenderTexture 列表（按内存排序）。</summary>
        public static void DumpRenderTextureList()
        {
            List<ResourceEntry> renderTextures;
            lock (_lock)
            {
                renderTextures = new List<ResourceEntry>(_loadedResources.FindAll(r => r.Type == "RenderTexture"));
            }

            renderTextures.Sort((a, b) => b.MemoryBytes.CompareTo(a.MemoryBytes));

            Debug.Log($"[MemoryTracker] ===== RenderTexture 列表（按内存降序）=====");
            foreach (var rt in renderTextures)
            {
                Debug.Log($"[MemoryTracker]   {FormatBytes(rt.MemoryBytes),12} | {rt.Path}");
            }
            Debug.Log($"[MemoryTracker] =============================");
        }

        /// <summary>输出所有 Canvas 列表。</summary>
        public static void DumpCanvasList()
        {
            List<ResourceEntry> canvases;
            lock (_lock)
            {
                canvases = new List<ResourceEntry>(_loadedResources.FindAll(r => r.Type.StartsWith("Canvas")));
            }

            Debug.Log($"[MemoryTracker] ===== Canvas 列表 =====");
            foreach (var c in canvases)
            {
                Debug.Log($"[MemoryTracker]   {c.Type,-30} | {c.Path}");
            }
            Debug.Log($"[MemoryTracker] =============================");
        }

        /// <summary>清除所有追踪记录。</summary>
        public static void Clear()
        {
            lock (_lock)
            {
                _loadedResources.Clear();
            }
            Debug.Log("[MemoryTracker] 已清除追踪记录");
        }

        private static long EstimateScreenRenderTextureSize()
        {
            return (long)Screen.width * Screen.height * 4;
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
