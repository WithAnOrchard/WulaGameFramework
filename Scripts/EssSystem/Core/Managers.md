# Managers 模块指南

## 概述

框架的所有业务 Manager 按职能层级直接铺在 `Core/` 下（与 `Base/` 同级）：`Foundation/` / `Presentation/` / `Application/`。Manager / Service 基类、事件系统、单例与 Util 在 `Core/Base/`（见 [Core 指南](Agent.md) 与 [Manager 基类指南](Base/Manager/Agent.md)）。

按职能分为三层：

- **Foundation** — 无感官输出、无业务依赖的纯数据/资源层（2 个）
- **Presentation** — 统一感官输出层（视觉 + 听觉，3 个）
- **Application** — 业务逻辑，按「是否跨 Application Manager 依赖」拆为 `SingleManagers/`（3 个，独立）与 `MultiManagers/`（3 个 + 2 个 MapManager 内部子 Manager，依赖其他 Manager）

## 模块清单

### `Foundation/` — 纯数据/资源层（优先级 -20 ~ 0）

| 子模块 | 优先级 | 内容 | 文档 |
|---|---:|---|---|
| `Foundation/DataManager/` | -20 | 数据持久化 + Service 自动注册 | [DataManager 指南](Foundation/DataManager/Agent.md) |
| `Foundation/ResourceManager/` | 0 | 资源加载/缓存（Prefab/Sprite/Audio/Texture） | [ResourceManager 指南](Foundation/ResourceManager/Agent.md) |

### `Presentation/` — 感官输出层（2 ~ 11）

| 子模块 | 优先级 | 内容 | 文档 |
|---|---:|---|---|
| `Presentation/InputManager/` | 2 | 输入抽象层：Action 事件 + 鼠标 + 轴向（统一封装 UnityEngine.Input） | [InputManager 指南](Presentation/InputManager/Agent.md) |
| `Presentation/AudioManager/` | 3 | BGM / SFX 音频管理 + 音量持久化（听觉输出） | [AudioManager 指南](Presentation/AudioManager/Agent.md) |
| `Presentation/CameraManager/` | 4 | 主相机引用 + 跟随/震屏/缩放 + 世界↔屏幕坐标转换（视觉输出） | [CameraManager 指南](Presentation/CameraManager/Agent.md) |
| `Presentation/UIManager/` | 5 | UI 实体注册中心 + Canvas 管理（视觉输出） | [UIManager 指南](Presentation/UIManager/Agent.md) |
| `Presentation/EffectsManager/` | 6 | VFX prefab 池化播放 + 屏幕闪光叠加（视觉输出） | [EffectsManager 指南](Presentation/EffectsManager/Agent.md) |
| `Presentation/LightManager/` | 7 | URP 主光 / 环境 / 雾 / 天空盒 / Volume 后处理 / 2D&3D 动态光 + 昼夜预设（**URP 专用**） | [LightManager 指南](Presentation/LightManager/Agent.md) |
| `Presentation/CharacterManager/` | 11 | 角色 Config 持久化 + 帧动画运行时 + 编辑器预览（角色外观资源工厂，被 EntityManager 通过 `CharacterViewBridge` 消费） | [CharacterManager 指南](Presentation/CharacterManager/Agent.md) |

### `Application/SingleManagers/` — 独立 Manager（无跨 Application 依赖）

| 子模块 | 优先级 | 内容 | 文档 |
|---|---:|---|---|
| `SingleManagers/InventoryManager/` | 10 | 背包/物品系统 + 物品 UI | [InventoryManager 指南](Application/SingleManagers/InventoryManager/Agent.md) |
| `SingleManagers/EntityManager/` | 13 | 实体系统：Capability 组合 + AI Brain + 伤害流水线 | [EntityManager 指南](Application/SingleManagers/EntityManager/Agent.md) |
| `SingleManagers/DialogueManager/` | 15 | 对话系统：对话树/选项/UI | [Dialogue 指南](Application/SingleManagers/DialogueManager/Agent.md) |

### `Application/MultiManagers/` — 复合 Manager（依赖其他 Application Manager）

| 子模块 | 优先级 | 依赖的 Application Manager | 内容 | 文档 |
|---|---:|---|---|---|
| `MultiManagers/MapManager/` | 12 / 13 / 14 | EntityManager（运行时事件 `"CreateEntity"` / `"DestroyEntity"`） | 地图系统总目录；含 `TopDown2D/`（`MapManager` 优先级 12）与 `Voxel3D/`（`Voxel3DMapManager` 13 + `Lighting/VoxelLightManager` 14） | [MapManager 指南](Application/MultiManagers/MapManager/Agent.md) |
| `MultiManagers/BuildingManager/` | 14 | EntityManager（DTO + 事件） | 建筑系统：放置/拆除/升级 | [BuildingManager 指南](Application/MultiManagers/BuildingManager/Agent.md) |
| `MultiManagers/SkillManager/` | 15 | EntityManager（DTO + 事件） | 技能系统：技能定义/释放/Buff/冷却 | [Skill 指南](Application/MultiManagers/SkillManager/Agent.md) |

> **关于 Voxel 子 Manager**：`Voxel3DMapManager` 与 `VoxelLightManager` 在物理目录上是 `MapManager/Voxel3D/` 的实现子模块，但各自保留独立 `[Manager(N)]` 注解，作为常规 Manager 注册到框架，不在本表顶层重复列出。

> **分类依据：**跨 Application Manager 依赖同时考虑「编译期 `using`」与「运行时 bare-string 事件」两类。`tools/audit_application_deps.ps1` 可随时生成依赖报告。

## 目录布局

```
Core/
├── Base/                      # 框架底层（AbstractGameManager / Singleton / Event / Manager基类 / Util）
├── Foundation/                # 纯数据/资源层（零感官输出）
│   ├── DataManager/
│   └── ResourceManager/
├── Presentation/              # 感官输出层（输入 + 视觉 + 听觉）
│   ├── InputManager/
│   ├── AudioManager/
│   ├── CameraManager/
│   ├── UIManager/
│   ├── EffectsManager/
│   ├── LightManager/           # URP 专用
│   └── CharacterManager/
└── Application/
    ├── SingleManagers/        # 独立模块，不依赖其他 Application Manager
    │   ├── InventoryManager/  [10]
    │   ├── EntityManager/     [13]
    │   └── DialogueManager/   [15]
    └── MultiManagers/         # 复合模块，依赖其他 Application Manager
        ├── MapManager/
        │   ├── Common/
        │   ├── TopDown2D/      → MapManager        [12]
        │   └── Voxel3D/        → Voxel3DMapManager [13]
        │       └── Lighting/   → VoxelLightManager [14]
        ├── BuildingManager/   [14]
        └── SkillManager/      [15]
```

## 依赖关系图

```
Foundation                Presentation               Application
─────────                ────────────               ────────
DataManager(-20)
    ↓
ResourceManager(0) ───→ AudioManager(3)
                        UIManager(5)
                            ↑
                        CharacterManager(11) ──────→ EntityManager(13)   [CharacterViewBridge]
                                                         ↑
                            InventoryManager(10) ────────┤
                                                         ↓
                                                    Application/World:
                                                    MapManager(12)
                                                    Voxel3DMapManager(13) ──→ VoxelLightManager(14)
                                                    BuildingManager(14)
                                                         ↓
                                                    Application/Systems:
                                                    DialogueManager(15)
                                                    SkillManager(15) ── 消费 EntityService
```

## 核心规则

### Manager vs Service

| | Manager | Service |
|---|---|---|
| 父类 | `SingletonMono<T>` | `SingletonNormal<T>` |
| 命名空间 | `EssSystem.Core.Base.Manager` | `EssSystem.Core.Base.Manager` |
| Unity 生命周期 | ✅ | ❌ |
| 挂到 GameObject | ✅ | ❌ |
| 数据持久化 | ❌ | ✅（自动） |
| 适用场景 | 需要 Update/Coroutine | 业务逻辑、数据管理 |

### 优先级总表（`[Manager(N)]`）

| Manager | 值 | 层级 | 备注 |
|---|---:|---|---|
| `EventProcessor` | -30 | Core/Base | 框架核心，最先 |
| `DataManager` | -20 | Foundation | 监听 Service 初始化事件 |
| `ResourceManager` | 0 | Foundation | |
| `AudioManager` | 3 | Presentation | 听觉输出 |
| `UIManager` | 5 | Presentation | |
| `InventoryManager` | 10 | Application/SingleManagers | |
| `CharacterManager` | 11 | Presentation | 角色外观资源 |
| `MapManager` | 12 | Application/MultiManagers | 2D 地图 |
| `EntityManager` | 13 | Application/SingleManagers | 实体系统 |
| `Voxel3DMapManager` | 13 | Application/MultiManagers（MapManager 内） | 3D 体素地图 |
| `BuildingManager` | 14 | Application/MultiManagers | 建筑系统 |
| `VoxelLightManager` | 14 | Application/MultiManagers（MapManager 内） | 体素光照 |
| `DialogueManager` | 15 | Application/SingleManagers | 对话系统 |
| `SkillManager` | 15 | Application/MultiManagers | 技能系统 |
| `DanmuManager` | 50 | 项目扩展 `Manager/` | 弹幕系统（非框架核心） |

数值越小越先 `Awake`（基于 Unity `DefaultExecutionOrder`）。

### 通信约束

- **Manager → 自己的 Service**：直接调用 `MyService.Instance.XXX(...)`（强类型、零开销）
- **跨模块调用**：通过 `EventProcessor.TriggerEvent` / `TriggerEventMethod`，避免直接持有其他 Manager 引用
- **Service → 任何 Manager**：通过 `EventProcessor.TriggerEvent`

### 文件组织

- 数据类（DAO）放在模块的 `Dao/` 文件夹
- 场景/Mono 行为放在模块的 `Runtime/` 文件夹
- 编辑器扩展放在模块的 `Editor/` 文件夹
- UI 表现层（Entity）统一在 `Presentation/UIManager/Entity/`，业务模块不得自己建 `Entity/`
- 业务模块只负责 Dao + Service 业务逻辑
- 每个 Manager 模块下放一个 `Agent.md` 作为模块文档

## 启动流程

`AbstractGameManager.Awake` 自动：
1. 确保基础 Manager 存在（EventProcessor / DataManager / ResourceManager / UIManager / AudioManager；其余业务 Manager 需自行挂在场景上）
2. 反射扫描所有 `Manager<T>` 子类组件
3. 按 `[Manager]` 优先级排序
4. 依次初始化
