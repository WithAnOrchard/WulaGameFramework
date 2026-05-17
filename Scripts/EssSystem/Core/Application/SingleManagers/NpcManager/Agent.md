# NpcManager 指南

## 概述

`NpcManager`（`[Manager(17)]`）+ `NpcService`（业务服务 + 持久化）管理游戏中的 NPC：
配置注册、运行时实例化、互动路由。

**职责单一**：本 Manager 仅管"NPC 是谁、在哪、能做什么类别的事"；具体对白由 DialogueManager
处理、商店由 ShopManager 处理、任务由未来 QuestManager 处理。`NpcConfig` 通过 `DialogueId` /
`ShopId` 字段反向触达对应模块，避免 NpcManager 知道任何业务细节。

## 状态

🚧 **骨架阶段**：Manager / Service 已挂入优先级链；Dao（`NpcConfig` / `NpcInstance` /
`NpcRole` / `NpcInteractionFlags`）已定义；Spawn / Despawn / InteractNpc /
InteractionPanel 与事件 API 尚未实现。详见 `Demo/Tribe/ToDo.md` 条目 #4 前置（NPC）M1-M3。

## 文件结构

```
NpcManager/
├── NpcManager.cs                薄门面（Manager 单例）
├── NpcService.cs                业务服务（CAT_CONFIGS / CAT_INSTANCES）
├── Agent.md                     本文档
└── Dao/
    ├── NpcConfig.cs             Id / DisplayName / CharacterConfigId / Role / DialogueId / ShopId / Tags / Interactions
    ├── NpcInstance.cs           运行时实例（InstanceId / ConfigId / WorldPosition / SceneInstanceId / IsAlive）
    ├── NpcRole.cs               Generic / Merchant / Quester / Trainer / Storyteller / Guard / Banker
    └── NpcInteractionFlags.cs   [Flags] Talk / Trade / Quest / Train / Bank
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `NpcService.CAT_CONFIGS`   = `"NpcConfigs"`   | 已注册 `NpcConfig`（按 Id） |
| `NpcService.CAT_INSTANCES` = `"NpcInstances"` | 运行时 `NpcInstance`（按 InstanceId） |

## 计划事件（M1 实施时新增）

> 当前骨架阶段尚未声明 `EVT_*` 常量。M1 里程碑落地时按 ToDo #4 第 (8) 节注册：
> RegisterNpc / SpawnNpc / DespawnNpc / InteractNpc /
> NpcInteractionOpened / NpcInteractionClosed。
