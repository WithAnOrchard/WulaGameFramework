using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn.Dao;
// §4.1 跨模块 EVT_X 走 bare-string 协议，不 using EntityManager

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn
{
    /// <summary>
    /// 实体生成的运行时调度 + 规则集持久化 + 区块级"已破坏"集合 + 分帧 spawn 队列。
    /// <para>
    /// 三块状态：
    /// <list type="bullet">
    /// <item>**规则集**（持久化）：<see cref="CAT_RULE_SETS"/> 分类，key = <c>"{mapConfigId}::{ruleSetId}"</c>。</item>
    /// <item>**已破坏 spawn**（运行时桶 + 写盘随 chunk 文件）：按 (mapId, cx, cy) 分桶。
    /// 区块加载时由 <c>MapService</c> 调 <see cref="SeedDestroyed"/> 注入；卸载时调 <see cref="DropChunkBuckets"/> 释放。</item>
    /// <item>**已 spawn 实体索引**（仅运行时）：按 (mapId, cx, cy) 分桶。区块卸载时统一发 <c>EVT_DESTROY_ENTITY</c>。</item>
    /// </list>
    /// </para>
    /// <para>**确定性**：所有 spawn 请求源自 <c>EntitySpawnDecorator</c>，instanceId 由 <see cref="ComposeInstanceId"/> 派生稳定，
    /// 同区块每次加载产出相同 instanceId 集合 → 已破坏的格子下次再加载也跳过。</para>
    /// </summary>
    public class EntitySpawnService : Service<EntitySpawnService>
    {
        public const string CAT_RULE_SETS = "SpawnRuleSets";
        public const string INSTANCE_ID_PREFIX = "spawn:";

        // ─── 规则集（运行时缓存，从 _dataStorage 重建） ──────────────────
        private readonly Dictionary<string, List<EntitySpawnRuleSet>> _ruleSetsByConfig = new();

        // ─── 已破坏 spawn 集合（按 chunk 桶化） ──────────────────────────
        private readonly Dictionary<ChunkKey, HashSet<string>> _destroyedByChunk = new();

        // ─── 已 spawn 实体（按 chunk 桶化，仅运行时） ────────────────
        private readonly Dictionary<ChunkKey, List<string>> _runtimeByChunk = new();

        // ─── 待处理队列去重集（防多次 Decorate 重复入队同一 instanceId） ──────────
        private readonly Dictionary<ChunkKey, HashSet<string>> _pendingByChunk = new();

        // ─── 分帧 spawn 队列 ──────────────────────────────────
        private readonly Queue<SpawnRequest> _spawnQueue = new();

        /// <summary>每次 <see cref="TickSpawnQueue"/> 处理上限；MapManager.Update 默认每帧调一次。</summary>
        public int EntitiesPerFrame { get; set; } = 8;

        protected override void Initialize()
        {
            base.Initialize();
            BuildRuleSetCache();
            Log("EntitySpawnService 初始化完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region 规则集 API

        /// <summary>注册或覆盖规则集到指定 MapConfigId。同 (mapConfigId, ruleSetId) 已存在则覆盖。</summary>
        public void RegisterRuleSet(string mapConfigId, EntitySpawnRuleSet set)
        {
            if (string.IsNullOrEmpty(mapConfigId) || set == null || string.IsNullOrEmpty(set.Id))
            {
                LogWarning("RegisterRuleSet: mapConfigId / set / set.Id 不能为空");
                return;
            }
            SetData(CAT_RULE_SETS, ComposeRuleSetKey(mapConfigId, set.Id), set);
            CacheRuleSet(mapConfigId, set);
            Log($"注册 SpawnRuleSet: [{mapConfigId}] {set.Id}（{set.Rules?.Count ?? 0} 条规则）", Color.blue);
        }

        /// <summary>获取该 MapConfigId 绑定的所有规则集（运行时缓存的快照）。</summary>
        public IReadOnlyList<EntitySpawnRuleSet> GetRuleSets(string mapConfigId)
        {
            if (string.IsNullOrEmpty(mapConfigId)) return Array.Empty<EntitySpawnRuleSet>();
            return _ruleSetsByConfig.TryGetValue(mapConfigId, out var list)
                ? (IReadOnlyList<EntitySpawnRuleSet>)list
                : Array.Empty<EntitySpawnRuleSet>();
        }

        /// <summary>移除规则集；返回是否命中。</summary>
        public bool RemoveRuleSet(string mapConfigId, string ruleSetId)
        {
            if (string.IsNullOrEmpty(mapConfigId) || string.IsNullOrEmpty(ruleSetId)) return false;
            var ok = RemoveData(CAT_RULE_SETS, ComposeRuleSetKey(mapConfigId, ruleSetId));
            if (_ruleSetsByConfig.TryGetValue(mapConfigId, out var list))
            {
                for (var i = list.Count - 1; i >= 0; i--)
                    if (list[i].Id == ruleSetId) { list.RemoveAt(i); ok = true; }
                if (list.Count == 0) _ruleSetsByConfig.Remove(mapConfigId);
            }
            if (ok) Log($"移除 SpawnRuleSet: [{mapConfigId}] {ruleSetId}", Color.yellow);
            return ok;
        }

        private void BuildRuleSetCache()
        {
            _ruleSetsByConfig.Clear();
            foreach (var key in GetKeys(CAT_RULE_SETS))
            {
                var set = GetData<EntitySpawnRuleSet>(CAT_RULE_SETS, key);
                if (set == null) continue;
                if (!TryParseRuleSetKey(key, out var mapConfigId, out _)) continue;
                CacheRuleSet(mapConfigId, set);
            }
        }

        private void CacheRuleSet(string mapConfigId, EntitySpawnRuleSet set)
        {
            if (!_ruleSetsByConfig.TryGetValue(mapConfigId, out var list))
                _ruleSetsByConfig[mapConfigId] = list = new List<EntitySpawnRuleSet>(2);
            for (var i = list.Count - 1; i >= 0; i--)
                if (list[i].Id == set.Id) list.RemoveAt(i);
            list.Add(set);
        }

        private static string ComposeRuleSetKey(string mapConfigId, string ruleSetId) =>
            $"{mapConfigId}::{ruleSetId}";

        private static bool TryParseRuleSetKey(string key, out string mapConfigId, out string ruleSetId)
        {
            mapConfigId = null; ruleSetId = null;
            if (string.IsNullOrEmpty(key)) return false;
            var idx = key.IndexOf("::", StringComparison.Ordinal);
            if (idx <= 0 || idx >= key.Length - 2) return false;
            mapConfigId = key.Substring(0, idx);
            ruleSetId = key.Substring(idx + 2);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 已破坏 spawn — 按 chunk 桶化

        /// <summary>区块加载时由 <c>MapService</c> 注入：把存档里的 destroyed 列表加入桶。</summary>
        public void SeedDestroyed(string mapId, int cx, int cy, IList<string> ids)
        {
            if (string.IsNullOrEmpty(mapId) || ids == null || ids.Count == 0) return;
            var key = new ChunkKey(mapId, cx, cy);
            if (!_destroyedByChunk.TryGetValue(key, out var set))
                _destroyedByChunk[key] = set = new HashSet<string>();
            for (var i = 0; i < ids.Count; i++) set.Add(ids[i]);
        }

        /// <summary>区块卸载时由 <c>MapService</c> 调用：丢弃 destroyed 桶（已写盘 → 安全丢弃）。</summary>
        public void DropChunkBuckets(string mapId, int cx, int cy)
        {
            var key = new ChunkKey(mapId, cx, cy);
            _destroyedByChunk.Remove(key);
            // 注意：runtime 桶由 DestroyChunkRuntimeEntities 处理，不在此触发以保留控制顺序。
        }

        /// <summary>取该区块的 destroyed id 集合（用于 <c>MapService</c> 写盘构造 ChunkSaveData）。</summary>
        public IReadOnlyCollection<string> GetDestroyedIds(string mapId, int cx, int cy)
        {
            return _destroyedByChunk.TryGetValue(new ChunkKey(mapId, cx, cy), out var set)
                ? (IReadOnlyCollection<string>)set
                : Array.Empty<string>();
        }

        /// <summary>
        /// 标记某 spawn 实体已**永久破坏**。
        /// <para>典型流程（业务砍树）：先 <see cref="MarkDestroyed"/>，再触发 <c>EntityMgr.EVT_DESTROY_ENTITY</c>。
        /// 标记后会让对应区块标 dirty（由 <c>MapService</c> 在 AutoSave / Unload 时写盘）。</para>
        /// </summary>
        public bool MarkDestroyed(string mapId, string instanceId)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(instanceId)) return false;
            if (!TryParseChunkFromInstanceId(instanceId, out _, out var cx, out var cy)) return false;
            var key = new ChunkKey(mapId, cx, cy);
            if (!_destroyedByChunk.TryGetValue(key, out var set))
                _destroyedByChunk[key] = set = new HashSet<string>();
            if (!set.Add(instanceId)) return false;

            // 也从 runtime 桶移除（业务侧通常会另调 EVT_DESTROY_ENTITY 销毁 GameObject，这里只清索引）
            if (_runtimeByChunk.TryGetValue(key, out var rt)) rt.Remove(instanceId);

            // 让 MapService 标 chunk dirty（避免循环依赖，MapService 在加载时设 PostFillHook 即可，
            // 这里通过 C# 事件向外抛一个 OnChunkDirty 由 MapService 订阅；为减少调用面，直接 Map.PeekChunk + MarkDirty）
            DirtyChunkLookup?.Invoke(mapId, cx, cy);
            return true;
        }

        /// <summary>取消 destroyed 标记（重新可被 spawn）。</summary>
        public bool UnmarkDestroyed(string mapId, string instanceId)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(instanceId)) return false;
            if (!TryParseChunkFromInstanceId(instanceId, out _, out var cx, out var cy)) return false;
            var key = new ChunkKey(mapId, cx, cy);
            if (!_destroyedByChunk.TryGetValue(key, out var set)) return false;
            if (!set.Remove(instanceId)) return false;
            DirtyChunkLookup?.Invoke(mapId, cx, cy);
            return true;
        }

        public bool IsDestroyed(string mapId, string instanceId)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(instanceId)) return false;
            if (!TryParseChunkFromInstanceId(instanceId, out _, out var cx, out var cy)) return false;
            return _destroyedByChunk.TryGetValue(new ChunkKey(mapId, cx, cy), out var set)
                   && set.Contains(instanceId);
        }

        public bool IsDestroyedInChunk(string mapId, int cx, int cy, string instanceId)
        {
            return _destroyedByChunk.TryGetValue(new ChunkKey(mapId, cx, cy), out var set)
                   && set.Contains(instanceId);
        }

        public void ClearDestroyedInChunk(string mapId, int cx, int cy)
        {
            var key = new ChunkKey(mapId, cx, cy);
            if (_destroyedByChunk.Remove(key))
                DirtyChunkLookup?.Invoke(mapId, cx, cy);
        }

        /// <summary>
        /// **MapService 订阅此事件**：当 EntitySpawnService 内部状态变化导致某 chunk 应该写盘时，
        /// 由 MapService 在自己的 PostFillHook 体系外被动响应（标 chunk dirty）。
        /// 这样避免 EntitySpawnService 反向 `using` MapService。
        /// </summary>
        public Action<string, int, int> DirtyChunkLookup;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 运行时 spawn 索引

        /// <summary>装饰器把命中的 spawn 请求入队；下个 Tick 由 MapManager 驱动消费。
        /// <para>双重去重：跳过已存活跳同 id 的实体，及已在队列中等待的 id。
        /// 防同一 chunk 多次 Decorate / unload 未及时清理导致的 "已存在" 警告。</para></summary>
        public void EnqueueSpawn(string mapId, int cx, int cy, string entityConfigId,
                                 string instanceId, Vector3 worldPos, Transform parent)
        {
            if (string.IsNullOrEmpty(entityConfigId) || string.IsNullOrEmpty(instanceId)) return;
            var key = new ChunkKey(mapId, cx, cy);

            // 跳过已活着的 —— 避免对 EntityService 发重复 EVT_CREATE_ENTITY
            if (_runtimeByChunk.TryGetValue(key, out var alive) && alive.Contains(instanceId)) return;

            // 跳过已在队列中等待的
            if (!_pendingByChunk.TryGetValue(key, out var pending))
                _pendingByChunk[key] = pending = new HashSet<string>();
            if (!pending.Add(instanceId)) return;

            _spawnQueue.Enqueue(new SpawnRequest
            {
                MapId = mapId,
                ChunkX = cx,
                ChunkY = cy,
                EntityConfigId = entityConfigId,
                InstanceId = instanceId,
                WorldPosition = worldPos,
                Parent = parent
            });
        }

        /// <summary>从 pending 集合中移除（入队去重计数器）。</summary>
        private void RemoveFromPending(in ChunkKey key, string instanceId)
        {
            if (_pendingByChunk.TryGetValue(key, out var set) && set.Remove(instanceId) && set.Count == 0)
                _pendingByChunk.Remove(key);
        }

        /// <summary>
        /// 由 <c>MapManager.Update</c> 每帧调用，从队列消费 <see cref="EntitiesPerFrame"/> 个请求。
        /// 内部会校验 chunk 是否仍加载（避免给已卸载的区块 spawn 实体）。
        /// </summary>
        public int TickSpawnQueue(Func<string, int, int, bool> isChunkLoaded)
        {
            var spawned = 0;
            var budget = EntitiesPerFrame;
            while (budget > 0 && _spawnQueue.Count > 0)
            {
                var req = _spawnQueue.Dequeue();
                var key = new ChunkKey(req.MapId, req.ChunkX, req.ChunkY);
                // 无论成败与否先从 pending 出队，避免后续 EnqueueSpawn 被误跳过
                RemoveFromPending(in key, req.InstanceId);

                if (isChunkLoaded != null && !isChunkLoaded(req.MapId, req.ChunkX, req.ChunkY))
                {
                    continue; // chunk 已卸载，丢弃
                }
                // 可能在队列出队前已被别的路径创建（理论上不应发生）——检查运行中桶防重复调 Create
                if (_runtimeByChunk.TryGetValue(key, out var existing) && existing.Contains(req.InstanceId))
                {
                    continue;
                }

                var data = new List<object> { req.EntityConfigId, req.InstanceId, req.Parent, req.WorldPosition };
                try
                {
                    // §4.1 跨模块 bare-string：EntityManager.EVT_CREATE_ENTITY
                    var result = EventProcessor.Instance.TriggerEventMethod("CreateEntity", data);
                    if (ResultCode.IsOk(result))
                    {
                        if (!_runtimeByChunk.TryGetValue(key, out var list))
                            _runtimeByChunk[key] = list = new List<string>(8);
                        list.Add(req.InstanceId);
                        spawned++;
                    }
                    else
                    {
                        LogWarning($"Spawn failed: {req.EntityConfigId}/{req.InstanceId} → {(result != null && result.Count > 0 ? result[0] : "<no result>")}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"Spawn exception: {req.EntityConfigId}/{req.InstanceId}: {ex.Message}");
                }
                budget--;
            }
            return spawned;
        }

        /// <summary>
        /// 区块卸载路径：销毁该 chunk 内已 spawn 的所有实体并清 runtime 桶。
        /// 不影响 destroyed 桶（destroyed 由 <see cref="DropChunkBuckets"/> 单独处理）。
        /// </summary>
        public void DestroyChunkRuntimeEntities(string mapId, int cx, int cy)
        {
            var key = new ChunkKey(mapId, cx, cy);
            if (_runtimeByChunk.TryGetValue(key, out var list))
            {
                for (var i = 0; i < list.Count; i++)
                {
                    try
                    {
                        // §4.1 跨模块 bare-string：EntityManager.EVT_DESTROY_ENTITY
                        EventProcessor.Instance.TriggerEventMethod("DestroyEntity", new List<object> { list[i] });
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"DestroyChunkRuntimeEntities exception: {list[i]}: {ex.Message}");
                    }
                }
                _runtimeByChunk.Remove(key);
            }
            // 同时清掉尚未出队的 pending，下次进入同 chunk 可重新入队
            _pendingByChunk.Remove(key);
        }

        public IReadOnlyList<string> GetRuntimeEntitiesInChunk(string mapId, int cx, int cy)
        {
            return _runtimeByChunk.TryGetValue(new ChunkKey(mapId, cx, cy), out var list)
                ? (IReadOnlyList<string>)list
                : Array.Empty<string>();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region instanceId helpers

        /// <summary>
        /// 派生 spawn 实体的稳定 instanceId（主体）。
        /// 格式：<c>spawn:{mapId}:{cx}:{cy}:{ruleId}:{lx}_{ly}</c>。
        /// </summary>
        public static string ComposeInstanceId(string mapId, int cx, int cy, string ruleId, int lx, int ly)
            => $"{INSTANCE_ID_PREFIX}{mapId}:{cx}:{cy}:{ruleId}:{lx}_{ly}";

        /// <summary>
        /// 派生 cluster 子项 instanceId。
        /// 格式：<c>spawn:{mapId}:{cx}:{cy}:{ruleId}:{seedLx}_{seedLy}#{candLx}_{candLy}</c>。
        /// 用 candidate 实际坐标编码而非自增序号 → 同 cluster 内某个邻居被销毁后，其它邻居的 id 不会漂移。
        /// </summary>
        public static string ComposeClusterInstanceId(string mapId, int cx, int cy, string ruleId,
                                                      int seedLx, int seedLy, int candLx, int candLy)
            => $"{INSTANCE_ID_PREFIX}{mapId}:{cx}:{cy}:{ruleId}:{seedLx}_{seedLy}#{candLx}_{candLy}";

        /// <summary>从 instanceId 反推 (mapId, cx, cy)。失败返回 false。</summary>
        public static bool TryParseChunkFromInstanceId(string instanceId, out string mapId, out int cx, out int cy)
        {
            mapId = null; cx = 0; cy = 0;
            if (string.IsNullOrEmpty(instanceId) || !instanceId.StartsWith(INSTANCE_ID_PREFIX, StringComparison.Ordinal))
                return false;
            var parts = instanceId.Split(':');
            if (parts.Length < 6) return false;
            mapId = parts[1];
            return int.TryParse(parts[2], out cx) && int.TryParse(parts[3], out cy);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 内部类型

        private struct SpawnRequest
        {
            public string MapId;
            public int ChunkX, ChunkY;
            public string EntityConfigId;
            public string InstanceId;
            public Vector3 WorldPosition;
            public Transform Parent;
        }

        private readonly struct ChunkKey : IEquatable<ChunkKey>
        {
            public readonly string MapId;
            public readonly int X, Y;
            public ChunkKey(string mapId, int x, int y) { MapId = mapId; X = x; Y = y; }
            public bool Equals(ChunkKey other) => X == other.X && Y == other.Y && MapId == other.MapId;
            public override bool Equals(object obj) => obj is ChunkKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    var h = MapId == null ? 0 : MapId.GetHashCode();
                    h = (h * 397) ^ X;
                    h = (h * 397) ^ Y;
                    return h;
                }
            }
        }

        #endregion
    }
}
