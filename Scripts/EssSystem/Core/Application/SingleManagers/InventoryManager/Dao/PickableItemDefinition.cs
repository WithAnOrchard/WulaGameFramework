using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.InventoryManager.Dao
{
    /// <summary>
    /// 可拾取物定义 — 通过 <c>InventoryManager.EVT_REGISTER_PICKABLE_ITEM</c> 注册，
    /// 通过 <c>InventoryManager.EVT_SPAWN_PICKABLE_ITEM</c> 在场景中生成 <c>PickableItem</c>。
    /// </summary>
    [System.Serializable]
    public class PickableItemDefinition
    {
        public string Id;
        public string ItemTemplateId;
        public string DisplayName;
        public string SpriteResourcePath;
        public int DefaultAmount = 1;
        public Vector2 ColliderSize = Vector2.one;
        public Vector2 ColliderOffset = Vector2.zero;

        public PickableItemDefinition() { }

        public PickableItemDefinition(string id, string itemTemplateId, string displayName, string spriteResourcePath, int defaultAmount = 1)
        {
            Id = id;
            ItemTemplateId = itemTemplateId;
            DisplayName = displayName;
            SpriteResourcePath = spriteResourcePath;
            DefaultAmount = Mathf.Max(1, defaultAmount);
        }
    }
}
