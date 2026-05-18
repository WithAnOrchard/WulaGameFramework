using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Entities
{
    /// <summary>蘑菇怪族系 —— 4 种同源变体（普通 / 毒 / 火 / 冰），共享 spritesheet 命名规范。
    /// 每个变体独立 <c>CharacterConfigId</c> + 独立懒注册。</summary>
    public static class Mushy
    {
        // ─── 角色 ID 常量 ─────────────────────────────────────
        public const string CharacterConfigId01 = "tribe_mushy_01";
        public const string CharacterConfigId02 = "tribe_mushy_02";
        public const string CharacterConfigId03 = "tribe_mushy_03";
        public const string CharacterConfigId04 = "tribe_mushy_04";

        private static bool _r01, _r02, _r03, _r04;

        private static void EnsureRegistered(ref bool flag, string id, string display,
            string sheetSuffix, float frameTime, float scale)
        {
            if (flag) return;
            CharacterConfigFactory.RegisterSheetCreature(
                configId: id, displayName: display,
                idleResourcePath: $"Tribe/Common/Entity/Mushy {sheetSuffix}_idle (16x16)",
                walkResourcePath: $"Tribe/Common/Entity/Mushy {sheetSuffix}_walk (16x16)",
                frameRate: 1f / frameTime,
                leftFrameIndices: new[] { 4, 5, 6, 7 },
                rightFrameIndices: new[] { 8, 9, 10, 11 },
                visualScale: scale, visualYOffset: 0f);
            flag = true;
        }

        // ─── 4 个变体 ─────────────────────────────────────────
        public static TribeCreatureConfig Mushy01()
        {
            EnsureRegistered(ref _r01, CharacterConfigId01, "蘑菇怪", "01", 0.12f, 8f);
            return new TribeCreatureConfig
            {
                Id = "mushy_01", DisplayName = "蘑菇怪",
                CharacterConfigId = CharacterConfigId01,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.35f, FreezePositionX = false,
                MaxHp = 5f, MoveSpeed = 1.4f, PatrolDistance = 2.5f,
                CanAttack = true, ContactDamage = 4f, DamageCooldown = 1.5f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 8f,
            };
        }

        public static TribeCreatureConfig Mushy02()
        {
            EnsureRegistered(ref _r02, CharacterConfigId02, "毒蘑菇", "02", 0.12f, 8f);
            return new TribeCreatureConfig
            {
                Id = "mushy_02", DisplayName = "毒蘑菇",
                CharacterConfigId = CharacterConfigId02,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.35f, FreezePositionX = false,
                MaxHp = 7f, MoveSpeed = 1.5f, PatrolDistance = 3f,
                CanAttack = true, ContactDamage = 6f, DamageCooldown = 1.2f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 10f,
            };
        }

        public static TribeCreatureConfig Mushy03()
        {
            EnsureRegistered(ref _r03, CharacterConfigId03, "火蘑菇", "03", 0.1f, 9f);
            return new TribeCreatureConfig
            {
                Id = "mushy_03", DisplayName = "火蘑菇",
                CharacterConfigId = CharacterConfigId03,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.4f, FreezePositionX = false,
                MaxHp = 10f, MoveSpeed = 1.6f, PatrolDistance = 3f,
                CanAttack = true, ContactDamage = 8f, DamageCooldown = 1f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 12f,
            };
        }

        public static TribeCreatureConfig Mushy04()
        {
            EnsureRegistered(ref _r04, CharacterConfigId04, "冰蘑菇", "04", 0.1f, 9f);
            return new TribeCreatureConfig
            {
                Id = "mushy_04", DisplayName = "冰蘑菇",
                CharacterConfigId = CharacterConfigId04,
                UseGravity = true, GravityScale = 5f,
                ColliderRadius = 0.4f, FreezePositionX = false,
                MaxHp = 10f, MoveSpeed = 1.6f, PatrolDistance = 3f,
                CanAttack = true, ContactDamage = 8f, DamageCooldown = 1f,
                EnableFlash = true, FlashDuration = 0.15f, FlashColor = Color.white,
                EnableKnockback = true, KnockbackForce = 12f,
            };
        }
    }
}
