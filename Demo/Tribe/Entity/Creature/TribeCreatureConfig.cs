using System;
using UnityEngine;

namespace Demo.Tribe.Entities
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

        // ─── 视觉 —— 走 CharacterManager 注册 ─────────────────
        /// <summary>对应 <c>CharacterManager.CharacterService</c> 已注册的 <c>CharacterConfig.ConfigId</c>。
        /// 每只生物的视觉（spritesheet 路径 / 帧率 / 缩放 / Y 偏移）全部存在那一份配置里。
        /// 业务侧约定：每个物种的 <c>Preset()</c> 静态方法返回前，已调用过 <c>EnsureCharacterRegistered()</c>。</summary>
        public string CharacterConfigId;

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

        // ─── 跳跃式移动（史莱姆专用）────────────────────────
        /// <summary>启用跳跃式移动 —— 关闭标准 Brain Patrol/Chase，改由 TribeSlimeHopBehavior 驱动。</summary>
        public bool UseHopMovement;

        /// <summary>正常巡游小蹦的水平速度。</summary>
        public float SmallHopHorizontal = 1.8f;

        /// <summary>正常巡游小蹦的起跳竖直速度。</summary>
        public float SmallHopVertical = 4.5f;

        /// <summary>受攻击 / 发现敌人时大蹦的水平速度（弹射玩家）。</summary>
        public float BigHopHorizontal = 5.5f;

        /// <summary>大蹦的起跳竖直速度。</summary>
        public float BigHopVertical = 9f;

        /// <summary>小蹦之间的冷却（落地后到下一次起跳）。</summary>
        public float HopCooldown = 1.5f;

        /// <summary>大蹦冷却（攻击姿势后稍歇一下）。</summary>
        public float BigHopCooldown = 0.6f;

        /// <summary>侦测玩家的半径 —— 在此范围内自动触发大蹦（朝玩家）。</summary>
        public float DetectionRange = 5f;

        /// <summary>活动范围半径（围绕出生点） —— 小蹦不会越过此距离。</summary>
        public float ActivityRadius = 10f;

        // ─── 进军目标（可选） ────────────────────────────
        /// <summary>进军目标世界 X：设为有限值时，史莱姆 SmallHop 优先朝该 X 方向移动，
        /// 直到 |x - target| ≤ <see cref="MarchArrivalThreshold"/> 后清除并切回随机巡游。
        /// 默认 NaN = 无进军行为，沿用原"出生点活动圈 + 随机"逻辑。</summary>
        public float MarchTargetX = float.NaN;

        /// <summary>到达进军目标的距离阈值。</summary>
        public float MarchArrivalThreshold = 1.5f;

        // ─── 巨大化（Skill: slime_giant）─────────────────
        /// <summary>每只史莱姆出生后一次性自动尝试 cast "slime_giant" 的概率（0..1）。
        /// 0 = 不学不放；&gt;0 = BindEntity 时学技能 + Awake 摇骰子，命中则 2~5 秒后自动施放。</summary>
        public float GiantChance;

        /// <summary>巨大化倍率（视觉 / 碰撞 / 跳跃 / 攻击力）。默认 2×。</summary>
        public float GiantScaleMultiplier = 2.0f;

        /// <summary>巨大化生命倍率（应用到 MaxHp，并把 CurrentHp 充满）。</summary>
        public float GiantHpMultiplier = 3.0f;

        /// <summary>巨大化减伤（0..1，例 0.5 = 入伤 ×0.5）。</summary>
        public float GiantDamageReduction = 0.5f;

        /// <summary>巨大化跳跃强度倍率（小蹦水平 / 竖直速度 ×；冷却 ÷）。</summary>
        public float GiantHopMultiplier = 1.5f;

        /// <summary>巨大化持续时间（秒）。</summary>
        public float GiantDuration = 12f;
    }
}
