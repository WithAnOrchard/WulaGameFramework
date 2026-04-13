using System.Collections.Generic;
using EssSystem.Core.Event;
using EssSystem.Core.Manager;
using EssSystem.UIManager.Dao;
using EssSystem.EssManager.UIManager.Entity;
using UnityEngine;

namespace EssSystem.EssManager.UIManager
{
    /// <summary>
    /// UI管理器 - Unity MonoBehaviour单例，用于UI管理
    /// </summary>
    public class UIManager : Manager<UIManager>
    {
        private UIService _uiService;

        protected override void Initialize()
        {
            base.Initialize();
            _uiService = UIService.Instance;
        }

       
    }
}
