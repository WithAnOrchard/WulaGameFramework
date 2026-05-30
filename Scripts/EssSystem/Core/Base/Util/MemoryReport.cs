using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace EssSystem.Core.Base.Util
{
    public static class MemoryReport
    {
        public static void GenerateReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("========== 内存分析报告 ==========");
            sb.AppendLine($"时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            sb.AppendLine("--- Unity 内存统计 ---");
            sb.AppendLine($"总保留内存: {FormatBytes(Profiler.GetTotalReservedMemoryLong())}");
            sb.AppendLine($"总已分配: {FormatBytes(Profiler.GetTotalAllocatedMemoryLong())}");
            sb.AppendLine($"总空闲内存: {FormatBytes(Profiler.GetTotalUnusedReservedMemoryLong())}");
            sb.AppendLine($"托管堆大小: {FormatBytes(Profiler.GetMonoHeapSizeLong())}");
            sb.AppendLine($"托管已使用: {FormatBytes(Profiler.GetMonoUsedSizeLong())}");
            sb.AppendLine();

            sb.AppendLine("--- Texture2D 列表 ---");
            var textures = Resources.FindObjectsOfTypeAll<Texture2D>();
            long totalTexMemory = 0;
            foreach (var tex in textures)
            {
                if (tex == null) continue;
                long mem = Profiler.GetRuntimeMemorySizeLong(tex);
                totalTexMemory += mem;
                sb.AppendLine($"  {tex.name} ({tex.width}x{tex.height}) - {FormatBytes(mem)}");
            }
            sb.AppendLine($"Texture2D 总计: {FormatBytes(totalTexMemory)}");
            sb.AppendLine();

            sb.AppendLine("--- RenderTexture 列表 ---");
            var rts = Resources.FindObjectsOfTypeAll<RenderTexture>();
            long totalRTMemory = 0;
            foreach (var rt in rts)
            {
                if (rt == null) continue;
                long mem = Profiler.GetRuntimeMemorySizeLong(rt);
                totalRTMemory += mem;
                sb.AppendLine($"  {rt.name} ({rt.width}x{rt.height}) - {FormatBytes(mem)}");
            }
            sb.AppendLine($"RenderTexture 总计: {FormatBytes(totalRTMemory)}");
            sb.AppendLine();

            sb.AppendLine("--- Sprite 列表 ---");
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            long totalSpriteMemory = 0;
            foreach (var sp in sprites)
            {
                if (sp == null) continue;
                long mem = Profiler.GetRuntimeMemorySizeLong(sp);
                totalSpriteMemory += mem;
                var texName = sp.texture != null ? sp.texture.name : "null";
                sb.AppendLine($"  {sp.name} (tex: {texName}) - {FormatBytes(mem)}");
            }
            sb.AppendLine($"Sprite 总计: {FormatBytes(totalSpriteMemory)}");

            string report = sb.ToString();
            Debug.Log(report);

            string path = Path.Combine(UnityEngine.Application.persistentDataPath, $"MemoryReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, report);
            Debug.Log($"[MemoryReport] 报告已保存到: {path}");
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
            return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
        }
    }
}
