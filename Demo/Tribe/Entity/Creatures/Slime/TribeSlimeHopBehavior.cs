using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace Demo.Tribe.Entities
{
    /// <summary>
    /// 史莱姆跳跃行为 —— 替代标准 Brain 巡游。
    /// <para>
    /// <b>小蹦（正常移动）</b>：本组件自驱动；冷却到期 + 在地上 + 大蹦技能不可用时执行。
    /// 朝随机方向起跳，不会越出 <see cref="TribeCreatureConfig.ActivityRadius"/>。
    /// </para>
    /// <para>
    /// <b>大蹦（特殊攻击）</b>：完全交给 <see cref="SkillManager"/>。
    /// 本组件在玩家进入侦测范围时调用 <c>EVT_CAST_SKILL</c> 触发 "slime_big_hop"；
    /// 技能冷却 / 命中由 SkillService 维护，效果实现走框架通用 <c>DashEffect</c>（参 <see cref="Skills.Slime"/>）。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeSlimeHopBehavior : MonoBehaviour
    {
        // ─── 配置 / 引用 ─────────────────────────────────
        private TribeCreatureConfig _config;
        private Entity _entity;
        private IDamageable _damageable;
        private Rigidbody2D _rb;
        private Collider2D _collider;

        private Transform _player;          // 缓存的玩家引用（懒查找）
        private Vector3 _anchor;            // 出生点（活动半径以此为中心）
        private float _nextSmallHopTime;    // 小蹦冷却到期时间（Time.time 比较）
        private float _nextBigHopAttemptTime; // 大蹦技能下次尝试 cast 的最早时间（避免每帧调 SkillManager）
        private bool _learned;              // 是否已通过 SkillManager 学会大蹦
        private bool _bound;
        private bool _marchDone;            // 进军目标已到达（实例本地，不污染 preset 共享 config）
        private float _giantCastTime = -1f; // 自动巨大化触发时间（>0 = 已 roll 命中等待施放；-1 = 不施放）

        public void Configure(TribeCreatureConfig config) => _config = config;

        /// <summary>
        /// 运行时替换 config 引用（Buff 用）—— 例如 "巨大化" Buff 把 SmallHopHorizontal/Vertical/HopCooldown
        /// 替换成放大后的副本。Buff 结束时再 Reconfigure 回原 config。
        /// </summary>
        public void Reconfigure(TribeCreatureConfig newConfig)
        {
            if (newConfig != null) _config = newConfig;
        }

        /// <summary>当前生效 config（Buff 系统读取原值用）。</summary>
        public TribeCreatureConfig CurrentConfig => _config;

        public void BindEntity(Entity entity)
        {
            _entity = entity;
            if (_entity == null) return;
            _damageable = _entity.Get<IDamageable>();
            if (_damageable != null && !_bound)
            {
                _damageable.Damaged += OnDamaged;
                _bound = true;
            }

            // 注册技能定义（幂等）+ 学技能
            Slime.EnsureSkillsRegistered();
            if (!string.IsNullOrEmpty(_entity.InstanceId) && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEventMethod(SkillManager.EVT_LEARN_SKILL,
                    new List<object> { _entity.InstanceId, Slime.SKILL_BIG_HOP });
                _learned = true;

                // 巨大化：按 config.GiantChance 摇骰；命中则学技能 + 2~5 秒后自动施放一次
                if (_config != null && _config.GiantChance > 0f)
                {
                    EventProcessor.Instance.TriggerEventMethod(SkillManager.EVT_LEARN_SKILL,
                        new List<object> { _entity.InstanceId, Slime.SKILL_GIANT });
                    if (Random.value <= _config.GiantChance)
                        _giantCastTime = Time.time + Random.Range(2f, 5f);
                }
            }
        }

        // ─── 生命周期 ──────────────────────────────────
        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _anchor = transform.position;
            _nextSmallHopTime = Time.time + Random.Range(0.4f, 1.2f);
        }

        private void OnDestroy()
        {
            if (_bound && _damageable != null) _damageable.Damaged -= OnDamaged;
        }

        private void FixedUpdate()
        {
            if (_config == null || _rb == null) return;
            if (_damageable != null && _damageable.IsDead) return;

            var grounded = IsGrounded();

            // 落地后清掉水平残余速度（drag=0 + 无摩擦 → 否则会一直滑）。
            // 不动 vy，让重力继续维持贴地。
            if (grounded && Mathf.Abs(_rb.linearVelocity.x) > 0.05f)
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            // 落地才能继续决策（空中不再做任何动作）
            if (!grounded) return;

            // ① 巨大化（自我增益）：摇骰命中且时间到 → 落地后立即施放一次
            if (TryCastGiant()) return;

            // ② 优先尝试大蹦技能（冷却由 SkillManager 控制；不 ready 则静默失败）
            if (TryCastBigHop()) return;

            // ② 否则按小蹦节奏巡游
            if (Time.time < _nextSmallHopTime) return;
            DoSmallHop();
        }

        // ─── 小蹦（自驱动） ──────────────────────────────
        private void DoSmallHop()
        {
            var direction = ResolveSmallHopDirection();
            var horizontal = _config.SmallHopHorizontal;
            var vertical = _config.SmallHopVertical;
            var cooldown = _config.HopCooldown + Random.Range(-0.2f, 0.3f);

            _rb.linearVelocity = new Vector2(direction * horizontal, vertical);

            // 朝向同步：走 CharacterManager（sheet 4×4 模式下按行挑选帧序列）。
            // 注：UseHopMovement 的史莱姆没有 Brain，TribeCreature.Update 不会推 Direction，必须在此驱动。
            if (direction != 0 && _entity != null && !string.IsNullOrEmpty(_entity.InstanceId))
                CharacterViewBridge.SetDirection(_entity.InstanceId, direction);

            _nextSmallHopTime = Time.time + Mathf.Max(0.25f, cooldown);
        }

        // ─── 巨大化（走 SkillManager / 自身 Buff）────────
        /// <summary>到点后施放一次 slime_giant；成功 / 失败都清除标记，避免反复重试。</summary>
        private bool TryCastGiant()
        {
            if (_giantCastTime < 0f || Time.time < _giantCastTime) return false;
            _giantCastTime = -1f; // 一次性：无论成败都不再尝试

            if (_entity == null || !EventProcessor.HasInstance) return false;
            var result = EventProcessor.Instance.TriggerEventMethod(
                SkillManager.EVT_CAST_SKILL,
                new List<object> { _entity, Slime.SKILL_GIANT, null, Vector3.zero, transform.position });
            var ok = result != null && result.Count > 0 && (object)result[0] == EssSystem.Core.Base.Util.ResultCode.OK;
            // 巨大化后稍歇，避免落地立刻小蹦盖过 buff 的"震慑感"
            if (ok) _nextSmallHopTime = Time.time + 0.6f;
            return ok;
        }

        // ─── 大蹦（走 SkillManager）──────────────────────
        /// <summary>尝试通过 SkillManager 释放大蹦。技能不可用 / 玩家不在范围内时返回 false。
        /// 成功 cast 时让小蹦冷却也跟着推后，避免技能命中后立刻又小蹦。</summary>
        private bool TryCastBigHop()
        {
            if (!_learned || _entity == null) return false;
            if (!EventProcessor.HasInstance) return false;
            if (Time.time < _nextBigHopAttemptTime) return false;

            EnsurePlayerRef();
            if (_player == null) return false;

            // 距离玩家在 BigHopRange 之内才尝试 cast
            var dx = _player.position.x - transform.position.x;
            var dy = _player.position.y - transform.position.y;
            var rangeSq = Slime.BigHopRange * Slime.BigHopRange;
            if (dx * dx + dy * dy > rangeSq)
            {
                _nextBigHopAttemptTime = Time.time + 0.5f;   // 远离时不必每帧再算
                return false;
            }

            var dirX = dx > 0f ? 1f : -1f;
            var direction = new Vector3(dirX, 0f, 0f);

            var result = EventProcessor.Instance.TriggerEventMethod(
                SkillManager.EVT_CAST_SKILL,
                new List<object> { _entity, Slime.SKILL_BIG_HOP, null, direction, transform.position });

            // SkillService 在冷却中会返回 Fail —— 安静处理：稍后再问一次
            var ok = result != null && result.Count > 0 && (object)result[0] == EssSystem.Core.Base.Util.ResultCode.OK;
            if (ok)
            {
                // 大蹦后让小蹦也歇一会儿，避免落地立刻再小蹦
                _nextSmallHopTime = Time.time + Mathf.Max(1.2f, _config.HopCooldown);
                _nextBigHopAttemptTime = Time.time + 0.5f;
                return true;
            }
            _nextBigHopAttemptTime = Time.time + 0.5f;
            return false;
        }

        // ─── 方向决策 ──────────────────────────────────
        private int ResolveSmallHopDirection()
        {
            // 玩家在大蹦侦测范围内时优先朝玩家继续小蹦（避免大蹦冲过去后"撤回 anchor"的迷惑动作）
            EnsurePlayerRef();
            if (_player != null)
            {
                var pdx = _player.position.x - transform.position.x;
                var pdy = _player.position.y - transform.position.y;
                var rangeSq = Slime.BigHopRange * Slime.BigHopRange;
                if (pdx * pdx + pdy * pdy <= rangeSq && Mathf.Abs(pdx) > 0.05f)
                    return pdx > 0f ? 1 : -1;
            }

            // 进军目标：未到达前一直朝目标 X 蹦（优先级高于活动圈 / 随机）。
            // _marchDone 用本地 flag，避免修改 preset 共享 config（同一 spawner 后续生成的史莱姆仍要进军）。
            if (!_marchDone && !float.IsNaN(_config.MarchTargetX))
            {
                var mdx = _config.MarchTargetX - transform.position.x;
                if (Mathf.Abs(mdx) > _config.MarchArrivalThreshold)
                    return mdx > 0f ? 1 : -1;
                // 已到达：把 anchor 改到当前点，让活动圈逻辑围绕营地附近巡游
                _anchor = transform.position;
                _marchDone = true;
            }

            // 否则启用活动圈回弹：超出 ActivityRadius * 0.85 时朝 anchor 蹦回来
            var dx = transform.position.x - _anchor.x;
            if (Mathf.Abs(dx) > _config.ActivityRadius * 0.85f)
                return dx > 0f ? -1 : 1;

            // 否则随机左右；偶尔原地蹦（direction = 0 → 仅垂直起跳）
            var roll = Random.value;
            if (roll < 0.15f) return 0;
            return Random.value < 0.5f ? -1 : 1;
        }

        // ─── 探测 ──────────────────────────────────────
        private void EnsurePlayerRef()
        {
            if (_player != null) return;
            try
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) { _player = tagged.transform; return; }
            }
            catch { /* 无 Player tag 时 FindGameObjectWithTag 抛异常 */ }
            var go = GameObject.Find("TribePlayer") ?? GameObject.Find("Player");
            if (go != null) _player = go.transform;
        }

        private bool IsGrounded()
        {
            // 短射线 + 圆形重叠组合：射线判断"脚下有东西"，并且竖直速度近 0 才算稳稳着地
            if (Mathf.Abs(_rb.linearVelocity.y) > 0.5f) return false;
            var origin = (Vector2)transform.position + Vector2.down * (_config.ColliderRadius * 0.5f);
            var hit = Physics2D.Raycast(origin, Vector2.down,
                _config.ColliderRadius + 0.15f, ~(1 << gameObject.layer));
            return hit.collider != null;
        }

        // ─── 受击回调 ──────────────────────────────────
        // 受击时仅缩短下次大蹦尝试间隔；冷却没好就走小蹦，避免被打了还杵着原地。
        private void OnDamaged(Entity self, Entity source, float dealt, string damageType)
        {
            _nextBigHopAttemptTime = 0f;
            _nextSmallHopTime = Mathf.Min(_nextSmallHopTime, Time.time + 0.2f);
        }
    }
}
