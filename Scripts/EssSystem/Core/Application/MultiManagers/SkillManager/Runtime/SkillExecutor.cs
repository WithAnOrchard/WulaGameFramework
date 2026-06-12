using UnityEngine;
namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao
{
    /// <summary>
    /// 技能执行器 —— 管理单次技能释放的完整生命周期。
    /// <para>阶段：Idle → Casting（前摇）→ Execute（命中/效果）→ [Channeling（引导，按间隔重复触发效果）] → Recovery（后摇）→ Done</para>
    /// </summary>
    public class SkillExecutor
    {
        public enum Phase { Idle, Casting, Execute, Channeling, Recovery, Done }

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsActive => CurrentPhase != Phase.Idle && CurrentPhase != Phase.Done;

        private SkillEffectContext _ctx;
        private float _timer;
        private float _channelTickTimer;

        /// <summary>
        /// 开始执行技能。
        /// </summary>
        /// <returns>true = 成功启动，false = 条件不满足。</returns>
        public bool Begin(SkillEffectContext ctx)
        {
            if (ctx?.Definition == null || string.IsNullOrEmpty(ctx.CasterId)) return false;
            if (ctx.Instance != null && !ctx.Instance.IsReady) return false;

            _ctx = ctx;
            TriggerCastStartEffects();

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

            // Channeling 是"软计时" —— 内部还要按 ChannelTickInterval 重复触发效果
            if (CurrentPhase == Phase.Channeling)
            {
                _timer -= deltaTime;
                _channelTickTimer += deltaTime;
                var interval = _ctx?.Definition?.ChannelTickInterval ?? 0f;
                if (interval > 0f && _channelTickTimer >= interval)
                {
                    _channelTickTimer -= interval;
                    ApplyEffectChain();
                }
                if (_timer <= 0f) EnterRecoveryOrDone();
                return;
            }

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

        /// <summary>打断当前施法（被击退/眩晕等）。Casting / Channeling 均可被打断。</summary>
        public void Interrupt()
        {
            if (CurrentPhase == Phase.Casting || CurrentPhase == Phase.Channeling)
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
            ApplyEffectChain();
            _ctx?.Instance?.StartCooldown();

            // 引导施法：Execute 之后进入 Channeling，按间隔反复触发效果
            if (_ctx?.Definition != null && _ctx.Definition.ChannelTime > 0f)
            {
                CurrentPhase = Phase.Channeling;
                _timer = _ctx.Definition.ChannelTime;
                _channelTickTimer = 0f;
                return;
            }

            EnterRecoveryOrDone();
        }

        private void EnterRecoveryOrDone()
        {
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

        /// <summary>执行一次效果链（Execute 阶段调用一次，Channeling 阶段每 tick 复用）。</summary>
        private void ApplyEffectChain()
        {
            if (_ctx?.Definition?.Effects == null) return;
            foreach (var effect in _ctx.Definition.Effects)
            {
                try { effect.Apply(_ctx); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SkillExecutor] 效果执行异常: {e.Message}");
                }
            }
        }

        private void TriggerCastStartEffects()
        {
            if (_ctx?.Definition?.Effects == null) return;
            foreach (var effect in _ctx.Definition.Effects)
            {
                if (effect is not ISkillCastStartEffect startEffect) continue;
                try { startEffect.OnCastStart(_ctx); }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[SkillExecutor] 施法起手效果异常: {e.Message}");
                }
            }
        }
    }
}
