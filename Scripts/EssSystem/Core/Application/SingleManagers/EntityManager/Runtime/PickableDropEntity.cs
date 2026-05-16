using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    [RequireComponent(typeof(BoxCollider2D))]
    public class PickableDropEntity : MonoBehaviour
    {
        [SerializeField] private string _pickableId;
        [SerializeField, Min(1f)] private float _maxHp = 1f;
        [SerializeField, Min(1)] private int _dropAmount = 1;
        [SerializeField] private Vector3 _dropOffset = Vector3.up * 0.25f;
        [SerializeField] private string _targetInventoryId = "player";

        private bool _dead;
        private string _entityInstanceId;

        public void Configure(string pickableId, float maxHp, int dropAmount, string targetInventoryId)
        {
            _pickableId = pickableId;
            _maxHp = Mathf.Max(1f, maxHp);
            _dropAmount = Mathf.Max(1, dropAmount);
            _targetInventoryId = string.IsNullOrEmpty(targetInventoryId) ? "player" : targetInventoryId;
            _dead = false;
            RegisterEntity();
        }

        public void Configure(string pickableId, float maxHp, int dropAmount, string targetInventoryId, Vector3 dropOffset)
        {
            _pickableId = pickableId;
            _maxHp = Mathf.Max(1f, maxHp);
            _dropAmount = Mathf.Max(1, dropAmount);
            _targetInventoryId = string.IsNullOrEmpty(targetInventoryId) ? "player" : targetInventoryId;
            _dropOffset = dropOffset;
            _dead = false;
            RegisterEntity();
        }

        private void Start()
        {
            RegisterEntity();
        }

        // 受击：由框架 EntityHandle.TakeDamage / EVT_DAMAGE_ENTITY 入口完成；本类不再暴露 TakeHit。

        private void DropPickableItem()
        {
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_pickableId)) return;
            EventProcessor.Instance.TriggerEventMethod(
                "InventorySpawnPickableItem",
                new List<object> { _pickableId, transform.position + _dropOffset, _targetInventoryId, _dropAmount });
        }

        private void RegisterEntity()
        {
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_pickableId) || !string.IsNullOrEmpty(_entityInstanceId)) return;
            _entityInstanceId = $"{gameObject.name}_{GetInstanceID()}";
            var definition = new EntityRuntimeDefinition
            {
                Kind = EntityKind.Static,
                Collider = BuildSpriteBoundsCollider(),
                CanMove = false,
                EnableFlashEffect = false, // 植物不需要闪烁效果
                EnableKnockbackEffect = false, // 植物不需要击退效果
                CanBeAttacked = true,
                MaxHp = _maxHp,
                CanAttack = false,
                Died = _ =>
                {
                    if (_dead) return;
                    _dead = true;
                    DropPickableItem();
                    Destroy(gameObject);
                }
            };
            EventProcessor.Instance.TriggerEventMethod(
                EntityManager.EVT_REGISTER_SCENE_ENTITY,
                new List<object> { _entityInstanceId, gameObject, definition });
        }

        private EntityColliderConfig BuildSpriteBoundsCollider()
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite == null) return EntityColliderConfig.OneCellBox(true);

            var bounds = sr.bounds;
            var scale = transform.lossyScale;
            var size = new Vector2(
                Mathf.Approximately(scale.x, 0f) ? bounds.size.x : bounds.size.x / Mathf.Abs(scale.x),
                Mathf.Approximately(scale.y, 0f) ? bounds.size.y : bounds.size.y / Mathf.Abs(scale.y));
            var offsetWorld = bounds.center - transform.position;
            var offset = new Vector2(
                Mathf.Approximately(scale.x, 0f) ? offsetWorld.x : offsetWorld.x / Mathf.Abs(scale.x),
                Mathf.Approximately(scale.y, 0f) ? offsetWorld.y : offsetWorld.y / Mathf.Abs(scale.y));

            return new EntityColliderConfig(EntityColliderShape.Box, size, offset, true);
        }
    }
}
