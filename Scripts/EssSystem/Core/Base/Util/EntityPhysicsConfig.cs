using System;
using UnityEngine;

namespace EssSystem.Core.Base.Util
{
    /// <summary>
    /// Rigidbody2D + Collider2D 常用配置参数包 —— 统一"重力 + 碰撞检测 + 约束"的重复初始化代码。
    /// <para><see cref="TribePlayerMovement"/> 和 <see cref="TribeSkeletonEnemy"/> 都有几乎相同的
    /// <c>ConfigureRigidbody()</c>，本结构将字段提取为可序列化数据，用一行 <see cref="ApplyTo"/> 完成配置。</para>
    /// </summary>
    [Serializable]
    public struct EntityPhysicsConfig
    {
        [Tooltip("启用重力时 gravityScale 值。")]
        public float GravityScale;

        [Tooltip("线性阻力。")]
        public float LinearDrag;

        [Tooltip("是否使用重力（false = Kinematic + isTrigger）。")]
        public bool UseGravity;

        [Tooltip("额外冻结 X 位置（典型：怪物防推）。")]
        public bool FreezePositionX;

        /// <summary>预设：横版 Dynamic 角色。</summary>
        public static EntityPhysicsConfig SideScroller(float gravityScale = 5f, float linearDrag = 0f, bool freezeX = false)
        {
            return new EntityPhysicsConfig
            {
                GravityScale = gravityScale,
                LinearDrag = linearDrag,
                UseGravity = true,
                FreezePositionX = freezeX,
            };
        }

        /// <summary>预设：俯视/无重力 Kinematic 角色。</summary>
        public static EntityPhysicsConfig TopDown()
        {
            return new EntityPhysicsConfig
            {
                GravityScale = 0f,
                LinearDrag = 0f,
                UseGravity = false,
                FreezePositionX = false,
            };
        }

        /// <summary>
        /// 一次性应用到 Rigidbody2D。
        /// <para>总是设 Continuous + Interpolate + FreezeRotation；重力关闭时切 Kinematic。</para>
        /// </summary>
        public void ApplyTo(Rigidbody2D rb)
        {
            if (rb == null) return;
            rb.gravityScale = UseGravity ? GravityScale : 0f;
            rb.bodyType = UseGravity ? RigidbodyType2D.Dynamic : RigidbodyType2D.Kinematic;
            rb.drag = LinearDrag;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var c = RigidbodyConstraints2D.FreezeRotation;
            if (UseGravity && FreezePositionX) c |= RigidbodyConstraints2D.FreezePositionX;
            rb.constraints = c;
        }

        /// <summary>同时配置 Collider2D 的 isTrigger（无重力 = trigger）。</summary>
        public void ApplyTo(Rigidbody2D rb, Collider2D col)
        {
            ApplyTo(rb);
            if (col != null) col.isTrigger = !UseGravity;
        }
    }
}
