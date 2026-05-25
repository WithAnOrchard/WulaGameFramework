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
    /// <b>M2 已实施</b>：PlantCrop / WaterCrop / FertilizeCrop / RemovePest / HarvestCrop / QueryFarmSlot + 广播对应事件。
    /// <b>未实施</b>：Upgrade / EnterFarm 留待后续。
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

        /// <summary>种植作物。data: [string instanceId, int row, int col, string cropConfigId, string inventoryId?]
        /// <para>inventoryId 缺省为 "player"。返回 Ok(FarmSlot) / Fail(msg)。</para></summary>
        public const string EVT_PLANT_CROP = "PlantCrop";

        /// <summary>浇水。data: [string instanceId, int row, int col]
        /// <para>返回 Ok(FarmSlot) / Fail(msg)。</para></summary>
        public const string EVT_WATER_CROP = "WaterCrop";

        /// <summary>施肥。data: [string instanceId, int row, int col, float boostSeconds?]
        /// <para>boostSeconds 缺省 300 秒。返回 Ok(FarmSlot) / Fail(msg)。</para></summary>
        public const string EVT_FERTILIZE = "FertilizeCrop";

        /// <summary>除虫。data: [string instanceId, int row, int col]
        /// <para>返回 Ok(FarmSlot) / Fail(msg)。</para></summary>
        public const string EVT_REMOVE_PEST = "RemovePest";

        /// <summary>收获成熟作物。data: [string instanceId, int row, int col, string inventoryId?]
        /// <para>inventoryId 缺省为 "player"。返回 Ok("已收获") / Fail(msg)。</para></summary>
        public const string EVT_HARVEST_CROP = "HarvestCrop";

        /// <summary>查询槽位状态（纯读）。data: [string instanceId, int row, int col]
        /// <para>返回 Ok(FarmSlot) / Fail(msg)。</para></summary>
        public const string EVT_QUERY_SLOT = "QueryFarmSlot";

        /// <summary>清除槽位（枯萎时手动清除，无产出）。data: [string instanceId, int row, int col]
        /// <para>返回 Ok("已清除") / Fail(msg)。</para></summary>
        public const string EVT_CLEAR_SLOT = "ClearFarmSlot";

        public FarmService Service => FarmService.Instance;

        // ── 生长 Tick 计时器（每秒驱动一次，避免逐帧运算）
        private float _tickTimer;
        private const float TickInterval = 1f;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("FarmManager 初始化完成", Color.green);
        }

        protected override void Update()
        {
            base.Update();
            if (Service == null) return;
            _tickTimer += Time.deltaTime;
            if (_tickTimer < TickInterval) return;
            _tickTimer = 0f;
            Service.TickAllFarms();
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

        [Event(EVT_PLANT_CROP)]
        public List<object> HandlePlantCrop(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 4)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col, cropConfigId, inventoryId?]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");
            if (!(data[3] is string cropId))       return ResultCode.Fail("data[3] 需为 cropConfigId");
            var invId = data.Count > 4 && data[4] is string s4 && !string.IsNullOrEmpty(s4) ? s4 : "player";

            var err = Service.PlantCrop(instanceId, row, col, cropId, invId);
            if (err != null) return ResultCode.Fail(err);
            return ResultCode.Ok(Service.QuerySlot(instanceId, row, col));
        }

        [Event(EVT_WATER_CROP)]
        public List<object> HandleWaterCrop(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 3)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");

            var err = Service.WaterCrop(instanceId, row, col);
            if (err != null) return ResultCode.Fail(err);
            return ResultCode.Ok(Service.QuerySlot(instanceId, row, col));
        }

        [Event(EVT_FERTILIZE)]
        public List<object> HandleFertilize(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 3)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col, boostSeconds?]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");
            var boost = data.Count > 3 ? System.Convert.ToSingle(data[3]) : 300f;

            var err = Service.Fertilize(instanceId, row, col, boost);
            if (err != null) return ResultCode.Fail(err);
            return ResultCode.Ok(Service.QuerySlot(instanceId, row, col));
        }

        [Event(EVT_REMOVE_PEST)]
        public List<object> HandleRemovePest(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 3)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");

            var err = Service.RemovePest(instanceId, row, col);
            if (err != null) return ResultCode.Fail(err);
            return ResultCode.Ok(Service.QuerySlot(instanceId, row, col));
        }

        [Event(EVT_HARVEST_CROP)]
        public List<object> HandleHarvestCrop(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 3)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col, inventoryId?]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");
            var invId = data.Count > 3 && data[3] is string s3 && !string.IsNullOrEmpty(s3) ? s3 : "player";

            var err = Service.HarvestCrop(instanceId, row, col, invId);
            if (err != null) return ResultCode.Fail(err);
            return ResultCode.Ok("已收获");
        }

        [Event(EVT_QUERY_SLOT)]
        public List<object> HandleQuerySlot(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 3)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");

            var slot = Service.QuerySlot(instanceId, row, col);
            return slot != null
                ? ResultCode.Ok(slot)
                : ResultCode.Fail($"槽位不存在: ({row},{col})");
        }

        [Event(EVT_CLEAR_SLOT)]
        public List<object> HandleClearSlot(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("FarmService 未初始化");
            if (data == null || data.Count < 3)
                return ResultCode.Fail("参数错误：需要 [instanceId, row, col]");
            if (!(data[0] is string instanceId)) return ResultCode.Fail("data[0] 需为 instanceId");
            if (!TryGetInt(data, 1, out var row))  return ResultCode.Fail("data[1] 需为 row(int)");
            if (!TryGetInt(data, 2, out var col))  return ResultCode.Fail("data[2] 需为 col(int)");

            var err = Service.ClearSlot(instanceId, row, col);
            return err != null ? ResultCode.Fail(err) : ResultCode.Ok("已清除");
        }

        // ── 内部工具 ────────────────────────────────────────────────────
        private static bool TryGetInt(List<object> data, int index, out int value)
        {
            value = 0;
            if (index >= data.Count || data[index] == null) return false;
            try { value = System.Convert.ToInt32(data[index]); return true; }
            catch { return false; }
        }
    }
}
