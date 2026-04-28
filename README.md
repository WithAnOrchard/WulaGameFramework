# WulaGameFramework

一个基于 **Unity + C#** 的轻量级游戏开发框架，主打 **特性驱动的事件系统**、**Manager/Service 双层单例** 和 **零反射数据持久化**。

> 源码仓库：<https://github.com/WithAnOrchard/WulaGameFramework>

---

## ✨ 核心特性

- 🧩 **泛型单例** — `SingletonNormal<T>`（普通类，懒加载线程安全）/ `SingletonMono<T>`（MonoBehaviour，自动 GO + DontDestroyOnLoad）
- 🎯 **统一事件中心** — `EventProcessor` 提供事件总线 + `[Event]`/`[EventListener]` 自动注册（启动时反射扫描）
- 📦 **零反射持久化** — `Service<T>` 实现 `IServicePersistence`，DataService 通过接口直接调用，按 `{TypeName}/{Category}.json` 分文件存
- 🎨 **DAO / Entity / Service 三层 UI** — 纯数据 `UIComponent` + Unity 端 `UIEntity` + 工厂 `UIEntityFactory`
- 🎒 **背包系统** — 物品模板、多容器、堆叠/权重、配置驱动
- 🖼️ **资源管理** — Prefab/Sprite/AudioClip/Texture 异步加载 + 缓存（`ResourceKey` 结构体键，零字符串拼接）
- 🧮 **优先级 Manager** — `[Manager(N)]` 控制 Awake 顺序（基于 Unity `DefaultExecutionOrder`）

---

## 📁 目录结构

```text
Assets/Scripts/
├── EssSystem/
│   ├── Core/                                    # 核心层
│   │   ├── AbstractGameManager.cs               # 启动入口：自动发现并初始化所有 Manager
│   │   ├── EssManagers/
│   │   │   ├── Manager/                         # Manager<T> / Service<T> 基类
│   │   │   ├── DataManager/                     # 数据持久化 + Service 自动注册
│   │   │   ├── ResourceManager/                 # 资源加载/缓存
│   │   │   └── UIManager/                       # UI 实体注册中心 (Dao/Entity/Editor)
│   │   ├── Event/                               # EventProcessor + [Event]/[EventListener]
│   │   ├── Singleton/                           # SingletonNormal / SingletonMono
│   │   └── Util/                                # AssemblyUtils, MainThreadDispatcher, MiniJson, ResultCode
│   └── Manager/
│       └── InventoryManager/                    # 业务示例：背包系统
└── GameManager.cs                               # 游戏入口示例（继承 AbstractGameManager）
```

---

## 🧱 架构分层

```text
┌────────────────────────────────────────────────────┐
│ 业务 Manager (10+)  InventoryManager / ...         │
└────────────────────────────────────────────────────┘
              ▲
┌────────────────────────────────────────────────────┐
│ 框架 Manager:  EventProcessor(-30)                 │
│              → DataManager(-20)                    │
│              → ResourceManager(0) → UIManager(5)   │
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
- **TextMeshPro**（`UIEntity` 直接 `using TMPro`）

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
using EssSystem.Core.EssManagers.Manager;

// 1) 定义 Service（自动被 DataService 发现并持久化）
public class ScoreService : Service<ScoreService>
{
    public const string CAT = "Stats";

    [Event("AddScore")]
    public List<object> OnAddScore(List<object> args)
    {
        var cur = GetData<int>(CAT, "score");
        SetData(CAT, "score", cur + (int)args[0]);
        return ResultCode.Ok();
    }
}

// 2) 任意地方触发事件
EventProcessor.Instance.TriggerEventMethod("AddScore", new List<object> { 10 });
```

应用退出时 `ScoreService` 数据自动写入 `{persistentDataPath}/ServiceData/ScoreService/Stats.json`。

---

## 🛠️ 关键模块

### Event 系统

- `[Event("xxx")]` — 单点 RPC 调用，通过 `TriggerEventMethod` 直接调
- `[EventListener("xxx", Priority = N)]` — 广播订阅（多监听者按优先级）
- `EventProcessor` 启动时反射扫描所有用户程序集（`AssemblyUtils.IsSystemAssembly` 跳过引擎/系统）
- 返回值：`ResultCode.Ok(data?)` / `ResultCode.Fail(msg)` / `ResultCode.IsOk(result)`
- 延迟 Target 解析（`ResolveTarget`）：扫描期低优先级 Manager 未 Awake 时不报错

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

### 资源管理

| Event | 用途 |
|---|---|
| `GetSprite/GetPrefab/GetAudioClip/GetTexture` | 同步获取（命中缓存） |
| `LoadXXXAsync` | 异步加载（返回 `["加载中"]` 或 `["成功", asset]`） |
| `GetExternalSprite` / `LoadExternalSpriteAsync` | 加载外部图片文件 |
| `AddPreloadConfig` | 添加预加载项（持久化保存，下次启动自动加载） |
| `UnloadResource` / `UnloadAllResources` | 卸载 |

`ResourceKey` 结构体作为缓存键，避免字符串拼接产生 GC。

---

## 📖 模块文档

每个核心子目录都有 `Agent.md` 说明，深入用法请直接查阅：

- [Core 总览](Scripts/EssSystem/Core/Agent.md)
- [Singleton](Scripts/EssSystem/Core/Singleton/Agent.md)
- [Event](Scripts/EssSystem/Core/Event/Agent.md)
- [EssManagers](Scripts/EssSystem/Core/EssManagers/Agent.md) → [Manager 基类](Scripts/EssSystem/Core/EssManagers/Manager/Agent.md) / [DataManager](Scripts/EssSystem/Core/EssManagers/DataManager/Agent.md) / [ResourceManager](Scripts/EssSystem/Core/EssManagers/ResourceManager/Agent.md) / [UIManager](Scripts/EssSystem/Core/EssManagers/UIManager/Agent.md)

---

## 🎯 设计亮点

1. **统一事件中心** — `EventProcessor` 同时承担事件总线 + 属性扫描自动注册（合并自原 `EventManager`）
2. **零反射持久化** — `IServicePersistence` 接口替代反射，DataService 直接调用
3. **优先级 Manager** — `[Manager(N)]` 等价 `[DefaultExecutionOrder]`，声明式依赖顺序
4. **延迟 Target 解析** — 扫描期低优先级 Manager 未就绪时调用时再解析，避免初始化竞争
5. **DAO 可序列化 / Entity 不可序列化** — UI 布局可持久化，Unity 实体运行时重建
6. **解耦但不教条** — 内部强类型直调（性能/类型安全），跨模块走 Event（解耦），合理混用

---

## 🧪 独立编译检查

`tools/compile_check.ps1` 用 Roslyn 命令行做语法+符号校验，**不需要打开 Unity**：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\compile_check.ps1
```

输出 `.build/WulaGameFramework.dll`，编译日志在 `.build/compile.log`。

---

## 📜 许可证

仓库未随附 LICENSE 文件，使用前请联系原作者 [@WithAnOrchard](https://github.com/WithAnOrchard) 确认授权。

## 🙋 致谢

- 原作者：[WithAnOrchard](https://github.com/WithAnOrchard)
- 仓库：<https://github.com/WithAnOrchard/WulaGameFramework>
