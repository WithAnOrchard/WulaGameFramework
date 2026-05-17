using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain
{
    /// <summary>
    /// <see cref="IBrain"/> 默认实现 —— Utility AI 调度器。
    /// <para>
    /// 决策循环：
    /// <list type="number">
    /// <item>按 <see cref="SensorInterval"/> 周期刷新所有 Sensor</item>
    /// <item>按 <see cref="DecisionInterval"/> 周期对所有 Consideration 打分</item>
    /// <item>最高分者（超过当前行为 + <see cref="InertiaBonus"/>）触发 Action 切换</item>
    /// <item>当前 Action 每帧 Tick 直到 Success/Failure</item>
    /// </list>
    /// </para>
    /// </summary>
    public class BrainComponent : IBrain
    {
        // ─── 配置 ─────────────────────────────────────────────────
        /// <summary>决策重评估间隔（秒）。0 = 每帧评估。</summary>
        public float DecisionInterval { get; set; } = 0.3f;

        /// <summary>感知刷新间隔（秒）。0 = 每帧。</summary>
        public float SensorInterval { get; set; } = 0.4f;

        /// <summary>防抖惯性 —— 当前行为享受的额外加分，避免频繁切换。</summary>
        public float InertiaBonus { get; set; } = 0.1f;

        /// <summary>分数低于此阈值的 Consideration 不参与竞争。</summary>
        public float MinScoreThreshold { get; set; } = 0.01f;

        // ─── IBrain 接口 ──────────────────────────────────────────
        public BrainContext Context { get; private set; }
        public bool Enabled { get; set; } = true;
        public IBrainAction CurrentAction { get; private set; }
        public string CurrentConsiderationId { get; private set; }
        public IReadOnlyList<Consideration> Considerations => _considerations;

        // ─── 内部状态 ─────────────────────────────────────────────
        private readonly List<Consideration> _considerations = new();
        private readonly List<ISensor> _sensors = new();
        private Entity _owner;
        private Consideration _currentConsideration;
        private float _decisionTimer;
        private float _sensorTimer;

        // ─── 构建 API ─────────────────────────────────────────────

        /// <summary>添加候选行为。</summary>
        public BrainComponent Add(Consideration consideration)
        {
            if (consideration != null) _considerations.Add(consideration);
            return this;
        }

        /// <summary>添加感知器。</summary>
        public BrainComponent AddSensor(ISensor sensor)
        {
            if (sensor != null) _sensors.Add(sensor);
            return this;
        }

        /// <summary>
        /// 内置巡逻行为 —— 在没有异常状态（无威胁、需求不紧迫）时作为默认行为执行。
        /// <para>
        /// 相当于自动添加一个低分 Consideration（基线 <paramref name="baseScore"/>），
        /// 只要其它行为没有超过它就会持续巡逻。替代独立 <see cref="IPatrol"/> 的功能。
        /// </para>
        /// </summary>
        /// <param name="speed">巡逻移动速度。</param>
        /// <param name="distance">从起始点最大偏移距离。</param>
        /// <param name="baseScore">基线分数（默认 0.2，确保是最低优先级）。</param>
        public BrainComponent WithPatrol(float speed, float distance, float baseScore = 0.2f)
        {
            _considerations.Add(new Consideration
            {
                Id = "Patrol",
                Score = _ => baseScore,
                CreateAction = _ => new PatrolAction(speed, distance)
            });
            return this;
        }

        // ─── IEntityCapability ────────────────────────────────────
        public void OnAttach(Entity owner)
        {
            _owner = owner;
            Context = new BrainContext { Self = owner };

            // 挂 Brain 时自动移除 IPatrol（互斥）
            if (owner.Has<IPatrol>()) owner.Remove<IPatrol>();

            // 监听受伤事件 → 记录威胁来源
            var dmg = owner.Get<IDamageable>();
            if (dmg != null) dmg.Damaged += OnOwnerDamaged;
        }

        public void OnDetach(Entity owner)
        {
            // 退出当前 Action
            if (CurrentAction != null)
            {
                try { CurrentAction.OnExit(Context); } catch { }
                CurrentAction = null;
            }

            var dmg = owner?.Get<IDamageable>();
            if (dmg != null) dmg.Damaged -= OnOwnerDamaged;

            _owner = null;
            _currentConsideration = null;
            CurrentConsiderationId = null;
        }

        // ─── ITickableCapability ──────────────────────────────────
        public void Tick(float deltaTime)
        {
            if (_owner == null || !Enabled) return;

            // 1) Sensor 降频刷新
            _sensorTimer -= deltaTime;
            if (_sensorTimer <= 0f)
            {
                _sensorTimer = SensorInterval;
                RefreshSensors();
            }

            // 2) 冷却衰减
            for (var i = 0; i < _considerations.Count; i++)
            {
                if (_considerations[i].CooldownRemaining > 0f)
                    _considerations[i].CooldownRemaining -= deltaTime;
            }

            // 3) 决策降频评估
            _decisionTimer -= deltaTime;
            if (_decisionTimer <= 0f)
            {
                _decisionTimer = DecisionInterval;
                Evaluate();
            }

            // 4) 当前 Action 执行
            if (CurrentAction != null)
            {
                var status = CurrentAction.Tick(Context, deltaTime);
                if (status != BrainStatus.Running)
                {
                    FinishCurrentAction();
                }
            }
        }

        // ─── 内部逻辑 ─────────────────────────────────────────────

        private void RefreshSensors()
        {
            Context.ClearPerception();
            for (var i = 0; i < _sensors.Count; i++)
            {
                _sensors[i].Sense(Context);
            }

            // 更新威胁距离
            if (Context.ThreatSource != null && _owner.CharacterRoot != null)
            {
                var threatPos = Context.ThreatSource.CharacterRoot != null
                    ? Context.ThreatSource.CharacterRoot.position
                    : Context.ThreatSource.WorldPosition;
                Context.DistanceToThreat = Vector3.Distance(_owner.CharacterRoot.position, threatPos);
            }
        }

        private void Evaluate()
        {
            Consideration best = null;
            var bestScore = MinScoreThreshold;

            for (var i = 0; i < _considerations.Count; i++)
            {
                var c = _considerations[i];
                if (c.CooldownRemaining > 0f || c.Score == null) continue;

                var score = Mathf.Clamp01(c.Score(Context));
                if (score <= 0f) continue;

                // 当前行为享受惯性加分
                if (c == _currentConsideration)
                    score += InertiaBonus;

                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            // 如果胜出者与当前相同，不切换
            if (best == _currentConsideration) return;

            // 需要切换
            if (best != null && best != _currentConsideration)
            {
                SwitchTo(best);
            }
            else if (best == null && _currentConsideration != null)
            {
                // 无行为达标，停止当前
                FinishCurrentAction();
            }
        }

        private void SwitchTo(Consideration next)
        {
            // 退出旧 Action
            if (CurrentAction != null)
            {
                try { CurrentAction.OnExit(Context); } catch { }
            }

            _currentConsideration = next;
            CurrentConsiderationId = next.Id;

            // 创建新 Action
            CurrentAction = next.CreateAction?.Invoke(Context);
            if (CurrentAction != null)
            {
                try { CurrentAction.OnEnter(Context); } catch { }
            }
        }

        private void FinishCurrentAction()
        {
            if (CurrentAction != null)
            {
                try { CurrentAction.OnExit(Context); } catch { }
                CurrentAction = null;
            }

            // 应用冷却
            if (_currentConsideration != null && _currentConsideration.Cooldown > 0f)
            {
                _currentConsideration.CooldownRemaining = _currentConsideration.Cooldown;
            }

            _currentConsideration = null;
            CurrentConsiderationId = null;

            // 立即重新评估
            _decisionTimer = 0f;
        }

        private void OnOwnerDamaged(Entity owner, Entity source, float dealt, string damageType)
        {
            if (source != null) Context.ThreatSource = source;
        }
    }
}
