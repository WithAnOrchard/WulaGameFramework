using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions
{
    /// <summary>
    /// Brain Action 共用的位移辅助 —— 让 Patrol / Chase / Flee / MoveTo 在
    /// "Dynamic Rigidbody2D"(物理) 与 "Kinematic / 无 RB"(逻辑) 两种宿主下都能正常移动。
    /// <para>
    /// 背景：<see cref="EntityService.Tick"/> 对 Dynamic Rigidbody2D 实体把
    /// <c>transform.position</c> 反向同步回 <see cref="Entity.WorldPosition"/>（防止悬浮）。
    /// 因此 Brain 直接写 WorldPosition 在 Dynamic 实体上会被立刻覆盖，表现为"原地卡死"。
    /// 本 helper 在装了 <see cref="Rigidbody2DMoverComponent"/> 时改写 <c>rb.velocity</c>，
    /// 走 Unity 物理积分；其它情况保持旧的 WorldPosition 直写路径。
    /// </para>
    /// </summary>
    internal static class BrainMoveHelper
    {
        /// <summary>沿 <paramref name="dir"/> 以 <paramref name="speed"/> 移动 <paramref name="self"/>。</summary>
        /// <param name="self">移动主体。</param>
        /// <param name="dir">方向（不必单位化；x/y 会按各自分量驱动）。</param>
        /// <param name="speed">速度（单位/秒）。</param>
        /// <param name="dt">逻辑路径用于推算位移；物理路径忽略（由 Unity 积分）。</param>
        public static void ApplyMove(Entity self, Vector2 dir, float speed, float dt)
        {
            if (self == null) return;

            var movable = self.Get<IMovable>();
            if (movable is Rigidbody2DMoverComponent rbMover)
            {
                // 物理路径：直接写 rb.velocity；横版模式保留 Y 分量（重力 / 跳跃不被抹平）
                var v = rbMover.Velocity;
                rbMover.Velocity = rbMover.SideScroller
                    ? new Vector3(dir.x * speed, v.y, 0f)
                    : new Vector3(dir.x * speed, dir.y * speed, 0f);
                return;
            }

            // 逻辑路径：直写 WorldPosition（Kinematic / 飞行 / 无 RB 实体）
            var pos = self.WorldPosition;
            pos.x += dir.x * speed * dt;
            pos.y += dir.y * speed * dt;
            self.WorldPosition = pos;
        }
    }
}
