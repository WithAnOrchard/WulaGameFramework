# WulaGameFramework — 项目级 Agent 指南

> 本文件是整个项目的顶层 Agent 指南，给 AI Agent / 协作开发者用。后续可在此文件追加项目特定约定。

## 项目定位

Unity + C# 轻量级游戏框架，核心思想：**Manager/Service 双层单例 + 统一事件中心 + 零反射持久化**。

代码全部在 `Assets/Scripts/` 下，分为：
- `EssSystem/Core/` — 框架核心 + 内置 Manager（`EssManagers/`：UIManager / ResourceManager / CharacterManager / EntityManager / MapManager / InventoryManager 等）
- `EssSystem/Manager/` — 第三方/外置业务 Manager（目前仅 `DanmuManager`）
- `GameManager.cs` — 项目入口（继承 `AbstractGameManager`）

## 🔴 必读文档（按优先级）

| 文档 | 内容 | 必读 |
|---|---|---|
| **`Managers.md`** | 所有 Manager 的优先级、路径、职责、依赖关系 | ⚠️ **必读** |
| **`Events.md`** | 所有 Event 的完整定义、参数、返回值 | ⚠️ **必读** |
| **`Anti-Patterns.md`** | 反模式黑名单、禁止事项 | ⚠️ **必读** |
| `README.md` | 项目总览 / 快速上手 | 推荐 |

## 开始之前必读

> 路径均以 `Assets/` 为根（即本文件所在目录）。AI Agent 读文件时请拼接完整 `Assets/...` 路径。

| 内容 | 文档（相对 `Assets/`） |
|---|---|
| 项目总览 / 快速上手 | `README.md` |
| **反模式 / 黑名单**（写代码前必看） | `Anti-Patterns.md` |
| Core 架构 + 子模块入口 | `Scripts/EssSystem/Core/Agent.md` |
| **Base 模块总体指南**（单例、Manager/Service、事件、日志等） | `Scripts/EssSystem/Core/Base/Agent.md` |
| **Platform 模块指南**（帧率控制、桌面覆盖层、窗口检测等） | `Scripts/EssSystem/Core/Platform/Agent.md` |
| **Foundation 模块指南**（数据持久化、资源加载、网络通讯） | `Scripts/EssSystem/Core/Foundation/Agent.md` |
| Manager 系统总览 | `Scripts/EssSystem/Core/EssManagers/Agent.md` |
| UI 实体管理 | `Scripts/EssSystem/Core/EssManagers/Presentation/UIManager/Agent.md` |
| 音频系统 | `Scripts/EssSystem/Core/EssManagers/Presentation/AudioManager/Agent.md` |
| 角色系统 | `Scripts/EssSystem/Core/EssManagers/Application/CharacterManager/Agent.md` |
| 实体系统 | `Scripts/EssSystem/Core/EssManagers/Application/EntityManager/Agent.md` |
| 建筑系统 | `Scripts/EssSystem/Core/EssManagers/Application/BuildingManager/Agent.md` |
| 背包系统 | `Scripts/EssSystem/Core/EssManagers/Application/InventoryManager/Agent.md` |
| 2D 地图系统 | `Scripts/EssSystem/Core/EssManagers/Application/MapManager/Agent.md` |
| 对话系统 | `Scripts/EssSystem/Core/EssManagers/Application/DialogueManager/Agent.md` |
| Presentation 层总览 | `Scripts/EssSystem/Core/Presentation/Agent.md` |
| 输入系统 | `Scripts/EssSystem/Core/Presentation/InputManager/Agent.md` |
| 相机系统 | `Scripts/EssSystem/Core/Presentation/CameraManager/Agent.md` |
| 灯光系统 | `Scripts/EssSystem/Core/Presentation/LightManager/Agent.md` |
| 特效系统 | `Scripts/EssSystem/Core/Presentation/EffectsManager/Agent.md` |
| 弹幕系统（可选） | `Scripts/EssSystem/Manager/DanmuManager/Agent.md` |

## 关键约定（核心铁律）

### 1. 优先级表（`[Manager(N)]`）

| Manager | 优先级 | 不可改 |
|---|---:|:---:|
| `EventProcessor` | -30 | ⚠️ 必须最先 |
| `DataManager` | -20 | ⚠️ 监听 Service 初始化事件 |
| `ResourceManager` | 0 | Core/Foundation 下 |
| `AudioManager` | 3 | Core/Presentation 下 |
| `UIManager` | 5 | Core/Presentation 下 |
| `InventoryManager` | 10 | Core/Application 下 |
| `CharacterManager` | 11 | Core/Presentation 下 |
| `MapManager` | 12 | Core/Application 下 |
| `EntityManager` | 13 | Core/Application 下，依赖 CharacterManager + MapManager |
| `BuildingManager` | 14 | Core/Application 下，依赖 EntityManager |
| `DialogueManager` | 15 | Core/Application 下 |
| `SceneInstanceManager` | 16 | 子场景 / 副本管理（骨架，ToDo #3） |
| `NpcManager` | 17 | NPC 配置 / 实例化 / 互动路由（骨架，ToDo #4 前置） |
| `CraftingManager` | 18 | 装备制作 + 蓝图（骨架，ToDo #5） |
| `ShopManager` | 19 | 商店交易（骨架，ToDo #4 后置） |
| 其它业务 Manager | 20+ | 新增默认起步 |
| `DanmuManager` | 50 | 可选第三方模块（B 站弹幕直播集成） |

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

### 7. 引擎组件归属 Manager（**强制规则**）

**Unity 引擎里和"已有 Manager 域"重叠的组件，业务代码一律不得自己 `AddComponent` / 直接构造，而必须走对应 Manager 的事件 API。** 这条等同于 §5（UI 必经 UIManager）的扩展，覆盖音频、角色视觉、动画等更多领域。

**禁区 / 替代路径**

| 禁区 | 反模式 | 必须改走 |
|---|---|---|
| **音频** | `gameObject.AddComponent<AudioSource>()` 自播 / 持有 `AudioClip`、自己 `Update` 改 `volume` | `EVT_PLAY_BGM` / `EVT_PLAY_SFX` / `EVT_PLAY_POSITIONAL_LOOP_SFX` 等 AudioManager Event；位置音用 positional loop API，由 AudioManager 持有 AudioSource 注册表，自动同步 SFXVolume |
| **角色视觉** | `AddComponent<SpriteRenderer>` + 自写 `_frames`/`_currentFrame` 帧动画 / 直接拼贴 sprite sheet | 注册一份 `CharacterConfig`（参 `DefaultCharacterConfigs` / `DefaultTreeCharacterConfigs` / `TribeCampfireCharacterConfig`），通过 `CharacterViewBridge.CreateCharacter` 实例化；动画用 `CharacterActionConfig` + `Sprite2DAnimator` 标准 8 状态机 |
| **uGUI**（参 §5） | `AddComponent<Canvas/Image/Button/Text/...>` | UIManager DAO 树 + `RegisterUIEntity` |
| **物品** | `AddComponent<PickableItem>` 自管掉落 | `EVT_REGISTER_PICKABLE_ITEM` + `EVT_SPAWN_PICKABLE_ITEM` |
| **碰撞 / 实体能力** | 自挂 Collider + 散装 HP/伤害字段 | `EVT_REGISTER_SCENE_ENTITY` + `EntityRuntimeDefinition` 描述能力（`CanMove` / `CanBeAttacked` / `CanAttack` / `EnableKnockbackEffect`），EntityService 装配 `IMovable`/`IDamageable` 等能力 |

**例外**：一次性、不属于任何已有 Manager 域的临时组件可以自挂（如：业务专用的 `MonoBehaviour` 状态控件、特例占位 `SpriteRenderer` 用作 debug 色块、Rigidbody2D 这种纯物理基件）。判定原则：**如果某个域已经有 Manager / Service，就一律走它**。

**为什么**：
- **统一生命周期**：Manager 持有注册表 → 全局批量修改（音量同步、面朝翻转、热重载）；业务自挂会漏掉 Manager 的副作用。
- **跨模块零耦合**：业务方只 bare-string 调 Event 协议（§4.1），不再 `using` 引擎细节命名空间。
- **可替换实现**：今天是 Sprite2D，明天 CharacterManager 切到 Spine / 3D Prefab，业务侧零改动；今天是 manual volume 衰减，明天 AudioManager 接 FMOD，业务侧零改动。
- **消除配置漂移**：见 2026-05-17 营火事故 —— 业务自挂 `AudioSource` + 自写帧动画，导致 SFXVolume 同步失败、热重载失效；迁回 AudioManager / CharacterManager 后两个 bug 一并消失。

**校验**：`agent_lint.ps1` 会扫 `Demo/` 下的 `AddComponent<AudioSource>` / `AddComponent<Canvas>` / 手写 sprite-sheet 帧循环模式，命中后输出 WARN。新增引擎组件要进 Manager 域时，请同步把禁区写入本表。

**参考迁移**：
- 营火：`Demo/Tribe/TribeCampfire.cs`（瘦到只剩音频锚点） + `Demo/Tribe/World/Features/TribeCampfireCharacterConfig.cs`（视觉走 CharacterManager） + `AudioManager.PlayPositionalLoopSFX`（音频走 AudioManager）。
- 树木：`DefaultTreeCharacterConfigs.RegisterAll(Service)` —— 完整的"零自挂"装饰物范式。

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
| `Manager.SyncServiceLoggingSettings()` | **仅在 Awake 时调用一次**，日志打印设置仅在重启后生效（不支持运行时实时同步）。Inspector 中的 `_serviceEnableLogging` 开关修改后需重启应用才能生效 |
| **Presentation 层优化（Phase 2）** | `CharacterManager.PreloadCharacterSprites(basePath)` 改为 public 方法，由业务方负责调用和路径配置；Sprite ID 格式规范为 `{category}_{variant}_{action}_{frameIndex}`；新增 `SpriteService` 专用事件（`EVT_GET_SPRITE_ASYNC` / `EVT_LOAD_SPRITE_ASYNC` / `EVT_REGISTER_SPRITE_TO_CACHE`）；所有 Presentation 模块文档完善 |

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
- **作用**: 注册一个 UI 实体到 UIManager，UIManager 创建 GameObject 并挂到 UI Canvas
- **参数**: `[string daoId, UIComponent component]`
- **返回**: `ResultCode.Ok(UIEntity)` / `ResultCode.Fail(msg)`
- **副作用**: 创建 GameObject 挂到 UI Canvas
```

**字段说明**：
- **常量**：完整引用路径，包含字符串值（让 AI 知道两边等价）
- **作用**：一句话讲清这个 Event 干什么、什么时候用——**schema 必填项**，消费方靠这一行判断要不要调
- **参数**：按顺序列每个位置参数的类型和含义；可选参数标 `(可选)`
- **返回**：成功/失败两种情况都要写
- **副作用**：是否创建 GameObject、修改持久化数据、触发其他事件等

> ⚠️ **禁止示例代码块**。模块 `Agent.md` 不写"如何调用"的代码片段——详细调用代码统一进 `Events.md` 维护（每个 Event 条目配 ≤ 8 行精简示例），避免 Agent.md 重复且膨胀。详见 §项目特定规则 → 规则 5。

### 全局 Event 索引

> ⚠️ **已移至 `Events.md`**。所有 Event 的完整定义、参数、返回值都在那里。本文件不再维护 Event 索引。

**查询 Event 时**：
1. 先查 `Events.md` 的全局索引表
2. 再查对应模块的 `Agent.md` 的 `## Event API` 章节

**新增 Event 时**：
1. 在定义方（Manager / Service）用常量 `[Event(EVT_XXX)]`
2. 在模块 `Agent.md` 的 `## Event API` 章节添加文档
3. 在根目录 `Events.md` 的对应分组添加条目
4. 运行 `agent_lint.ps1 -Strict` 检查一致性

---
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

参考 `@d:\Desktop\WulaFrameGameWork\WulaGameFramework\Assets\Scripts\Demo\DobeCat\DobeCatGameManager.cs` 的实现。

### 3. `[EventListener]` / `[Event]` 找不到 Target 时的行为

`[EventListener]` 是**类型级别**注册（程序集扫描即注册），与运行时是否真的有该 MonoBehaviour 实例 **无关**。当广播 / 命令触发时若 `Target == null`：

- 监听器路径：静默返回空列表（缺组件是合法状态）
- 命令路径：`LogWarning` 提示命令被丢弃（命令通常预期有响应）

参考 `@d:\Desktop\WulaFrameGameWork\WulaGameFramework\Assets\Scripts\EssSystem\Core\Event\EventProcessor.cs:417-468`。

业务代码不需要 try/catch；遇到此警告说明业务侧有 Manager / Service 没在场景中挂载，按需 AddComponent 即可。

### 4. 代码改动后必须跑编译工具并消除所有 Error

**强制规则**：任何代码（`.cs`）改动完成后，**必须**运行 `tools/` 下的编译校验工具确认无 error，**未消除所有 error 之前不算完成**。

```powershell
# Roslyn 命令行编译校验（首选，无需开 Unity）
.\tools\compile_check.ps1

# 或：聚焦检查编译 error
.\tools\check_compile_errors.ps1
```

**要求**：
1. 改动后立刻跑一次（即使只改了一行）。
2. 脚本输出的 **每一条 error** 都必须修掉——不允许"残留 error、稍后再看"。
3. 警告（warning）按 §4.1 等其他既定规则的要求处理（lint 警告不能留、`agent_lint.ps1 -Strict` 必须通过）。
4. 若报错来自未保存的文件、IDE 缓存或脚本本身 bug，需在提交前用 IDE 编译再确认一次。
5. 工具脚本新增 / 重命名时同步更新本节。

**为什么**：
- 业务模块强耦合（见 §2 通信路由），一个未声明的 `EVT_XXX` 或缺 using 会导致下游大面积编译失败，且 Editor 内实时编译有延迟、容易漏。
- `compile_check.ps1` / `check_compile_errors.ps1` 走 Roslyn 离线校验，能在不开 Unity 的情况下秒级发现 error，是 CI / pre-commit 兜底前的最快一道闸。

### 5. 模块 Agent.md 不得详述代码用法，由 Events.md 统一承载

**架构前提**：本框架跨模块调用统一走 Event（§2 通信路由）。消费方拿到的就是"事件名 + 参数 + 作用"，**不需要**知道发布方内部实现细节。

**强制规则**：
1. **模块 `Agent.md` 不写"如何调用"的代码示例**——`## Event API` 章节只保留五项：**常量、作用、参数、返回、副作用**。
2. **"怎么用"统一进 `Events.md`**：每个 Event 在 `Events.md` 的分组条目里给出**精简**调用片段（≤ 8 行）和完整副作用说明；这是跨模块查询的唯一入口。
3. **模块 `Agent.md` 严禁出现**：
   - `EventProcessor.Instance.TriggerEventMethod(...)` 之类调用代码
   - `UIPanelComponent` / `UIButtonComponent` 等 DAO 链式 `.SetPosition` 示例
   - 业务方对接流程图 / 状态机 / 序列图
   - 重复 `Events.md` 已写过的参数表
4. **允许保留**：模块职责一句话、Manager 优先级、依赖关系、与其他模块的协作关系（指向 `Events.md` 的具体事件名即可）、运行时序注意事项。

**为什么**：
- 模块 `Agent.md` 数量多（20+），每个 1KB 示例 × 20 = 20KB 上下文，AI 读完整框架时被无用代码淹没，**严重挤占有效决策空间**。
- 详细代码示例的"高保真"诱惑很强，但调用方式在重命名/重构时极易过时；`Events.md` 单一来源更易维护。
- 消费方只需知道："要干 X → 调 `EVT_XXX` → 传 `[a, b]` → 拿到 `ResultCode`"，多余代码是噪音。

**校验**：`agent_lint.ps1` 新增规则扫描模块 `Agent.md` 的 `## Event API` 章节，若出现 ```csharp ... ``` 代码块 → 报错。`tools/new-module.ps1` 模板同步更新为新 schema，新模块不会忘记。

### 6. 占位（后续追加）

- 命名规范（变量前缀、私有字段下划线等）
- 团队约定（PR 流程、commit message 风格）
- 性能基线（启动时间、扫描耗时）
- 集成第三方库的注意事项
