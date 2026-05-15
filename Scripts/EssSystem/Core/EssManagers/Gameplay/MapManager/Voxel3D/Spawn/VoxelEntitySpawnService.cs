using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn.Dao;
// §4.1 跨模块 EVT_X 走 bare-string，不 using EntityManager

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn
{
    /// <summary>
    /// 体素实体生成的运行时调度（与 2D <c>EntitySpawnService</c> 平行；独立队列/destroyed/runtime
    /// 避免与 2D mapId 冲突 + IsChunkLoaded 路由复杂化）。
    /// <para>三块状态：</para>
    /// <list type="bullet">
    /// <item>**规则集**（持久化）：<see cref="CAT_RULE_SETS"/> key = <c>"{configId}::{ruleSetId}"</c></item>
    /// <item>**已破坏 spawn**（运行时桶 + 写盘随 chunk 文件）：按 (mapId, cx, cz) 分桶</item>
    /// <item>**已 spawn 实体索引**（仅运行时）：按 (mapId, cx, cz) 分桶；卸载时统一 EVT_DESTROY_ENTITY</item>
    /// </list>
    /// </summary>
    public class VoxelEntitySpawnService : Service<VoxelEntitySpawnService>
    {
        public const string CAT_RULE_SETS = "VoxelSpawnRuleSets";
        public const string INSTANCE_ID_PREFIX = "vspawn:";

        // 规则集（运行时缓存，从 _dataStorage 重建）
        private readonly Dictionary<string, List<VoxelEntitySpawnRuleSet>> _ruleSetsByConfig = new();

        // 已破坏 spawn / 已 spawn 实体 / pending 队列去重
        private readonly Dictionary<ChunkKey, HashSet<string>> _destroyedByChunk = new();
        private readonly Dictionary<ChunkKey, List<string>>    _runtimeByChunk   = new();
        private readonly Dictionary<ChunkKey, HashSet<string>> _pendingByChunk   = new();

        private readonly Queue<SpawnRequest> _spawnQueue = new();

        public int EntitiesPerFrame { get; set; } = 8;

        /// <summary>由 <c>Voxel3DMapService</c> 订阅：destroyed 桶变化时反向标 chunk dirty。</summary>
        public Action<string, int, int> DirtyChunkLookup;

        protected override void Initialize()
        {
            base.Initialize();
            BuildRuleSetCache();
            Log("VoxelEntitySpawnService 初始化完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region 规则集 API

        public void RegisterRuleSet(string configId, VoxelEntitySpawnRuleSet set)
        {
            if (string.IsNullOrEmpty(configId) || set == null || string.IsNullOrEmpty(set.Id))
            {
                LogWarning("RegisterRuleSet: configId / set / set.Id 不能为空");
                return;
            }
            SetData(CAT_RULE_SETS, ComposeRuleSetKey(configId, set.Id), set);
            CacheRuleSet(configId, set);
            Log($"注册 VoxelSpawnRuleSet: [{configId}] {set.Id}（{set.Rules?.Count ?? 0} 条规则）", Color.blue);
        }

        public IReadOnlyList<VoxelEntitySpawnRuleSet> GetRuleSets(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return Array.Empty<VoxelEntitySpawnRuleSet>();
            return _ruleSetsByConfig.TryGetValue(configId, out var list)
                ? (IReadOnlyList<VoxelEntitySpawnRuleSet>)list
                : Array.Empty<VoxelEntitySpawnRuleSet>();
        }

        public bool RemoveRuleSet(string configId, string ruleSetId)
        {
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(ruleSetId)) return false;
            var ok = RemoveData(CAT_RULE_SETS, ComposeRuleSetKey(configId, ruleSetId));
            if (_ruleSetsByConfig.TryGetValue(configId, out var list))
            {
                for (var i = list.Count - 1; i >= 0; i--)
                    if (list[i].Id == ruleSetId) { list.RemoveAt(i); ok = true; }
                if (list.Count == 0) _ruleSetsByConfig.Remove(configId);
            }
            if (ok) Log($"移除 VoxelSpawnRuleSet: [{configId}] {ruleSetId}", Color.yellow);
            return ok;
        }

        private void BuildRuleSetCache()
        {
            _ruleSetsByConfig.Clear();
            foreach (var key in GetKeys(CAT_RULE_SETS))
            {
                var set = GetData<VoxelEntitySpawnRuleSet>(CAT_RULE_SETS, key);
                if (set == null) continue;
                if (!TryParseRuleSetKey(key, out var configId, out _)) continue;
                CacheRuleSet(configId, set);
            }
        }

        private void CacheRuleSet(string configId, VoxelEntitySpawnRuleSet set)
        {
            if (!_ruleSetsByConfig.TryGetValue(configId, out var list))
                _ruleSetsByConfig[configId] = list = new List<VoxelEntitySpawnRuleSet>(2);
            for (var i = list.Count - 1; i >= 0; i--)
                if (list[i].Id == set.Id) list.RemoveAt(i);
            list.Add(set);
        }

        private static string ComposeRuleSetKey(string configId, string ruleSetId) =>
            $"{configId}::{ruleSetId}";

        private static bool TryParseRuleSetKey(string key, out string configId, out string ruleSetId)
        {
            configId = null; ruleSetId = null;
            if (string.IsNullOrEmpty(key)) return false;
            var idx = key.IndexOf("::", StringComparison.Ordinal);
            if (idx <= 0 || idx >= key.Length - 2) return false;
            configId = key.Substring(0, idx);
            ruleSetId = key.Substring(idx + 2);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 已破坏 spawn — 按 chunk 桶化

        public void SeedDestroyed(string mapId, int cx, int cz, IList<string> ids)
        {
            if (string.IsNullOrEmpty(mapId) || ids == null || ids.Count == 0) return;
            var key = new ChunkKey(mapId, cx, cz);
            if (!_destroyedByChunk.TryGetValue(key, out var set))
                _destroyedByChunk[key] = set = new HashSet<string>();
            for (var i = 0; i < ids.Count; i++) set.Add(ids[i]);
        }

        public void DropChunkBuckets(string mapId, int cx, int cz)
        {
            _destroyedByChunk.Remove(new ChunkKey(mapId, cx, cz));
        }

        public IReadOnlyCollection<string> GetDestroyedIds(string mapId, int cx, int cz)
        {
            return _destroyedByChunk.TryGetValue(new ChunkKey(mapId, cx, cz), out var set)
                ? (IReadOnlyCollection<string>)set
                : Array.Empty<string>();
        }

        public bool MarkDestroyed(string mapId, string instanceId)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(instanceId)) return false;
            if (!TryParseChunkFromInstanceId(instanceId, out _, out var cx, out var cz)) return false;
            var key = new ChunkKey(mapId, cx, cz);
            if (!_destroyedByChunk.TryGetValue(key, out var set))
                _destroyedByChunk[key] = set = new HashSet<string>();
            if (!set.Add(instanceId)) return false;
            if (_runtimeByChunk.TryGetValue(key, out var rt)) rt.Remove(instanceId);
            DirtyChunkLookup?.Invoke(mapId, cx, cz);
            return true;
        }

        public bool UnmarkDestroyed(string mapId, string instanceId)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(instanceId)) return false;
            if (!TryParseChunkFromInstanceId(instanceId, out _, out var cx, out var cz)) return false;
            var key = new ChunkKey(mapId, cx, cz);
            if (!_destroyedByChunk.TryGetValue(key, out var set)) return false;
            if (!set.Remove(instanceId)) return false;
            DirtyChunkLookup?.Invoke(mapId, cx, cz);
            return true;
        }

        public bool IsDestroyed(string mapId, string instanceId)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(instanceId)) return false;
            if (!TryParseChunkFromInstanceId(instanceId, out _, out var cx, out var cz)) return false;
            return _destroyedByChunk.TryGetValue(new ChunkKey(mapId, cx, cz), out var set)
                   && set.Contains(instanceId);
        }

        public bool IsDestroyedInChunk(string mapId, int cx, int cz, string instanceId)
        {
            return _destroyedByChunk.TryGetValue(new ChunkKey(mapId, cx, cz), out var set)
                   && set.Contains(instanceId);
        }

        public void ClearDestroyedInChunk(string mapId, int cx, int cz)
        {
            var key = new ChunkKey(mapId, cx, cz);
            if (_destroyedByChunk.Remove(key))
                DirtyChunkLookup?.Invoke(mapId, cx, cz);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 运行时 spawn 索引

        public void EnqueueSpawn(string mapId, int cx, int cz, string entityConfigId,
                                 string instanceId, Vector3 worldPos, Transform parent)
        {
            if (string.IsNullOrEmpty(entityConfigId) || string.IsNullOrEmpty(instanceId)) return;
            var key = new ChunkKey(mapId, cx, cz);

            if (_runtimeByChunk.TryGetValue(key, out var alive) && alive.Contains(instanceId)) return;
            if (!_pendingByChunk.TryGetValue(key, out var pending))
                _pendingByChunk[key] = pending = new HashSet<string>();
            if (!pending.Add(instanceId)) return;

            _spawnQueue.Enqueue(new SpawnRequest
            {
                MapId = mapId, ChunkX = cx, ChunkZ = cz,
                EntityConfigId = entityConfigId, InstanceId = instanceId,
                WorldPosition = worldPos, Parent = parent,
            });
        }

        private void RemoveFromPending(in ChunkKey key, string instanceId)
        {
            if (_pendingByChunk.TryGetValue(key, out var set) && set.Remove(instanceId) && set.Count == 0)
                _pendingByChunk.Remove(key);
        }

        public int TickSpawnQueue(Func<string, int, int, bool> isChunkLoaded)
        {
            var spawned = 0;
            var budget = EntitiesPerFrame;
            while (budget > 0 && _spawnQueue.Count > 0)
            {
                var req = _spawnQueue.Dequeue();
                var key = new ChunkKey(req.MapId, req.ChunkX, req.ChunkZ);
                RemoveFromPending(in key, req.InstanceId);

                if (isChunkLoaded != null && !isChunkLoaded(req.MapId, req.ChunkX, req.ChunkZ)) continue;
                if (_runtimeByChunk.TryGetValue(key, out var existing) && existing.Contains(req.InstanceId)) continue;

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
                        LogWarning($"VoxelSpawn failed: {req.EntityConfigId}/{req.InstanceId} → {(result != null && result.Count > 0 ? result[0] : "<no result>")}");
                    }
                }
                catch (Exception ex)
                {
                    LogWarning($"VoxelSpawn exception: {req.EntityConfigId}/{req.InstanceId}: {ex.Message}");
                }
                budget--;
            }
            return spawned;
        }

        public void DestroyChunkRuntimeEntities(string mapId, int cx, int cz)
        {
            var key = new ChunkKey(mapId, cx, cz);
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
            _pendingByChunk.Remove(key);
        }

        public IReadOnlyList<string> GetRuntimeEntitiesInChunk(string mapId, int cx, int cz)
        {
            return _runtimeByChunk.TryGetValue(new ChunkKey(mapId, cx, cz), out var list)
                ? (IReadOnlyList<string>)list
                : Array.Empty<string>();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region instanceId helpers

        /// <summary>主体 instanceId：<c>vspawn:{mapId}:{cx}:{cz}:{ruleId}:{lx}_{lz}</c>。</summary>
        public static string ComposeInstanceId(string mapId, int cx, int cz, string ruleId, int lx, int lz)
            => $"{INSTANCE_ID_PREFIX}{mapId}:{cx}:{cz}:{ruleId}:{lx}_{lz}";

        /// <summary>cluster 子项 instanceId：<c>vspawn:{mapId}:{cx}:{cz}:{ruleId}:{seedLx}_{seedLz}#{candLx}_{candLz}</c>。</summary>
        public static string ComposeClusterInstanceId(string mapId, int cx, int cz, string ruleId,
                                                      int seedLx, int seedLz, int candLx, int candLz)
            => $"{INSTANCE_ID_PREFIX}{mapId}:{cx}:{cz}:{ruleId}:{seedLx}_{seedLz}#{candLx}_{candLz}";

        public static bool TryParseChunkFromInstanceId(string instanceId, out string mapId, out int cx, out int cz)
        {
            mapId = null; cx = 0; cz = 0;
            if (string.IsNullOrEmpty(instanceId) || !instanceId.StartsWith(INSTANCE_ID_PREFIX, StringComparison.Ordinal))
                return false;
            var parts = instanceId.Split(':');
            if (parts.Length < 6) return false;
            mapId = parts[1];
            return int.TryParse(parts[2], out cx) && int.TryParse(parts[3], out cz);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 内部类型

        private struct SpawnRequest
        {
            public string MapId;
            public int ChunkX, ChunkZ;
            public string EntityConfigId;
            public string InstanceId;
            public Vector3 WorldPosition;
            public Transform Parent;
        }

        private readonly struct ChunkKey : IEquatable<ChunkKey>
        {
            public readonly string MapId;
            public readonly int X, Z;
            public ChunkKey(string mapId, int x, int z) { MapId = mapId; X = x; Z = z; }
            public bool Equals(ChunkKey other) => X == other.X && Z == other.Z && MapId == other.MapId;
            public override bool Equals(object obj) => obj is ChunkKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    var h = MapId == null ? 0 : MapId.GetHashCode();
                    h = (h * 397) ^ X;
                    h = (h * 397) ^ Z;
                    return h;
                }
            }
        }

        #endregion
    }
}
