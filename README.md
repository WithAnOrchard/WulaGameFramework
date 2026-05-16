# WulaGameFramework

一个基于 **Unity + C#** 的轻量级游戏框架，主打 **特性驱动的事件系统**、**Manager/Service 双层单例**、**零反射数据持久化** 和 **跨模块字符串协议解耦**。

> 源码仓库：<https://github.com/WithAnOrchard/WulaGameFramework>

---

## ✨ 核心特性

- 🧩 **泛型单例** — `SingletonNormal<T>`（纯 C# 懒加载线程安全）/ `SingletonMono<T>`（MonoBehaviour，自动 GO + DontDestroyOnLoad）
- 🎯 **统一事件中心** — `EventProcessor` 提供事件总线 + `[Event]`/`[EventListener]` 反射自动注册；`EVT_XXX` 常量协议 + `agent_lint` 校验
- 📦 **零反射持久化** — `Service<T>` 实现 `IServicePersistence`，DataService 通过接口直接调用，按 `{TypeName}/{Category}.json` 分文件存
- 🎨 **DAO / Entity / Service 三层 UI** — 可序列化 `UIComponent` Dao + Unity `UIEntity` + `UIEntityFactory`，业务模块禁止自建 Canvas/uGUI
- 🧙 **角色系统** — `CharacterManager` 模板化角色，多部件 + 帧事件广播（`EVT_FRAME_EVENT`）
- 🧱 **实体系统** — `EntityManager` 组合 Character + 行为，跨模块只暴露 `Transform` / `string instanceId`
- 🗺️ **2D 地图系统** — `MapManager` 基于模板（TopDownRandom 等），Perlin 高度 + 生物群系 + 河流流量累积
- 🎒 **背包系统** — 物品模板、多容器、堆叠/权重/拆堆，配置驱动 UI，全程通过 Event 调 UIManager
- 🖼️ **资源管理** — Prefab / Sprite / AudioClip / Texture / RuleTile / AnimationClip 异步加载 + 缓存（`ResourceKey` 结构体键，零字符串拼接）
- 🧮 **优先级 Manager** — `[Manager(N)]` 声明式 Awake 顺序（基于 Unity `DefaultExecutionOrder`）
- 📺 **可选业务模块** — `DanmuManager` 接入 B 站直播长连接，DayNight Demo 演示昼夜阶段 / 波次 / 据点 / 建造

---

## 📁 目录结构

```text
Assets/
├── Scripts/
│   ├── EssSystem/
│   │   ├── Core/
│   │   │   ├── AbstractGameManager.cs           # 启动入口：自动发现并初始化所有 Manager
│   │   │   ├── Singleton/                       # SingletonNormal / SingletonMono / PlayModeResetGuard
│   │   │   ├── Event/                           # EventProcessor + [Event]/[EventListener]
│   │   │   ├── Util/                            # AssemblyUtils / MainThreadDispatcher / MiniJson / ResultCode
│   │   │   └── EssManagers/
│   │   │       ├── Manager/                     # Manager<T> / Service<T> 基类 + IServicePersistence
│   │   │       ├── Foundation/
│   │   │       │   ├── DataManager/             # 数据持久化 + Service 自动注册
│   │   │       │   └── ResourceManager/         # 资源加载/缓存
│   │   │       ├── Presentation/
│   │   │       │   └── UIManager/               # UI Dao/Entity/Service
│   │   │       └── Application/
│   │   │           ├── CharacterManager/        # 角色 + 部件 + 帧事件
│   │   │           ├── EntityManager/           # 实体 (Character + 行为)
│   │   │           ├── MapManager/              # 2D 地图（Dao/Templates/TopDownRandom 等）
│   │   │           └── InventoryManager/        # 背包系统
│   │   └── Manager/
│   │       └── DanmuManager/                    # 可选第三方业务：B 站直播弹幕
│   ├── Demo/
│   │   ├── DayNight/                            # 昼夜求生 Demo（WaveSpawn / BaseDefense / Construction / DayNightHud）
│   │   └── DayNight3D/                          # 3D 占位 Demo
│   ├── GameManager.cs                           # 游戏入口示例（继承 AbstractGameManager）
│   └── TestPlayer.cs
├── Resources/                                   # 运行时资源（Sprites / Tiles / DayNight3D...）
├── tools/                                       # PowerShell 工具脚本（lint/compile_check/new-module/install-hooks）
├── Agent.md                                     # 项目顶层 Agent 指南（必读）
├── Anti-Patterns.md                             # 反模式黑名单（写代码前必读）
└── README.md                                    # 本文件
```

---

## 🧱 架构分层

```text
┌────────────────────────────────────────────────────┐
│ 业务 Manager (10+)  InventoryManager / ...         │
└────────────────────────────────────────────────────┘
              ▲
┌────────────────────────────────────────────────────┐
│ 框架 Manager:                                      │
│   EventProcessor(-30) → DataManager(-20)           │
│   → ResourceManager(0) → UIManager(5)              │
│ Application Manager:                                  │
│   InventoryManager(10) → CharacterManager(11)      │
│   → MapManager(12) → EntityManager(13)             │
└────────────────────────────────────────────────────┘
              ▲
┌────────────────────────────────────────────────────┐
│ 基础设施: Singleton / Service / Util               │
└────────────────────────────────────────────────────┘
```

### Manager vs Service

| 维度 | `Manager<T>` | `Service<T>` |
|---|---|---|
| 父类 | `SingletonMono<T>` | `SingletonNormal<T>` |
| Unity 生命周期 | ✅ 挂 GameObject | ❌ 纯 C# 单例 |
| 数据持久化 | ❌ | ✅ 自动 |
| 适用场景 | 需要 Update/场景交互 | 业务逻辑、纯数据管理 |

### 通信模式

| 场景 | 推荐方式 |
|---|---|
| 同模块内 Manager → 自己的 Service | 直接 `Service.Instance.XXX(...)` |
| 跨模块解耦调用 | `EventProcessor.Instance.TriggerEvent("X", data)` |
| 调 `[Event]` 标注的方法 | `EventProcessor.Instance.TriggerEventMethod("X", data)` |
| 监听广播事件 | `[EventListener("X")]` |

---

## 🚀 快速上手

### 1. 环境要求

- **Unity 2022.3 LTS** 或更高（C# 9 features：record、`new()` target-typed 等）
- **uGUI**（`Text` / `Button` / `Image`）—— 不依赖 TextMeshPro。需要高 DPI 文字时用超采样（`FontSize×2 + Size×2 + SetScale(0.5,0.5)`），详见 `Agent.md`

### 2. 集成方式

```bash
# 把 Scripts/EssSystem 拷贝到你的 Assets/ 下
```

或作为子目录导入：

```bash
git clone https://github.com/WithAnOrchard/WulaGameFramework.git
```

### 3. 场景搭建

把空 GameObject 挂上一个继承 `AbstractGameManager` 的脚本（参考 `GameManager.cs`）。
`AbstractGameManager.Awake` 会自动：
1. 确保 4 个基础 Manager（`EventProcessor` / `DataManager` / `ResourceManager` / `UIManager`）存在
2. 反射扫描 GameObject 上所有 `Manager<T>` 子类
3. 按 `[Manager(N)]` 优先级排序并依次初始化

### 4. 最小示例

```csharp
using System.Collections.Generic;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.Base.Manager;

// 1) 定义 Service（自动被 DataService 发现并持久化）
public class ScoreService : Service<ScoreService>
{
    public const string CAT = "Stats";
    public const string EVT_ADD = "AddScore";   // ✅ 定义方必须用常量

    [Event(EVT_ADD)]
    public List<object> OnAddScore(List<object> args)
    {
        var cur = GetData<int>(CAT, "score");
        SetData(CAT, "score", cur + (int)args[0]);
        return ResultCode.Ok();
    }
}

// 2) 跨模块调用：消费方走 bare-string，避免 using 耦合
EventProcessor.Instance.TriggerEventMethod("AddScore", new List<object> { 10 });
```

应用退出时 `ScoreService` 数据自动写入 `{persistentDataPath}/ServiceData/ScoreService/Stats.json`。

> 💡 用 `tools/new-module.ps1 -Name Quest -Priority 15` 一键生成符合规范的业务 Manager 脚手架（含 `Manager.cs` / `Service.cs` / `Dao/` / `Agent.md`）。

---

## 🛠️ 关键模块

### Event 系统

- `[Event(EVT_XXX)]` — 单点 RPC 调用，通过 `TriggerEventMethod` 触发
- `[EventListener("xxx", Priority = N)]` — 广播订阅（多监听者按优先级）
- `EventProcessor` 启动时反射扫描所有用户程序集（`AssemblyUtils.IsSystemAssembly` 跳过引擎/系统）
- 返回值：`ResultCode.Ok(data?)` / `ResultCode.Fail(msg)` / `ResultCode.IsOk(result)`
- 延迟 Target 解析（`ResolveTarget`）：扫描期低优先级 Manager 未 Awake 时不报错
- **事件名常量化（强制规则）**：定义方 `[Event(EVT_X)]` 必须引用常量；跨模块消费方走 bare-string，避免 `using` 业务模块。`agent_lint.ps1` 在 pre-commit 强制执行

### DataManager / Service<T>

- `Service<T>` 实现 `IServicePersistence`，DataService 通过接口直接调用 `SaveAllCategories()`，**零反射**
- 路径：`{persistentDataPath}/ServiceData/{TypeName}/{Category}.json`（按分类拆文件）
- `SetData(category, key, value)` 立即保存该分类
- 序列化：MiniJson（pretty）+ `AssemblyQualifiedName` 类型标注，支持嵌套对象还原
- Service 自动注册：`Initialize` 触发 `OnServiceInitialized` 事件，DataService 监听后入列表

### UI 系统

| 类型 | 用途 |
|---|---|
| `UIComponent` | Dao 基类（Id / Name / Children / Visible / Interactable） |
| `UIPanelComponent` / `UIButtonComponent` / `UITextComponent` | 内置组件类型 |
| `UIEntity` | Unity 端 MonoBehaviour，把 Dao 同步到 UGUI/TMP |
| `UIEntityFactory` | 根据 Dao 类型创建 GameObject 树 |
| `UIService` | UIEntity 内存缓存 + 注册/注销/销毁 API |

```csharp
var panel = new UIPanelComponent("inv", "背包")
    .SetPosition(960, 540).SetSize(680, 560).SetVisible(true);
panel.AddChild(new UIButtonComponent("inv_close", "关闭", "×"));

EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity",
    new List<object> { panel.Id, panel });
```

### Inventory 系统

`InventoryManager`（`[Manager(10)]`）+ `InventoryService`：

- DAO / Service / Manager 三层
- 物品模板（`InventoryItem` 链式 API）
- 多容器（玩家背包 / 箱子 / ...）
- 堆叠 / 权重 / 槽位移动 / 拆堆
- 自动持久化（数据走 `_dataStorage`）
- 配置驱动 UI（`InventoryConfig` + `PanelConfig` + `SlotConfig` + `ButtonConfig`）
- **完全通过 Event 调用 UIManager**，零直接依赖

### Character / Entity 系统

- `CharacterManager`（`[Manager(11)]`）：模板化角色 + 多部件 + 动画帧事件广播 (`CharacterService.EVT_FRAME_EVENT`)
- `EntityManager`（`[Manager(13)]`）：组合 `Character` + 行为脚本，跨模块协议只暴露 `Transform` 和 `string instanceId`，不泄露内部 `Character` / `Entity` 类型
- 创建：`EVT_CREATE_CHARACTER` / `EVT_CREATE_ENTITY`，返回 `Transform`

### Map 系统

`MapManager`（`[Manager(12)]`）：

- 模板化生成（`Dao/Templates/TopDownRandom` 等），运行时通过 `MapTemplateRegistry` 注册
- 公共抽象：`Map` / `Chunk` / `Tile` / `TileType` / `MapConfig` / `IMapGenerator`（`Dao/`）
- 当前模板：Perlin 高度 + 生物群系分类 + 区域级河流流量累积（D8 + 池塘逃逸 + 海岸出口）
- 目前以纯 C# API（`MapService.Instance.XXX`）为主，无跨模块 Event

### Danmu 系统（可选）

`DanmuManager` —— 接入 B 站直播长连接，广播弹幕 / 礼物 / 原始 `DanmakuModel`：

- `EVT_CONNECTED` / `EVT_DISCONNECTED` / `EVT_DANMAKU` / `EVT_GIFT` / `EVT_RAW`
- 业务监听走标准 `[EventListener("OnDanmuComment")]`，无需依赖此模块

### DayNight Demo

`Demo/DayNight/` 演示完整业务集成：

- `DayNightGameManager`：昼夜阶段切换广播
- `WaveSpawnManager`（20）/ `BaseDefenseManager`（21）/ `ConstructionManager`（22）/ `DayNightHudManager`（23）
- 全部模块通过 Event 解耦，零跨模块 `using`

### 资源管理

| Event | 用途 |
|---|---|
| `GetSprite` / `GetPrefab` / `GetAudioClip` / `GetTexture` / `GetRuleTile` | 同步获取（命中缓存） |
| `GetAnimationClip` / `GetModelClips` / `GetAllModelPaths` | 取 FBX/Model 内动画 |
| `LoadPrefabAsync` / `LoadSpriteAsync` / `LoadRuleTileAsync` | 异步加载（`ResultCode.Ok(asset)`） |
| `GetExternalSprite` / `LoadExternalSpriteAsync` | 加载外部图片文件（含 `OnExternalImageLoaded` 广播） |
| `AddPreloadConfig` | 添加预加载项（持久化保存，下次启动自动加载） |
| `UnloadResource` / `UnloadAllResources` | 卸载 |
| `OnResourcesLoaded` | 资源全部预加载/索引完成**广播** |

`ResourceKey` 结构体作为缓存键，避免字符串拼接产生 GC。

---

## 📖 模块文档

每个核心子目录都有 `Agent.md` 说明，深入用法请直接查阅：

- 项目顶层：[Agent.md](Agent.md)（**必读，含全局 Event 索引**）/ [Anti-Patterns.md](Anti-Patterns.md)
- Core：[Core 总览](Scripts/EssSystem/Core/Agent.md) / [Managers 总览](Scripts/EssSystem/Core/Managers.md) / [Manager 基类](Scripts/EssSystem/Core/Base/Manager/Agent.md) / [Singleton](Scripts/EssSystem/Core/Base/Singleton/Agent.md) / [Event](Scripts/EssSystem/Core/Base/Event/Agent.md)
- Foundation：[DataManager](Scripts/EssSystem/Core/Foundation/DataManager/Agent.md) / [ResourceManager](Scripts/EssSystem/Core/Foundation/ResourceManager/Agent.md)
- Presentation：[AudioManager](Scripts/EssSystem/Core/Presentation/AudioManager/Agent.md) / [UIManager](Scripts/EssSystem/Core/Presentation/UIManager/Agent.md) / [CharacterManager](Scripts/EssSystem/Core/Presentation/CharacterManager/Agent.md)
- Application/SingleManagers：[InventoryManager](Scripts/EssSystem/Core/Application/SingleManagers/InventoryManager/Agent.md) / [EntityManager](Scripts/EssSystem/Core/Application/SingleManagers/EntityManager/Agent.md) / [DialogueManager](Scripts/EssSystem/Core/Application/SingleManagers/DialogueManager/Agent.md)
- Application/MultiManagers：[MapManager](Scripts/EssSystem/Core/Application/MultiManagers/MapManager/Agent.md) / [BuildingManager](Scripts/EssSystem/Core/Application/MultiManagers/BuildingManager/Agent.md) / [SkillManager](Scripts/EssSystem/Core/Application/MultiManagers/SkillManager/Agent.md)
- 第三方业务：[DanmuManager](Scripts/EssSystem/Manager/DanmuManager/Agent.md)
- Demo：[DayNight](Scripts/Demo/DayNight/Agent.md)

---

## 🎯 设计亮点

1. **统一事件中心** — `EventProcessor` 同时承担事件总线 + 属性扫描自动注册（合并自原 `EventManager`）
2. **零反射持久化** — `IServicePersistence` 接口替代反射，DataService 直接调用
3. **优先级 Manager** — `[Manager(N)]` 等价 `[DefaultExecutionOrder]`，声明式依赖顺序
4. **延迟 Target 解析** — 扫描期低优先级 Manager 未就绪时调用时再解析，避免初始化竞争
5. **DAO 可序列化 / Entity 不可序列化** — UI 布局可持久化，Unity 实体运行时重建
6. **跨模块协议中立类型** — 事件参数只用 `GameObject` / `Transform` / `string id`，避免泄露 `UIEntity` / `Character` / `Entity` 等模块私有类型
7. **事件名非对称协议** — 定义方常量（重命名安全）+ 消费方字符串（零 using 耦合），由 `agent_lint` 兜底
8. **解耦但不教条** — 同模块内强类型直调（性能/类型安全），跨模块走 Event（解耦），合理混用

---

## 🧪 工具脚本

所有脚本位于 `tools/`，PowerShell 可直接调用：

| 脚本 | 用途 |
|---|---|
| `compile_check.ps1` | Roslyn 命令行语法+符号校验，无需 Unity；输出 `.build/WulaGameFramework.dll` + `compile.log` |
| `agent_lint.ps1` | Event/Agent.md 一致性校验（`-Strict` 模式 CI / pre-commit 用），扫描 `[Event]` 裸字符串、`EVT_XXX` 常量声明、模块 Agent.md 覆盖率 |
| `new-module.ps1` | 业务 Manager 脚手架生成器：`-Name Quest -Priority 15` 一键产生 `Manager.cs` / `Service.cs` / `Dao/` / `Agent.md`，全部按规范预填 |
| `install-hooks.ps1` | 一次性安装 git pre-commit hook（提交前自动跑 `agent_lint -Strict`，破坏规则即拒绝 commit） |
| `test_minijson.ps1` | MiniJson round-trip 回归测试 |

```powershell
# 编译检查
powershell -NoProfile -ExecutionPolicy Bypass -File tools\compile_check.ps1

# Event/Agent.md 校验（建议提交前手动跑一次）
.\tools\agent_lint.ps1 -Strict

# 创建新业务 Manager
.\tools\new-module.ps1 -Name Quest -Priority 15
```

---

## 📜 许可证

仓库未随附 LICENSE 文件，使用前请联系原作者 [@WithAnOrchard](https://github.com/WithAnOrchard) 确认授权。

## 🙋 致谢

- 原作者：[WithAnOrchard](https://github.com/WithAnOrchard)
- 仓库：<https://github.com/WithAnOrchard/WulaGameFramework>
