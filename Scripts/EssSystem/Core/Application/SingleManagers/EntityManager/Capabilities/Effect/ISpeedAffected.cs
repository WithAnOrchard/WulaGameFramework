using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 速度被外部 Buff 修改的能力接口 —— 让 <c>SlowEffect</c> / <c>HasteEffect</c> 等不依赖
    /// 框架的 2D <see cref="Movement.IMovable"/>，也能作用在 3D 实体（如 Cubic）上。
    /// <para>
    /// <b>约定</b>：
    /// <list type="bullet">
    /// <item>默认 <c>SpeedMultiplier = 1f</c>，即不减速不加速。</item>
    /// <item>Buff 应用时记录原值、设置新值（通常 <c>orig * Multiplier</c>）。</item>
    /// <item>Buff 到期还原回原值（不是 1），避免叠加 Buff 互相踩。</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>实现者</b>：<c>CubicEntity</c>（3D 低多边形横版实体）；未来 2.5D / 3D 自定义实体都可挂此接口。
    /// 实现类在 <c>HookEntityCallbacks</c> 里调 <c>Runtime.With&lt;ISpeedAffected&gt;(this)</c> 把自身注册成该能力。
    /// </para>
    /// </summary>
    public interface ISpeedAffected : IEntityCapability
    {
        /// <summary>当前速度倍率（0=静止，1=正常，&gt;1=加速）。</summary>
        float SpeedMultiplier { get; set; }
    }
}
