using System;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.SkillManager.Dao.Buffs
{
    /// <summary>
    /// Buff 运行时实例 —— 挂在某个 Entity 上的一个 Buff 效果。
    /// <para>由 <see cref="SkillService"/> 统一管理生命周期和 Tick。</para>
    /// </summary>
    public class BuffInstance
    {
        /// <summary>Buff 唯一标识（如 "burn", "speed_up"）。</summary>
        public string BuffId;

        /// <summary>Buff 来源实体。</summary>
        public Entity Source;

        /// <summary>Buff 附着的目标实体。</summary>
        public Entity Target;

        /// <summary>总持续时间（秒）。</summary>
        public float Duration;

        /// <summary>剩余时间（秒）。</summary>
        public float Remaining;

        /// <summary>Buff 是否已过期。</summary>
        public bool IsExpired => Duration > 0f && Remaining <= 0f;

        /// <summary>每次 Tick 执行的效果（如持续伤害/回复）。</summary>
        public Action<BuffInstance, float> OnTick;

        /// <summary>Buff 结束时回调（清理修改的属性等）。</summary>
        public Action<BuffInstance> OnExpire;

        /// <summary>Tick 间隔（秒）。</summary>
        public float TickInterval = 1f;

        /// <summary>内部计时器。</summary>
        internal float TickTimer;

        /// <summary>推进 Buff 计时。由 SkillService 调用。</summary>
        public void Tick(float deltaTime)
        {
            if (IsExpired) return;

            if (Duration > 0f)
                Remaining -= deltaTime;

            if (OnTick != null)
            {
                TickTimer += deltaTime;
                if (TickTimer >= TickInterval)
                {
                    TickTimer -= TickInterval;
                    OnTick(this, deltaTime);
                }
            }
        }
    }
}
