using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Common.Util;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn
{
    /// <summary>
    /// 璇勪及鎵€鏈夌粦瀹氬埌 <c>map.ConfigId</c> 鐨?<see cref="VoxelEntitySpawnRuleSet"/>锛屾妸鍛戒腑浣嶇疆鍏ラ槦
    /// 鍒?<see cref="VoxelEntitySpawnService"/> 鐨?spawn 闃熷垪锛堜笉鐩存帴鍒涘缓 GameObject锛屽垎甯ф秷璐归伩鍏?spike锛夈€?    /// <para>**纭畾鎬х害鏉?*锛堜笌 2D <c>EntitySpawnDecorator</c> 鍚屾瀯锛夛細</para>
    /// <list type="number">
    /// <item>瑙勫垯鎸?(Priority asc, RuleId asc) 鎺掑簭</item>
    /// <item>鍖哄潡鍐呮牸瀛愭寜琛屼富搴忛亶鍘?(lz, lx)</item>
    /// <item>鎵€鏈?RNG 娲剧敓鑷?<c>ChunkSeed.Rng(mapId, cx, cz, rule.TileRngTag)</c>锛堜笌 2D 鍏变韩 ChunkSeed锛?/item>
    /// <item>cluster 鍊欓€夋寜 (dz asc, dx asc) 瀛楀吀搴忔帓搴忓悗鐢?RNG 鎶藉彇</item>
    /// </list>
    /// <para>**spawn 钀藉湴鐐?*锛?c>(wx + 0.5, height + 1, wz + 0.5)</c> 鈥斺€?绔欏湪 column 椤堕潰涔嬩笂涓€鏍笺€?/para>
    /// </summary>
    public class VoxelEntitySpawnDecorator : IVoxelChunkDecorator
    {
        public string Id => "VoxelEntitySpawn";

        /// <summary>瑁呴グ鍣?Priority锛堣秺灏忚秺鍏堬級銆?00 浠嬩簬鍦板舰瑁呴グ(100~200)涓庣粨鏋勭敓鎴?400+)涔嬮棿銆?/summary>
        public int Priority { get; set; } = 300;

        /// <summary>鏈瑁呴グ鍏佽 spawn 鐨勫叏灞€涓婇檺锛堣法瑙勫垯锛涢槻姝㈠崟 chunk 绉疮杩囧锛夈€?/summary>
        public int GlobalMaxPerChunk { get; set; } = 32;

        public void Decorate(VoxelMap map, VoxelChunk chunk)
        {
            if (map == null || chunk == null) return;
            var service = VoxelEntitySpawnService.Instance;
            var sets = service.GetRuleSets(map.ConfigId);
            if (sets == null || sets.Count == 0) return;

            // 鏀堕泦 + 鎺掑簭鎵€鏈夎鍒欙紙璺ㄥ涓?RuleSet锛?            var rules = new List<VoxelEntitySpawnRule>();
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

            var occupiedAcrossRules = new HashSet<int>();   // 璺ㄨ鍒欏叡浜紝閬垮厤涓嶅悓瑙勫垯鍦ㄥ悓涓€鏍间笂鍙犲姞
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
                var occupiedThisRule = new HashSet<int>();   // 鐢ㄤ簬 MinSpacing

                for (var lz = 0; lz < size; lz++)
                {
                    if (ruleHits >= rule.MaxPerChunk) break;
                    if (spawnedTotal >= GlobalMaxPerChunk) break;
                    for (var lx = 0; lx < size; lx++)
                    {
                        if (ruleHits >= rule.MaxPerChunk) break;
                        if (spawnedTotal >= GlobalMaxPerChunk) break;

                        // 瀵嗗害鎺烽鍏堟秷鑰?RNG锛屾棤璁哄悗缁槸鍚﹁繃婊ら€氳繃 鈫?RNG 搴忓垪绋冲畾
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

                        // 鈥斺€?Cluster 鈥斺€?                        if (rule.ClusterMax > 1 && rule.ClusterRadius > 0)
                        {
                            var clusterCount = rng.Next(
                                Mathf.Max(1, rule.ClusterMin),
                                Mathf.Max(1, rule.ClusterMax) + 1);
                            var remaining = clusterCount - 1;   // 鍚富浣?                            if (remaining <= 0) continue;

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
                                    continue;   // 浠嶆秷鑰?cluster 鍚嶉锛岄伩鍏?remaining 婕傜Щ
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

        // 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        private static bool PassesFilters(VoxelEntitySpawnRule rule, VoxelChunk chunk, int lx, int lz)
        {
            var idx = chunk.Index(lx, lz);
            var top = chunk.TopBlocks[idx];
            var side = chunk.SideBlocks[idx];
            var height = chunk.Heights[idx];

            // TopBlock 闄愬畾锛堟渶甯哥敤锛?            if (rule.TopBlockIds != null && rule.TopBlockIds.Length > 0)
            {
                var matched = false;
                for (var i = 0; i < rule.TopBlockIds.Length; i++)
                {
                    if (rule.TopBlockIds[i] == top) { matched = true; break; }
                }
                if (!matched) return false;
            }

            // SideBlock 闄愬畾锛堝皯鐢級
            if (rule.SideBlockIds != null && rule.SideBlockIds.Length > 0)
            {
                var matched = false;
                for (var i = 0; i < rule.SideBlockIds.Length; i++)
                {
                    if (rule.SideBlockIds[i] == side) { matched = true; break; }
                }
                if (!matched) return false;
            }

            // 楂樺害杩囨护
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
            // column 椤堕潰鍦?y=height锛岀帺瀹?瀹炰綋绔欏湪 height+1 涓€鏍?            var pos = new Vector3(wx + 0.5f, height + 1f, wz + 0.5f);
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
            return list;   // 宸叉寜 (dz, dx) 瀛楀吀搴忕敓鎴?        }

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
