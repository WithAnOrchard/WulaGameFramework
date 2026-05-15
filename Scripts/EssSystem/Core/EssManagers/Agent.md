# EssManagers 模块指南

## 概述

`EssSystem.Core.EssManagers` 提供框架的 Manager 系统，包括基类与 6 个核心 Manager。

## 模块组成

按职能分为三组（`Foundation` / `Presentation` / `Gameplay`），加上基础架构 `Manager/`。

### 基础架构

| 子模块 | 内容 | 文档 |
|---|---|---|
| `Manager/` | `Manager<T>` / `Service<T>` 基类、`ManagerAttribute` | [Manager 指南](Manager/Agent.md) |

### `Foundation/` — 框架基础服务（优先级 -20 ~ 3）

| 子模块 | 优先级 | 内容 | 文档 |
|---|---:|---|---|
| `Foundation/DataManager/` | -20 | 数据持久化 + Service 自动注册 | [DataManager 指南](Foundation/DataManager/Agent.md) |
| `Foundation/ResourceManager/` | 0 | 资源加载/缓存（Prefab/Sprite/Audio/Texture） | [ResourceManager 指南](Foundation/ResourceManager/Agent.md) |
| `Foundation/AudioManager/` | 3 | BGM / SFX 音频管理 + 音量持久化 | [AudioManager 指南](Foundation/AudioManager/Agent.md) |

### `Presentation/` — 表现层（5）

| 子模块 | 优先级 | 内容 | 文档 |
|---|---:|---|---|
| `Presentation/UIManager/` | 5 | UI 实体注册中心 + Canvas 管理 | [UIManager 指南](Presentation/UIManager/Agent.md) |

### `Gameplay/` — 游戏业务（10+）

| 子模块 | 优先级 | 内容 | 文档 |
|---|---:|---|---|
| `Gameplay/InventoryManager/` | 10 | 背包系统 | [InventoryManager 指南](Gameplay/InventoryManager/Agent.md) |
| `Gameplay/CharacterManager/` | 11 | 角色系统：Config 持久化 + 运行时实例 + 帧动画 + 预览面板 | [CharacterManager 指南](Gameplay/CharacterManager/Agent.md) |
| `Gameplay/MapManager/` | 12 | 2D/3D 地图系统：分块生成 + 持久化 + 流式加载 | [MapManager 指南](Gameplay/MapManager/Agent.md) |
| `Gameplay/EntityManager/` | 13 | 实体系统：Config + 运行时实例（依赖 CharacterManager + MapManager） | [EntityManager 指南](Gameplay/EntityManager/Agent.md) |

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
| `AudioManager` | 3 | 音频系统 |
| `UIManager` | 5 | |
| `InventoryManager` | 10 | |
| `CharacterManager` | 11 | 角色系统 |
| 其他业务 Manager | 12+ | |

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
1. 确保基础 Manager 存在（EventProcessor / DataManager / ResourceManager / UIManager 等；其余业务 Manager 需自行挂在场景上）
2. 反射扫描所有 `Manager<T>` 子类组件
3. 按 `[Manager]` 优先级排序
4. 依次初始化
