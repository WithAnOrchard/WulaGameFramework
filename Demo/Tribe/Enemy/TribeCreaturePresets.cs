using UnityEngine;

namespace Demo.Tribe.Enemy
{
    /// <summary>
    /// 预定义生物配置 —— 所有动物和怪物的参数集中管理。
    /// <para>动物：<see cref="CanAttack"/> = false，不带接触伤害和血条。</para>
    /// <para>怪物：<see cref="CanAttack"/> = true，带接触伤害和血条。</para>
    /// </summary>
    public static class TribeCreaturePresets
    {
        // ═══════════════════════════════════════════════════════
        //  动物（Animals）—— 不攻击，无伤害
        // ═══════════════════════════════════════════════════════

        public static TribeCreatureConfig Cow() => new TribeCreatureConfig
        {
            Id = "cow", DisplayName = "奶牛",
            IdleResourcePath = "Tribe/Entity/Cow_idle (20x16)",
            WalkResourcePath = "Tribe/Entity/Cow_walk (20x16)",
            VisualScale = 10f, VisualYOffset = 0f,
            FrameTime = 0.15f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.45f, FreezePositionX = true,
            MaxHp = 20f, MoveSpeed = 0.6f, PatrolDistance = 3f,
            CanAttack = true, ContactDamage = 12f, DamageCooldown = 1.2f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 6f,
        };

        public static TribeCreatureConfig Hen() => new TribeCreatureConfig
        {
            Id = "hen", DisplayName = "母鸡",
            IdleResourcePath = "Tribe/Entity/Hen_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Hen_walk (16x16)",
            VisualScale = 8f, VisualYOffset = 0f,
            FrameTime = 0.12f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.3f, FreezePositionX = true,
            MaxHp = 3f, MoveSpeed = 0.8f, PatrolDistance = 2f,
            CanAttack = false,
        };

        public static TribeCreatureConfig Pig() => new TribeCreatureConfig
        {
            Id = "pig", DisplayName = "小猪",
            IdleResourcePath = "Tribe/Entity/Pig_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Pig_walk (16x16)",
            VisualScale = 8f, VisualYOffset = 0f,
            FrameTime = 0.12f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.35f, FreezePositionX = true,
            MaxHp = 5f, MoveSpeed = 0.7f, PatrolDistance = 2.5f,
            CanAttack = false,
        };

        // ═══════════════════════════════════════════════════════
        //  怪物（Monsters）—— 有攻击、接触伤害、血条
        // ═══════════════════════════════════════════════════════

        public static TribeCreatureConfig Skeleton() => new TribeCreatureConfig
        {
            Id = "skeleton", DisplayName = "骷髅",
            IdleResourcePath = "Tribe/Entity/Skeleton 01_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Skeleton 01_walk (16x16)",
            VisualScale = 10f, VisualYOffset = 0f,
            FrameTime = 0.1f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.45f, FreezePositionX = true,
            MaxHp = 10f, MoveSpeed = 1.2f, PatrolDistance = 2.5f,
            CanAttack = true, ContactDamage = 8f, DamageCooldown = 1f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 15f,
        };

        public static TribeCreatureConfig Slime() => new TribeCreatureConfig
        {
            Id = "slime", DisplayName = "史莱姆",
            IdleResourcePath = "Tribe/Entity/Slime 01_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Slime 01_walk (16x16)",
            VisualScale = 8f, VisualYOffset = 0f,
            FrameTime = 0.12f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.35f, FreezePositionX = true,
            MaxHp = 6f, MoveSpeed = 0.8f, PatrolDistance = 2f,
            CanAttack = true, ContactDamage = 5f, DamageCooldown = 1.2f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 10f,
        };

        public static TribeCreatureConfig Wolf() => new TribeCreatureConfig
        {
            Id = "wolf", DisplayName = "恶狼",
            IdleResourcePath = "Tribe/Entity/Wolf 01_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Wolf 01_walk (16x16)",
            VisualScale = 10f, VisualYOffset = 0f,
            FrameTime = 0.08f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.45f, FreezePositionX = true,
            MaxHp = 12f, MoveSpeed = 1.8f, PatrolDistance = 4f,
            CanAttack = true, ContactDamage = 10f, DamageCooldown = 0.8f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 18f,
        };

        public static TribeCreatureConfig Bat() => new TribeCreatureConfig
        {
            Id = "bat", DisplayName = "蝙蝠",
            IdleResourcePath = "Tribe/Entity/Bat 01_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Bat 01_fly (16x16)",
            VisualScale = 8f, VisualYOffset = 0f,
            FrameTime = 0.08f, Pivot = SpritePivot.Center,
            UseGravity = false, GravityScale = 0f,
            ColliderRadius = 0.3f, FreezePositionX = false,
            MaxHp = 4f, MoveSpeed = 1.5f, PatrolDistance = 3f,
            CanAttack = true, ContactDamage = 6f, DamageCooldown = 1f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 8f,
        };

        public static TribeCreatureConfig Mushy01() => new TribeCreatureConfig
        {
            Id = "mushy_01", DisplayName = "蘑菇怪",
            IdleResourcePath = "Tribe/Entity/Mushy 01_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Mushy 01_walk (16x16)",
            VisualScale = 8f, VisualYOffset = 0f,
            FrameTime = 0.12f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.35f, FreezePositionX = true,
            MaxHp = 5f, MoveSpeed = 0.6f, PatrolDistance = 1.5f,
            CanAttack = true, ContactDamage = 4f, DamageCooldown = 1.5f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 8f,
        };

        public static TribeCreatureConfig Mushy02() => new TribeCreatureConfig
        {
            Id = "mushy_02", DisplayName = "毒蘑菇",
            IdleResourcePath = "Tribe/Entity/Mushy 02_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Mushy 02_walk (16x16)",
            VisualScale = 8f, VisualYOffset = 0f,
            FrameTime = 0.12f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.35f, FreezePositionX = true,
            MaxHp = 7f, MoveSpeed = 0.7f, PatrolDistance = 2f,
            CanAttack = true, ContactDamage = 6f, DamageCooldown = 1.2f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 10f,
        };

        public static TribeCreatureConfig Mushy03() => new TribeCreatureConfig
        {
            Id = "mushy_03", DisplayName = "火蘑菇",
            IdleResourcePath = "Tribe/Entity/Mushy 03_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Mushy 03_walk (16x16)",
            VisualScale = 9f, VisualYOffset = 0f,
            FrameTime = 0.1f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.4f, FreezePositionX = true,
            MaxHp = 10f, MoveSpeed = 0.8f, PatrolDistance = 2f,
            CanAttack = true, ContactDamage = 8f, DamageCooldown = 1f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 12f,
        };

        public static TribeCreatureConfig Mushy04() => new TribeCreatureConfig
        {
            Id = "mushy_04", DisplayName = "冰蘑菇",
            IdleResourcePath = "Tribe/Entity/Mushy 04_idle (16x16)",
            WalkResourcePath = "Tribe/Entity/Mushy 04_walk (16x16)",
            VisualScale = 9f, VisualYOffset = 0f,
            FrameTime = 0.1f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.4f, FreezePositionX = true,
            MaxHp = 10f, MoveSpeed = 0.8f, PatrolDistance = 2f,
            CanAttack = true, ContactDamage = 8f, DamageCooldown = 1f,
            EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 12f,
        };

        public static TribeCreatureConfig Ogre() => new TribeCreatureConfig
        {
            Id = "ogre", DisplayName = "食人魔",
            IdleResourcePath = "Tribe/Entity/Ogre 03_idle (32x32)",
            WalkResourcePath = "Tribe/Entity/Ogre 03_walk (32x32)",
            VisualScale = 10f, VisualYOffset = 0.76f,
            FrameTime = 0.12f, Pivot = SpritePivot.Center,
            UseGravity = true, GravityScale = 5f,
            ColliderRadius = 0.55f, FreezePositionX = true,
            MaxHp = 25f, MoveSpeed = 0.9f, PatrolDistance = 3f,
            CanAttack = true, ContactDamage = 15f, DamageCooldown = 1.5f,
            EnableFlash = true, FlashDuration = 0.2f, FlashColor = Color.white,
            EnableKnockback = true, KnockbackForce = 4f,
        };

        // ─── 工具方法 ─────────────────────────────────────────
        /// <summary>返回所有预设（便于世界生成器批量使用）。</summary>
        public static TribeCreatureConfig[] All() => new[]
        {
            Cow(), Hen(), Pig(),
            Skeleton(), Slime(), Wolf(), Bat(),
            Mushy01(), Mushy02(), Mushy03(), Mushy04(),
            Ogre(),
        };

        /// <summary>仅动物（被动、无攻击性）。</summary>
        public static TribeCreatureConfig[] Animals() => new[] { Hen(), Pig() };

        /// <summary>仅怪物。</summary>
        public static TribeCreatureConfig[] Monsters() => new[]
        {
            Skeleton(), Slime(), Wolf(), Bat(),
            Mushy01(), Mushy02(), Mushy03(), Mushy04(),
            Ogre(),
        };
    }
}
