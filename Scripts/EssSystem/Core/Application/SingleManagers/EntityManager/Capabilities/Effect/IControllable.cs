using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 控制状态能力 —— 表示实体当前是否受眩晕 / 沉默 / 缴械等控制效果影响。
    /// <para>
    /// 框架内置消费方：
    /// <list type="bullet">
    /// <item><see cref="MovableComponent"/> / <see cref="Rigidbody2DMoverComponent"/> 在 <c>Move()</c> 时检测 <see cref="Stunned"/>，
    ///   命中则当帧 velocity 强制归零（横版保留重力 Y）。</item>
    /// <item><c>SkillService.CastSkill</c> 检测 <see cref="Silenced"/>，命中则拒绝施法。</item>
    /// </list>
    /// </para>
    /// <para>典型实现 <see cref="ControllableComponent"/> 提供"叠加计数"语义 ——
    /// 多个 Stun Buff 同时存在时只要计数 &gt; 0 就 Stunned；每个 Buff 在 OnExpire 时减一。</para>
    /// </summary>
    public interface IControllable : IEntityCapability
    {
        /// <summary>是否被眩晕（无法移动 / 无法主动行动）。</summary>
        bool Stunned { get; }

        /// <summary>是否被沉默（无法施放技能）。</summary>
        bool Silenced { get; }

        /// <summary>压入一层 Stun（计数 +1）。</summary>
        void PushStun();

        /// <summary>弹出一层 Stun（计数 -1，下限 0）。</summary>
        void PopStun();

        /// <summary>压入一层 Silence（计数 +1）。</summary>
        void PushSilence();

        /// <summary>弹出一层 Silence（计数 -1，下限 0）。</summary>
        void PopSilence();
    }
}
