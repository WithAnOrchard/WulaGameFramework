using UnityEngine;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager.Entity
{
    /// <summary>
    /// 背包物品 Entity — 把 <see cref="InventoryItem"/> 绑到一个 GameObject（例如一个槽位图标）
    /// <para>
    /// 通常不直接持久化，只在运行时由 <see cref="InventoryEntity"/> 根据 slot 变化即时生成/销毁。
    /// </para>
    /// </summary>
    public class InventoryItemEntity : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private InventoryItem _item;

        /// <summary>关联的物品 Dao（换值会触发 <see cref="SyncFromDao"/>）</summary>
        public InventoryItem Item
        {
            get => _item;
            set
            {
                if (_item == value) return;
                _item = value;
                SyncFromDao();
            }
        }

        public string ItemId => _item?.Id;
        public int Stack => _item?.CurrentStack ?? 0;

        /// <summary>
        /// 子类重写：把物品数据画到 UI（图标、名称、堆叠数字）
        /// </summary>
        protected virtual void SyncFromDao()
        {
            if (_item == null)
            {
                gameObject.name = "[Empty Item]";
                return;
            }
            gameObject.name = $"{_item.Name ?? _item.Id} x{_item.CurrentStack}";
        }
    }
}
