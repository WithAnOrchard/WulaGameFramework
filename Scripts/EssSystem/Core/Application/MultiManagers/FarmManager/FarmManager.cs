using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.FarmManager
{
    /// <summary>
    /// 农场系统门面 —— 农场配置注册 / 实例化 / 作物种植&生长 / 子场景路由。
    /// <para>
    /// 与其它 Manager 的关系（依赖方向）：
    /// <list type="bullet">
    ///   <item>← <c>InventoryManager</c>（种子消耗 + 产物入背包，bare-string 事件）</item>
    ///   <item>← <c>EntityManager</c>（农场作为 Entity；IInteractable 能力进入子场景）</item>
    ///   <item>← <c>SceneInstanceManager</c>（农场内部小场景）</item>
    /// </list>
    /// 本 Manager 仅管"农场是谁/在哪/种了什么"；不知道场景渲染细节、不知道 UI 细节。
    /// </para>
    /// <para>
    /// <b>M1 已实施</b>：RegisterFarmConfig / RegisterCropConfig / SpawnFarm + 广播 OnFarmSpawned。
    /// <b>未实施</b>：Plant / Harvest / Water / Upgrade / EnterFarm 留待后续。
    /// </para>
    /// </summary>
    [Manager(18)]
    public class FarmManager : Manager<FarmManager>
    {
        // ─── 跨模块调用方使用的常量 ───
        /// <summary>注册一份 FarmConfig。data: [FarmConfig]</summary>
        public const string EVT_REGISTER_FARM_CONFIG = "RegisterFarmConfig";

        /// <summary>注册一份 CropConfig。data: [CropConfig]</summary>
        public const string EVT_REGISTER_CROP_CONFIG = "RegisterCropConfig";

        /// <summary>实例化一座农场。data: [string configId, Vector3 worldPosition, string instanceId?]
        /// <para>返回 Ok(FarmInstance) / Fail(msg)。</para></summary>
        public const string EVT_SPAWN_FARM = "SpawnFarm";

        public FarmService Service => FarmService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("FarmManager 初始化完成", Color.green);
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

        // ============================================================
        // Event API
        // ============================================================

        [Event(EVT_REGISTER_FARM_CONFIG)]
        public List<object> HandleRegisterFarmConfig(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 1 || !(data[0] is FarmConfig cfg))
                return ResultCode.Fail("参数错误：需要 [FarmConfig]");
            Service.RegisterFarmConfig(cfg);
            return ResultCode.Ok(cfg.Id);
        }

        [Event(EVT_REGISTER_CROP_CONFIG)]
        public List<object> HandleRegisterCropConfig(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 1 || !(data[0] is CropConfig cfg))
                return ResultCode.Fail("参数错误：需要 [CropConfig]");
            Service.RegisterCropConfig(cfg);
            return ResultCode.Ok(cfg.Id);
        }

        [Event(EVT_SPAWN_FARM)]
        public List<object> HandleSpawnFarm(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 2) return ResultCode.Fail("参数错误：需要 [configId, worldPos, instanceId?]");
            if (!(data[0] is string configId) || string.IsNullOrEmpty(configId))
                return ResultCode.Fail("参数错误：data[0] 需为非空 configId");
            if (!(data[1] is Vector3 worldPos)) return ResultCode.Fail("参数错误：data[1] 需为 Vector3");
            var instanceId = data.Count > 2 ? data[2] as string : null;

            var inst = Service.SpawnFarm(configId, worldPos, instanceId);
            return inst != null
                ? ResultCode.Ok(inst)
                : ResultCode.Fail($"SpawnFarm 失败：configId={configId}");
        }
    }
}
