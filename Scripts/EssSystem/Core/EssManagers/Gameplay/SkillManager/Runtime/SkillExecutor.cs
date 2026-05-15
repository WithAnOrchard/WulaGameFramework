using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.SkillManager.Dao
{
    /// <summary>
    /// 技能执行器 —— 管理单次技能释放的完整生命周期。
    /// <para>阶段：Idle → Casting（前摇）→ Execute（命中/效果）→ Recovery（后摇）→ Done</para>
    /// </summary>
    public class SkillExecutor
    {
        public enum Phase { Idle, Casting, Execute, Recovery, Done }

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsActive => CurrentPhase != Phase.Idle && CurrentPhase != Phase.Done;

        private SkillEffectContext _ctx;
        private float _timer;

        /// <summary>
        /// 开始执行技能。
        /// </summary>
        /// <returns>true = 成功启动，false = 条件不满足。</returns>
        public bool Begin(SkillEffectContext ctx)
        {
            if (ctx?.Definition == null || ctx.Caster == null) return false;
            if (ctx.Instance != null && !ctx.Instance.IsReady) return false;

            _ctx = ctx;

            // 前摇
            if (_ctx.Definition.CastTime > 0f)
            {
                CurrentPhase = Phase.Casting;
                _timer = _ctx.Definition.CastTime;
            }
            else
            {
                // 无前摇，直接执行
                ExecuteEffects();
            }
            return true;
        }

        /// <summary>每帧推进。</summary>
        public void Tick(float deltaTime)
        {
            if (!IsActive) return;

            _timer -= deltaTime;
            if (_timer > 0f) return;

            switch (CurrentPhase)
            {
                case Phase.Casting:
                    ExecuteEffects();
                    break;
                case Phase.Recovery:
                    CurrentPhase = Phase.Done;
                    break;
            }
        }

        /// <summary>打断当前施法（被击退/眩晕等）。</summary>
        public void Interrupt()
        {
            if (CurrentPhase == Phase.Casting)
            {
                CurrentPhase = Phase.Done;
                _ctx = null;
            }
        }

        /// <summary>重置为空闲状态（可复用）。</summary>
        public void Reset()
        {
            CurrentPhase = Phase.Idle;
            _timer = 0f;
            _ctx = null;
        }

        private void ExecuteEffects()
        {
            CurrentPhase = Phase.Execute;

            // 执行效果链
            if (_ctx?.Definition?.Effects != null)
            {
                foreach (var effect in _ctx.Definition.Effects)
                {
                    try { effect.Apply(_ctx); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"[SkillExecutor] 效果执行异常: {e.Message}");
                    }
                }
            }

            // 开始冷却
            _ctx?.Instance?.StartCooldown();

            // 后摇
            if (_ctx?.Definition != null && _ctx.Definition.RecoveryTime > 0f)
            {
                CurrentPhase = Phase.Recovery;
                _timer = _ctx.Definition.RecoveryTime;
            }
            else
            {
                CurrentPhase = Phase.Done;
            }
        }
    }
}
