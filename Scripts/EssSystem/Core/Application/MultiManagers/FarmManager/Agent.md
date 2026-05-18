# FarmManager 指南

## 概述

`FarmManager`（`[Manager(18)]`）+ `FarmService`（业务服务 + 持久化）管理游戏中的农场建筑与作物种植系统。

**职责单一**：本 Manager 仅管"农场是谁、在哪、长着什么、长到哪一阶段"；
- 种子消耗 / 产物入背包 → `InventoryManager`
- 农场作为可互动 Entity / 进入子场景 → `EntityManager` + `SceneInstanceManager`
- 作物视觉 / 农场建筑视觉 → 业务侧自由选择（`CharacterManager` 多部件 sprite、裸 ResourceManager spriteId 均可，CropConfig 仅暴露 `StageSpriteIds`）

## 状态

🚧 **骨架阶段**：Manager / Service / Dao（`FarmConfig` / `FarmInstance` / `FarmSlot` /
`CropConfig` / `CropGrowthStage` + `BuildCost` / `FarmUpgradeStep`）已挂入优先级链；
Spawn / Plant / Water / Harvest / Upgrade / EnterFarm 与事件 API 尚未实现。

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
    ├── CropConfig.cs             作物模板：Id / DisplayName / SeedItemId / OutputItemId / OutputAmount / StageDurations / StageSpriteIds
    └── CropGrowthStage.cs        枚举：Empty / Seed / Sprout / Growing / Mature / Wilted
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `FarmService.CAT_FARM_CONFIGS` = `"FarmConfigs"` | 已注册 `FarmConfig`（按 Id） |
| `FarmService.CAT_CROP_CONFIGS` = `"CropConfigs"` | 已注册 `CropConfig`（按 Id） |
| `FarmService.CAT_INSTANCES`    = `"FarmInstances"` | 运行时 `FarmInstance`（按 InstanceId） |

## 计划事件（M1 实施时新增）

> 当前骨架阶段尚未声明 `EVT_*` 常量。落地时按以下分组注册（保持 bare-string §4.1）：

**配置注册**
- `RegisterFarmConfig` / `RegisterCropConfig` — 业务启动时灌入模板

**实例生命周期**
- `SpawnFarm` — `[configId, worldPosition, instanceId?]` → 实例化 FarmInstance + 扣建造材料
- `DespawnFarm` — `[instanceId]` → 销毁实例（保留槽位作物丢弃规则待定）
- `UpgradeFarm` — `[instanceId]` → 检查 BuildCost、扣材料、累加 Rows/Cols、解锁作物

**种植循环**
- `PlantCrop` — `[instanceId, slotIndex, cropConfigId]` → 扣种子物品 + 设 Slot.Stage=Seed
- `WaterCrop` — `[instanceId, slotIndex]` → Slot.Watered=true（加速生长，规则待定）
- `HarvestCrop` — `[instanceId, slotIndex]` → 入产物到 InventoryManager + 清空槽位

**子场景路由**
- `EnterFarm` — `[instanceId]` → 通过 SceneInstanceManager 切到内部场景；记录 ActiveSceneInstanceId
- `ExitFarm` — `[instanceId]` → 离开子场景

**广播事件**（业务侧订阅扩展世界边界等）
- `OnFarmSpawned` / `OnFarmUpgraded` / `OnCropMatured` / `OnCropHarvested`

## 与上游模块的解耦

- FarmManager **不**直接 `using InventoryManager` 命名空间；扣种子 / 入产物均走 bare-string 事件（如 `RemoveItemFromInventory` / `AddItemToInventory`）。
- 农场可互动 = 业务侧在 SpawnFarm 后用 `Entity.CanInteract` 挂 `IInteractable`，回调里 `TriggerEventMethod("EnterFarm", ...)`。
- 边界扩展（玩家"地图极限"随农场数量推进）= Tribe 业务侧订阅 `OnFarmSpawned`，自己维护一个左边界 X 值。
