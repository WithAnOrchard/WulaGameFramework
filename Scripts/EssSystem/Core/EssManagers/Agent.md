# EssManagers 模块指南

## 概述

`EssSystem.Core.EssManagers` 提供框架的 Manager 系统，包括基类与 4 个核心 Manager。

## 模块组成

| 子模块 | 内容 | 文档 |
|---|---|---|
| `Manager/` | `Manager<T>` / `Service<T>` 基类、`ManagerAttribute` | [Manager 指南](Manager/Agent.md) |
| `DataManager/` | 数据持久化 + Service 自动注册 | [DataManager 指南](DataManager/Agent.md) |
| `ResourceManager/` | 资源加载/缓存（Prefab/Sprite/Audio/Texture） | [ResourceManager 指南](ResourceManager/Agent.md) |
| `UIManager/` | UI 实体注册中心 + Canvas 管理 | [UIManager 指南](UIManager/Agent.md) |

## 核心规则

### Manager vs Service

| | Manager | Service |
|---|---|---|
| 父类 | `SingletonMono<T>` | `SingletonNormal<T>` |
| Unity 生命周期 | ✅ | ❌ |
| 挂到 GameObject | ✅ | ❌ |
| 数据持久化 | ❌ | ✅（自动） |
| 适用场景 | 需要 Update/Coroutine | 业务逻辑、数据管理 |

### 优先级（`[Manager(N)]`）

| Manager | 值 | 备注 |
|---|---:|---|
| `EventProcessor` | -30 | 框架核心，最先 |
| `DataManager` | -20 | 监听 Service 初始化事件 |
| `ResourceManager` | 0 | |
| `UIManager` | 5 | |
| 业务 Manager | 10+ | InventoryManager(10) 等 |

数值越小越先 `Awake`（基于 Unity `DefaultExecutionOrder`）。

### 通信约束

- **Manager → 自己的 Service**：直接调用 `MyService.Instance.XXX(...)` 即可（强类型、零开销）
- **跨模块调用**：通过 `EventProcessor.TriggerEvent` / `TriggerEventMethod`，避免直接持有其他 Manager 引用
- **Service → 任何 Manager**：通过 `EventProcessor.TriggerEvent`

### 文件组织

- 数据类（DAO）放在 `Dao/` 文件夹
- UI 表现层（Entity）统一在 `UIManager/Entity/`，业务模块不得自己建 `Entity/`
- 业务模块只负责 Dao + Service 业务逻辑

## 启动流程

`AbstractGameManager.Awake` 自动：
1. 确保 4 个基础 Manager 存在（EventProcessor / DataManager / ResourceManager / UIManager）
2. 反射扫描所有 `Manager<T>` 子类组件
3. 按 `[Manager]` 优先级排序
4. 依次初始化
