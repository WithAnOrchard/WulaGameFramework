using System;

namespace EssSystem.Core.Application.SingleManagers.SceneInstanceManager.Dao
{
    /// <summary>
    /// 子场景行为约束 —— 玩家进入时由 SceneInstanceService 应用到 Instance 内部。
    /// </summary>
    [Serializable]
    public class InstanceRules
    {
        /// <summary>是否禁止敌人 spawn（safe 主题默认 true）。</summary>
        public bool DisableEnemySpawn;

        /// <summary>是否强制和平区（敌人 AI 锁定 / 不主动攻击）。</summary>
        public bool ForceFriendly;

        /// <summary>玩家在场内的每秒额外 HP 回复（≤0 = 无）。</summary>
        public float HpRegenPerSec;

        /// <summary>是否锁定昼夜时间（接未来 TimeOfDayManager）。</summary>
        public bool LockTimeOfDay;

        /// <summary>所有玩家离开多久后自动进入低频休眠（&lt;0 = 永不休眠，0 = 立即）。</summary>
        public float HibernateAfterEmptySeconds = 60f;
    }
}
