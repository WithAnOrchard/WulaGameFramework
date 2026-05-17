# SceneInstanceManager 指南

## 概述

`SceneInstanceManager`（`[Manager(16)]`）+ `SceneInstanceService`（业务服务 + 持久化）
管理子场景（Instance）/ 副本系统。

**多人在线核心策略**：所有 Instance 与 OverWorld **共存**于同一 Unity Scene，通过坐标偏移
（如 `OriginOffset = (50000, 0)`）彼此隔离。玩家"进入"是瞬移到目标坐标，**不切场景、不冻结**。
任意时刻多个玩家可分布在 OverWorld + 不同 Instance，互相不影响。

## 状态

🚧 **骨架阶段**：Manager / Service 已挂入优先级链；Dao（`InstanceConfig` / `InstanceTheme`
/ `InstanceRules` / `PortalSpec`）已定义；进 / 出流程、Membership、Hibernation 与事件
API 尚未实现。详见 `Demo/Tribe/ToDo.md` 条目 #3 各里程碑。

## 文件结构

```
SceneInstanceManager/
├── SceneInstanceManager.cs      薄门面（Manager 单例）
├── SceneInstanceService.cs      业务服务（CAT_INSTANCES / CAT_MEMBERSHIP）
├── Agent.md                     本文档
└── Dao/
    ├── InstanceConfig.cs        Id / Theme / OriginOffset / EntryPosition / Rules / ExitPortals
    ├── InstanceTheme.cs         Safe / Event / Combat / Puzzle / Social
    ├── InstanceRules.cs         DisableEnemySpawn / ForceFriendly / HpRegenPerSec / LockTimeOfDay / HibernateAfterEmptySeconds
    └── PortalSpec.cs            出 / 入门描述（Position / TargetInstanceId / Prompt）
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `SceneInstanceService.CAT_INSTANCES`  = `"Instances"`  | 全部已注册 `InstanceConfig`（按 Id） |
| `SceneInstanceService.CAT_MEMBERSHIP` = `"Membership"` | 玩家 ↔ 当前所在 Instance（按 playerId） |

## 计划事件（M1 实施时新增）

> 当前骨架阶段尚未声明 `EVT_*` 常量。M1 里程碑落地时按 ToDo #3 第 (8) 节注册：
> RegisterInstance / EnterInstance / ExitInstance / InstancePlayerEntered / InstancePlayerExited
> / InstanceHibernated / InstanceAwoke。
