using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.FarmManager
{
    /// <summary>
    /// 农场业务服务 —— 持久化 FarmConfig / CropConfig 注册表 + 维护运行时 FarmInstance 集合。
    /// <para>
    /// <b>M1 已实施</b>：RegisterFarmConfig / RegisterCropConfig / SpawnFarm + 广播 OnFarmSpawned。
    /// <b>未实施</b>：Plant / Harvest / Water / Upgrade / EnterFarm 等留待后续里程碑。
    /// </para>
    /// </summary>
    public class FarmService : Service<FarmService>
    {
        #region 数据分类

        /// <summary>已注册的 FarmConfig（按 Id）。</summary>
        public const string CAT_FARM_CONFIGS = "FarmConfigs";

        /// <summary>已注册的 CropConfig（按 Id）。</summary>
        public const string CAT_CROP_CONFIGS = "CropConfigs";

        /// <summary>运行时 FarmInstance（按 InstanceId）。</summary>
        public const string CAT_INSTANCES    = "FarmInstances";

        #endregion

        #region 广播事件名

        /// <summary>农场实例化成功广播。data: [string instanceId, FarmInstance instance]</summary>
        public const string EVT_ON_FARM_SPAWNED = "OnFarmSpawned";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("FarmService 初始化完成", Color.green);
        }

        #region Config Registration

        /// <summary>注册 / 覆盖一份 FarmConfig（按 Id）。</summary>
        public void RegisterFarmConfig(FarmConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Id))
            {
                LogWarning("忽略空 FarmConfig 或缺 Id");
                return;
            }
            SetData(CAT_FARM_CONFIGS, config.Id, config);
            Log($"注册 FarmConfig: {config.Id} ({config.DisplayName})", Color.blue);
        }

        /// <summary>注册 / 覆盖一份 CropConfig（按 Id）。</summary>
        public void RegisterCropConfig(CropConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.Id))
            {
                LogWarning("忽略空 CropConfig 或缺 Id");
                return;
            }
            SetData(CAT_CROP_CONFIGS, config.Id, config);
            Log($"注册 CropConfig: {config.Id} ({config.DisplayName})", Color.blue);
        }

        public FarmConfig GetFarmConfig(string id) =>
            string.IsNullOrEmpty(id) ? null : GetData<FarmConfig>(CAT_FARM_CONFIGS, id);

        public CropConfig GetCropConfig(string id) =>
            string.IsNullOrEmpty(id) ? null : GetData<CropConfig>(CAT_CROP_CONFIGS, id);

        #endregion

        #region Instance Management

        public FarmInstance GetFarm(string instanceId) =>
            string.IsNullOrEmpty(instanceId) ? null : GetData<FarmInstance>(CAT_INSTANCES, instanceId);

        public IEnumerable<FarmInstance> GetAllFarms()
        {
            if (!_dataStorage.TryGetValue(CAT_INSTANCES, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is FarmInstance fi) yield return fi;
        }

        /// <summary>实例化一座农场。重复 instanceId 直接返回已有实例（不重建）。
        /// 注：本方法只产出"农场数据"；视觉、互动、子场景、扣材料一律由订阅 <see cref="EVT_ON_FARM_SPAWNED"/>
        /// 的业务侧自己处理，保持 FarmService 与 UI / Inventory / Map 解耦（§4.1）。</summary>
        public FarmInstance SpawnFarm(string configId, Vector3 worldPosition, string instanceId = null)
        {
            if (string.IsNullOrEmpty(configId))
            {
                LogWarning("SpawnFarm: 缺 configId");
                return null;
            }
            var config = GetFarmConfig(configId);
            if (config == null)
            {
                LogWarning($"SpawnFarm: FarmConfig 未注册: {configId}");
                return null;
            }

            // 自动生成 instanceId（"farm_0001" 形式按 CAT_INSTANCES 当前数量推算）
            if (string.IsNullOrEmpty(instanceId))
            {
                var n = _dataStorage.TryGetValue(CAT_INSTANCES, out var d) ? d.Count : 0;
                instanceId = $"farm_{(n + 1):D4}";
            }
            if (HasData(CAT_INSTANCES, instanceId))
            {
                LogWarning($"SpawnFarm: 实例 {instanceId} 已存在，返回已有");
                return GetFarm(instanceId);
            }

            // 初始化网格（按 InitialRows × InitialCols 预填空槽位）
            var rows = Mathf.Max(1, config.InitialRows);
            var cols = Mathf.Max(1, config.InitialCols);
            var slots = new List<FarmSlot>(rows * cols);
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                    slots.Add(new FarmSlot { Row = r, Col = c });

            var instance = new FarmInstance
            {
                InstanceId    = instanceId,
                ConfigId      = configId,
                WorldPosition = worldPosition,
                Level         = 0,
                Rows          = rows,
                Cols          = cols,
                Slots         = slots,
            };

            SetData(CAT_INSTANCES, instanceId, instance);
            Log($"SpawnFarm: {instanceId} @ {worldPosition} (config={configId}, {rows}x{cols})", Color.green);

            // 广播 —— Tribe 业务侧用来：①扩展边界 ②创建视觉 ③挂 IInteractable
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_ON_FARM_SPAWNED,
                    new List<object> { instanceId, instance });

            return instance;
        }

        #endregion
    }
}
