using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Foundation.DataManager
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
        [SerializeField] private int _serviceCount = 0;
        [SerializeField] private string[] _serviceNames = System.Array.Empty<string>();
        [SerializeField, Tooltip("数据文件夹路径")] private string _dataFolderPath = "";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            _dataService = DataService.Instance;
            // D2: 路径终生不变，一次性赋值不要每帧拼。
            _dataFolderPath = System.IO.Path.Combine(UnityEngine.Application.persistentDataPath, "ServiceData");
            Log("DataManager 初始化完成", Color.green);
        }

        // D1: 删除自家 Update，调试信息同步走基类 0.25s 节流钩子。
        protected override void UpdateServiceInspectorInfo()
        {
            base.UpdateServiceInspectorInfo();
            if (_dataService == null) return;
            var serviceInstances = _dataService.GetServiceInstances();
            if (serviceInstances == null) return;

            var count = serviceInstances.Count;
            _serviceCount = count;
            // 仅在长度变化时重建，平时复用数组
            if (_serviceNames == null || _serviceNames.Length != count)
                _serviceNames = new string[count];
            for (int i = 0; i < count; i++)
                _serviceNames[i] = serviceInstances[i]?.GetType().Name ?? "Unknown";
        }
    }
}