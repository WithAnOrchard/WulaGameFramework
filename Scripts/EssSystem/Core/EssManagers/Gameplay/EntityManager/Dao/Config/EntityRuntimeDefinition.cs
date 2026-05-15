using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config
{
    [Serializable]
    public class EntityRuntimeDefinition
    {
        public EntityKind Kind = EntityKind.Static;
        public EntityColliderConfig Collider = EntityColliderConfig.OneCellBox(true);
        public bool CanMove;
        public float MoveSpeed;
        public bool CanBeAttacked;
        public float MaxHp = 1f;
        public bool CanAttack;
        public float AttackPower = 1f;
        public float AttackRange = 1.5f;
        public float AttackCooldown = 0.6f;
        public Action<string> Died;

        [Header("Flash Effect")]
        [Tooltip("是否启用受伤闪烁效果")]
        public bool EnableFlashEffect = true;
        [Tooltip("变白闪烁持续时间（秒）")]
        public float FlashDuration = 0.15f;
        [Tooltip("变白颜色")]
        public Color FlashColor = Color.white;

        [Header("Knockback Effect")]
        [Tooltip("是否启用击退效果")]
        public bool EnableKnockbackEffect = true;
        [Tooltip("击退力度")]
        public float KnockbackForce = 5f;
        [Tooltip("击退持续时间（秒）")]
        public float KnockbackDuration = 0.2f;
    }
}
