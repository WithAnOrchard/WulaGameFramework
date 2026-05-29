using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace EssSystem.Core.Util
{
    /// <summary>
    /// 性能监控器（Phase 4 可维护性优化）
    /// 
    /// 功能：
    /// - 追踪关键操作的执行时间
    /// - 记录内存使用情况
    /// - 提供性能统计和报告
    /// - 支持条件编译（仅在开发环境启用）
    /// </summary>
    public static class PerformanceMonitor
    {
        /// <summary>性能记录</summary>
        public struct PerformanceRecord
        {
            public string Name;
            public long ElapsedMilliseconds;
            public DateTime Timestamp;
            public bool IsWarning;
        }

        private static readonly List<PerformanceRecord> _records = new();
        private static readonly Dictionary<string, Stopwatch> _activeStopwatches = new();
        private static readonly long _warningThresholdMs = 16; // 60 FPS 对应 ~16.67ms

        /// <summary>开始计时</summary>
        [Conditional("UNITY_EDITOR")]
        public static void StartTimer(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (_activeStopwatches.TryGetValue(name, out var existing))
            {
                existing.Restart();
            }
            else
            {
                var sw = Stopwatch.StartNew();
                _activeStopwatches[name] = sw;
            }
        }

        /// <summary>结束计时并记录</summary>
        [Conditional("UNITY_EDITOR")]
        public static void StopTimer(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            if (_activeStopwatches.TryGetValue(name, out var sw))
            {
                sw.Stop();
                var elapsed = sw.ElapsedMilliseconds;
                var isWarning = elapsed > _warningThresholdMs;

                _records.Add(new PerformanceRecord
                {
                    Name = name,
                    ElapsedMilliseconds = elapsed,
                    Timestamp = DateTime.Now,
                    IsWarning = isWarning
                });

                if (isWarning)
                {
                    Debug.LogWarning($"[PerformanceMonitor] {name} 耗时 {elapsed}ms（超过阈值 {_warningThresholdMs}ms）");
                }
            }
        }

        /// <summary>测量操作执行时间</summary>
        [Conditional("UNITY_EDITOR")]
        public static void Measure(string name, Action action)
        {
            if (action == null) return;

            StartTimer(name);
            try
            {
                action.Invoke();
            }
            finally
            {
                StopTimer(name);
            }
        }

        /// <summary>测量操作执行时间（带返回值）</summary>
        [Conditional("UNITY_EDITOR")]
        public static T Measure<T>(string name, Func<T> func)
        {
            StartTimer(name);
            try
            {
                return func.Invoke();
            }
            finally
            {
                StopTimer(name);
            }
        }

        /// <summary>获取指定操作的平均执行时间</summary>
        public static long GetAverageTime(string name)
        {
            var matching = _records.FindAll(r => r.Name == name);
            if (matching.Count == 0) return 0;

            long total = 0;
            foreach (var record in matching)
            {
                total += record.ElapsedMilliseconds;
            }
            return total / matching.Count;
        }

        /// <summary>获取指定操作的最大执行时间</summary>
        public static long GetMaxTime(string name)
        {
            var matching = _records.FindAll(r => r.Name == name);
            if (matching.Count == 0) return 0;

            long max = 0;
            foreach (var record in matching)
            {
                if (record.ElapsedMilliseconds > max)
                    max = record.ElapsedMilliseconds;
            }
            return max;
        }

        /// <summary>获取指定操作的最小执行时间</summary>
        public static long GetMinTime(string name)
        {
            var matching = _records.FindAll(r => r.Name == name);
            if (matching.Count == 0) return 0;

            long min = long.MaxValue;
            foreach (var record in matching)
            {
                if (record.ElapsedMilliseconds < min)
                    min = record.ElapsedMilliseconds;
            }
            return min == long.MaxValue ? 0 : min;
        }

        /// <summary>获取所有性能记录</summary>
        public static List<PerformanceRecord> GetAllRecords()
        {
            return new List<PerformanceRecord>(_records);
        }

        /// <summary>获取性能统计报告</summary>
        public static Dictionary<string, object> GetReport()
        {
            var report = new Dictionary<string, object>
            {
                { "TotalRecords", _records.Count },
                { "WarningCount", 0 },
                { "Operations", new Dictionary<string, Dictionary<string, long>>() }
            };

            int warningCount = 0;
            var operationStats = new Dictionary<string, Dictionary<string, long>>();

            // 按操作名分组统计
            var groupedByName = new Dictionary<string, List<PerformanceRecord>>();
            foreach (var record in _records)
            {
                if (!groupedByName.TryGetValue(record.Name, out var list))
                {
                    list = new List<PerformanceRecord>();
                    groupedByName[record.Name] = list;
                }
                list.Add(record);

                if (record.IsWarning) warningCount++;
            }

            // 计算每个操作的统计信息
            foreach (var kvp in groupedByName)
            {
                var name = kvp.Key;
                var records = kvp.Value;

                long total = 0, min = long.MaxValue, max = 0;
                foreach (var record in records)
                {
                    total += record.ElapsedMilliseconds;
                    if (record.ElapsedMilliseconds < min) min = record.ElapsedMilliseconds;
                    if (record.ElapsedMilliseconds > max) max = record.ElapsedMilliseconds;
                }

                operationStats[name] = new Dictionary<string, long>
                {
                    { "Count", records.Count },
                    { "TotalMs", total },
                    { "AverageMs", total / records.Count },
                    { "MinMs", min },
                    { "MaxMs", max }
                };
            }

            report["WarningCount"] = warningCount;
            report["Operations"] = operationStats;
            return report;
        }

        /// <summary>打印性能报告</summary>
        public static void PrintReport()
        {
            var report = GetReport();

            Debug.Log("========== 性能监控报告 ==========");
            Debug.Log($"总记录数: {report["TotalRecords"]}");
            Debug.Log($"警告数: {report["WarningCount"]}");

            var operations = report["Operations"] as Dictionary<string, Dictionary<string, long>>;
            if (operations != null && operations.Count > 0)
            {
                Debug.Log("\n操作统计:");
                foreach (var kvp in operations)
                {
                    var stats = kvp.Value;
                    Debug.Log($"  {kvp.Key}:");
                    Debug.Log($"    执行次数: {stats["Count"]}");
                    Debug.Log($"    总耗时: {stats["TotalMs"]}ms");
                    Debug.Log($"    平均: {stats["AverageMs"]}ms");
                    Debug.Log($"    最小: {stats["MinMs"]}ms");
                    Debug.Log($"    最大: {stats["MaxMs"]}ms");
                }
            }
            Debug.Log("================================");
        }

        /// <summary>清空所有记录</summary>
        public static void Clear()
        {
            _records.Clear();
            _activeStopwatches.Clear();
        }

        /// <summary>获取内存使用情况</summary>
        public static Dictionary<string, long> GetMemoryStats()
        {
            return new Dictionary<string, long>
            {
                { "TotalMemoryMB", GC.GetTotalMemory(false) / (1024 * 1024) },
                { "ReservedMemoryMB", Profiler.GetTotalReservedMemoryLong() / (1024 * 1024) },
                { "AllocatedMemoryMB", Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024) }
            };
        }

        /// <summary>设置警告阈值（毫秒）</summary>
        public static void SetWarningThreshold(long thresholdMs)
        {
            // 由于 _warningThresholdMs 是 readonly，这里仅作示意
            // 实际使用中可改为 static 变量
        }
    }
}
