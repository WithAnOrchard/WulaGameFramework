# WulaGameFramework — 项目级 Agent 指南

> 本文件是整个项目的顶层 Agent 指南，给 AI Agent / 协作开发者用。后续可在此文件追加项目特定约定。

## 项目定位

Unity + C# 轻量级游戏框架，核心思想：**Manager/Service 双层单例 + 统一事件中心 + 零反射持久化**。

代码全部在 `Assets/Scripts/` 下，分为：
- `EssSystem/Core/` — 框架核心 + 内置 Manager（`EssManagers/`：UIManager / ResourceManager / CharacterManager / EntityManager / MapManager / InventoryManager 等）
- `EssSystem/Manager/` — 第三方/外置业务 Manager（目前仅 `DanmuManager`）
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
| 数据持久化 | `Scripts/EssSystem/Core/EssManagers/Foundation/DataManager/Agent.md` |
| 资源加载 | `Scripts/EssSystem/Core/EssManagers/Foundation/ResourceManager/Agent.md` |
| UI 实体管理 | `Scripts/EssSystem/Core/EssManagers/Presentation/UIManager/Agent.md` |
| 音频系统 | `Scripts/EssSystem/Core/EssManagers/Presentation/AudioManager/Agent.md` |
| 角色系统 | `Scripts/EssSystem/Core/EssManagers/Application/CharacterManager/Agent.md` |
| 实体系统 | `Scripts/EssSystem/Core/EssManagers/Application/EntityManager/Agent.md` |
| 建筑系统 | `Scripts/EssSystem/Core/EssManagers/Application/BuildingManager/Agent.md` |
| 背包系统 | `Scripts/EssSystem/Core/EssManagers/Application/InventoryManager/Agent.md` |
| 2D 地图系统 | `Scripts/EssSystem/Core/EssManagers/Application/MapManager/Agent.md` |
| 对话系统 | `Scripts/EssSystem/Core/EssManagers/Application/DialogueManager/Agent.md` |
| 弹幕系统（可选） | `Scripts/EssSystem/Manager/DanmuManager/Agent.md` |
| 昼夜求生 Demo | `Scripts/Demo/DayNight/Agent.md` |

## 关键约定（核心铁律）

### 1. 优先级表（`[Manager(N)]`）

| Manager | 优先级 | 不可改 |
|---|---:|:---:|
| `EventProcessor` | -30 | ⚠️ 必须最先 |
| `DataManager` | -20 | ⚠️ 监听 Service 初始化事件 |
| `ResourceManager` | 0 | |
| `AudioManager` | 3 | Core/EssManagers/Foundation 下 |
| `UIManager` | 5 | |
| `InventoryManager` | 10 | Core/EssManagers 下 |
| `CharacterManager` | 11 | Core/EssManagers 下 |
| `MapManager` | 12 | Core/EssManagers 下 |
| `EntityManager` | 13 | Core/EssManagers 下，依赖 CharacterManager + MapManager |
| `BuildingManager` | 14 | Core/EssManagers 下，依赖 EntityManager |
| `DialogueManager` | 15 | Core/EssManagers 下 |
| 其它业务 Manager | 16+ | 新增默认起步 |
| `WaveSpawnManager`（Demo） | 20 | Demo/DayNight 下 |
| `BaseDefenseManager`（Demo） | 21 | Demo/DayNight 下 |
| `ConstructionManager`（Demo） | 22 | Demo/DayNight 下 |
| `DayNightHudManager`（Demo） | 23 | Demo/DayNight 下，依赖 UIManager |

### 2. 通信路由

```
同模块 Manager → 自己的 Service       直接 Service.Instance.XXX(...)
跨模块解耦                           EventProcessor.TriggerEvent / TriggerEventMethod
广播订阅                             [EventListener("X")]
```

不要在业务模块里直接 `using` 其他业务 Manager 的命名空间——通过事件解耦。

**协议层只用 Unity 原生类型 + 字符串 ID**：跨模块事件参数避免暴露 `UIEntity` / `Character` / `Entity` 等模块私有类型，改用 `GameObject` / `Transform` / `string instanceId`。这样调用方无需 `using` 对方模块的 `Entity` / `Dao` 命名空间。

例：`UIManager.EVT_GET_UI_GAMEOBJECT` 返回 `GameObject` 而非 `UIEntity`；`CharacterManager.EVT_CREATE_CHARACTER` 返回 `Transform` 而非 `Character`。

### 3. 文件组织

- 数据类（DAO）放 `Dao/` 文件夹
- UI 表现层（Entity）**只能**放在 `EssSystem/Core/EssManagers/Presentation/UIManager/Entity/`
- 业务模块只产 `Dao` + `Service` + `Manager`

### 4. 命名/返回值

- `[Event]` 方法名：动词开头（`OpenInventoryUI`, `GetUIEntity`）
- `[EventListener]` 方法名：`On` 开头（`OnPlayerDamage`）
- 返回值统一用 `ResultCode.Ok(data?)` / `ResultCode.Fail(msg)`，调用方用 `ResultCode.IsOk(result)` 判断

### 4.1 事件名常量化（**强制规则**）

事件名遵循"**定义方常量、消费方字符串**"的非对称规则。这同时满足两个目标：发布者重命名安全、消费者跨模块零 `using` 编译耦合。

**① 定义方（事件发布者：Manager / Service 自己）**——必须用常量

```csharp
public class UIManager : Manager<UIManager>
{
    public const string EVT_GET_ENTITY = "GetUIEntity";

    [Event(EVT_GET_ENTITY)]                        // ✅ 必须引用常量
    public List<object> GetUIEntity(List<object> data) { ... }
}
```

`[Event("GetUIEntity")]` 直接写字符串会被 `agent_lint.ps1 [1]` 拒绝。

**② 消费方（监听方 / 调用方）**——走字符串协议

```csharp
// ✅ 跨模块监听：直接 bare-string，不 using 发布者
[EventListener("OnResourcesLoaded")]
public List<object> OnResourcesLoaded(List<object> data) { ... }

// ✅ 跨模块调用：直接 bare-string
EventProcessor.Instance.TriggerEventMethod("GetUIEntity", data);
```

**禁止**调用方仅为读取 `EVT_X` 常量而 `using` 其它业务 Manager 的命名空间——这等同于运行时引用，破坏了 Event 系统的解耦初衷。

**校验**：`agent_lint.ps1 [6]` 扫描所有 `[EventListener("...")]` / `TriggerEventMethod("...")` / `TriggerEvent("...")` / `HasListener("...")` 的 bare-string，cross-ref 全工程已声明的 `EVT_XXX` value 池——未在任何类中作为常量声明的字符串视为打错事件名/无中生有的接口，立即报错。

**例外**：同模块内（同 Manager + 自己的 Service + 自己的 Dao/Editor）调用可继续使用 `EVT_X` 常量——本来就在同一 `using` 范围内，无额外编译耦合代价，重命名安全更划算。

**收益对比**：

| 路径 | 重命名安全 | 跨模块 using | IDE 跳转 |
|---|:---:|:---:|:---:|
| 定义方 const | ✓ | — | ✓ |
| 同模块消费 const | ✓ | 不需要 | ✓ |
| **跨模块消费 bare-string** | ✗（靠 lint 拦） | **0** | 靠根 Agent.md 索引 |

跨模块消费失去 IDE 跳转的代价由两点弥补：(a) lint [6] 必拦事件名拼写错误，(b) 根 `Agent.md` 维护事件速查表作为权威索引。

**已应用范围**：`UIManager` / `ResourceManager` / `AudioManager` / `InventoryManager` / `CharacterManager` / `EntityManager` / `BuildingManager` / `DanmuService` / `Service<T>.EVT_INITIALIZED` / `CharacterService.EVT_FRAME_EVENT`。共 80 个 `EVT_XXX` 常量，全部在根 `Agent.md` 全局索引登记。`MapManager` 当前仅提供纯 C# API。新增模块**必须遵守**此规则；CI 由 `agent_lint.ps1 -Strict` 在 pre-commit 阶段强制执行。

### 5. UI 必须经 UIManager（**强制规则**）

任何**运行时构造的 UI**——Canvas、Panel、Button、Text、布局等——**必须**通过 `UIManager` 提供的 DAO 树 + Event 注册流程，**禁止**业务模块自己 `new GameObject + AddComponent<Canvas/Image/Button/Text/...>`。

**唯一允许的入口**：
- 构建 `UIPanelComponent` / `UIButtonComponent` / `UITextComponent` 树（链式 `.SetPosition` / `.SetSize` / `.SetText` 等）
- `EventProcessor.Instance.TriggerEventMethod("RegisterUIEntity", new List<object>{ rootId, rootDao })` （消费方走 bare-string，参 §4.1）
- 销毁/隐藏：`"UnregisterUIEntity"` 真正销毁；改 `dao.Visible` 仅显隐保留缓存
- 按钮交互：`btnDao.OnClick += handler;`（而不是 `btn.onClick.AddListener`）

**禁止的反模式**：
- `gameObject.AddComponent<Canvas>()` / `AddComponent<CanvasScaler>()` / 自建 EventSystem
- `gameObject.AddComponent<VerticalLayoutGroup>()` / `HorizontalLayoutGroup`（UIManager 暂未支持，请用绝对坐标，未来扩展由 UIManager 统一加）
- `gameObject.AddComponent<Text>()` 等 UI 组件直创 —— UI 文本统一由 UIManager 内部（`UIEntityFactory` / `UIButtonEntity` / `UITextEntity`）创建 uGUI `Text`，字体从 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` 取
- 直接 `using UnityEngine.UI;` 在业务 Manager / Game 层（除非你在 `UIManager/Entity/` 下扩展新 UIType）

**文本清晰度（超采样方案）**：
- 框架仍用 uGUI `Text`（LegacyRuntime.ttf），**未引入 TextMeshPro**
- 需要"高 DPI 清晰文字"时，在业务层用**超采样**技巧：`dao.FontSize ×= 2`（例如 20→40）+ `dao.Size ×= 2`（保持视觉一致）+ `dao.SetScale(0.5f, 0.5f)`。等于字体以 2× 分辨率栅格化再缩小渲染，显著降低模糊感
- 倍率建议用整数倍（2×/3×），避免非整数缩放与 Canvas pixelPerfect 冲突
- 若将来需要真正矢量文字（任意缩放都清晰），可切换到 TextMeshPro，但这是架构级改动，需全面评估

**例外**：纯非交互的 SpriteRenderer 渲染（如 Character 自身贴图）不属于 UI，正常用 `SpriteRenderer`。规则只覆盖 `Canvas` / uGUI 范畴。

**参考实现**：`Scripts/EssSystem/Core/EssManagers/Application/InventoryManager/UI/InventoryUIBuilder.cs` —— 整个背包 UI 全部走 UIManager DAO，可直接照搬模式。

### 6. Service 持久化

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
| 业务模块目录归位 | `InventoryManager` / `MapManager` / `EntityManager` 全部迁到 `Core/Foundation·Presentation·Application/` 三层（与 `Base/` 同级平铺）；`Manager/` 仅留可选第三方模块（如 `DanmuManager`） |
| 跨模块解耦完成 | `EntityManager` ↔ `CharacterManager`、`InventoryManager` / `CharacterPreviewPanel` / `GameManager` ↔ `UIManager` 全部走 EventProcessor + Unity 中立类型；UIComponent (DAO) 通过 `EVT_DAO_PROPERTY_CHANGED` 广播取代直接 `using UIManager.Entity` |
| `AudioManager` 重构 | 从 `Manager/`（基类文件夹）迁到 `Presentation/AudioManager/`；namespace `EssSystem.Core.Presentation.AudioManager`；优先级 `[Manager(3)]`；全部 12 个事件改 `[Event(EVT_XXX)]`（去掉手动 `AddListener`）；音频加载走 `ResourceManager`（bare-string `"GetAudioClip"`）；调用方（EntityService / TribePlayerCombat / TribePlayerInteraction）改 bare-string §4.1 |
| `UIEntity` 命名空间 | 从 `EssSystem.Core.UI.Entity.*` 收回到 `EssSystem.Core.Presentation.UIManager.Entity.*`（属于 UIManager 的私有实现） |
| `EventProcessor.TriggerEventMethod` | catch 块解 `TargetInvocationException` → 暴露真实 `InnerException`，不再吞 listener 抛出的业务异常 |

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
| 修改持久化 | `EssManagers/Foundation/DataManager/Agent.md` |

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
| `InventoryManager.EVT_HOTBAR_USE` | `InventoryHotbarUse` | InventoryManager | 玩家按 1~9 使用快捷栏槽位**广播**（args: `[invId, slotIndex, item]`） |
| `InventoryManager.EVT_REGISTER_ITEM` | `InventoryRegisterItem` | InventoryManager | 注册物品模板 |
| `InventoryManager.EVT_REGISTER_PICKABLE_ITEM` | `InventoryRegisterPickableItem` | InventoryManager | 注册可拾取物定义 |
| `InventoryManager.EVT_SPAWN_PICKABLE_ITEM` | `InventorySpawnPickableItem` | InventoryManager | 在场景中生成可拾取物 |
| `InventoryService.EVT_CREATE` | `InventoryCreate` | InventoryManager | 创建容器 |
| `InventoryService.EVT_DELETE` | `InventoryDelete` | InventoryManager | 删除容器 |
| `InventoryService.EVT_ADD` | `InventoryAdd` | InventoryManager | 添加物品 |
| `InventoryService.EVT_REMOVE` | `InventoryRemove` | InventoryManager | 移除物品 |
| `InventoryService.EVT_MOVE` | `InventoryMove` | InventoryManager | 槽位移动 |
| `InventoryService.EVT_QUERY` | `InventoryQuery` | InventoryManager | 查询容器 |
| `InventoryService.EVT_CHANGED` | `InventoryChanged` | InventoryManager | 背包变化**广播**（用 `[EventListener]` 订阅） |
| `ResourceManager.EVT_GET_PREFAB` | `GetPrefab` | Core/ResourceManager | 同步取 Prefab（仅缓存） |
| `ResourceManager.EVT_GET_SPRITE` | `GetSprite` | Core/ResourceManager | 同步取 Sprite |
| `ResourceManager.EVT_GET_AUDIO_CLIP` | `GetAudioClip` | Core/ResourceManager | 同步取 AudioClip |
| `ResourceManager.EVT_GET_TEXTURE` | `GetTexture` | Core/ResourceManager | 同步取 Texture |
| `ResourceManager.EVT_GET_MATERIAL` | `GetMaterial` | Core/ResourceManager | 同步取 Material（LightManager 用于加载天空盒） |
| `ResourceManager.EVT_GET_RULE_TILE` | `GetRuleTile` | Core/ResourceManager | 同步取 RuleTile |
| `ResourceManager.EVT_GET_EXTERNAL_SPRITE` | `GetExternalSprite` | Core/ResourceManager | 同步取外部图片缓存 |
| `ResourceManager.EVT_LOAD_PREFAB_ASYNC` | `LoadPrefabAsync` | Core/ResourceManager | 异步加载 Prefab |
| `ResourceManager.EVT_LOAD_SPRITE_ASYNC` | `LoadSpriteAsync` | Core/ResourceManager | 异步加载 Sprite |
| `ResourceManager.EVT_LOAD_RULE_TILE_ASYNC` | `LoadRuleTileAsync` | Core/ResourceManager | 异步加载 RuleTile |
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
| `ResourceManager.EVT_GET_ANIMATION_CLIP` | `GetAnimationClip` | Core/ResourceManager | 同步取 AnimationClip（按 clip 名，含 FBX 子资产） |
| `ResourceManager.EVT_GET_MODEL_CLIPS` | `GetModelClips` | Core/ResourceManager | 取 FBX/Model 内全部 AnimationClip（别名 → `ResourceService.EVT_GET_MODEL_CLIPS`） |
| `ResourceService.EVT_GET_ALL_MODEL_PATHS` | `GetAllModelPaths` | Core/ResourceManager | 枚举已索引的所有 FBX/Model 路径 |
| `ResourceService.EVT_RESOURCES_LOADED` | `OnResourcesLoaded` | Core/ResourceManager | 资源全部预加载/索引完成后**广播** |
| `CharacterService.EVT_FRAME_EVENT` | `OnCharacterFrameEvent` | Core/CharacterManager | 角色动画某帧触发的**广播**，参数 `[GameObject owner, string eventName, string actionName, int frameIndex]`；详见 `CharacterManager/Agent.md` |
| `CharacterManager.EVT_CREATE_CHARACTER` | `CreateCharacter` | Core/CharacterManager | 创建 Character；data: `[configId, instanceId, parent?(Transform), worldPosition?(Vector3)]` → `Ok(Transform root)` |
| `CharacterManager.EVT_DESTROY_CHARACTER` | `DestroyCharacter` | Core/CharacterManager | 销毁 Character；data: `[instanceId]` |
| `CharacterManager.EVT_PLAY_ACTION` | `PlayCharacterAction` | Core/CharacterManager | 播放动作；data: `[instanceId, actionName, partId?]` |
| `CharacterManager.EVT_STOP_ACTION` | `StopCharacterAction` | Core/CharacterManager | 停止动作；data: `[instanceId, partId?]` |
| `CharacterManager.EVT_SET_CHARACTER_SCALE` | `SetCharacterScale` | Core/CharacterManager | 设置根节点 localScale；data: `[instanceId, Vector3]` |
| `CharacterManager.EVT_SET_CHARACTER_POSITION` | `SetCharacterPosition` | Core/CharacterManager | 设置世界坐标；data: `[instanceId, Vector3]` |
| `CharacterManager.EVT_MOVE_CHARACTER` | `MoveCharacter` | Core/CharacterManager | 在当前位置上平移；data: `[instanceId, Vector3 delta]` |
| `CharacterManager.EVT_PLAY_LOCOMOTION` | `PlayCharacterLocomotion` | Core/CharacterManager | 分发运动状态（idle/walk/airborne）；data: `[instanceId, moving(bool), grounded(bool, 可选)]` |
| `CharacterManager.EVT_TRIGGER_ATTACK` | `TriggerCharacterAttack` | Core/CharacterManager | 触发攻击锁定动画；data: `[instanceId, duration(float)]` |
| `CharacterManager.EVT_SET_FACING` | `SetCharacterFacing` | Core/CharacterManager | 设置面朝方向（翻转 localScale.x）；data: `[instanceId, facingRight(bool)]` |
| `EntityManager.EVT_CREATE_ENTITY` | `CreateEntity` | Core/EntityManager | 创建 Entity；data: `[configId, instanceId, parent?, worldPosition?]` → `Ok(Transform CharacterRoot)`（E2 后协议解耦不返 Entity） |
| `EntityManager.EVT_DESTROY_ENTITY` | `DestroyEntity` | Core/EntityManager | 销毁 Entity；data: `[instanceId]` |
| `EntityManager.EVT_REGISTER_SCENE_ENTITY` | `RegisterSceneEntity` | Core/EntityManager | 注册已有场景 GameObject 为 Entity；data: `[instanceId, GameObject host, EntityRuntimeDefinition definition]` |
| `EntityManager.EVT_DAMAGE_ENTITY` | `DamageEntity` | Core/EntityManager | 对运行时 Entity 造成伤害；data: `[instanceId, damage, damageType?]` |
| `EntityManager.EVT_REGISTER_ENTITY_CONFIG` | `RegisterEntityConfig` | Core/EntityManager | 注册 Entity 配置（模板）；data: `[EntityConfig]` → `Ok(configId)` |
| `EntityManager.EVT_GET_ENTITY` | `GetEntity` | Core/EntityManager | 查询 Entity 实例；data: `[instanceId]` → `Ok(Entity)` |
| `EntityManager.EVT_APPLY_COLLIDER` | `ApplyCollider` | Core/EntityManager | 应用 Collider 到 GameObject；data: `[GameObject, EntityColliderConfig]` |
| `EntityManager.EVT_ATTACH_ENTITY_HANDLE` | `AttachEntityHandle` | Core/EntityManager | 挂载 EntityHandle 桥接；data: `[GameObject, Entity]` |
| `SkillManager.EVT_REGISTER_SKILL` | `RegisterSkill` | Core/SkillManager | 注册技能定义；data: `[SkillDefinition]` |
| `SkillManager.EVT_LEARN_SKILL` | `LearnSkill` | Core/SkillManager | 实体学习技能；data: `[entityId, skillId]` |
| `SkillManager.EVT_CAST_SKILL` | `CastSkill` | Core/SkillManager | 释放技能；data: `[Entity caster, skillId, target?, Vector3 dir?, Vector3 pos?]` → `Ok`/`Fail` |
| `BuildingManager.EVT_REGISTER_BUILDING_CONFIG` | `RegisterBuildingConfig` | Core/BuildingManager | 注册建筑模板（命令），参数 `[BuildingConfig]` → `Ok(configId)` |
| `BuildingManager.EVT_PLACE_BUILDING` | `PlaceBuilding` | Core/BuildingManager | 放置建筑（命令），参数 `[configId, instanceId, Vector3 position, bool startCompleted?]` → `Ok(Transform)` |
| `BuildingManager.EVT_SUPPLY_BUILDING` | `SupplyBuilding` | Core/BuildingManager | 送材料（命令），参数 `[instanceId, itemId, int amount]` → `Ok(int remaining)` |
| `BuildingManager.EVT_DESTROY_BUILDING` | `DestroyBuilding` | Core/BuildingManager | 销毁建筑（命令），参数 `[instanceId]` → `Ok(instanceId)` |
| `BuildingService.EVT_COMPLETED` | `OnBuildingCompleted` | Core/BuildingManager | 建造完成**广播**，参数 `[instanceId, configId]` |
| `BuildingService.EVT_DESTROYED` | `OnBuildingDestroyed` | Core/BuildingManager | 建筑销毁**广播**，参数 `[instanceId]` |
| `BuildingService.EVT_SUPPLY_PROGRESS` | `OnBuildingSupplyProgress` | Core/BuildingManager | 补给进度**广播**，参数 `[instanceId, itemId, remaining]` |
| `UIManager.EVT_GET_CANVAS_TRANSFORM` | `GetUICanvasTransform` | Core/UIManager | 获取 Canvas 根 Transform（避免 `using UIManager`） |
| `UIManager.EVT_GET_UI_GAMEOBJECT` | `GetUIGameObject` | Core/UIManager | 按 daoId 查 UI GameObject（不暴露 UIEntity 类型） |
| `UIManager.EVT_DAO_PROPERTY_CHANGED` | `UIDaoPropertyChanged` | Core/UIManager | UIComponent 属性变更广播（`[daoId, propName, value]`，UIService 内转发给 UIEntity） |
| `DanmuService.EVT_CONNECTED` | `OnDanmuConnected` | DanmuManager | B 站长连接握手成功**广播**，参数 `[long roomId]` |
| `DanmuService.EVT_DISCONNECTED` | `OnDanmuDisconnected` | DanmuManager | B 站长连接断开**广播**，参数 `[Exception errorOrNull]` |
| `DanmuService.EVT_DANMAKU` | `OnDanmuComment` | DanmuManager | 普通弹幕评论**广播**，参数 `[string userName, string commentText, long userId]` |
| `DanmuService.EVT_GIFT` | `OnDanmuGift` | DanmuManager | 礼物**广播**，参数 `[string userName, string giftName, int giftCount, long userId]` |
| `DanmuService.EVT_RAW` | `OnDanmuRaw` | DanmuManager | 全类型原始 `DanmakuModel`**广播**（含 SuperChat / 上船 / 进场等高级类型） |
| `AudioManager.EVT_PLAY_BGM` | `PlayBGM` | Core/AudioManager | 播放背景音乐（命令），参数 `[string path, bool fade?]` |
| `AudioManager.EVT_STOP_BGM` | `StopBGM` | Core/AudioManager | 停止背景音乐（命令），参数 `[bool fade?]` |
| `AudioManager.EVT_PAUSE_BGM` | `PauseBGM` | Core/AudioManager | 暂停背景音乐（命令） |
| `AudioManager.EVT_RESUME_BGM` | `ResumeBGM` | Core/AudioManager | 继续背景音乐（命令） |
| `AudioManager.EVT_PLAY_SFX` | `PlaySFX` | Core/AudioManager | 播放自定义音效（命令），参数 `[string path, float volumeScale?]` |
| `AudioManager.EVT_SET_MASTER_VOLUME` | `SetMasterVolume` | Core/AudioManager | 设置主音量（命令），参数 `[float volume]` |
| `AudioManager.EVT_SET_BGM_VOLUME` | `SetBGMVolume` | Core/AudioManager | 设置 BGM 音量（命令），参数 `[float volume]` |
| `AudioManager.EVT_SET_SFX_VOLUME` | `SetSFXVolume` | Core/AudioManager | 设置 SFX 音量（命令），参数 `[float volume]` |
| `AudioManager.EVT_PLAY_DAMAGE_SFX` | `PlayDamageSFX` | Core/AudioManager | 播放受伤音效（便捷命令） |
| `AudioManager.EVT_PLAY_ATTACK_SFX` | `PlayAttackSFX` | Core/AudioManager | 播放攻击音效（便捷命令） |
| `AudioManager.EVT_PLAY_UI_SFX` | `PlayUISFX` | Core/AudioManager | 播放 UI 操作音效（便捷命令） |
| `AudioManager.EVT_PLAY_ITEM_USE_SFX` | `PlayItemUseSFX` | Core/AudioManager | 播放物品使用音效（便捷命令） |
| `CameraManager.EVT_GET_MAIN_CAMERA` | `GetMainCamera` | Core/CameraManager | 取主相机引用（查询），返回 `Ok(Camera)` |
| `CameraManager.EVT_FOLLOW_TARGET` | `FollowCameraTarget` | Core/CameraManager | 设置跟随目标（命令），参数 `[Transform target, Vector3 offset?]` |
| `CameraManager.EVT_STOP_FOLLOW` | `StopCameraFollow` | Core/CameraManager | 停止跟随（命令） |
| `CameraManager.EVT_SHAKE` | `ShakeCamera` | Core/CameraManager | 触发震屏（命令），参数 `[float amplitude, float duration, int frequency?]` |
| `CameraManager.EVT_SET_ZOOM` | `SetCameraZoom` | Core/CameraManager | 设置缩放（命令），参数 `[float value, float duration?]` |
| `CameraManager.EVT_WORLD_TO_SCREEN` | `WorldToScreenPoint` | Core/CameraManager | 世界→屏幕坐标（查询），参数 `[Vector3]`，返回 `Ok(Vector2)` |
| `CameraManager.EVT_SCREEN_TO_WORLD` | `ScreenToWorldPoint` | Core/CameraManager | 屏幕→世界坐标（查询），参数 `[Vector2 screenPos, float zDistance?]`，返回 `Ok(Vector3)` |
| `CameraManager.EVT_SET_POSITION` | `SetCameraPosition` | Core/CameraManager | 瞬间设置相机位置（命令），参数 `[Vector3 worldPos]` |
| `CameraManager.EVT_LOOK_AT` | `LookCameraAt` | Core/CameraManager | 瞬间相机朝向某点（命令），参数 `[Vector3 worldPoint]` |
| `InputManager.EVT_BIND_ACTION` | `BindInputAction` | Core/InputManager | 覆盖 Action 绑定（命令），参数 `[string actionName, params KeyCode[] keys]` |
| `InputManager.EVT_UNBIND_ACTION` | `UnbindInputAction` | Core/InputManager | 解绑 Action（命令），参数 `[string actionName]` |
| `InputManager.EVT_IS_PRESSED` | `IsInputPressed` | Core/InputManager | Action 是否按住（查询），参数 `[string actionName]`，返回 `Ok(bool)` |
| `InputManager.EVT_IS_DOWN` | `IsInputDown` | Core/InputManager | Action 本帧是否按下（查询），参数 `[string actionName]`，返回 `Ok(bool)` |
| `InputManager.EVT_IS_UP` | `IsInputUp` | Core/InputManager | Action 本帧是否抬起（查询），参数 `[string actionName]`，返回 `Ok(bool)` |
| `InputManager.EVT_GET_AXIS` | `GetInputAxis` | Core/InputManager | 取轴向值（查询），参数 `[axisName]` 或 `[negativeAction, positiveAction]`，返回 `Ok(float)` |
| `InputManager.EVT_GET_MOVE_AXIS` | `GetInputMoveAxis` | Core/InputManager | 取 2D 移动向量（查询），返回 `Ok(Vector2)` |
| `InputManager.EVT_GET_MOUSE_POS` | `GetMouseScreenPosition` | Core/InputManager | 鼠标屏幕坐标（查询），返回 `Ok(Vector2)` |
| `InputManager.EVT_GET_MOUSE_SCROLL` | `GetMouseScroll` | Core/InputManager | 鼠标滚轮 delta（查询），返回 `Ok(float)` |
| `InputManager.EVT_INPUT_DOWN` | `OnInputDown` | Core/InputManager | Action 本帧按下**广播**，参数 `[string actionName]` |
| `InputManager.EVT_INPUT_UP` | `OnInputUp` | Core/InputManager | Action 本帧抬起**广播**，参数 `[string actionName]` |
| `EffectsManager.EVT_REGISTER_VFX` | `RegisterVFX` | Core/EffectsManager | 注册 VFX 资源映射（命令），参数 `[string vfxId, string prefabPath]` |
| `EffectsManager.EVT_UNREGISTER_VFX` | `UnregisterVFX` | Core/EffectsManager | 移除 VFX 注册（命令），参数 `[string vfxId]` |
| `EffectsManager.EVT_PLAY_VFX` | `PlayVFX` | Core/EffectsManager | 播放 VFX（命令），参数 `[string vfxId, Vector3 worldPos, Quaternion? rot, float? autoDestroy]`，返回 `Ok(string instanceId)` |
| `EffectsManager.EVT_STOP_VFX` | `StopVFX` | Core/EffectsManager | 停止单个 VFX 实例（命令），参数 `[string instanceId]` |
| `EffectsManager.EVT_STOP_ALL_VFX` | `StopAllVFX` | Core/EffectsManager | 停止所有 VFX（命令） |
| `EffectsManager.EVT_SCREEN_FLASH` | `PlayScreenFlash` | Core/EffectsManager | 屏幕闪光（命令），参数 `[Color color, float duration?]` |
| `LightManager.EVT_SET_SUN_COLOR` | `SetSunColor` | Core/LightManager | 设置主光颜色（命令），参数 `[Color]` |
| `LightManager.EVT_SET_SUN_INTENSITY` | `SetSunIntensity` | Core/LightManager | 设置主光强度（命令），参数 `[float]` |
| `LightManager.EVT_SET_SUN_ROTATION` | `SetSunRotation` | Core/LightManager | 设置主光朝向（命令），参数 `[Vector3 euler]` |
| `LightManager.EVT_SET_AMBIENT` | `SetAmbientLight` | Core/LightManager | 设置环境光（命令），参数 `[Color color, float intensity?]` |
| `LightManager.EVT_SET_FOG` | `SetFog` | Core/LightManager | 设置雾（命令），参数 `[bool enable, Color? color, float? density]` |
| `LightManager.EVT_SET_SKYBOX` | `SetSkybox` | Core/LightManager | 切换天空盒（命令），参数 `[string resourcesPath]`（走 ResourceManager `GetMaterial`） |
| `LightManager.EVT_SET_BLOOM` | `SetBloom` | Core/LightManager | 设置 URP Bloom（命令），参数 `[float intensity, float? threshold]` |
| `LightManager.EVT_SET_VIGNETTE` | `SetVignette` | Core/LightManager | 设置 URP Vignette（命令），参数 `[float intensity, Color? color]` |
| `LightManager.EVT_SET_CHROMATIC_ABERRATION` | `SetChromaticAberration` | Core/LightManager | 设置 URP 色差（命令），参数 `[float strength]` |
| `LightManager.EVT_SET_COLOR_ADJUSTMENTS` | `SetColorAdjustments` | Core/LightManager | 设置 URP 调色（命令），参数 `[float postExposure, float? saturation, float? contrast]` |
| `LightManager.EVT_REGISTER_PRESET` | `RegisterLightPreset` | Core/LightManager | 注册灯光预设（命令），参数 `[LightPreset]` |
| `LightManager.EVT_APPLY_PRESET` | `ApplyLightPreset` | Core/LightManager | 应用预设（命令），参数 `[string presetName, float duration?]`，支持插值过渡 |
| `LightManager.EVT_REGISTER_LIGHT` | `RegisterLight` | Core/LightManager | 注册 3D 动态光（命令），参数 `[string lightId, Light]` |
| `LightManager.EVT_SET_LIGHT_INTENSITY` | `SetLightIntensity` | Core/LightManager | 设置 3D 光强度（命令），参数 `[string lightId, float intensity, float? duration]` |
| `LightManager.EVT_REGISTER_LIGHT_2D` | `RegisterLight2D` | Core/LightManager | 注册 URP 2D Light2D（命令），参数 `[string lightId, Light2D]` |
| `LightManager.EVT_SET_LIGHT_2D_INTENSITY` | `SetLight2DIntensity` | Core/LightManager | 设置 2D 光强度（命令），参数 `[string lightId, float intensity, float? duration]` |
| `LightManager.EVT_SET_LIGHT_2D_COLOR` | `SetLight2DColor` | Core/LightManager | 设置 2D 光颜色（命令），参数 `[string lightId, Color]` |
| `DayNightGameManager.EVT_PHASE_CHANGED` | `DayNightPhaseChanged` | Demo/DayNight | 昼夜阶段切换**广播**，参数 `[bool isNight, int round, bool isBossNight]` |
| `WaveSpawnService.EVT_WAVE_STARTED` | `OnWaveStarted` | Demo/DayNight | 波次开始**广播**，参数 `[int round, int waveIndex, int totalEnemies]` |
| `WaveSpawnService.EVT_WAVE_CLEARED` | `OnWaveCleared` | Demo/DayNight | 波次清完**广播**，参数 `[int round, int waveIndex]` |
| `BaseDefenseManager.EVT_DAMAGE_BASE` | `DamageBase` | Demo/DayNight | 对据点造成伤害（命令），参数 `[int amount]` |
| `BaseDefenseManager.EVT_RESET_BASE` | `ResetBase` | Demo/DayNight | 重置据点 HP（命令） |
| `BaseDefenseService.EVT_HP_CHANGED` | `OnBaseHpChanged` | Demo/DayNight | 据点 HP 变更**广播**，参数 `[int currentHp, int maxHp, int delta]` |
| `BaseDefenseService.EVT_DESTROYED` | `OnBaseDestroyed` | Demo/DayNight | 据点击毁**广播**，无参 |
| `ConstructionManager.EVT_PLACE` | `PlaceConstruction` | Demo/DayNight | 放置工事（命令），参数 `[string typeId, Vector3 position, float rotation?]` → `Ok(string instanceId)` |
| `ConstructionManager.EVT_REMOVE` | `RemoveConstruction` | Demo/DayNight | 移除工事（命令），参数 `[string instanceId]` |
| `ConstructionService.EVT_PLACED` | `OnConstructionPlaced` | Demo/DayNight | 工事已放置**广播**，参数 `[string instanceId, string typeId, Vector3 position]` |
| `ConstructionService.EVT_REMOVED` | `OnConstructionRemoved` | Demo/DayNight | 工事已移除**广播**，参数 `[string instanceId]` |
| `DialogueManager.EVT_OPEN_UI` | `OpenDialogueUI` | Core/DialogueManager | 打开对话 UI 并启动会话（命令），参数 `[string dialogueId, string configId?]` |
| `DialogueManager.EVT_CLOSE_UI` | `CloseDialogueUI` | Core/DialogueManager | 结束对话并隐藏 UI（命令），无参 |
| `DialogueService.EVT_REGISTER_DIALOGUE` | `RegisterDialogue` | Core/DialogueManager | 注册 `Dialogue`（命令），参数 `[Dialogue]` |
| `DialogueService.EVT_REGISTER_CONFIG` | `RegisterDialogueConfig` | Core/DialogueManager | 注册 `DialogueConfig`（命令），参数 `[DialogueConfig]` |
| `DialogueService.EVT_ADVANCE` | `AdvanceDialogue` | Core/DialogueManager | 推进到下一行（命令），无参 |
| `DialogueService.EVT_SELECT_OPTION` | `SelectDialogueOption` | Core/DialogueManager | 选择当前行第 N 个选项（命令），参数 `[int index]` |
| `DialogueService.EVT_END` | `EndDialogue` | Core/DialogueManager | 强制结束当前会话（命令），无参 |
| `DialogueService.EVT_QUERY_CURRENT` | `QueryDialogueCurrent` | Core/DialogueManager | 查询当前会话（查询），返回 `[OK, dialogueId, lineId, configId]` |
| `DialogueService.EVT_STARTED` | `OnDialogueStarted` | Core/DialogueManager | 对话启动**广播**，参数 `[string dialogueId, string configId]` |
| `DialogueService.EVT_LINE_CHANGED` | `OnDialogueLineChanged` | Core/DialogueManager | 当前行切换**广播**，参数 `[string dialogueId, string lineId]` |
| `DialogueService.EVT_ENDED` | `OnDialogueEnded` | Core/DialogueManager | 对话结束**广播**，参数 `[string dialogueId]` |

> ℹ️ **几乎无 Event 的模块**：`MapManager` 当前以纯 C# API 为主（`MapService.Instance.XXX`），目前不暴露 `EVT_*`。`CharacterManager` 已有完整 Event API（10 个 `EVT_*` 常量），跨模块调用统一经 `CharacterViewBridge` 收口。若将来新增跨模块 Event，必须同步更新本表并运行 `agent_lint.ps1 -Strict`。

> ⚠️ **façade vs Service 同名**：`ResourceManager.EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` 与 `ResourceService.EVT_UNLOAD_RESOURCE` / `EVT_UNLOAD_ALL_RESOURCES` **字符串相同**，仅后者实际生效（字典覆盖）。调用方只需用 façade 常量。

## 项目特定规则

> 此章节记录项目实战中踩出来的硬性约定，后续新模块务必遵守。

### 1. `RuntimeInitializeOnLoadMethod` 阶段选择

**注册到进程级静态字典/Registry**（如 `MapTemplateRegistry`）的代码 **绝对不能** 用 `RuntimeInitializeLoadType.SubsystemRegistration`。

- **为什么**：`@d:\Desktop\WulaFrameGameWork\WulaGameFramework\Assets\Scripts\EssSystem\Core\Singleton\PlayModeResetGuard.cs:26` 也跑在该阶段，会清空已知静态注册表（`MapTemplateRegistry._templates` 等）；同阶段内多个 `RuntimeInitializeOnLoadMethod` 之间执行顺序 **未定义**，一旦清理后跑就抹掉你的注册。
- **正确做法**：用 `RuntimeInitializeLoadType.BeforeSceneLoad`。它严格晚于 `SubsystemRegistration`（清完之后），又赶在所有 `Awake()` 之前。

```csharp
// ✓ 正确
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void AutoRegister() {
    if (!XxxRegistry.Contains(Id))
        XxxRegistry.Register(new MyTemplate());
}

// ✗ 错误：会被 PlayModeResetGuard 抹掉
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
```

新增一类静态注册表时，同步在 `PlayModeResetGuard.ResetStaticRegistries()` 里加入清理逻辑，并在本规则里登记。

### 2. 运行时通过代码挂 Manager / Service 的时序

业务 `AbstractGameManager` 子类（默认 order=0）若要在 **更高优先级 Manager 的 Awake 之前**改其 Inspector 字段（例：`MapManager.SetTemplateId`），必须遵守：

```csharp
// ✓ 正确：先 Inactive，AddComponent 不触发 Awake；改完字段再激活
var holder = new GameObject(nameof(XxxManager));
holder.SetActive(false);
holder.transform.SetParent(transform);
var mgr = holder.AddComponent<XxxManager>();
mgr.SetSomeField(...);
holder.SetActive(true);   // 此刻才同步触发 Awake/Initialize

// ✗ 错误：AddComponent 立即同步触发 Awake，SetSomeField 来不及
var mgr = new GameObject().AddComponent<XxxManager>();
mgr.SetSomeField(...);
```

参考 `@d:\Desktop\WulaFrameGameWork\WulaGameFramework\Assets\Scripts\Demo\DayNight\DayNightGameManager.cs:113-134` 的实现。

### 3. `[EventListener]` / `[Event]` 找不到 Target 时的行为

`[EventListener]` 是**类型级别**注册（程序集扫描即注册），与运行时是否真的有该 MonoBehaviour 实例 **无关**。当广播 / 命令触发时若 `Target == null`：

- 监听器路径：静默返回空列表（缺组件是合法状态）
- 命令路径：`LogWarning` 提示命令被丢弃（命令通常预期有响应）

参考 `@d:\Desktop\WulaFrameGameWork\WulaGameFramework\Assets\Scripts\EssSystem\Core\Event\EventProcessor.cs:417-468`。

业务代码不需要 try/catch；遇到此警告说明业务侧有 Manager / Service 没在场景中挂载，按需 AddComponent 即可。

### 4. 占位（后续追加）

- 命名规范（变量前缀、私有字段下划线等）
- 团队约定（PR 流程、commit message 风格）
- 性能基线（启动时间、扫描耗时）
- 集成第三方库的注意事项
