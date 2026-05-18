using System.Collections.Generic;
using UnityEngine;

namespace Demo.Tribe.Entities
{
    /// <summary>
    /// 通用生物刷新点 —— 周期性生成 <see cref="TribeCreature"/>，维持最多 <see cref="MaxAlive"/> 只在场。
    /// <list type="bullet">
    /// <item>每 <see cref="Interval"/> 秒尝试刷新一次；若已达上限则等下一个周期。</item>
    /// <item>每次最多新增 <see cref="BatchSize"/> 只（默认 1）。</item>
    /// <item>使用弱引用列表跟踪已生成生物：被 destroy（死亡 / 卸载）后自动从计数中扣除。</item>
    /// </list>
    /// <para>本组件不做视觉，仅做生成调度；视觉标记由 <c>CreatureSpawnerFeature</c> 或外部决定。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeCreatureSpawner : MonoBehaviour
    {
        // ─── 配置 ─────────────────────────────────────────
        public TribeCreatureConfig CreatureConfig;
        public string DisplayNamePrefix = "史莱姆_";

        /// <summary>最多在场生物数（含本生成点产出的存活个体）。</summary>
        [Min(1)] public int MaxAlive = 3;

        /// <summary>刷新间隔（秒）。</summary>
        [Min(0.1f)] public float Interval = 8f;

        /// <summary>每次刷新最多生成几只（受 MaxAlive 上限约束）。</summary>
        [Min(1)] public int BatchSize = 1;

        /// <summary>初始延迟（秒），避免场景刚加载就一波怪。</summary>
        [Min(0f)] public float InitialDelay = 3f;

        /// <summary>生成时围绕生成点的水平随机偏移（±值）。</summary>
        public float HorizontalJitter = 1.5f;

        /// <summary>生成 sortingOrder（视觉层）。由 Feature 写入。</summary>
        public int SortingOrder;

        /// <summary>所有生成生物的父节点（建议 = ctx.EnemiesRoot）。</summary>
        public Transform EnemiesRoot;

        // ─── 运行时 ───────────────────────────────────────
        private readonly List<TribeCreature> _alive = new List<TribeCreature>();
        private float _nextSpawnTime;
        private int _spawnCounter;

        private void Start()
        {
            _nextSpawnTime = Time.time + InitialDelay;
        }

        private void Update()
        {
            if (CreatureConfig == null) return;

            // 清理已被 Destroy 的引用（Unity fake-null 安全）
            for (var i = _alive.Count - 1; i >= 0; i--)
            {
                if (_alive[i] == null) _alive.RemoveAt(i);
            }

            if (Time.time < _nextSpawnTime) return;
            _nextSpawnTime = Time.time + Interval;

            var room = MaxAlive - _alive.Count;
            if (room <= 0) return;
            var toSpawn = Mathf.Min(BatchSize, room);
            for (var i = 0; i < toSpawn; i++) SpawnOne();
        }

        private void SpawnOne()
        {
            var spawnerPos = transform.position;
            var jitter = Random.Range(-HorizontalJitter, HorizontalJitter);
            var pos = new Vector3(spawnerPos.x + jitter, spawnerPos.y, 0f);

            _spawnCounter++;
            var go = new GameObject($"{DisplayNamePrefix}{_spawnCounter}");
            go.transform.position = pos;
            if (EnemiesRoot != null) go.transform.SetParent(EnemiesRoot, true);

            var creature = go.AddComponent<TribeCreature>();
            creature.Configure(CreatureConfig);
            creature.SortingOrder = SortingOrder;
            _alive.Add(creature);
        }
    }
}
