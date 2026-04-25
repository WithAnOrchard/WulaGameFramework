using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.EssManager.InventoryManager.Dao;

namespace EssSystem.EssManager.InventoryManager
{
    /// <summary>
    /// 背包门面 — 挂到场景里的单例 MonoBehaviour
    /// <para>
    /// 只负责：生命周期、注册默认模板。<br/>
    /// 绝大多数业务逻辑放在 <see cref="InventoryService"/> 里，本类仅转发或包薄。
    /// </para>
    /// </summary>
    [Manager(5)]
    public class InventoryManager : Manager<InventoryManager>
    {
        #region Inspector

        [Header("Default Templates (auto-registered)")]
        [Tooltip("是否启动时注册几个调试用默认模板（Potion/Sword）")]
        [SerializeField] private bool _registerDebugTemplates = true;

        #endregion

        /// <summary>底层 Service（同等于 InventoryService.Instance，但 Inspector 里可见）</summary>
        public InventoryService Service { get; private set; }

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            Service = InventoryService.Instance;

            if (_registerDebugTemplates) {}

            Log("InventoryManager 初始化完成", Color.green);
        }

      

        #endregion
    }
}
