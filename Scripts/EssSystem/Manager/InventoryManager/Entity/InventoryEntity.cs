using UnityEngine;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager.Entity
{
    /// <summary>
    /// 背包 Entity — 把一个 <see cref="Inventory"/> Dao 绑定到 Unity GameObject
    /// <para>
    /// 参照 <c>UIEntity</c> 的模式：Awake 时向 <see cref="InventoryService"/> 注册自己，OnDestroy 时注销。
    /// 子类可在 <see cref="SyncFromDao"/> 里绘制 slot UI。
    /// </para>
    /// </summary>
    public class InventoryEntity : MonoBehaviour
    {
        [SerializeField, HideInInspector]
        private Inventory _dao;

        /// <summary>关联的 Dao</summary>
        public Inventory Dao
        {
            get => _dao;
            set => SetDao(value);
        }

        /// <summary>便捷：Dao.Id</summary>
        public string DaoId => _dao?.Id;

        protected virtual void Awake() => RegisterSelf();

        protected virtual void OnDestroy() => UnregisterSelf();

        protected virtual void SetDao(Inventory dao)
        {
            if (_dao == dao) return;
            // 换绑：先注销旧 ID
            if (_dao != null) UnregisterSelf();
            _dao = dao;
            if (_dao != null)
            {
                RegisterSelf();
                SyncFromDao();
            }
        }

        /// <summary>
        /// 根据 Dao 刷新 UI — 子类重写实现具体视图
        /// </summary>
        protected virtual void SyncFromDao()
        {
            if (_dao == null) return;
            gameObject.name = _dao.Name ?? _dao.Id;
        }

        private void RegisterSelf()
        {
            if (_dao == null || string.IsNullOrEmpty(_dao.Id)) return;
            InventoryService.Instance.RegisterEntity(_dao.Id, this);
        }

        private void UnregisterSelf()
        {
            if (_dao == null || string.IsNullOrEmpty(_dao.Id)) return;
            var svc = InventoryService.TryGetInstance();
            svc?.UnregisterEntity(_dao.Id);
        }

        #region Static Helpers

        /// <summary>通过背包 ID 获取 Entity（可能为 null）</summary>
        public static InventoryEntity Get(string inventoryId) =>
            InventoryService.Instance.GetEntity(inventoryId);

        #endregion
    }
}
