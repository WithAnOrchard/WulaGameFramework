using System;
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

        /// <summary>种植成功广播。data: [string instanceId, FarmSlot slot]</summary>
        public const string EVT_ON_CROP_PLANTED = "OnCropPlanted";

        /// <summary>浇水成功广播。data: [string instanceId, FarmSlot slot]</summary>
        public const string EVT_ON_CROP_WATERED = "OnCropWatered";

        /// <summary>施肥成功广播。data: [string instanceId, FarmSlot slot]</summary>
        public const string EVT_ON_CROP_FERTILIZED = "OnCropFertilized";

        /// <summary>害虫出现广播。data: [string instanceId, FarmSlot slot]</summary>
        public const string EVT_ON_PEST_SPAWNED = "OnPestSpawned";

        /// <summary>除虫成功广播。data: [string instanceId, FarmSlot slot]</summary>
        public const string EVT_ON_PEST_REMOVED = "OnPestRemoved";

        /// <summary>收获成功广播。data: [string instanceId, FarmSlot slot, string cropConfigId, int amount]</summary>
        public const string EVT_ON_CROP_HARVESTED = "OnCropHarvested";

        /// <summary>作物枯萎广播。data: [string instanceId, FarmSlot slot]</summary>
        public const string EVT_ON_CROP_WILTED = "OnCropWilted";

        /// <summary>生长阶段推进广播。data: [string instanceId, FarmSlot slot, CropGrowthStage oldStage, CropGrowthStage newStage]</summary>
        public const string EVT_ON_CROP_STAGE_CHANGED = "OnCropStageChanged";

        #endregion

        #region 生长参数（可运行时调整）

        /// <summary>浇水后的生长速度倍数（默认 2×）。</summary>
        public float WateredSpeedMultiplier    = 2f;

        /// <summary>施肥激活期间的生长速度叠加倍数（默认 1.5×）。</summary>
        public float FertilizedSpeedMultiplier = 1.5f;

        /// <summary>种植后到第一次可能出现害虫的最短延迟（秒，默认 120）。</summary>
        public float PestMinDelaySec = 120f;

        /// <summary>种植后到第一次可能出现害虫的最长延迟（秒，默认 600）。</summary>
        public float PestMaxDelaySec = 600f;

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

        // ============================================================
        #region Farm Actions

        private FarmSlot GetSlot(FarmInstance inst, int row, int col) =>
            inst?.Slots?.Find(s => s.Row == row && s.Col == col);

        /// <summary>
        /// 种植作物。通过 bare-string 事件从 <paramref name="inventoryId"/> 背包扣除种子（SeedItemId）。
        /// </summary>
        /// <returns>null = 成功；非 null = 错误信息。</returns>
        public string PlantCrop(string instanceId, int row, int col,
            string cropConfigId, string inventoryId = "player")
        {
            var inst = GetFarm(instanceId);
            if (inst == null) return $"农场实例不存在: {instanceId}";

            var slot = GetSlot(inst, row, col);
            if (slot == null)              return $"槽位不存在: ({row},{col})";
            if (slot.Stage != CropGrowthStage.Empty) return "槽位已有作物，请先收获或清除";

            var crop = GetCropConfig(cropConfigId);
            if (crop == null) return $"作物配置不存在: {cropConfigId}";

            var farmConfig = GetFarmConfig(inst.ConfigId);
            if (farmConfig?.AllowedCropIds != null && farmConfig.AllowedCropIds.Count > 0
                && !farmConfig.AllowedCropIds.Contains(cropConfigId))
                return $"此农场不允许种植: {cropConfigId}";

            // 扣除种子（§4.1 bare-string 调用 InventoryService.EVT_REMOVE）
            if (EventProcessor.HasInstance && !string.IsNullOrEmpty(crop.SeedItemId))
            {
                var removeResult = EventProcessor.Instance.TriggerEventMethod(
                    "InventoryRemove",
                    new List<object> { inventoryId, crop.SeedItemId, 1 });
                // InventoryResult.Partial 的 Success=true 但 Remaining>0，代表实际未扣到；需额外检查
                bool ok = removeResult != null && removeResult.Count >= 2
                    && removeResult[0] as string == "成功"
                    && removeResult[1] is EssSystem.Core.Application.SingleManagers.InventoryManager.Dao.InventoryResult ir
                    && ir.Remaining == 0;
                if (!ok)
                    return $"背包中没有足够的种子: {crop.SeedItemId}";
            }

            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            slot.CropConfigId             = cropConfigId;
            slot.PlantedAtUnixSeconds     = nowUnix;
            slot.StageStartUnixSeconds    = nowUnix;
            slot.Stage                    = CropGrowthStage.Seed;
            slot.Watered                  = false;
            slot.HasPest                  = false;
            slot.FertilizeBoostUntilUnix  = 0;
            slot.ScheduledPestUnixSeconds = nowUnix
                + (long)UnityEngine.Random.Range(PestMinDelaySec, PestMaxDelaySec);

            Log($"PlantCrop: {cropConfigId} at ({row},{col}) in {instanceId}", Color.green);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_ON_CROP_PLANTED,
                    new List<object> { instanceId, slot });

            return null;
        }

        /// <summary>浇水。Watered=true 后生长速度×<see cref="WateredSpeedMultiplier"/>，阶段推进时自动重置。</summary>
        /// <returns>null = 成功；非 null = 错误信息。</returns>
        public string WaterCrop(string instanceId, int row, int col)
        {
            var inst = GetFarm(instanceId);
            if (inst == null) return $"农场实例不存在: {instanceId}";

            var slot = GetSlot(inst, row, col);
            if (slot == null)                        return $"槽位不存在: ({row},{col})";
            if (slot.Stage == CropGrowthStage.Empty) return "槽位为空";
            if (slot.Stage == CropGrowthStage.Wilted) return "作物已枯萎";
            if (slot.Watered)                        return "本阶段已浇过水，等待下一阶段";

            slot.Watered = true;
            Log($"WaterCrop: ({row},{col}) in {instanceId}", Color.cyan);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_ON_CROP_WATERED,
                    new List<object> { instanceId, slot });

            return null;
        }

        /// <summary>
        /// 施肥。激活期间生长速度额外×<see cref="FertilizedSpeedMultiplier"/>；
        /// 重复施肥从当前时刻（或原有效期末尾）累加 <paramref name="boostSeconds"/>。
        /// </summary>
        /// <returns>null = 成功；非 null = 错误信息。</returns>
        public string Fertilize(string instanceId, int row, int col, float boostSeconds = 300f)
        {
            var inst = GetFarm(instanceId);
            if (inst == null) return $"农场实例不存在: {instanceId}";

            var slot = GetSlot(inst, row, col);
            if (slot == null)                         return $"槽位不存在: ({row},{col})";
            if (slot.Stage == CropGrowthStage.Empty)  return "槽位为空";
            if (slot.Stage == CropGrowthStage.Wilted) return "作物已枯萎";

            var nowUnix   = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var boostBase = Math.Max(nowUnix, slot.FertilizeBoostUntilUnix);
            slot.FertilizeBoostUntilUnix = boostBase + (long)boostSeconds;

            Log($"Fertilize: ({row},{col}) in {instanceId}, 到期={slot.FertilizeBoostUntilUnix}", Color.green);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_ON_CROP_FERTILIZED,
                    new List<object> { instanceId, slot });

            return null;
        }

        /// <summary>除虫。消灭害虫后作物恢复正常生长；下次害虫触发时间随机重新安排。</summary>
        /// <returns>null = 成功；非 null = 错误信息。</returns>
        public string RemovePest(string instanceId, int row, int col)
        {
            var inst = GetFarm(instanceId);
            if (inst == null) return $"农场实例不存在: {instanceId}";

            var slot = GetSlot(inst, row, col);
            if (slot == null)    return $"槽位不存在: ({row},{col})";
            if (!slot.HasPest)   return "该槽位没有害虫";

            slot.HasPest = false;
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            slot.ScheduledPestUnixSeconds = nowUnix
                + (long)UnityEngine.Random.Range(PestMinDelaySec, PestMaxDelaySec);

            Log($"RemovePest: ({row},{col}) in {instanceId}", Color.green);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_ON_PEST_REMOVED,
                    new List<object> { instanceId, slot });

            return null;
        }

        /// <summary>
        /// 收获成熟作物。产物通过 bare-string 事件写入 <paramref name="inventoryId"/> 背包；
        /// 收获后槽位恢复 Empty 状态。
        /// </summary>
        /// <returns>null = 成功；非 null = 错误信息。</returns>
        public string HarvestCrop(string instanceId, int row, int col, string inventoryId = "player")
        {
            var inst = GetFarm(instanceId);
            if (inst == null) return $"农场实例不存在: {instanceId}";

            var slot = GetSlot(inst, row, col);
            if (slot == null)                          return $"槽位不存在: ({row},{col})";
            if (slot.Stage != CropGrowthStage.Mature) return $"作物尚未成熟，当前阶段: {slot.Stage}";

            var crop = GetCropConfig(slot.CropConfigId);
            if (crop == null) return $"作物配置丢失: {slot.CropConfigId}";

            var harvestedCropId = slot.CropConfigId;
            var amount          = crop.OutputAmount;

            // 产物入背包（§4.1 bare-string 调用 InventoryService.EVT_ADD）
            if (EventProcessor.HasInstance && !string.IsNullOrEmpty(crop.OutputItemId))
                EventProcessor.Instance.TriggerEventMethod(
                    "InventoryAdd",
                    new List<object> { inventoryId, crop.OutputItemId, amount });

            Log($"HarvestCrop: {amount}×{crop.OutputItemId} from ({row},{col}) in {instanceId}", Color.green);

            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEvent(EVT_ON_CROP_HARVESTED,
                    new List<object> { instanceId, slot, harvestedCropId, amount });

            // 清空槽位
            slot.CropConfigId             = null;
            slot.PlantedAtUnixSeconds     = 0;
            slot.StageStartUnixSeconds    = 0;
            slot.Stage                    = CropGrowthStage.Empty;
            slot.Watered                  = false;
            slot.HasPest                  = false;
            slot.FertilizeBoostUntilUnix  = 0;
            slot.ScheduledPestUnixSeconds = 0;

            return null;
        }

        /// <summary>清除槽位（枯萎时由玩家手动清除，无产出）。</summary>
        /// <returns>null = 成功；非 null = 错误信息。</returns>
        public string ClearSlot(string instanceId, int row, int col)
        {
            var inst = GetFarm(instanceId);
            if (inst == null) return $"农场实例不存在: {instanceId}";
            var slot = GetSlot(inst, row, col);
            if (slot == null)                        return $"槽位不存在: ({row},{col})";
            if (slot.Stage == CropGrowthStage.Empty) return "槽位已经是空的";

            slot.CropConfigId             = null;
            slot.PlantedAtUnixSeconds     = 0;
            slot.StageStartUnixSeconds    = 0;
            slot.Stage                    = CropGrowthStage.Empty;
            slot.Watered                  = false;
            slot.HasPest                  = false;
            slot.FertilizeBoostUntilUnix  = 0;
            slot.ScheduledPestUnixSeconds = 0;

            Log($"ClearSlot: ({row},{col}) in {instanceId}", Color.yellow);
            return null;
        }

        /// <summary>查询槽位当前状态（纯读，无副作用）。</summary>
        public FarmSlot QuerySlot(string instanceId, int row, int col) =>
            GetSlot(GetFarm(instanceId), row, col);

        #endregion

        // ============================================================
        #region Growth Tick

        /// <summary>
        /// 驱动所有农场的生长推进与害虫触发检查。
        /// 应由 <see cref="FarmManager"/> 每隔固定帧周期（约 1 秒）调用一次。
        /// </summary>
        public void TickAllFarms()
        {
            var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var inst in GetAllFarms())
                TickFarm(inst, nowUnix);
        }

        private void TickFarm(FarmInstance inst, long nowUnix)
        {
            if (inst?.Slots == null) return;
            foreach (var slot in inst.Slots)
                TickSlot(inst.InstanceId, slot, nowUnix);
        }

        private void TickSlot(string instanceId, FarmSlot slot, long nowUnix)
        {
            if (slot == null) return;
            if (slot.Stage == CropGrowthStage.Empty || slot.Stage == CropGrowthStage.Wilted) return;

            var crop = GetCropConfig(slot.CropConfigId);
            if (crop == null) return;

            // ── 害虫触发检查 ─────────────────────────────────────────
            if (!slot.HasPest
                && slot.ScheduledPestUnixSeconds > 0
                && nowUnix >= slot.ScheduledPestUnixSeconds
                && (slot.Stage == CropGrowthStage.Sprout || slot.Stage == CropGrowthStage.Growing))
            {
                slot.HasPest = true;
                Log($"[Farm] 害虫出现: {instanceId} ({slot.Row},{slot.Col})", Color.yellow);
                if (EventProcessor.HasInstance)
                    EventProcessor.Instance.TriggerEvent(EVT_ON_PEST_SPAWNED,
                        new List<object> { instanceId, slot });
            }

            // ── 有害虫时生长停滞 ─────────────────────────────────────
            if (slot.HasPest) return;

            // ── 生长阶段推进 ─────────────────────────────────────────
            // stageIndex: Seed=0, Sprout=1, Growing=2, Mature=3 → 对应 StageDurations 索引
            var stageIndex = (int)slot.Stage - 1;
            if (stageIndex < 0
                || crop.StageDurations == null
                || stageIndex >= crop.StageDurations.Count) return;

            var stageDuration = crop.StageDurations[stageIndex];
            if (stageDuration <= 0f) // 时长 ≤0 视为即时推进
            {
                AdvanceStage(instanceId, slot, nowUnix);
                return;
            }

            var realElapsed = (float)(nowUnix - slot.StageStartUnixSeconds);
            var speedMult   = 1f;
            if (slot.Watered)                          speedMult *= WateredSpeedMultiplier;
            if (slot.FertilizeBoostUntilUnix > nowUnix) speedMult *= FertilizedSpeedMultiplier;

            if (realElapsed * speedMult >= stageDuration)
                AdvanceStage(instanceId, slot, nowUnix);
        }

        private void AdvanceStage(string instanceId, FarmSlot slot, long nowUnix)
        {
            var oldStage = slot.Stage;
            CropGrowthStage newStage;
            switch (oldStage)
            {
                case CropGrowthStage.Seed:    newStage = CropGrowthStage.Sprout;  break;
                case CropGrowthStage.Sprout:  newStage = CropGrowthStage.Growing; break;
                case CropGrowthStage.Growing: newStage = CropGrowthStage.Mature;  break;
                case CropGrowthStage.Mature:  newStage = CropGrowthStage.Wilted;  break;
                default: return;
            }

            slot.Stage             = newStage;
            slot.StageStartUnixSeconds = nowUnix;
            slot.Watered           = false; // 浇水效果随阶段推进重置

            Log($"[Farm] 阶段推进: {instanceId} ({slot.Row},{slot.Col}) {oldStage}→{newStage}", Color.green);

            if (!EventProcessor.HasInstance) return;

            if (newStage == CropGrowthStage.Wilted)
                EventProcessor.Instance.TriggerEvent(EVT_ON_CROP_WILTED,
                    new List<object> { instanceId, slot });
            else
                EventProcessor.Instance.TriggerEvent(EVT_ON_CROP_STAGE_CHANGED,
                    new List<object> { instanceId, slot, oldStage, newStage });
        }

        #endregion
    }
}
