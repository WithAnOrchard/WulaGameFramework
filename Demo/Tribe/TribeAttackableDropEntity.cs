using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;

namespace Demo.Tribe
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class TribeAttackableDropEntity : PickableDropEntity
    {
    }
}
