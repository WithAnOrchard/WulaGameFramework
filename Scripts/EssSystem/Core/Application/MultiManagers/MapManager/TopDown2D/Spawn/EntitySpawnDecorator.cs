using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.Common.Util;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn
{
    /// <summary>
    /// 评估所有绑定到 <c>map.ConfigId</c> 的 <see cref="EntitySpawnRuleSet"/>，把命中的位置
    /// 入队到 <see cref="EntitySpawnService"/> 的 spawn 队列（不直接创建 GameObject，避免单帧尖刺）。
    /// <para>
    /// **确定性约束**（详见 Agent.md "确定性"章节）：
    /// <list type="number">
    /// <item>规则按 (Priority asc, RuleId asc) 排序</item>
    /// <item>区块内 tile 按行主序遍历 (ly, lx)</item>
    /// <item>所有 RNG 派生自 <c>ChunkSeed.Rng(mapId, cx, cy, rule.TileRngTag)</c></item>
    /// <item>cluster 候选按 (dy asc, dx asc) 字典序排序后用 RNG 抽取</item>
    /// </list>
    /// </para>
    /// <para>
    /// **持久化交互**：每个候选位置先查 <see cref="EntitySpawnService.IsDestroyedInChunk"/>，命中即跳过。
    /// 因此玩家砍掉的 spawn 实体重新进入区块时不会复活。
    /// </para>
    /// </summary>
    public class EntitySpawnDecorator : IChunkDecorator
    {
        public string Id => "EntitySpawn";

        /// <summary>装饰器 Priority（越小越先）。300 介于地形装饰(100~200)与结构生成(400+)之间。</summary>
        public int Priority { get; set; } = 300;

        /// <summary>本次装饰允许 spawn 的全局上限（跨规则；防止单 chunk 内积累过多）。</summary>
        public int GlobalMaxPerChunk { get; set; } = 32;

        /// <summary>未实现 <see cref="IMapMetaProvider"/> 的生成器是否记录一次警告（避免反复刷）。</summary>
        private static readonly HashSet<string> _warnedConfigIds = new();

        public void Decorate(Map map, Chunk chunk)
        {
            if (map == null || chunk == null) return;
            var service = EntitySpawnService.Instance;
            var sets = service.GetRuleSets(map.ConfigId);
            if (sets == null || sets.Count == 0) return;

            // 收集 + 排序所有规则（跨多个 RuleSet）
            var rules = new List<EntitySpawnRule>();
            foreach (var set in sets)
            {
                if (set?.Rules == null) continue;
                rules.AddRange(set.Rules);
            }
            if (rules.Count == 0) return;
            rules.Sort(static (a, b) =>
            {
                var c = a.Priority.CompareTo(b.Priority);
                if (c != 0) return c;
                return string.CompareOrdinal(a.RuleId, b.RuleId);
            });

            var meta = map.Generator as IMapMetaProvider;

            var occupiedAcrossRules = new HashSet<int>();   // 跨规则共享，避免不同规则在同一格上叠加
            var spawnedTotal = 0;

            for (var ri = 0; ri < rules.Count; ri++)
            {
                if (spawnedTotal >= GlobalMaxPerChunk) break;
                var rule = rules[ri];
                if (string.IsNullOrEmpty(rule.RuleId) || string.IsNullOrEmpty(rule.EntityConfigId)) continue;
                if (rule.MaxPerChunk <= 0) continue;

                if (RuleRequiresMeta(rule) && meta == null)
                {
                    if (_warnedConfigIds.Add(map.ConfigId))
                        Debug.LogWarning($"[EntitySpawnDecorator] Rule '{rule.RuleId}' on '{map.ConfigId}' " +
                                         $"requires biome/elevation/temp/moisture meta but generator does not implement IMapMetaProvider. Rule skipped.");
                    continue;
                }

                var rng = ChunkSeed.Rng(map.MapId, chunk.ChunkX, chunk.ChunkY, rule.TileRngTag ?? "spawn");
                var ruleHits = 0;
                var occupiedThisRule = new HashSet<int>();   // 用于 MinSpacing

                for (var ly = 0; ly < chunk.Size; ly++)
                {
                    if (ruleHits >= rule.MaxPerChunk) break;
                    if (spawnedTotal >= GlobalMaxPerChunk) break;
                    for (var lx = 0; lx < chunk.Size; lx++)
                    {
                        if (ruleHits >= rule.MaxPerChunk) break;
                        if (spawnedTotal >= GlobalMaxPerChunk) break;

                        // 密度掷骰必须**先消耗 RNG**，无论后续是否过滤通过 → 保证 RNG 序列稳定
                        var roll = rng.NextDouble();
                        if (roll >= rule.DensityPerTile) continue;
                        if (!PassesFilters(rule, chunk, lx, ly, meta)) continue;
                        if (!CheckSpacing(rule.MinSpacing, lx, ly, occupiedThisRule, chunk.Size)) continue;

                        var seedIdx = ly * chunk.Size + lx;
                        if (occupiedAcrossRules.Contains(seedIdx)) continue;

                        var seedId = EntitySpawnService.ComposeInstanceId(
                            map.MapId, chunk.ChunkX, chunk.ChunkY, rule.RuleId, lx, ly);

                        if (!service.IsDestroyedInChunk(map.MapId, chunk.ChunkX, chunk.ChunkY, seedId))
                        {
                            EnqueueAt(service, map, chunk, rule, lx, ly, seedId);
                            occupiedAcrossRules.Add(seedIdx);
                            occupiedThisRule.Add(seedIdx);
                            ruleHits++;
                            spawnedTotal++;
                        }

                        // —— Cluster ——
                        if (rule.ClusterMax > 1 && rule.ClusterRadius > 0)
                        {
                            var clusterCount = rng.Next(
                                Mathf.Max(1, rule.ClusterMin),
                                Mathf.Max(1, rule.ClusterMax) + 1);  // [min, max]
                            // clusterCount 含主体；剩余 (clusterCount - 1) 个邻居要采样
                            var remaining = clusterCount - 1;
                            if (remaining <= 0) continue;

                            // 候选集合：曼哈顿距离 ≤ ClusterRadius 的邻居（不含原点），按 (dy, dx) 排序保稳定
                            var candidates = BuildClusterCandidates(lx, ly, rule.ClusterRadius, chunk.Size);
                            ShuffleDeterministic(candidates, rng);

                            for (var ci = 0; ci < candidates.Count && remaining > 0
                                                                  && ruleHits < rule.MaxPerChunk
                                                                  && spawnedTotal < GlobalMaxPerChunk; ci++)
                            {
                                var (cLx, cLy) = candidates[ci];
                                if (!PassesFilters(rule, chunk, cLx, cLy, meta)) continue;
                                if (!CheckSpacing(rule.MinSpacing, cLx, cLy, occupiedThisRule, chunk.Size)) continue;
                                var cIdx = cLy * chunk.Size + cLx;
                                if (occupiedAcrossRules.Contains(cIdx)) continue;

                                var clusterId = EntitySpawnService.ComposeClusterInstanceId(
                                    map.MapId, chunk.ChunkX, chunk.ChunkY, rule.RuleId, lx, ly, cLx, cLy);
                                if (service.IsDestroyedInChunk(map.MapId, chunk.ChunkX, chunk.ChunkY, clusterId))
                                {
                                    remaining--;
                                    continue; // 仍消耗 cluster 名额，避免重新计算 remaining 漂移
                                }

                                EnqueueAt(service, map, chunk, rule, cLx, cLy, clusterId);
                                occupiedAcrossRules.Add(cIdx);
                                occupiedThisRule.Add(cIdx);
                                ruleHits++;
                                spawnedTotal++;
                                remaining--;
                            }
                        }
                    }
                }
            }
        }

        // ────────────────────────────────────────────────────────────────
        /// <summary>仅 BiomeIds 过滤（pre-river 群系）需要 meta；其余字段从 Tile 直接读。</summary>
        private static bool RuleRequiresMeta(EntitySpawnRule rule) =>
            rule.BiomeIds != null && rule.BiomeIds.Length > 0;

        private static bool PassesFilters(EntitySpawnRule rule, Chunk chunk, int lx, int ly, IMapMetaProvider meta)
        {
            var tile = chunk.GetTile(lx, ly);

            // TileTypeId 限定（post-river，最常用 —— 排除水/河等不该 spawn 的格子）
            if (rule.TileTypeIds != null && rule.TileTypeIds.Length > 0)
            {
                var typeId = tile?.TypeId;
                var matched = false;
                for (var i = 0; i < rule.TileTypeIds.Length; i++)
                {
                    if (rule.TileTypeIds[i] == typeId) { matched = true; break; }
                }
                if (!matched) return false;
            }

            // Elevation / Temperature / Moisture：Tile 上已有 byte 缓存，无需 resample
            if (tile != null)
            {
                if (!rule.ElevationRange.Contains(tile.ElevationNormalized)) return false;
                if (!rule.TemperatureRange.Contains(tile.TemperatureNormalized)) return false;
                if (!rule.MoistureRange.Contains(tile.MoistureNormalized)) return false;
            }
            else
            {
                if (rule.ElevationRange.HasValue || rule.TemperatureRange.HasValue || rule.MoistureRange.HasValue)
                    return false;
            }

            // BiomeIds（pre-river 群系，需 meta provider）
            if (rule.BiomeIds != null && rule.BiomeIds.Length > 0)
            {
                if (meta == null) return false;
                var wx = chunk.WorldOriginX + lx;
                var wy = chunk.WorldOriginY + ly;
                if (!meta.TryGetTileMeta(wx, wy, out var m)) return false;
                var matched = false;
                for (var i = 0; i < rule.BiomeIds.Length; i++)
                {
                    if (rule.BiomeIds[i] == m.BiomeId) { matched = true; break; }
                }
                if (!matched) return false;
            }
            return true;
        }

        private static bool CheckSpacing(int minSpacing, int lx, int ly, HashSet<int> occupied, int chunkSize)
        {
            if (minSpacing <= 0 || occupied.Count == 0) return true;
            // 精确曼哈顿距离比较（小数据集）
            foreach (var idx in occupied)
            {
                var oy = idx / chunkSize;
                var ox = idx - oy * chunkSize;
                var d = Mathf.Abs(ox - lx) + Mathf.Abs(oy - ly);
                if (d < minSpacing) return false;
            }
            return true;
        }

        private static void EnqueueAt(EntitySpawnService service, Map map, Chunk chunk,
                                      EntitySpawnRule rule, int lx, int ly, string instanceId)
        {
            var wx = chunk.WorldOriginX + lx;
            var wy = chunk.WorldOriginY + ly;
            // 单元格中心
            var pos = new Vector3(wx + 0.5f, wy + 0.5f, 0f);
            service.EnqueueSpawn(map.MapId, chunk.ChunkX, chunk.ChunkY,
                                 rule.EntityConfigId, instanceId, pos, parent: null);
        }

        private static List<(int lx, int ly)> BuildClusterCandidates(int seedLx, int seedLy, int radius, int chunkSize)
        {
            var list = new List<(int, int)>(radius * (radius + 1) * 2);
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    if (Mathf.Abs(dx) + Mathf.Abs(dy) > radius) continue;
                    var lx = seedLx + dx;
                    var ly = seedLy + dy;
                    if (lx < 0 || ly < 0 || lx >= chunkSize || ly >= chunkSize) continue;
                    list.Add((lx, ly));
                }
            }
            // 候选已按 (dy, dx) 字典序生成，无需再排序
            return list;
        }

        private static void ShuffleDeterministic<T>(IList<T> list, System.Random rng)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
