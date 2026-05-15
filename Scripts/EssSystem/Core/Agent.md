# EssSystem 框架总览

## 概述

`EssSystem` 是一套 Unity C# 游戏框架，核心在 `EssSystem.Core`，提供单例、事件总线、Manager/Service 分层架构与数据持久化。业务模块以 Manager 为单位组织，按优先级自动初始化。

## 顶层结构

```
Scripts/EssSystem/
├── Core/                            ← 框架核心
│   ├── Base/                        ← 框架原语（被 EssManagers 依赖）
│   │   ├── AbstractGameManager.cs       启动入口，按优先级自动初始化所有 Manager
│   │   ├── Event/                       EventProcessor 事件总线（-30）
│   │   ├── Singleton/                   SingletonNormal / SingletonMono 基类
│   │   └── Util/                        通用工具（ResultCode, MiniJson, AssemblyUtils 等）
│   └── EssManagers/                 ← 所有 Manager 模块（消费 Base）
│       ├── Manager/                     Manager<T> / Service<T> 基类
│       ├── Foundation/                  基础服务（优先级 -20 ~ 3）
│       ├── Presentation/                表现层（优先级 5）
│       └── Gameplay/                    游戏业务（优先级 10+）
└── Manager/                         ← 扩展 Manager（非核心，可选）
    └── DanmuManager/                    弹幕系统（50）

Demo/                                ← 演示项目（不属于框架核心）
├── Tribe/                           ← 横版部落生存 Demo
├── DayNight/                        ← 2D 昼夜循环 Demo
└── DayNight3D/                      ← 3D 昼夜循环 Demo
```

## Manager 完整清单（按初始化优先级排序）

| 优先级 | Manager | 分组 | 职责 |
|---:|---|---|---|
| **-30** | `EventProcessor` | Core/Event | 事件总线 + `[Event]`/`[EventListener]` 自动注册 |
| **-20** | `DataManager` | Foundation | Service 自动注册 + 应用退出持久化 |
| **0** | `ResourceManager` | Foundation | 资源加载/缓存（Prefab/Sprite/Audio/Texture） |
| **3** | `AudioManager` | Foundation | BGM / SFX 播放 + 音量持久化 |
| **5** | `UIManager` | Presentation | Canvas 管理 + UI 实体注册中心 |
| **10** | `InventoryManager` | Gameplay | 背包/物品系统 + 物品 UI |
| **11** | `CharacterManager` | Gameplay | 角色 Config 持久化 + 帧动画运行时 + 预览面板 |
| **12** | `MapManager` | Gameplay | 2D 地图：分块生成/持久化/流式加载（TopDown + SideScroller） |
| **13** | `EntityManager` | Gameplay | 实体系统：能力(Capability)组合 + AI Brain + 伤害流水线 |
| **13** | `Voxel3DMapManager` | Gameplay | 3D 体素地图：分块/Mesh/Lighting |
| **14** | `BuildingManager` | Gameplay | 建筑系统：放置/拆除/升级 |
| **14** | `VoxelLightManager` | Gameplay | 体素光照计算 |
| **15** | `DialogueManager` | Gameplay | 对话系统：对话树/选项/UI |
| **15** | `SkillManager` | Gameplay | 技能系统：技能定义/释放/Buff/冷却 |
| **50** | `DanmuManager` | Manager(扩展) | 弹幕消息系统 |

## 目录详细结构

```
Core/EssManagers/
├── Manager/                         ← 基类
│   ├── Manager.cs                       Manager<T>（MonoBehaviour 单例）
│   └── Service.cs                       Service<T>（纯 C# 单例 + IServicePersistence）
│
├── Foundation/                      ← 基础服务
│   ├── DataManager/                     数据持久化 + Service 自动注册
│   ├── ResourceManager/                 资源加载/缓存
│   └── AudioManager/                    音频管理
│
├── Presentation/                    ← 表现层
│   └── UIManager/
│       ├── Dao/CommonComponents/        通用 UI 组件
│       └── Entity/CommonEntity/         通用 UI 实体（Toast/弹窗等）
│
└── Gameplay/                        ← 游戏业务
    ├── InventoryManager/
    │   ├── Dao/UIConfig/                物品 UI 配置
    │   └── UI/                          背包界面
    │
    ├── CharacterManager/
    │   ├── Dao/Config/                  角色配置数据
    │   └── Runtime/Preview/             帧动画运行时 + 编辑器预览
    │
    ├── MapManager/
    │   ├── Common/Util/                 通用地图工具
    │   ├── TopDown2D/                   2D 地图
    │   │   ├── Dao/Config/                  地图配置
    │   │   ├── Dao/Generator/               生成器接口
    │   │   ├── Dao/Templates/               模板实现
    │   │   │   ├── TopDownRandom/               俯视角随机（Perlin + 生态 + 河流）
    │   │   │   └── SideScrollerRandom/          横版随机（地形 + 洞穴）
    │   │   ├── Runtime/                     运行时（MapView/分块加载）
    │   │   ├── Persistence/                 地图存档
    │   │   └── Spawn/                       实体生成点
    │   └── Voxel3D/                    3D 体素地图
    │       ├── Dao/Templates/               体素模板
    │       ├── Generator/                   体素生成器
    │       ├── Lighting/Dao/                光照数据
    │       └── Persistence/                 体素存档
    │
    ├── EntityManager/
    │   ├── Dao/                          实体核心
    │   │   ├── Entity.cs                    实体数据类
    │   │   ├── EntityKind.cs                实体类型枚举
    │   │   ├── Config/                      实体配置
    │   │   └── Capabilities/                能力系统
    │   │       ├── IEntityCapability.cs          能力基接口
    │   │       ├── ITickableCapability.cs        可 Tick 能力
    │   │       ├── IDamageable.cs                可受伤（HP/伤害/治疗/复活）
    │   │       ├── IAttacker.cs                  攻击者
    │   │       ├── IMovable.cs                   可移动
    │   │       ├── IJumpable.cs                  可跳跃
    │   │       ├── IFacing.cs                    朝向
    │   │       ├── IPatrol.cs                    巡逻
    │   │       ├── IBrain.cs                     AI 大脑（Utility AI）
    │   │       ├── IAura.cs                      光环（范围治疗/伤害）
    │   │       ├── IContactDamage.cs             接触伤害
    │   │       ├── IFlashEffect.cs               受伤闪烁
    │   │       ├── IKnockbackEffect.cs           击退
    │   │       ├── IInvulnerable.cs              无敌
    │   │       ├── IGroundSensor.cs              地面检测
    │   │       ├── IHarvester.cs                 采集
    │   │       ├── INeeds.cs                     需求（饥饿等）
    │   │       ├── IStorage.cs                   存储
    │   │       ├── IPhaseThrough.cs              穿透
    │   │       ├── Default/                 默认实现
    │   │       └── Brain/                   AI 系统
    │   │           ├── BrainComponent.cs        Utility AI 主控
    │   │           ├── BrainContext.cs           AI 上下文
    │   │           ├── Consideration.cs         评估函数
    │   │           ├── IBrainAction.cs          行为接口
    │   │           ├── ISensor.cs               感知器接口
    │   │           ├── Default/RangeSensor.cs   范围感知器
    │   │           └── Actions/                 行为库
    │   │               ├── PatrolAction.cs          巡逻
    │   │               ├── FleeAction.cs            逃跑
    │   │               ├── ChaseAction.cs           追击
    │   │               ├── AttackAction.cs          攻击
    │   │               ├── MoveToAction.cs          移动到
    │   │               ├── IdleAction.cs            待机
    │   │               ├── EatAction.cs             进食
    │   │               └── SequenceAction.cs        序列组合
    │   ├── Runtime/                      运行时组件
    │   │   ├── EntityHandle.cs              Unity ↔ Entity 桥接
    │   │   ├── PickableDropEntity.cs        可攻击掉落物
    │   │   ├── CharacterController3D.cs     3D 角色控制器
    │   │   └── CameraController3D.cs        3D 摄像机
    │   ├── EntityManager.cs              Manager 薄壳
    │   ├── EntityService.cs              业务核心（注册/伤害/Tick/同步）
    │   └── CharacterViewBridge.cs        角色视图桥接
    │
    ├── BuildingManager/
    │   ├── Dao/Config/                   建筑配置
    │   └── Runtime/                      建筑运行时
    │
    ├── DialogueManager/
    │   ├── Dao/UIConfig/                 对话 UI 配置
    │   └── UI/                           对话界面
    │
    └── SkillManager/
        ├── Dao/                          技能数据
        │   ├── SkillDefinition.cs            技能静态定义
        │   ├── SkillInstance.cs              技能运行时实例
        │   ├── SkillSlot.cs                  技能槽位
        │   ├── ISkillEffect.cs               效果接口 + 上下文
        │   ├── Effects/                      效果实现
        │   │   ├── DamageEffect.cs               伤害
        │   │   ├── HealEffect.cs                 治疗
        │   │   ├── BuffEffect.cs                 施加 Buff
        │   │   └── AoeEffect.cs                  范围效果
        │   └── Buffs/
        │       └── BuffInstance.cs               Buff 运行时
        ├── Runtime/
        │   └── SkillExecutor.cs              执行管线
        ├── SkillManager.cs               Manager 薄壳
        └── SkillService.cs               业务核心
```

## 初始化流程

```
AbstractGameManager.Awake()
  1. EnsureBaseManagers() → 确保 EventProcessor / DataManager / ResourceManager / UIManager / AudioManager 存在
  2. 反射扫描场景中所有 Manager<T> 子类组件
  3. 按 [Manager(N)] 优先级排序（N 越小越先）
  4. 依次调用 Initialize()
```

## 通信模式

| 场景 | 推荐方式 |
|---|---|
| Manager → 自己的 Service | 直接调用 `MyService.Instance.XXX()` |
| 跨模块解耦调用 | `EventProcessor.Instance.TriggerEvent("EventName", data)` |
| 调用 `[Event]` 标注方法 | `EventProcessor.Instance.TriggerEventMethod("EventName", data)` |
| 监听广播事件 | 方法上加 `[EventListener("EventName")]` |

## Service 自动注册 + 持久化

```
Service 构造 → SingletonNormal.Instance
    ↓
Service.Initialize() → 触发 "OnServiceInitialized"
    ↓
DataService 监听 → 自动注册到 _serviceInstances
    ↓
Application.quitting → DataService.SaveAllCategories() → 所有 IServicePersistence 存盘
```

## ResultCode

`EssSystem.Core.ResultCode`（`Util/ResultCode.cs`）：
- `ResultCode.OK` / `ResultCode.ERROR`
- `ResultCode.Ok(data?)` / `ResultCode.Fail(msg)`
- `ResultCode.IsOk(result)`

## 核心设计原则

- **Manager 是薄壳**：只做事件路由 + 驱动 Service.Tick，不含业务逻辑
- **Service 是业务核心**：纯 C# 单例，可持久化，所有状态和算法在此
- **Capability 组合模式**：Entity 的能力通过 `IEntityCapability` 接口组合，不用继承
- **数据驱动**：Config/Definition 是纯数据，运行时实例持有引用
- **bare-string 事件**：跨模块调用走字符串事件，模块内直接调用 Service API

## 子模块文档

- [Base/Singleton 指南](Base/Singleton/Agent.md)
- [Base/Event 指南](Base/Event/Agent.md)
- [EssManagers 指南](EssManagers/Agent.md)
- [Manager/Service 基类指南](EssManagers/Manager/Agent.md)
- [DataManager 指南](EssManagers/Foundation/DataManager/Agent.md)
- [ResourceManager 指南](EssManagers/Foundation/ResourceManager/Agent.md)
- [AudioManager 指南](EssManagers/Foundation/AudioManager/Agent.md)
- [UIManager 指南](EssManagers/Presentation/UIManager/Agent.md)
- [InventoryManager 指南](EssManagers/Gameplay/InventoryManager/Agent.md)
- [CharacterManager 指南](EssManagers/Gameplay/CharacterManager/Agent.md)
- [MapManager 指南](EssManagers/Gameplay/MapManager/Agent.md)
- [EntityManager 指南](EssManagers/Gameplay/EntityManager/Agent.md)
- [BuildingManager 指南](EssManagers/Gameplay/BuildingManager/Agent.md)
- [DialogueManager 指南](EssManagers/Gameplay/DialogueManager/Agent.md)
- [SkillManager 指南](EssManagers/Gameplay/SkillManager/Agent.md)
