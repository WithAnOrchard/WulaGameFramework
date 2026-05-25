# FarmManager 指南

## 概述

`FarmManager`（`[Manager(18)]`）+ `FarmService`（业务服务 + 持久化）管理游戏中的农场建筑与作物种植系统。

**职责单一**：本 Manager 仅管"农场是谁、在哪、长着什么、长到哪一阶段"；
- 种子消耗 / 产物入背包 → `InventoryManager`
- 农场作为可互动 Entity / 进入子场景 → `EntityManager` + `SceneInstanceManager`
- 作物视觉 / 农场建筑视觉 → 业务侧自由选择（`CharacterManager` 多部件 sprite、裸 ResourceManager spriteId 均可，CropConfig 仅暴露 `StageSpriteIds`）

## 状态

**M1 已实施**：SpawnFarm + 广播 OnFarmSpawned。

**M2 已实施**：PlantCrop / WaterCrop / FertilizeCrop / RemovePest / HarvestCrop / QueryFarmSlot。
生长周期由 `FarmManager.Update`（1秒一Tick）驱动，支持浇水加速、施肥加速、害虫停滞、枯萎过期。

🚧 **尚未实施**：UpgradeFarm / EnterFarm / ExitFarm。

业务上游需求（玩家提出）：
1. 营地以左侧某一边界为地图极限，禁止玩家越界。
2. 建造一座农场扩展一格宽度的可行走边界。
3. 走近农场可进入"农场内部小场景"，在子场景里种植 / 收割。
4. 农场可升级，升级后扩张容量 / 解锁新作物 / 进一步扩展边界。

本 Manager 是上述需求的核心数据/状态层，边界扩展逻辑由业务侧（Tribe / CampFeature）订阅
`FarmInstance` 的注册/销毁事件后驱动。

## 文件结构

```
FarmManager/
├── FarmManager.cs                薄门面（Manager 单例）
├── FarmService.cs                业务服务（CAT_FARM_CONFIGS / CAT_CROP_CONFIGS / CAT_INSTANCES）
├── Agent.md                      本文档
└── Dao/
    ├── FarmConfig.cs             农场模板：Id / DisplayName / Rows×Cols / AllowedCropIds / BuildCosts / Upgrades / InteriorSceneInstanceId
    ├── FarmInstance.cs           运行时实例：InstanceId / ConfigId / WorldPosition / Level / Rows×Cols / Slots / ActiveSceneInstanceId
    ├── FarmSlot.cs               单个槽位：Row / Col / CropConfigId / PlantedAtUnixSeconds / Stage / Watered
    │                              + HasPest / FertilizeBoostUntilUnix / StageStartUnixSeconds / ScheduledPestUnixSeconds
    ├── CropConfig.cs             作物模板：Id / DisplayName / SeedItemId / OutputItemId / OutputAmount / StageDurations / StageSpriteIds
    └── CropGrowthStage.cs        枚举：Empty / Seed / Sprout / Growing / Mature / Wilted
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `FarmService.CAT_FARM_CONFIGS` = `"FarmConfigs"` | 已注册 `FarmConfig`（按 Id） |
| `FarmService.CAT_CROP_CONFIGS` = `"CropConfigs"` | 已注册 `CropConfig`（按 Id） |
| `FarmService.CAT_INSTANCES`    = `"FarmInstances"` | 运行时 `FarmInstance`（按 InstanceId） |

## Event API

### 命令事件（业务方 -> FarmManager）

通过 `EventProcessor.Instance.TriggerEventMethod(EVT_*, args)` 调用。

| 常量 | 字符串 | 参数 | 返回 |
|---|---|---|---|
| `EVT_REGISTER_FARM_CONFIG` | `"RegisterFarmConfig"` | `[FarmConfig]` | `Ok(id)` |
| `EVT_REGISTER_CROP_CONFIG` | `"RegisterCropConfig"` | `[CropConfig]` | `Ok(id)` |
| `EVT_SPAWN_FARM` | `"SpawnFarm"` | `[configId, Vector3, instanceId?]` | `Ok(FarmInstance)` |
| `EVT_PLANT_CROP` | `"PlantCrop"` | `[instanceId, row, col, cropConfigId, inventoryId?]` | `Ok(FarmSlot)` |
| `EVT_WATER_CROP` | `"WaterCrop"` | `[instanceId, row, col]` | `Ok(FarmSlot)` |
| `EVT_FERTILIZE` | `"FertilizeCrop"` | `[instanceId, row, col, boostSeconds?=300]` | `Ok(FarmSlot)` |
| `EVT_REMOVE_PEST` | `"RemovePest"` | `[instanceId, row, col]` | `Ok(FarmSlot)` |
| `EVT_HARVEST_CROP` | `"HarvestCrop"` | `[instanceId, row, col, inventoryId?]` | `Ok("已收获")` |
| `EVT_QUERY_SLOT` | `"QueryFarmSlot"` | `[instanceId, row, col]` | `Ok(FarmSlot)` |
| `EVT_CLEAR_SLOT` | `"ClearFarmSlot"` | `[instanceId, row, col]` | `Ok("已清除")` / `Fail(msg)` |

### 广播事件（FarmService -> 业务方）

通过 `[EventListener(FarmService.EVT_*)]` 订阅。

| 常量 | 字符串 | 参数 |
|---|---|---|
| `EVT_ON_FARM_SPAWNED` | `"OnFarmSpawned"` | `[instanceId, FarmInstance]` |
| `EVT_ON_CROP_PLANTED` | `"OnCropPlanted"` | `[instanceId, FarmSlot]` |
| `EVT_ON_CROP_WATERED` | `"OnCropWatered"` | `[instanceId, FarmSlot]` |
| `EVT_ON_CROP_FERTILIZED` | `"OnCropFertilized"` | `[instanceId, FarmSlot]` |
| `EVT_ON_PEST_SPAWNED` | `"OnPestSpawned"` | `[instanceId, FarmSlot]` |
| `EVT_ON_PEST_REMOVED` | `"OnPestRemoved"` | `[instanceId, FarmSlot]` |
| `EVT_ON_CROP_STAGE_CHANGED` | `"OnCropStageChanged"` | `[instanceId, FarmSlot, oldStage, newStage]` |
| `EVT_ON_CROP_HARVESTED` | `"OnCropHarvested"` | `[instanceId, FarmSlot, cropConfigId, amount]` |
| `EVT_ON_CROP_WILTED` | `"OnCropWilted"` | `[instanceId, FarmSlot]` |

### 计划中（尚未实现）

- `UpgradeFarm` / `DespawnFarm`
- `EnterFarm` / `ExitFarm`
- 广播：`OnFarmUpgraded`

## 生长机制

- `FarmManager.Update` 每秒调用 `FarmService.TickAllFarms()`。
- 每个占用槽位：计算 `realElapsed = nowUnix - slot.StageStartUnixSeconds`，乘以速度倍数（浇水“2×” / 施肥“1.5×” / 害虫“0×”），与 `CropConfig.StageDurations[stageIndex]` 比较推进阶段。
- 枯萎：`Mature` 阶段对应 `StageDurations[3]`（可选）超时后自动变为 `Wilted`。
- 害虫：广播 `OnPestSpawned`，搜集时间就预约在 `ScheduledPestUnixSeconds`，默认随机 120–600 秒后。宇虫阶段（Sprout / Growing）才能触发。
- 速度参数可在运行时覆盖：`FarmService.Instance.WateredSpeedMultiplier`、`FertilizedSpeedMultiplier`、`PestMinDelaySec`、`PestMaxDelaySec`。

## 与上游模块的解耦

- FarmManager **不**直接 `using InventoryManager` 命名空间；扣种子 / 入产物均走 bare-string 事件（如 `RemoveItemFromInventory` / `AddItemToInventory`）。
- 农场可互动 = 业务侧在 SpawnFarm 后用 `Entity.CanInteract` 挂 `IInteractable`，回调里 `TriggerEventMethod("EnterFarm", ...)`。
- 边界扩展（玩家"地图极限"随农场数量推进）= Tribe 业务侧订阅 `OnFarmSpawned`，自己维护一个左边界 X 值。
