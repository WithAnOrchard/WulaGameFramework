using UnityEngine;
using EssSystem.Core.EssManagers.Manager;

namespace EssSystem.Core.EssManagers.Foundation.DataManager
{
    /// <summary>
    /// 数据管理器 - 符合架构规范
    /// 优先级设为最高，确保在其他 Manager 之前初始化
    /// </summary>
    [Manager(-20)]
    public class DataManager : Manager<DataManager>
    {
        private DataService _dataService;

        #region Inspector Debug Fields

        [Header("Debug Information")]
        [SerializeField]
        private int _serviceCount = 0;

        [SerializeField]
        private string[] _serviceNames = new string[0];

        [SerializeField, Tooltip("数据文件夹路径")]
        private string _dataFolderPath = "";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            _dataService = DataService.Instance;
            Log("DataManager 初始化完成", Color.green);
        }

        protected override void Update()
        {
            base.Update();
            UpdateDebugInfo();
        }

        private void UpdateDebugInfo()
        {
            if (_dataService != null)
            {
                var serviceInstances = _dataService.GetServiceInstances();
                if (serviceInstances != null)
                {
                    _serviceCount = serviceInstances.Count;
                    _serviceNames = new string[serviceInstances.Count];
                    for (int i = 0; i < serviceInstances.Count; i++)
                    {
                        _serviceNames[i] = serviceInstances[i]?.GetType().Name ?? "Unknown";
                    }
                    _dataFolderPath = Application.persistentDataPath + "/ServiceData";
                }
            }
        }
    }
}