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
    }
}
