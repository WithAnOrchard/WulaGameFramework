# WulaGameFramework — 项目级 Agent 指南

> 本文件是整个项目的顶层 Agent 指南，给 AI Agent / 协作开发者用。后续可在此文件追加项目特定约定。

## 项目定位

Unity + C# 轻量级游戏框架，核心思想：**Manager/Service 双层单例 + 统一事件中心 + 零反射持久化**。

代码全部在 `Assets/Scripts/` 下，分为：
- `EssSystem/Core/` — 框架核心
- `EssSystem/Manager/` — 业务 Manager（如 InventoryManager）
- `GameManager.cs` — 项目入口（继承 `AbstractGameManager`）

## 开始之前必读

> 路径均以 `Assets/` 为根（即本文件所在目录）。AI Agent 读文件时请拼接完整 `Assets/...` 路径。

| 内容 | 文档（相对 `Assets/`） |
|---|---|
| 项目总览 / 快速上手 | `README.md` |
| **反模式 / 黑名单**（写代码前必看） | `Anti-Patterns.md` |
| Core 架构 + 子模块入口 | `Scripts/EssSystem/Core/Agent.md` |
| 单例基类 | `Scripts/EssSystem/Core/Singleton/Agent.md` |
| 事件系统 | `Scripts/EssSystem/Core/Event/Agent.md` |
| Manager 系统总览 | `Scripts/EssSystem/Core/EssManagers/Agent.md` |
| Manager / Service 基类 | `Scripts/EssSystem/Core/EssManagers/Manager/Agent.md` |
| 数据持久化 | `Scripts/EssSystem/Core/EssManagers/DataManager/Agent.md` |
| 资源加载 | `Scripts/EssSystem/Core/EssManagers/ResourceManager/Agent.md` |
| UI 实体管理 | `Scripts/EssSystem/Core/EssManagers/UIManager/Agent.md` |

## 关键约定（核心铁律）

### 1. 优先级表（`[Manager(N)]`）

| Manager | 优先级 | 不可改 |
|---|---:|:---:|
| `EventProcessor` | -30 | ⚠️ 必须最先 |
| `DataManager` | -20 | ⚠️ 监听 Service 初始化事件 |
| `ResourceManager` | 0 | |
| `UIManager` | 5 | |
| 业务 Manager | 10+ | InventoryManager(10) |

### 2. 通信路由

```
同模块 Manager → 自己的 Service       直接 Service.Instance.XXX(...)
跨模块解耦                           EventProcessor.TriggerEvent / TriggerEventMethod
广播订阅                             [EventListener("X")]
```

不要在业务模块里直接 `using` 其他业务 Manager 的命名空间——通过事件解耦。

### 3. 文件组织

- 数据类（DAO）放 `Dao/` 文件夹
- UI 表现层（Entity）**只能**放在 `EssSystem/Core/EssManagers/UIManager/Entity/`
- 业务模块只产 `Dao` + `Service` + `Manager`

### 4. 命名/返回值

- `[Event]` 方法名：动词开头（`OpenInventoryUI`, `GetUIEntity`）
- `[EventListener]` 方法名：`On` 开头（`OnPlayerDamage`）
- 返回值统一用 `ResultCode.Ok(data?)` / `ResultCode.Fail(msg)`，调用方用 `ResultCode.IsOk(result)` 判断

### 4.1 事件名常量化（**强制规则**）

每个 `[Event]` 必须在所属类暴露 `public const string EVT_XXX = "...";`，且 `[Event(EVT_XXX)]` 必须引用常量。**禁止**在 `[Event("...")]` 或调用方写魔法字符串。

**定义方**：
```csharp
public class UIManager : Manager<UIManager>
{
    public const string EVT_GET_ENTITY = "GetUIEntity";

    [Event(EVT_GET_ENTITY)]
    public List<object> GetUIEntity(List<object> data) { ... }
}
```

**调用方**：
```csharp
EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_GET_ENTITY, data);
```

**收益**：IDE 跳转可达 / 重命名安全 / AI 不会拼错事件名 / 全局可搜索。

已应用到：`UIManager` (4 个)、`InventoryManager` (2 个)、`Service<T>.EVT_INITIALIZED`、`InventoryService.EVT_*` (5 个)。新增模块**必须遵守**。

### 5. Service 持久化

- 数据进 `SetData(category, key, value)` 即自动保存
- 写自定义数据类必须 `[Serializable]`
- **绝不**存 `GameObject` / `MonoBehaviour`（不可序列化）

## 当前架构状态（重要历史决策）

| 主题 | 当前状态 |
|---|---|
| `EventManager` | **已合并入** `EventProcessor`（namespace `EssSystem.Core.Event`） |
| `Event` 类 | **重命名为** `EventBase`（避免与 namespace 同名冲突） |
| `Manager<T>` | **删除**了 7 个空生命周期占位（FixedUpdate/LateUpdate/OnEnable/OnDisable/OnApplicationFocus/OnApplicationPause/Cleanup），子类需要直接声明 |
| `EnableLogging` | 改为自动属性 |
| `ResultCode` | 移到 `Util/ResultCode.cs`，namespace 仍为 `EssSystem.Core` |
| `IServicePersistence` | 非泛型接口，DataService 通过它持久化（零反射） |
| `ResolveTarget` | 调用时延迟解析，修复扫描期 Manager 未 Awake 问题 |
| `UIService` 中 `ServiceXxx` 事件 | **已删除**（冗余 thin wrapper） |
| `InventoryManager` | 改为通过 Event 调用 UIManager，完全解耦 |

## 工具脚本

- `tools/compile_check.ps1` — Roslyn 命令行编译校验，无需开 Unity
- `tools/test_minijson.ps1` — MiniJson round-trip 回归测试
- **`tools/new-module.ps1`** — 业务 Manager 脚手架生成器（推荐 AI 使用）
- **`tools/agent_lint.ps1`** — Event/Agent.md 一致性校验（**提交前必跑**）
- **`tools/install-hooks.ps1`** — 安装 git pre-commit hook（一次性）；之后每次 `git commit` 自动跑 `agent_lint -Strict`，破坏规则时 commit 被拒

```powershell
# 报告差异（默认）
.\tools\agent_lint.ps1
# CI / pre-commit：任一警告/错误退出码 1
.\tools\agent_lint.ps1 -Strict
# 详细列出每个 EVT_XXX
.\tools\agent_lint.ps1 -Verbose
```

**校验项（5 步）**：
1. 任何 `[Event("...")]` / `[EventListener("...")]` 裸字符串 → **错误**
2. 收集所有 `public const string EVT_XXX = "..."`（含别名 `= OtherClass.EVT_*`）
3. 每个 EVT 必须在所属模块 `Agent.md` 中出现常量名 → **错误**
4. 含 `[Event]` 的模块文件夹必须有 `Agent.md` 且含 `## Event API` 章节
5. 根 `Agent.md` 全局 Event 索引必须覆盖所有非别名 EVT（允许 `Service<T>.EVT_*` 泛型形式）

附加：检测同字符串多源（`_eventMethods` 字典 key 冲突），输出 WARN。

```powershell
# 创建一个新业务 Manager（例如 QuestManager，优先级 15）
.\tools\new-module.ps1 -Name Quest -Priority 15
```

自动生成 `Manager/QuestManager/` 目录，含 `QuestManager.cs` / `QuestService.cs` / `Dao/QuestData.cs` / `Agent.md`，全部按照 4.1 节常量化规范模板预填，AI/作者只需填业务，**不会忘记任何 boilerplate**。

## AI 行为契约（**核心，所有 AI Agent 必读**）

### 写代码 _之前_

| 场景 | 必读文件 |
|---|---|
| 新增模块 | `Assets/Agent.md`（本文件）+ `Core/Agent.md` |
| 调用某模块的 Event | 该模块的 `Agent.md` 的 `## Event API` 章节 |
| 修改 Manager / Service 基类 | `EssManagers/Manager/Agent.md` |
| 修改 Event 系统 | `Event/Agent.md` |
| 修改持久化 | `EssManagers/DataManager/Agent.md` |

读不到 Event 的参数/返回值定义时，**不要猜**——先用 `grep_search` 在源码里找 `[Event(EVT_XXX)]` 看签名，或要求作者补 Agent.md。

### 写代码 _之后_

任何修改了 `[Event]` / 新增了 `[Event]` / 修改了事件参数的代码改动，**必须**同步更新所属模块 `Agent.md` 的 `## Event API` 章节。**这是硬性规则**。

修改完后一句话自查：「我新增/改了哪个 EVT_XXX？对应 Agent.md 改了吗？」

### Event API Schema（每个模块 Agent.md 必备）

每个模块的 `Agent.md` **必须**含 `## Event API` 章节，按以下 schema 罗列所有 `[Event]`：

```markdown
## Event API

### `EVT_REGISTER_ENTITY` — 注册 UI 实体
- **常量**: `UIManager.EVT_REGISTER_ENTITY` = `"RegisterUIEntity"`
- **参数**: `[string daoId, UIComponent component]`
- **返回**: `ResultCode.Ok(UIEntity)` / `ResultCode.Fail(msg)`
- **副作用**: 创建 GameObject 挂到 UI Canvas
- **示例**:
  ```csharp
  EventProcessor.Instance.TriggerEventMethod(
      UIManager.EVT_REGISTER_ENTITY,
      new List<object> { "panel_id", panelComponent });
  ```
```

**字段说明**：
- **常量**：完整引用路径，包含字符串值（让 AI 知道两边等价）
- **参数**：按顺序列每个位置参数的类型和含义；可选参数标 `(可选)`
- **返回**：成功/失败两种情况都要写
- **副作用**：是否创建 GameObject、修改持久化数据、触发其他事件等
- **示例**：可直接复制粘贴运行的代码片段

### 全局 Event 索引

为了让 AI 不必读所有 Agent.md 就能定位某个 Event，每次新增 Event 时**必须**同步更新本节。

| 常量 | 字符串值 | 定义模块 | 用途 |
|---|---|---|---|
| `Service<T>.EVT_INITIALIZED` | `OnServiceInitialized` | Core/Manager | Service 初始化完成（DataService 监听用于自动注册） |
| `UIManager.EVT_REGISTER_ENTITY` | `RegisterUIEntity` | Core/UIManager | 注册 UI 实体（命令） |
| `UIManager.EVT_GET_ENTITY` | `GetUIEntity` | Core/UIManager | 获取已注册的 UI 实体（查询） |
| `UIManager.EVT_UNREGISTER_ENTITY` | `UnregisterUIEntity` | Core/UIManager | 注销并销毁 UI 实体（命令） |
| `UIManager.EVT_HOT_RELOAD` | `HotReloadUIConfigs` | Core/UIManager | 热重载 UI 配置（命令） |
| `InventoryManager.EVT_OPEN_UI` | `OpenInventoryUI` | InventoryManager | 打开背包 UI（命令） |
| `InventoryManager.EVT_CLOSE_UI` | `CloseInventoryUI` | InventoryManager | 关闭背包 UI（命令） |
| `InventoryService.EVT_CREATE` | `InventoryCreate` | InventoryManager | 创建容器 |
| `InventoryService.EVT_DELETE` | `InventoryDelete` | InventoryManager | 删除容器 |
| `InventoryService.EVT_ADD` | `InventoryAdd` | InventoryManager | 添加物品 |
| `InventoryService.EVT_REMOVE` | `InventoryRemove` | InventoryManager | 移除物品 |
| `InventoryService.EVT_MOVE` | `InventoryMove` | InventoryManager | 槽位移动 |
| `InventoryService.EVT_QUERY` | `InventoryQuery` | InventoryManager | 查询容器 |
| `InventoryService.EVT_CHANGED` | `InventoryChanged` | InventoryManager | 背包变化**广播**（用 `[EventListener]` 订阅） |
| `InventoryService.EVT_OPEN_UI` | `OnOpenInventoryUI` | InventoryManager | UI 已打开**广播**（订阅用，区别于 `InventoryManager.EVT_OPEN_UI`） |
| `InventoryService.EVT_CLOSE_UI` | `OnCloseInventoryUI` | InventoryManager | UI 已关闭**广播**（订阅用） |
| `ResourceManager.EVT_GET_PREFAB` | `GetPrefab` | Core/ResourceManager | 同步取 Prefab（仅缓存） |
| `ResourceManager.EVT_GET_SPRITE` | `GetSprite` | Core/ResourceManager | 同步取 Sprite |
| `ResourceManager.EVT_GET_AUDIO_CLIP` | `GetAudioClip` | Core/ResourceManager | 同步取 AudioClip |
| `ResourceManager.EVT_GET_TEXTURE` | `GetTexture` | Core/ResourceManager | 同步取 Texture |
| `ResourceManager.EVT_GET_EXTERNAL_SPRITE` | `GetExternalSprite` | Core/ResourceManager | 同步取外部图片缓存 |
| `ResourceManager.EVT_LOAD_PREFAB_ASYNC` | `LoadPrefabAsync` | Core/ResourceManager | 异步加载 Prefab |
| `ResourceManager.EVT_LOAD_SPRITE_ASYNC` | `LoadSpriteAsync` | Core/ResourceManager | 异步加载 Sprite |
| `ResourceManager.EVT_LOAD_EXTERNAL_SPRITE_ASYNC` | `LoadExternalSpriteAsync` | Core/ResourceManager | 异步加载外部图片 |
| `ResourceManager.EVT_ADD_PRELOAD_CONFIG` | `AddPreloadConfig` | Core/ResourceManager | 添加预加载项（持久化） |
| `ResourceManager.EVT_UNLOAD_RESOURCE` | `UnloadResource` | Core/ResourceManager | 卸载单个（与 Service 同名） |
| `ResourceManager.EVT_UNLOAD_ALL_RESOURCES` | `UnloadAllResources` | Core/ResourceManager | 全量卸载（与 Service 同名） |
| `ResourceService.EVT_DATA_LOADED` | `OnResourceDataLoaded` | Core/ResourceManager | 启动后跳预加载（内部） |
| `ResourceService.EVT_GET_RESOURCE` | `GetResource` | Core/ResourceManager | 同步获取底层（内部） |
| `ResourceService.EVT_LOAD_RESOURCE_ASYNC` | `LoadResourceAsync` | Core/ResourceManager | 异步加载底层（内部） |
| `ResourceService.EVT_LOAD_EXTERNAL_IMAGE_ASYNC` | `LoadExternalImageAsync` | Core/ResourceManager | 外部图片加载底层（内部） |
| `ResourceService.EVT_ADD_RESOURCE_CONFIG` | `AddResourceConfig` | Core/ResourceManager | 写预加载配置（内部） |
| `ResourceService.EVT_EXTERNAL_IMAGE_LOADED` | `OnExternalImageLoaded` | Core/ResourceManager | 外部图片加载成功**广播** |
| `ResourceService.EVT_EXTERNAL_IMAGE_LOAD_FAILED` | `OnExternalImageLoadFailed` | Core/ResourceManager | 外部图片加载失败**广播** |

> ⚠️ **命名冲突警示**：`InventoryManager.EVT_OPEN_UI` 是**命令**（`"OpenInventoryUI"`），`InventoryService.EVT_OPEN_UI` 是**广播**（`"OnOpenInventoryUI"`）。命令由调用方主动触发；广播由 Service 在 UI 实际打开后发出，供其他模块监听。混用会找不到 handler。

> ⚠️ **façade vs Service 同名**：`ResourceManager.EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` 与 `ResourceService.EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` **字符串相同**，仅后者实际生效（字典覆盖）。调用方只需用 façade 常量。

## 项目特定规则（占位，后续追加）

> 此章节预留给项目特定约定，可在此追加：
>
> - 命名规范（变量前缀、私有字段下划线等）
> - 团队约定（PR 流程、commit message 风格）
> - 性能基线（启动时间、扫描耗时）
> - 已知坑点 / 临时 workaround
> - 集成第三方库的注意事项
> - ……
