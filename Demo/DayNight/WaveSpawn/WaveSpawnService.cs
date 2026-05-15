using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Base.Event;
using Demo.DayNight.WaveSpawn.Dao;

namespace Demo.DayNight.WaveSpawn
{
    /// <summary>波次生成 Service —— 负责存配置 + 推动当前波的刷怪 tick。
    /// <para>跨模块只暴露 <c>EVT_WAVE_STARTED</c> / <c>EVT_WAVE_CLEARED</c> 两条 **广播**；
    /// 实际刷敌人通过 <see cref="EssSystem.EssManagers.EntityManager"/> 的 <c>EVT_CREATE_ENTITY</c> 走，
    /// 不直接 <c>using</c> EntityManager，避免耦合。</para></summary>
    public class WaveSpawnService : Service<WaveSpawnService>
    {
        // ─── 数据分类 ────────────────────────────────────────────
        public const string CAT_CONFIGS = "Configs";

        // ─── Event 名常量（跨模块广播）──────────────────────────
        /// <summary>波次开始 **广播**。参数 <c>[int round, int waveIndex, int totalEnemies]</c>。</summary>
        public const string EVT_WAVE_STARTED = "OnWaveStarted";

        /// <summary>波次清完 **广播**。参数 <c>[int round, int waveIndex]</c>。</summary>
        public const string EVT_WAVE_CLEARED = "OnWaveCleared";

        /// <summary>跨模块创建实体事件名（避免 <c>using EntityManager</c>）。</summary>
        private const string EXT_CREATE_ENTITY = "CreateEntity";

        // ─── 运行时状态 ─────────────────────────────────────────
        /// <summary>当前在跑的波次配置；null 表示未在刷怪。</summary>
        private WaveConfig _activeConfig;
        private int _activeRound;
        private int _activeWaveIndex;

        /// <summary>每条 entry 的运行时进度（已刷数量、累计时间）。</summary>
        private readonly List<EntryRuntime> _runtimes = new();

        /// <summary>已刷出的实例 id 集合，用于判断"波清完"。</summary>
        private readonly HashSet<string> _aliveInstanceIds = new();

        protected override void Initialize()
        {
            base.Initialize();
            Log("WaveSpawnService 初始化完成", Color.green);
        }

        // ─── Public API ──────────────────────────────────────────
        public void RegisterConfig(WaveConfig cfg)
        {
            if (cfg == null || string.IsNullOrEmpty(cfg.ConfigId))
            {
                LogWarning("RegisterConfig: 配置或 ConfigId 为空");
                return;
            }
            SetData(CAT_CONFIGS, cfg.ConfigId, cfg);
        }

        public WaveConfig GetConfig(string configId) =>
            string.IsNullOrEmpty(configId) ? null : GetData<WaveConfig>(CAT_CONFIGS, configId);

        public IEnumerable<WaveConfig> GetAllConfigs()
        {
            if (!_dataStorage.TryGetValue(CAT_CONFIGS, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is WaveConfig c) yield return c;
        }

        /// <summary>挑选一条对应回合 / BOSS 状态的配置开刷。返回 true 表示成功启动。</summary>
        public bool StartWaveForRound(int round, bool isBossNight, int waveIndex = 0)
        {
            var cfg = PickConfig(round, isBossNight);
            if (cfg == null)
            {
                LogWarning($"StartWaveForRound: 未匹配到 round={round}, boss={isBossNight} 的 WaveConfig");
                return false;
            }
            return StartWave(cfg, round, waveIndex);
        }

        public bool StartWave(WaveConfig cfg, int round, int waveIndex = 0)
        {
            if (cfg == null) return false;
            _activeConfig = cfg;
            _activeRound = round;
            _activeWaveIndex = waveIndex;
            _runtimes.Clear();
            _aliveInstanceIds.Clear();

            var total = 0;
            for (var i = 0; i < cfg.Entries.Count; i++)
            {
                var e = cfg.Entries[i];
                if (e == null) continue;
                _runtimes.Add(new EntryRuntime { Entry = e });
                total += Mathf.Max(0, e.Count);
            }

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_WAVE_STARTED,
                    new List<object> { round, waveIndex, total });
            Log($"启动波次 {cfg.ConfigId} | round={round} wave={waveIndex} total={total}", Color.cyan);
            return true;
        }

        /// <summary>取消当前波次（不广播 cleared）。</summary>
        public void CancelActiveWave()
        {
            _activeConfig = null;
            _runtimes.Clear();
            _aliveInstanceIds.Clear();
        }

        /// <summary>主循环驱动：从 <see cref="WaveSpawnManager"/> 调用。</summary>
        public void Tick(float deltaTime, Transform spawnRoot, Vector3 spawnCenter, float spawnRadius)
        {
            if (_activeConfig == null) return;

            for (var i = 0; i < _runtimes.Count; i++)
            {
                var rt = _runtimes[i];
                if (rt.Spawned >= rt.Entry.Count) continue;
                rt.Elapsed += deltaTime;
                if (rt.Elapsed < rt.Entry.StartDelay) continue;

                var localT = rt.Elapsed - rt.Entry.StartDelay;
                var shouldHaveSpawned = rt.Entry.SpawnInterval <= 0f
                    ? rt.Entry.Count
                    : Mathf.Min(rt.Entry.Count, Mathf.FloorToInt(localT / rt.Entry.SpawnInterval) + 1);

                while (rt.Spawned < shouldHaveSpawned)
                {
                    SpawnOne(rt.Entry, spawnRoot, spawnCenter, spawnRadius);
                    rt.Spawned++;
                }
            }

            if (IsActiveWaveFullySpawned() && _aliveInstanceIds.Count == 0)
                CompleteActiveWave();
        }

        // ─── Internal ───────────────────────────────────────────
        private WaveConfig PickConfig(int round, bool isBossNight)
        {
            WaveConfig best = null;
            foreach (var cfg in GetAllConfigs())
            {
                if (cfg.IsBossWave != isBossNight) continue;
                if (round < cfg.MinRound) continue;
                if (cfg.MaxRound > 0 && round > cfg.MaxRound) continue;
                // 取 MinRound 最大、最贴合当前 round 的那条
                if (best == null || cfg.MinRound > best.MinRound) best = cfg;
            }
            return best;
        }

        private void SpawnOne(WaveEntry entry, Transform parent, Vector3 center, float radius)
        {
            if (string.IsNullOrEmpty(entry.EntityConfigId)) return;
            var pos = center + (Vector3)(Random.insideUnitCircle.normalized * radius);
            var instanceId = $"wave:{_activeRound}:{_activeWaveIndex}:{entry.EntityConfigId}:{_aliveInstanceIds.Count}";

            if (!EventProcessor.HasInstance) return;
            var result = EventProcessor.Instance.TriggerEventMethod(EXT_CREATE_ENTITY,
                new List<object> { entry.EntityConfigId, instanceId, parent, pos });
            if (ResultCode.IsOk(result)) _aliveInstanceIds.Add(instanceId);
        }

        private bool IsActiveWaveFullySpawned()
        {
            for (var i = 0; i < _runtimes.Count; i++)
                if (_runtimes[i].Spawned < _runtimes[i].Entry.Count) return false;
            return true;
        }

        private void CompleteActiveWave()
        {
            var cfg = _activeConfig;
            var round = _activeRound;
            var waveIndex = _activeWaveIndex;
            _activeConfig = null;
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_WAVE_CLEARED,
                    new List<object> { round, waveIndex });
            Log($"波次清完 {cfg?.ConfigId} | round={round} wave={waveIndex}", Color.green);
        }

        /// <summary>外部（敌人 OnDeath 处理器）通知某 instance 已死亡。</summary>
        public void NotifyEntityDied(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return;
            _aliveInstanceIds.Remove(instanceId);
        }

        /// <summary>查询：当前是否有活跃波次。</summary>
        public bool HasActiveWave => _activeConfig != null;

        // ─── Private types ──────────────────────────────────────
        private class EntryRuntime
        {
            public WaveEntry Entry;
            public int Spawned;
            public float Elapsed;
        }
    }
}
