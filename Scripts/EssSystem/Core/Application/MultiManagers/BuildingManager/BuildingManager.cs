using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao.Config;

namespace EssSystem.Core.Application.MultiManagers.BuildingManager
{
    /// <summary>
    /// 建筑系统门面。<para>
    /// 优先级 14：晚于 EntityManager(13)，建筑创建依赖 EntityService 已就绪。</para>
    /// <para>对外接口全部走 §4.1 bare-string；内部 C# API 见 <see cref="BuildingService"/>。</para>
    /// </summary>
    [Manager(14)]
    public class BuildingManager : Manager<BuildingManager>
    {
        // ─── Event 名常量（命令）
        /// <summary>注册建筑模板。data: [BuildingConfig config] → Ok(configId).</summary>
        public const string EVT_REGISTER_BUILDING_CONFIG = "RegisterBuildingConfig";
        /// <summary>放置建筑。data: [string configId, string instanceId, Vector3 position, bool startCompleted?]
        /// → Ok(Transform CharacterRoot) / Fail.</summary>
        public const string EVT_PLACE_BUILDING = "PlaceBuilding";
        /// <summary>送材料。data: [string instanceId, string itemId, int amount] → Ok(int remaining).</summary>
        public const string EVT_SUPPLY_BUILDING = "SupplyBuilding";
        /// <summary>销毁建筑。data: [string instanceId] → Ok(instanceId).</summary>
        public const string EVT_DESTROY_BUILDING = "DestroyBuilding";

        // ─── Event 名常量（广播 —— 别名指向 Service 上同名常量）
        public const string EVT_COMPLETED        = BuildingService.EVT_COMPLETED;
        public const string EVT_DESTROYED        = BuildingService.EVT_DESTROYED;
        public const string EVT_SUPPLY_PROGRESS  = BuildingService.EVT_SUPPLY_PROGRESS;

        [Header("Default Templates")]
        [Tooltip("是否启动时注册 4 个示范建筑模板（铁丝网/治疗塔/墙/采集器）；业务侧可用同 ConfigId 覆盖。")]
        [SerializeField] private bool _registerDebugTemplates = true;

        public BuildingService Service => BuildingService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates)
            {
                Service.RegisterConfig(DefaultBuildingConfigs.BuildBarbedWire());
                Service.RegisterConfig(DefaultBuildingConfigs.BuildHealingTower());
                Service.RegisterConfig(DefaultBuildingConfigs.BuildWall());
                Service.RegisterConfig(DefaultBuildingConfigs.BuildHarvester());
            }

            Log("BuildingManager 初始化完成", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        // ─── Event Methods ────────────────────────────────────────

        [Event(EVT_REGISTER_BUILDING_CONFIG)]
        public List<object> RegisterBuildingConfig(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("BuildingService 尚未初始化");
            if (data == null || data.Count < 1 || !(data[0] is BuildingConfig cfg))
                return ResultCode.Fail("参数无效：需要 [BuildingConfig]");
            Service.RegisterConfig(cfg);
            return ResultCode.Ok(cfg.ConfigId);
        }

        [Event(EVT_PLACE_BUILDING)]
        public List<object> PlaceBuilding(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("BuildingService 尚未初始化");
            if (data == null || data.Count < 3) return ResultCode.Fail("参数无效：需要 [configId, instanceId, Vector3 position, bool startCompleted?]");
            var configId   = data[0] as string;
            var instanceId = data[1] as string;
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(instanceId))
                return ResultCode.Fail("configId / instanceId 不能为空");
            if (!(data[2] is Vector3 pos)) return ResultCode.Fail("position 必须为 Vector3");
            var startCompleted = data.Count > 3 && data[3] is bool b && b;

            var building = Service.PlaceBuilding(configId, instanceId, pos, startCompleted);
            if (building == null) return ResultCode.Fail($"放置建筑失败: {instanceId}");
            return ResultCode.Ok(building.Entity != null ? building.Entity.CharacterRoot : null);
        }

        [Event(EVT_SUPPLY_BUILDING)]
        public List<object> SupplyBuilding(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("BuildingService 尚未初始化");
            if (data == null || data.Count < 3) return ResultCode.Fail("参数无效：需要 [instanceId, itemId, int amount]");
            var instanceId = data[0] as string;
            var itemId     = data[1] as string;
            var amount     = System.Convert.ToInt32(data[2]);
            var remaining  = Service.SupplyMaterial(instanceId, itemId, amount);
            if (remaining < 0) return ResultCode.Fail($"补给失败（不存在或已完成）: {instanceId}");
            return ResultCode.Ok(remaining);
        }

        [Event(EVT_DESTROY_BUILDING)]
        public List<object> DestroyBuilding(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("BuildingService 尚未初始化");
            if (data == null || data.Count < 1) return ResultCode.Fail("参数无效：需要 [instanceId]");
            var instanceId = data[0] as string;
            return Service.DestroyBuilding(instanceId)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"建筑不存在: {instanceId}");
        }
    }
}
