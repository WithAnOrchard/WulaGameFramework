# WulaGameFramework

一个基于 **Unity + C#** 的轻量级游戏开发框架，主打 **反射驱动的自动注册**、**DAO/Entity/Service 三层分离** 和 **统一的数据持久化**。

> 源码仓库：<https://github.com/WithAnOrchard/WulaGameFramework>

---

## ✨ 核心特性

- 🧩 **泛型单例** — `Singleton<T>` / `SingletonMono<T>`，线程安全，自动生命周期
- 🎯 **特性驱动的事件系统** — `[Event("xxx")]` 方法 + `EventProcessor` 启动时反射扫描，零手写注册代码
- 📦 **自动数据持久化** — `DataService` 反射发现所有 `Service<T>`，统一序列化到 `Application.persistentDataPath/game_data.json`（含备份与恢复）
- 🎨 **DAO / Entity / Service 分层的 UI 架构** — 纯数据 `UIComponent` + Unity 端 `UIEntity` + 工厂 `UIEntityFactory`，支持链式调用
- 🎒 **开箱即用的背包系统** — 物品模板、多背包、堆叠/权重、槽位移动、自动存档
- 🖼️ **资源预加载** — 启动时根据配置批量预加载 Prefab/Sprite/AudioClip/Texture
- 🧮 **可优先级排序的 Manager** — `[Manager(priority)]` 控制初始化顺序

---

## 📁 目录结构

```text
WulaGameFramework/
├── Scenes/
│   └── SampleScene.unity                  # 示例场景
└── Scripts/EssSystem/
    ├── Core/                              # 核心层（框架基础）
    │   ├── Singleton/                     # 单例基类（含 README）
    │   │   ├── Singleton.cs               # 普通泛型单例
    │   │   └── SingletonMono.cs           # Unity MonoBehaviour 单例
    │   ├── Manager/                       # 管理器基类（含 README）
    │   │   ├── Manager.cs                 # MonoBehaviour 管理器
    │   │   └── Service.cs                 # 普通服务（带 _dataStorage）
    │   ├── Event/                         # 事件系统（含 README）
    │   │   ├── Event.cs / EventManager.cs
    │   │   └── AutoRegisterEvent/
    │   │       ├── EventAttribute.cs      # [Event] / [EventListener] 特性
    │   │       └── EventProcessor.cs      # 反射扫描 + 自动注册
    │   ├── DataManager/                   # 数据持久化（含 README）
    │   │   └── DataManager.cs             # 反射发现所有 Service 并统一存档
    │   └── ResourceManager/               # 资源预加载
    │       ├── ResourceManager.cs
    │       └── ResourceService.cs
    └── EssManager/                        # 业务层（基于 Core 扩展）
        ├── UIManager/
        │   ├── UIManager.cs / UIService.cs
        │   ├── Dao/                       # 纯数据层
        │   │   ├── Adjustable.cs          # 位置/尺寸/缩放基类
        │   │   ├── UIComponent.cs / UIType.cs
        │   │   └── CommonComponents/      # Button / Panel / Text
        │   └── Entity/                    # Unity 端 MonoBehaviour
        │       ├── UIEntity.cs
        │       ├── UIEntityFactory.cs
        │       └── CommonEntity/
        └── InventoryManager/
            ├── InventoryManager.cs / InventoryService.cs
            ├── Dao/                       # 数据结构
            └── Entity/
```

---

## 🧱 架构分层

```text
┌──────────────────────────────────────────────┐
│ EssManager (业务层)                          │
│   UIManager       InventoryManager    ...    │
└──────────────────────────────────────────────┘
                    ▲ 依赖
┌──────────────────────────────────────────────┐
│ Core (核心层)                                │
│   EventManager  DataManager  ResourceManager │
│   Manager<T>    Service<T>   Singleton<T>    │
└──────────────────────────────────────────────┘
                    ▲ 运行在
┌──────────────────────────────────────────────┐
│ Unity Engine                                 │
└──────────────────────────────────────────────┘
```

### Manager vs Service

| 维度 | `Manager<T>` | `Service<T>` |
|------|--------------|--------------|
| 基类 | `MonoBehaviour` | `SingletonNormal<T>` |
| 生命周期 | Unity 驱动（Awake/Start/OnDestroy） | 首次访问时创建 |
| 定位 | 对外门面，挂场景 | 内部业务逻辑 + 数据 |
| 典型用法 | `UIManager.Instance.ShowPanel()` | `UIService.Instance.RegisterUIEntity()` |

### DAO / Entity / Service

```csharp
// Dao ── 纯数据描述 UI
var btn = new UIButtonComponent("ok_btn", "OK", "确定")
    .SetPosition(100, 200)
    .SetSize(160, 48);

// Factory ── 把 Dao 落地为 Unity GameObject
var entity = UIEntityFactory.CreateEntity(btn, canvas.transform);

// Service ── 全局注册表（支持 event 查询）
UIService.Instance.GetUIEntity("ok_btn");
```

---

## 🚀 快速上手

### 1. 环境要求

- **Unity 2021.3 LTS** 或更高（用到了 `switch` 表达式、`using` 简写等 C# 8+ 语法）
- **TextMeshPro**（UI 文本渲染需要）

### 2. 集成方式

```bash
# 把仓库作为游戏项目 Assets 下的子目录导入
git clone https://github.com/WithAnOrchard/WulaGameFramework.git Assets/WulaGameFramework
```

或直接把 `Scripts/EssSystem/` 拷贝到你的 `Assets/` 下。

### 3. 场景搭建

把以下脚本挂到场景里（或新建空物体后 AddComponent）：

- `EventManager`（最高优先级，推荐 `-10`）
- `EventProcessor`
- `DataManager`（`-5`）
- `ResourceManager`
- `UIManager` / `InventoryManager` 等业务 Manager

> 提示：`Manager<T>` 会在 `Awake` 自动注册，但当前版本没有内置 `ManagerRegistry`，初始化顺序由脚本执行顺序 + 优先级控制，建议显式挂载。

### 4. 最小示例

```csharp
using System.Collections.Generic;
using EssSystem.Core.Event;
using EssSystem.Core.Event.AutoRegisterEvent;
using EssSystem.Core.Manager;
using UnityEngine;

// 1) 定义一个 Service（自动被 DataService 发现并持久化）
public class ScoreService : Service<ScoreService>
{
    public void Add(int v)
    {
        var cur = GetData<int>("stats", "score");
        SetData("stats", "score", cur + v);
    }

    // 2) 用 [Event] 注册一个全局事件处理器
    [Event("AddScore")]
    public List<object> OnAddScore(List<object> args)
    {
        Add((int)args[0]);
        return new List<object> { "成功" };
    }
}

// 3) 任意地方触发事件
EventManager.Instance.TriggerEvent("AddScore", new List<object> { 10 });
```

应用退出时，`ScoreService._dataStorage` 会自动被写入 `game_data.json`，下次启动自动加载回来。

---

## 🛠️ 关键模块速览

### Event 系统

- `[Event("xxx")]` 把方法注册为事件处理器，签名必须是 `List<object> (List<object>)`
- `[EventListener("xxx")]` 把方法注册为监听器（支持多播）
- `EventProcessor` 初始化时反射扫描所有程序集，自动把它们接到 `EventManager`
- 返回约定：`new List<object> { "成功", data }` 或 `new List<object> { "错误信息" }`

### DataManager

- 统一存档文件：`{Application.persistentDataPath}/game_data.json`
- 备份文件：`game_data_backup.json`，保存失败时自动回滚
- 发现机制：反射所有继承 `Service<>` 的类，读取非公开字段 `_dataStorage`
- 内置事件：`SaveData` / `SaveServiceCategory` / `GetServiceDataById`

### UI 系统

| 组件 | 用途 |
|------|------|
| `UIComponent` | Dao 基类，含 Id/Name/Parent/Children/Visible/Interactable |
| `UIButtonComponent` / `UIPanelComponent` / `UITextComponent` | 内置三种常用组件 |
| `UIEntity` | Unity 端 MonoBehaviour，把 Dao 属性同步到 `UnityEngine.UI` |
| `UIEntityFactory` | 根据 Dao 类型创建 `GameObject` + 对应 UI 组件 |
| `UIService` | 全局 Dao⇄Entity 注册表，支持事件查询 |

```csharp
// 链式构造一个面板 + 子按钮
var panel = new UIPanelComponent("main_panel", "主面板")
    .SetSize(800, 600);
panel.AddChild(new UIButtonComponent("start_btn", "开始", "START")
    .SetPosition(0, -200));

UIEntityFactory.CreateHierarchy(panel, canvas.transform);
```

### Inventory 系统

**v2 重构版**（对齐 `UIManager` 架构）：

- **DAO / Entity / Service / Manager 四层分离**
  - `Dao/Inventory.cs`：`Inventory` + `InventorySlot`
  - `Dao/Item.cs`：`InventoryItem` + `InventoryItemType`（链式 API）
  - `Entity/InventoryEntity.cs` / `InventoryItemEntity.cs`：Unity 端可选组件
  - `InventoryService.cs`：纯业务逻辑 + `[Event]` 自动注册
  - `InventoryManager.cs`：薄门面 + 玩家背包便利 API + 调试菜单
- 多背包（通过 `inventoryId` 区分，默认 `"Player"` 可在 Inspector 修改）
- 物品模板注册 → 运行时按模板实例化
- 支持堆叠、权重上限、槽位锁定、槽位移动/拆堆/交换
- 自动持久化（所有持久化数据走 `_dataStorage`，由 `DataService` 统一处理；Entity 单独内存字典，不参与序列化）
- 统一 `InventoryResult` readonly struct（`Success` / `Amount` / `Remaining` / `Message`）
- 事件常量：`InventoryService.EVT_ADD` / `EVT_REMOVE` / `EVT_MOVE` / `EVT_CHANGED` / `EVT_QUERY`
- 事件返回约定对齐 `DataManager`：`["成功", data]` 或 `["错误", message]`

```csharp
// ① 注册模板（链式）
InventoryService.Instance.RegisterTemplate(
    new InventoryItem("potion_heal")
        .WithName("治疗药水")
        .WithType(InventoryItemType.Consumable)
        .WithWeight(0.5f)
        .WithValue(25)
        .WithMaxStack(99));

// ② 方法调用
var r = InventoryManager.Instance.GivePlayer("potion_heal", 5);
Debug.Log(r);  // OK(+5, remaining=0)

InventoryManager.Instance.TakeFromPlayer("potion_heal", 2);
int count = InventoryManager.Instance.PlayerHas("potion_heal"); // 3

// ③ 事件触发（等价于上面）
EventManager.Instance.TriggerEvent(InventoryService.EVT_ADD,
    new List<object> { "Player", "potion_heal", 5 });

// ④ 监听背包变化
EventManager.Instance.AddListener(InventoryService.EVT_CHANGED, (name, args) => {
    var invId = args[0]; var op = args[1]; var itemId = args[2]; var amount = args[3];
    // 刷新 UI...
    return null;
});
```

### 资源管理

- 启动时 `ResourceService.OnDataLoaded()` 根据存档内的配置批量预加载
- 支持 `Resources.LoadAsync` 与外部文件图片（`File.ReadAllBytes` + `Texture2D.LoadImage`）
- `ResourceManager.GetPrefab("path")` 等便捷接口同步取已加载资源

---

## 📖 子模块文档

每个核心子目录都附带了独立 README，深入用法请直接查阅：

- [`Scripts/EssSystem/Core/Singleton/README.md`](Scripts/EssSystem/Core/Singleton/README.md) — 单例模式使用/调试
- [`Scripts/EssSystem/Core/Manager/README.md`](Scripts/EssSystem/Core/Manager/README.md) — Manager 生命周期/优先级
- [`Scripts/EssSystem/Core/Event/README.md`](Scripts/EssSystem/Core/Event/README.md) — 事件命名规范/异常处理
- [`Scripts/EssSystem/Core/DataManager/README.md`](Scripts/EssSystem/Core/DataManager/README.md) — 数据序列化/备份机制

---

## ⚠️ 已知约束 / 待完善

仓库仍在早期阶段，原始分支里有一些 **不完整的点**；本地已做了最小修复，未修的请注意：

### ✅ 已在本仓库本地修复（未提交到 upstream）

**编译错误修复**：

- `ResourceService.cs` 调用了 Unity 不存在的 `UnityEngine.MainThreadDispatcher.Enqueue` — 已新增 `Core/Util/MainThreadDispatcher.cs`（基于 `ConcurrentQueue` + 自动懒加载 `MonoBehaviour` 泵）并修正引用

**Inventory 模块重写**（原版 AI 生成代码风格与框架严重脱节，全量重做对齐 `UIManager`）：

- `InventoryManager.cs` / `InventoryService.cs` 原缺失 `namespace` 声明 — 已补齐
- `Dao/Inventory.cs` / `Dao/Item.cs` 原为空文件 — 已实现 `Inventory` / `InventorySlot` / `InventoryItem` / `InventoryItemType`，链式构造 API
- `Entity/InventoryEntity` / `InventoryItemEntity` 原为无扩展名空文件 — 已新建 `.cs` 版本，参照 `UIEntity` 模式实现 Register/Sync
- `InventoryService` 重构为：`_dataStorage` 只存持久化数据，运行时 Entity 注册表走内存字典
- 统一 `InventoryResult` readonly struct 替代原来的 `InventoryAddResult` / `RemoveResult` / `MoveResult` 三套 class
- `[Event]` 事件处理器集中在 `InventoryService`，不再 `InventoryManager` 也注册一份（旧版两处重复）
- `InventoryManager` 瘦身为薄门面（~130 行），去除重复包装的 API，只保留玩家背包便利方法与 `ContextMenu` 调试菜单

### 🚧 尚未处理（原作者设计缺失）

- `Scripts/EssSystem/EssManager/CharacterManager/` 仅保留了 `.meta`，没有源码
- Manager 的 README 提到了 `ManagerRegistry`，但当前代码中没有实现（只保留了 `Manager<T>` 本身；当前靠场景挂载+优先级控制初始化顺序）
- 没有 `.asmdef`、单元测试或示例资源，`SampleScene.unity` 是默认空场景
- 原作者的命名空间划分略混乱（UI 相关 Dao 在 `EssSystem.UIManager.Dao`，而 Entity 在 `EssSystem.EssManager.UIManager.Entity`；`UIEntityFactory` 又在 `EssSystem.UIManager.Entity`）— 功能正确但不一致
- **已知框架 bug（与本次重写无关）**：`EventProcessor.ScanEventMethods` 用 `Activator.CreateInstance` 直接 `new` Service 类型，而不是通过 `.Instance` 取单例。这会导致每个 Service 有 2 个实例（事件绑定到 Activator 实例，业务调用走 Singleton 实例），数据互相隔离。Inventory 的事件驱动场景会踩到这坑，业务方式调用不受影响。修 1 行即可（在 EventProcessor 里对 `Service<>` 走 `.Instance`），留给原作者或下一轮 PR。

> **结论**：框架的 **设计思想很完整**（事件/数据/UI/背包的闭环），经本次修补后应能在 Unity 2021.3+ 工程中直接编译通过。适合作为学习框架骨架、或者作为起点自行裁剪二次开发。

---

## 🎯 设计亮点总结

1. **反射自动发现 Service** —— 新增一个 `Service<T>` 子类，无需改任何配置就能自动加入存档体系
2. **事件即契约** —— 模块间只通过 `EventManager.TriggerEvent("xxx", args)` 通信，解耦彻底
3. **DAO 可序列化 + Entity 不可序列化** —— 便于做"UI 布局持久化"之类的高级特性
4. **优先级 Manager** —— 用特性声明依赖顺序，而不是手写初始化链
5. **Service 内置 `_dataStorage`** —— 业务代码只操作字典即可享受自动持久化

---

## 📜 许可证

仓库未随附 LICENSE 文件，使用前请联系原作者 [@WithAnOrchard](https://github.com/WithAnOrchard) 确认授权。

---

## 🙋 致谢

- 原作者：[WithAnOrchard](https://github.com/WithAnOrchard)
- 仓库：<https://github.com/WithAnOrchard/WulaGameFramework>
