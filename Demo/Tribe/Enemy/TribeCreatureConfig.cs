using System;
using UnityEngine;

namespace Demo.Tribe.Enemy
{
    /// <summary>
    /// 生物配置 —— 纯数据，描述一个可生成的动物或怪物的所有参数。
    /// <list type="bullet">
    /// <item><b>动物</b>：<see cref="CanAttack"/> = false，无接触伤害</item>
    /// <item><b>怪物</b>：<see cref="CanAttack"/> = true，有接触伤害 + 血条</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class TribeCreatureConfig
    {
        // ─── 标识 ─────────────────────────────────────────────
        public string Id;
        public string DisplayName;

        // ─── 动画资源路径（Resources 下） ──────────────────────
        public string IdleResourcePath;
        public string WalkResourcePath;

        // ─── 视觉 ─────────────────────────────────────────────
        public float VisualScale = 10f;
        public float VisualYOffset = 0f;
        public float FrameTime = 0.1f;
        public SpritePivot Pivot = SpritePivot.Center;

        // ─── 物理 ─────────────────────────────────────────────
        public bool UseGravity = true;
        public float GravityScale = 5f;
        public float ColliderRadius = 0.45f;
        public bool FreezePositionX = true;

        // ─── 数值 ─────────────────────────────────────────────
        public float MaxHp = 10f;
        public float MoveSpeed = 1.2f;
        public float PatrolDistance = 2.5f;

        // ─── 战斗 ─────────────────────────────────────────────
        public bool CanAttack;
        public float ContactDamage = 8f;
        public float DamageCooldown = 1f;

        // ─── 闪烁 / 击退 ──────────────────────────────────────
        public bool EnableFlash = true;
        public float FlashDuration = 0.15f;
        public Color FlashColor = Color.white;
        public bool EnableKnockback = true;
        public float KnockbackForce = 15f;

        // ─── 掉落 ─────────────────────────────────────────────
        public string DropPickableId;
        public int DropAmount;
    }
}
