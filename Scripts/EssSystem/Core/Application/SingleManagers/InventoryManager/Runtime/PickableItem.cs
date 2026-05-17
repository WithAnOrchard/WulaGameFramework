using UnityEngine;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Runtime
{
    /// <summary>
    /// 场景中的可拾取物 MonoBehaviour —— 玩家进入触发器时把指定物品塞进 <see cref="_targetInventoryId"/>。
    /// 通常由 <c>InventoryManager.EVT_SPAWN_PICKABLE_ITEM</c> 自动挂载。
    /// </summary>
    [DisallowMultipleComponent]
    public class PickableItem : MonoBehaviour
    {
        [Header("Pickup Target")]
        [SerializeField] private string _targetInventoryId = InventoryManager.ID_PLAYER;

        [Header("Item")]
        [SerializeField] private string _itemTemplateId = "Apple";
        [SerializeField, Min(1)] private int _amount = 1;

        [Header("Pickup Rules")]
        [SerializeField] private bool _destroyWhenPicked = true;
        [SerializeField] private bool _disableAfterPicked = true;
        [SerializeField] private bool _logResult = true;

        private bool _picked;

        public string TargetInventoryId
        {
            get => _targetInventoryId;
            set => _targetInventoryId = value;
        }

        public string ItemTemplateId
        {
            get => _itemTemplateId;
            set => _itemTemplateId = value;
        }

        public int Amount
        {
            get => _amount;
            set => _amount = Mathf.Max(1, value);
        }

        public void Configure(string targetInventoryId, string itemTemplateId, int amount)
        {
            _targetInventoryId = targetInventoryId;
            _itemTemplateId = itemTemplateId;
            _amount = Mathf.Max(1, amount);
            _picked = false;
            EnsureTriggerCollider();
        }

        private void Reset() => EnsureTriggerCollider();
        private void Awake() => EnsureTriggerCollider();

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsPlayerCollider(other)) GiveToTargetInventory();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (IsPlayerCollider(other)) GiveToTargetInventory();
        }

        // 框架层不得引用 Demo —— 仅按 tag/名字识别玩家。
        // 业务侧（TribePlayer 等）需保证根 GameObject 设置 tag="Player" 或名字包含 "Player"。
        private static bool IsPlayerCollider(Component other)
        {
            if (other == null) return false;
            var go = other.gameObject;
            if (go.CompareTag("Player")) return true;
            return go.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public InventoryResult GiveToTargetInventory()
        {
            if (_picked) return InventoryResult.Fail("物品已被拾取");
            if (string.IsNullOrEmpty(_targetInventoryId)) return InventoryResult.Fail("目标容器 ID 为空");
            if (string.IsNullOrEmpty(_itemTemplateId)) return InventoryResult.Fail("物品模板 ID 为空");

            var item = InventoryService.Instance.InstantiateTemplate(_itemTemplateId, _amount);
            if (item == null) return InventoryResult.Fail($"找不到物品模板: {_itemTemplateId}");

            var result = InventoryService.Instance.AddItem(_targetInventoryId, item, _amount);
            if (!result.Success || result.Amount <= 0)
            {
                if (_logResult) Debug.LogWarning($"拾取失败: {_itemTemplateId} x{_amount} -> {_targetInventoryId}, {result.Message}", this);
                return result;
            }

            _picked = result.Remaining <= 0;
            if (_logResult) Debug.Log($"拾取物品: {_itemTemplateId} x{result.Amount} -> {_targetInventoryId}", this);

            if (_picked)
            {
                if (_disableAfterPicked) enabled = false;
                if (_destroyWhenPicked) Destroy(gameObject);
            }

            return result;
        }

        private void EnsureTriggerCollider()
        {
            var circleColliders = GetComponentsInChildren<CircleCollider2D>();
            foreach (var circleCollider in circleColliders)
            {
                if (circleCollider != null && circleCollider.isTrigger) return;
            }

            var physicalCollider2D = GetComponent<CircleCollider2D>();
            if (physicalCollider2D != null && !physicalCollider2D.isTrigger)
            {
                var pickupTriggerGo = new GameObject("PickupTrigger");
                pickupTriggerGo.transform.SetParent(transform, false);
                var pickupTrigger = pickupTriggerGo.AddComponent<CircleCollider2D>();
                pickupTrigger.isTrigger = true;
                pickupTrigger.radius = physicalCollider2D.radius;
                pickupTrigger.offset = physicalCollider2D.offset;
                return;
            }

            var pickupCollider2D = gameObject.AddComponent<CircleCollider2D>();
            pickupCollider2D.isTrigger = true;

            var pickupCollider = GetComponent<Collider>();
            if (pickupCollider != null) pickupCollider.isTrigger = true;
        }
    }
}
