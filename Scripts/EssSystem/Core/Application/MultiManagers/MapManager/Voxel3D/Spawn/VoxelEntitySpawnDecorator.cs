using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Common.Util;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Spawn.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Spawn
{
    /// <summary>
    /// 评估所有绑定到 <c>map.ConfigId</c> 的 <see cref="VoxelEntitySpawnRuleSet"/>，把命中位置入队
    /// 到 <see cref="VoxelEntitySpawnService"/> 的 spawn 队列（不直接创建 GameObject，分帧消费避免 spike）。
    /// <para>**确定性约束**（与 2D <c>EntitySpawnDecorator</c> 同构）：</para>
    /// <list type="number">
    /// <item>规则按 (Priority asc, RuleId asc) 排序</item>
    /// <item>区块内格子按行主序遍历 (lz, lx)</item>
    /// <item>所有 RNG 派生自 <c>ChunkSeed.Rng(mapId, cx, cz, rule.TileRngTag)</c>（与 2D 共享 ChunkSeed）</item>
    /// <item>cluster 候选按 (dz asc, dx asc) 字典序排序后用 RNG 抽取</item>
    /// </list>
    /// <para>**spawn 落地点**：<c>(wx + 0.5, height + 1, wz + 0.5)</c> —— 站在 column 顶面之上一格。</para>
    /// </summary>
    public class VoxelEntitySpawnDecorator : IVoxelChunkDecorator
    {
        public string Id => "VoxelEntitySpawn";

        /// <summary>装饰器 Priority（越小越先）。300 介于地形装饰(100~200)与结构生成(400+)之间。</summary>
        public int Priority { get; set; } = 300;

        /// <summary>本次装饰允许 spawn 的全局上限（跨规则；防止单 chunk 积累过多）。</summary>
        public int GlobalMaxPerChunk { get; set; } = 32;

        public void Decorate(VoxelMap map, VoxelChunk chunk)
        {
            if (map == null || chunk == null) return;
            var service = VoxelEntitySpawnService.Instance;
            var sets = service.GetRuleSets(map.ConfigId);
            if (sets == null || sets.Count == 0) return;

            // 收集 + 排序所有规则（跨多个 RuleSet）
            var rules = new List<VoxelEntitySpawnRule>();
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

            var occupiedAcrossRules = new HashSet<int>();   // 跨规则共享，避免不同规则在同一格上叠加
            var spawnedTotal = 0;
            var size = chunk.Size;

            for (var ri = 0; ri < rules.Count; ri++)
            {
                if (spawnedTotal >= GlobalMaxPerChunk) break;
                var rule = rules[ri];
                if (string.IsNullOrEmpty(rule.RuleId) || string.IsNullOrEmpty(rule.EntityConfigId)) continue;
                if (rule.MaxPerChunk <= 0) continue;

                var rng = ChunkSeed.Rng(map.MapId, chunk.ChunkX, chunk.ChunkZ, rule.TileRngTag ?? "spawn");
                var ruleHits = 0;
                var occupiedThisRule = new HashSet<int>();   // 用于 MinSpacing

                for (var lz = 0; lz < size; lz++)
                {
                    if (ruleHits >= rule.MaxPerChunk) break;
                    if (spawnedTotal >= GlobalMaxPerChunk) break;
                    for (var lx = 0; lx < size; lx++)
                    {
                        if (ruleHits >= rule.MaxPerChunk) break;
                        if (spawnedTotal >= GlobalMaxPerChunk) break;

                        // 密度抽取先消耗 RNG，无论后续是否过滤通过 → RNG 序列稳定
                        var roll = rng.NextDouble();
                        if (roll >= rule.DensityPerTile) continue;
                        if (!PassesFilters(rule, chunk, lx, lz)) continue;
                        if (!CheckSpacing(rule.MinSpacing, lx, lz, occupiedThisRule, size)) continue;

                        var seedIdx = lz * size + lx;
                        if (occupiedAcrossRules.Contains(seedIdx)) continue;

                        var seedId = VoxelEntitySpawnService.ComposeInstanceId(
                            map.MapId, chunk.ChunkX, chunk.ChunkZ, rule.RuleId, lx, lz);

                        if (!service.IsDestroyedInChunk(map.MapId, chunk.ChunkX, chunk.ChunkZ, seedId))
                        {
                            EnqueueAt(service, map, chunk, rule, lx, lz, seedId);
                            occupiedAcrossRules.Add(seedIdx);
                            occupiedThisRule.Add(seedIdx);
                            ruleHits++;
                            spawnedTotal++;
                        }

                        // ─── Cluster ───
                        if (rule.ClusterMax > 1 && rule.ClusterRadius > 0)
                        {
                            var clusterCount = rng.Next(
                                Mathf.Max(1, rule.ClusterMin),
                                Mathf.Max(1, rule.ClusterMax) + 1);
                            var remaining = clusterCount - 1;   // 含主体
                            if (remaining <= 0) continue;

                            var candidates = BuildClusterCandidates(lx, lz, rule.ClusterRadius, size);
                            ShuffleDeterministic(candidates, rng);

                            for (var ci = 0; ci < candidates.Count && remaining > 0
                                                                  && ruleHits < rule.MaxPerChunk
                                                                  && spawnedTotal < GlobalMaxPerChunk; ci++)
                            {
                                var (cLx, cLz) = candidates[ci];
                                if (!PassesFilters(rule, chunk, cLx, cLz)) continue;
                                if (!CheckSpacing(rule.MinSpacing, cLx, cLz, occupiedThisRule, size)) continue;
                                var cIdx = cLz * size + cLx;
                                if (occupiedAcrossRules.Contains(cIdx)) continue;

                                var clusterId = VoxelEntitySpawnService.ComposeClusterInstanceId(
                                    map.MapId, chunk.ChunkX, chunk.ChunkZ, rule.RuleId, lx, lz, cLx, cLz);
                                if (service.IsDestroyedInChunk(map.MapId, chunk.ChunkX, chunk.ChunkZ, clusterId))
                                {
                                    remaining--;
                                    continue;   // 仍消耗 cluster 名额，避免 remaining 漂移
                                }

                                EnqueueAt(service, map, chunk, rule, cLx, cLz, clusterId);
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

        // ─────────────────────────────────────────────────────────────
        private static bool PassesFilters(VoxelEntitySpawnRule rule, VoxelChunk chunk, int lx, int lz)
        {
            var idx = chunk.Index(lx, lz);
            var top = chunk.TopBlocks[idx];
            var side = chunk.SideBlocks[idx];
            var height = chunk.Heights[idx];

            // TopBlock 限定（最常用）
            if (rule.TopBlockIds != null && rule.TopBlockIds.Length > 0)
            {
                var matched = false;
                for (var i = 0; i < rule.TopBlockIds.Length; i++)
                {
                    if (rule.TopBlockIds[i] == top) { matched = true; break; }
                }
                if (!matched) return false;
            }

            // SideBlock 限定（少用）
            if (rule.SideBlockIds != null && rule.SideBlockIds.Length > 0)
            {
                var matched = false;
                for (var i = 0; i < rule.SideBlockIds.Length; i++)
                {
                    if (rule.SideBlockIds[i] == side) { matched = true; break; }
                }
                if (!matched) return false;
            }

            // 高度过滤
            if (!rule.HeightRange.Contains(height)) return false;

            return true;
        }

        private static bool CheckSpacing(int minSpacing, int lx, int lz, HashSet<int> occupied, int chunkSize)
        {
            if (minSpacing <= 0 || occupied.Count == 0) return true;
            foreach (var idx in occupied)
            {
                var oz = idx / chunkSize;
                var ox = idx - oz * chunkSize;
                var d = Mathf.Abs(ox - lx) + Mathf.Abs(oz - lz);
                if (d < minSpacing) return false;
            }
            return true;
        }

        private static void EnqueueAt(VoxelEntitySpawnService service, VoxelMap map, VoxelChunk chunk,
                                      VoxelEntitySpawnRule rule, int lx, int lz, string instanceId)
        {
            var idx = chunk.Index(lx, lz);
            var height = chunk.Heights[idx];
            var wx = chunk.WorldMinX + lx;
            var wz = chunk.WorldMinZ + lz;
            // column top is at y=height; entity stands one block above
            var pos = new Vector3(wx + 0.5f, height + 1f, wz + 0.5f);
            service.EnqueueSpawn(map.MapId, chunk.ChunkX, chunk.ChunkZ,
                                 rule.EntityConfigId, instanceId, pos, parent: null);
        }

        private static List<(int lx, int lz)> BuildClusterCandidates(int seedLx, int seedLz, int radius, int chunkSize)
        {
            var list = new List<(int, int)>(radius * (radius + 1) * 2);
            for (var dz = -radius; dz <= radius; dz++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    if (Mathf.Abs(dx) + Mathf.Abs(dz) > radius) continue;
                    var lx = seedLx + dx;
                    var lz = seedLz + dz;
                    if (lx < 0 || lz < 0 || lx >= chunkSize || lz >= chunkSize) continue;
                    list.Add((lx, lz));
                }
            }
            return list; // BuildClusterCandidates: candidates already in (dz, dx) lexicographic order
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
