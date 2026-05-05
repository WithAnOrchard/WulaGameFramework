namespace EssSystem.EssManager.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 不可被攻击标记 —— 存在此能力的 Entity 永远免疫伤害（即便同时挂了 <see cref="IDamageable"/>）。
    /// <para>
    /// 业务在伤害结算流水线的最顶部应统一检查：若 target 有 <c>IInvulnerable</c>，直接 short-circuit，
    /// 不调用 <c>IDamageable.TakeDamage</c>、不触发受伤事件。
    /// </para>
    /// <para>
    /// 典型用途：剧情无敌、训练场假人、场景不可破坏物、Boss 特定阶段的霸体帧。
    /// </para>
    /// </summary>
    public interface IInvulnerable : IEntityCapability
    {
        /// <summary>是否当前生效（可为 false 模拟"限时无敌"；业务伤害结算应读这个字段）。</summary>
        bool Active { get; }

        /// <summary>无敌来源标签（调试 / 伤害 Log 展示用；如 "ScriptedCutscene" / "BuffGodMode"）。</summary>
        string Reason { get; }
    }
}
