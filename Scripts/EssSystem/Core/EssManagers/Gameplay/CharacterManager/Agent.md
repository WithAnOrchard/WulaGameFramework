# CharacterManager 指南

## 概述

`CharacterManager`（`[Manager(11)]`，薄门面）+ `CharacterService`（业务核心 + 配置持久化）提供通用的角色系统：

- 一个 Character 由多个**部件**组成（如 Body / Head / Weapon）
- 渲染模式由 `CharacterConfig.RenderMode` 决定（**整 Character 一致**，不混用）：
  - **`CharacterRenderMode.Sprite2D`**（默认）—— `CharacterPartView2D` + `SpriteRenderer` + 帧序列（手动 Update 切帧）
  - **`CharacterRenderMode.Sprite2DAnimator`** —— `CharacterPartView2DAnimator` + `SpriteRenderer` + Unity `Animator` + `AnimatorOverrideController`（运行时 `new AnimationClip` 覆盖 placeholder） + `Update` 读 normalizedTime 手动 swap sprite。**前提**：先跑一次 `Tools/Character/Build Sprite Animator Base Controller` 生成 `Resources/Generated/CharacterAnimBase.controller`（只生成一次）
  - **`CharacterRenderMode.Prefab3D`** —— `CharacterPartView3D` + 实例化 Prefab + `Animator` + **AnimatorController 状态切换**（要求 .controller 资产）
  - **`CharacterRenderMode.Prefab3DClips`** —— `CharacterPartView3DClips` + 实例化 FBX + **Playables 直接播 AnimationClip**（**零配置**，无需 AnimatorController）
- 2D 部件按 PartType 区分 Static/Dynamic；3D 部件天然有 Animator，统一按 Dynamic 处理
- 配置持久化（JSON），实例运行时仅在内存
- Sprite / Prefab 加载都走 `ResourceManager` 的 `GetResource` Event，**完全解耦**

## 文件结构

```
CharacterManager/
├── CharacterManager.cs              薄门面：默认注册（无 Event 暴露）
├── CharacterService.cs              业务核心 + 持久化（纯 C# API，无 [Event] 包装）
├── Agent.md                         本文档
├── Runtime/
│   ├── CharacterView.cs             角色根 MonoBehaviour（持有所有部件 View + OnActionComplete / PlayThenReturn）
│   ├── CharacterPartView.cs         单部件 View 抽象基类（Setup/Play/Stop API + 帧事件广播工具）
│   ├── CharacterPartView2D.cs       2D 部件实现（SpriteRenderer + 帧动画 + 2D FrameEvents）
│   ├── CharacterPartView3D.cs       3D 部件实现（实例化 Prefab + AnimatorController state + NormalizedTimeEvents）
│   ├── CharacterPartView3DClips.cs  3D 部件实现（实例化 FBX + Playables AnimationClip + Mixer CrossFade）
│   └── Preview/
│       └── CharacterPreviewPanel.cs 运行时预览/调试 uGUI 面板（仅 2D Warrior/Mage）
├── Editor/
│   ├── CharacterSpriteSheetSlicer.cs       切片工具：Tools/Character/Slice Selected Sprite Sheets (8x6)
│   ├── FBXAnimatorControllerBuilder.cs     一键工具：Tools/Character/Build AnimatorController From Selected FBX
│   └── FBXManifestBuilder.cs               Build 预处理 + 菜单：Tools/Character/Rebuild FBX Manifest（生成 Resources/CharacterFBXManifest.json）
└── Dao/
    ├── Character.cs                 运行时实例（非持久化）
    ├── DefaultCharacterConfigs.cs   内置默认配置（Warrior / Mage）—— Dynamic 部件 + 8 动作
    ├── CharacterConfigFactory.cs    快速生成 Config（2D：MakeSimpleMonster/MakeLayered；3D：MakeSimplePrefab3D）
    ├── CharacterVariantPools.cs     给预览面板用：枚举每个部件可选 sheet 前缀
    └── Config/
        ├── CharacterConfig.cs           配置：ConfigId + DisplayName + RenderMode + Parts[]
        ├── CharacterPartConfig.cs       单部件：PartId + Static/Dynamic + 变换 + PrefabPath(3D)
        ├── CharacterPartType.cs         enum：Static / Dynamic（仅 2D 区分；3D 统一 Dynamic）
        ├── CharacterRenderMode.cs       enum：Sprite2D / Prefab3D
        └── CharacterActionConfig.cs     单动作：ActionName + Sprite序列(2D) + AnimatorState(3D) + Loop + FrameEvents
```

## 内置默认配置

启动时 `CharacterManager`（`_registerDebugTemplates=true` 时）会**强制覆盖注册**两个示例 Model（以代码为准，避免旧版本持久化数据遗留，例如早期 Static 部件）：

| ConfigId | 说明 |
|---|---|
| `"Warrior"` | 战士：Skin + Cloth + Eyes + Hair + Head(Helmet) + Weapon(Sword) + Shield |
| `"Mage"` | 法师：Skin + Cloth + Eyes + Hair + Head(WitchHat) + Weapon(Rod) |

两个 Model 的**每个部件均为 `Dynamic`**，共 8 个动作：

| 动作 | 帧数 | FPS | Loop |
|---|---|---|---|
| Walk | 6 | 12 | ✓ |
| Idle | 4 | 8 | ✓ |
| Jump | 3 | 10 | ✓ |
| Attack | 4 | 14 | ✓ |
| Defend | 4 | 10 | ✓ |
| Damage | 3 | 12 | ✓ |
| Death | 5 | 8 | ✓ |
| Special | 6 | 12 | ✓ |

**使用前必须跑切片工具**：在 Project 视图中选中 `Resources/Sprites/Characters/PixArt/` 下的 PNG 或子文件夹，菜单 `Tools/Character/Slice Selected Sprite Sheets (8x6)` 会把选中的 PNG（文件夹则递归）按 8×6 网格切成 35 个子 Sprite，命名 `{sheetPrefix}_{Action}_{frameIndex}`（前缀由路径推导，如 `Headgear_Helmet_Close_1`）。未选中时会报警告并跳过（不回退全量处理以防误伤）。

业务侧若要保留自己的持久化配置：**关闭** `CharacterManager._registerDebugTemplates` 即可（否则每次启动都会被覆盖）；或在 Manager 初始化**之后**再调 `RegisterConfig` 以同 ConfigId 再次覆盖。

## 运行时预览面板

`CharacterPreviewPanel` 是一个调试/预览 uGUI 面板：

- **使用**：在场景里新建空 GameObject，挂上 `CharacterPreviewPanel`，或按 `GameManager` 的 K 键触发
- **顶部两行**：
  - `Model: ◀ [Warrior] ▶` —— 切换整个 Model
  - `Action: ◀ [Idle] ▶` —— 切换当前播放动作（Walk/Idle/Jump/Attack/Defend/Damage/Death/Special）
- **左侧**：每个 Dynamic Part 一行，`◀ [SheetPrefix] ▶` 切变体；变体切换时用 `DefaultCharacterConfigs.MakeAnimatedPart` 重建该部件 8 个动作并播放当前动作
- **中间**：真实的 `CharacterView` GameObject（世界坐标 `_previewPosition`，需要相机对准）
- **变体来源**：`CharacterVariantPools.GetVariants(modelId, partId)` 返回 sheet 前缀列表（扫 `Resources.LoadAll<Texture2D>` 后用 `DerivePrefix` 推导）
- **依赖**：场景里需要一个渲染 SpriteRenderer 的 Camera；UI 全部走 `UIManager` 的 Canvas（自动创建 + EventSystem）
- **UI 实现**：完全使用 `UIPanelComponent` / `UIButtonComponent` / `UITextComponent` DAO 树，通过 `UIManager.EVT_REGISTER_ENTITY` 注册（遵循 `Assets/Agent.md` 第 5 条铁律：UI 必须经 UIManager）。Model 切换时 `EVT_UNREGISTER_ENTITY` + 重建；Action/Scale/变体切换走 in-place `dao.Text = ...`

## 数据分类

| 常量 | 用途 | 持久化 |
|---|---|---|
| `CharacterService.CAT_CONFIGS` = `"Configs"` | `CharacterConfig` | ✅ |
| `CharacterService.CAT_INSTANCES` = `"Characters"` | 运行时 `Character` | ❌（仅内存） |

## C# API（业务层主入口）

业务层直接调用 `CharacterService.Instance` 即可，大部分功能无需经 `EventProcessor`：

- 创建 / 销毁：`CharacterService.Instance.CreateCharacter(...)` / `DestroyCharacter(...)`
- 配置：`RegisterConfig(...)` / `GetConfig(...)` / `GetAllConfigs()`
- 动作控制：`PlayAction(...)` / `StopAction(...)`、或直接 `character.View.Play(...)` / `Stop(...)` / `PlayThenReturn(...)`

## Event API

`CharacterManager` 对外暴露下列 `[Event]` 入口（供其他模块通过 `EventProcessor.TriggerEventMethod` 调度，避免直接 `using CharacterManager`）：

| 常量 | 值 | data 入参 | 返回 |
|---|---|---|---|
| `EVT_CREATE_CHARACTER` | `"CreateCharacter"` | `[configId, instanceId, parent?(Transform), worldPosition?(Vector3)]` | `Ok(Transform root)` / `Fail` |
| `EVT_DESTROY_CHARACTER` | `"DestroyCharacter"` | `[instanceId]` | `Ok(instanceId)` / `Fail` |
| `EVT_PLAY_ACTION` | `"PlayCharacterAction"` | `[instanceId, actionName, partId?(string)]` | `Ok` / `Fail` |
| `EVT_STOP_ACTION` | `"StopCharacterAction"` | `[instanceId, partId?(string)]` | `Ok` / `Fail` |
| `EVT_SET_CHARACTER_SCALE` | `"SetCharacterScale"` | `[instanceId, Vector3 scale]` | `Ok` / `Fail` |
| `EVT_SET_CHARACTER_POSITION` | `"SetCharacterPosition"` | `[instanceId, Vector3 worldPosition]` | `Ok` / `Fail` |
| `EVT_MOVE_CHARACTER` | `"MoveCharacter"` | `[instanceId, Vector3 delta]` | `Ok` / `Fail` |

### `EVT_FRAME_EVENT` — 角色动画帧事件（广播）
- **常量**：`CharacterService.EVT_FRAME_EVENT` = `"OnCharacterFrameEvent"`
- **触发源**：`CharacterPartView` 播到 `CharacterActionConfig.FrameEvents` 登记的帧时自动发出（先 `HasListener` 判空避免无用广播）。
- **参数**：`[GameObject owner, string eventName, string actionName, int frameIndex]`
- **订阅**：`[EventListener(CharacterService.EVT_FRAME_EVENT)]`
- **示例**：见下文"动画事件 / 回弹"。

## 用法示例

### 注册配置（C# 链式 API）
```csharp
var cfg = new CharacterConfig("Hero", "勇者")
    .WithPart(new CharacterPartConfig("Body", CharacterPartType.Static)
        .WithStatic("Hero_Body")
        .WithSortingOrder(0))
    .WithPart(new CharacterPartConfig("Head", CharacterPartType.Dynamic)
        .WithDynamic("Idle",
            new CharacterActionConfig("Idle")
                .WithSprites("Hero_Head_0", "Hero_Head_1", "Hero_Head_2")
                .WithFrameRate(8f).WithLoop(true),
            new CharacterActionConfig("Attack")
                .WithSprites("Hero_Head_Atk_0", "Hero_Head_Atk_1")
                .WithFrameRate(12f).WithLoop(false))
        .WithSortingOrder(1));

CharacterService.Instance.RegisterConfig(cfg);
```

### 实例化 + 播放动作
```csharp
var hero = CharacterService.Instance.CreateCharacter("Hero", "p1", parent: null, worldPosition: Vector3.zero);
hero.View.Play("Attack", partId: "Head");
// 或播完自动回 Idle
hero.View.PlayThenReturn("Attack", "Idle");
```

## 快速实例化（给 EntityManager 用）

`CharacterConfigFactory` 提供"一行注册"：

```csharp
// 1) 启动时注册（通常放进各业务 Manager 的 Initialize 里）
CharacterConfigFactory.RegisterSimpleMonster("slime_green", "slime_green");      // 单部件
CharacterConfigFactory.RegisterLayered("goblin_warrior",                          // 多部件
    ("Body",   "goblin_body",   0),
    ("Armor",  "goblin_armor",  1),
    ("Weapon", "goblin_sword",  2));

// 2) 业务层生成实体（EntityManager 的活）
var monster = CharacterService.Instance.CreateCharacter("slime_green", "m_001", parent, pos);
monster.View.Play("Walk");
```

全部复用 `DefaultCharacterConfigs.Actions` 里的 8 个标准动作（Walk / Idle / Jump / Attack / Defend / Damage / Death / Special），命名规则 `{sheetPrefix}_{ActionName}_{frameIndex}`，Sprite 切片流水线跟 Warrior / Mage 一致。

## 动画事件 / 回弹

### OnActionComplete + PlayThenReturn（非循环动作播完自动回基础动作）

```csharp
// 典型：受伤动作播完回到 Idle
character.View.PlayThenReturn("Damage", "Idle");

// 或直接监听完成事件
character.View.OnActionComplete += action => {
    if (action == "Death") MarkDead();
};
```

- 要求 `DamageAction.Loop = false`（默认 Death 是 false，其它在 `DefaultCharacterConfigs` 里都是 true，需要按需调整）
- `OnActionComplete` 由**首个 Dynamic 部件**（pivot part）的完成代表整组完成 —— 所有部件同步播放，这是安全的简化

### 帧事件（某一帧触发业务自定义逻辑）

在 `CharacterActionConfig.FrameEvents` 里按 `frameIndex → eventName` 登记，到帧时由 `CharacterPartView` 通过 `EventProcessor` 广播：

```csharp
var attack = new CharacterActionConfig("Attack")
    .WithSprites(...)
    .WithFrameRate(10f)
    .WithLoop(false)
    .WithFrameEvent(2, "HitCheck")   // 第 2 帧触发 "HitCheck"
    .WithFrameEvent(3, "HitSound");  // 第 3 帧触发 "HitSound"
```

事件名：`CharacterService.EVT_FRAME_EVENT`（字符串值 `"OnCharacterFrameEvent"`），参数 `[GameObject owner, string eventName, string actionName, int frameIndex]`。业务层按需监听：

```csharp
[EventListener(CharacterService.EVT_FRAME_EVENT)]
public List<object> OnCharacterFrameEvent(List<object> data) {
    var owner      = data[0] as GameObject;
    var evtName    = data[1] as string;
    var actionName = data[2] as string;
    var frameIndex = (int)data[3];
    if (evtName == "HitCheck") DoHitCheck(owner);
    return ResultCode.Ok();
}
```

为避免没人监听时浪费，`CharacterPartView` 先 `EventProcessor.HasListener(CharacterService.EVT_FRAME_EVENT)` 再广播。

## 3D 模式（Prefab + Animator）

### 工作原理

| 阶段 | 行为 |
|---|---|
| **Build** | `CharacterView` 看到 `config.RenderMode == Prefab3D`，每个 Part 挂 `CharacterPartView3D` |
| **Setup** | `CharacterPartView3D` 通过 `EVT_GET_RESOURCE`（type = `"Prefab"`）加载 `PartConfig.PrefabPath`，`Instantiate` 为子节点，缓存其上的 `Animator` |
| **Play** | 解析 `ActionConfig.ResolveAnimatorState()`（默认取 `ActionName`），用 `Animator.HasState` 校验，命中后 `CrossFadeInFixedTime` 或 `Play` |
| **完成检测** | `Loop=false` 的动作，`Update` 内观察 `StateInfo.normalizedTime ≥ 1 && !IsInTransition`，只触发一次 `OnActionComplete` |
| **帧事件** | `NormalizedTimeEvents`（dict<float, string>）在归一化时间跨越阈值时通过 `EVT_FRAME_EVENT` 广播（frameIndex = -1） |

### 资源约定

- Prefab 路径走 `ResourceManager`，请项目方按 `ResourceManager/Agent.md` 注册资源（`Resources/...` 自动 Fallback）。
- Prefab 必须挂 `Animator` + `Animator Controller`；状态机里要有 `ActionConfig.AnimatorStateName`（或 `ActionName`）对应的 State。
- Prefab 自身的 transform 在实例化后被重置为单位（位置 / 旋转 / 缩放都归零）；外部偏移用 `PartConfig.LocalPosition` / `LocalScale` / `LocalEulerAngles` 控制。
- 整个 Character 的 Render 模式不混用：一个 `CharacterConfig` 要么全 2D 要么全 3D。

### 用法示例

#### 单部件 3D 角色（一行注册）

```csharp
// 假设项目已有 Prefab："Resources/Models/Characters3D/zombie.prefab"
// 且 Animator Controller 含 Idle / Walk / Attack / Death 等 state
CharacterConfigFactory.RegisterSimplePrefab3D(
    configId:        "zombie",
    prefabPath:      "Models/Characters3D/zombie",
    defaultAction:   "Idle",
    actionStateNames: new[] { "Idle", "Walk", "Attack", "Death" });

var z = CharacterService.Instance.CreateCharacter("zombie", "z_001", parent: null, worldPosition: Vector3.zero);
z.View.Play("Walk");

// Death 一次性 + 自动回 Idle —— 注意 Death state 必须是非循环（Loop = false）
z.View.PlayThenReturn("Death", "Idle");
```

#### 手写 3D 多部件 Config（链式 API）

```csharp
var hero3D = new CharacterConfig("Hero3D", "勇者(3D)")
    .WithRenderMode(CharacterRenderMode.Prefab3D)
    .WithPart(new CharacterPartConfig("Body", CharacterPartType.Dynamic)
        .WithPrefab("Models/Characters3D/hero_body")          // 含 Animator
        .WithDynamic("Idle",
            new CharacterActionConfig("Idle"),                // ActionName 即 state 名
            new CharacterActionConfig("Walk"),
            new CharacterActionConfig("Attack")
                .WithLoop(false)
                .WithAnimatorState("Attack01", layer: 0, crossFadeDuration: 0.15f)
                .WithNormalizedTimeEvent(0.45f, "HitCheck"))) // 在 45% 时刻广播帧事件
    .WithPart(new CharacterPartConfig("Weapon", CharacterPartType.Dynamic)
        .WithPrefab("Models/Characters3D/sword")
        .WithLocalPosition(new Vector3(0.2f, 1.0f, 0))
        .WithLocalRotation(new Vector3(0, 90, 0))
        .WithDynamic(string.Empty));                          // 武器 Prefab 没有 Animator 也 OK

CharacterService.Instance.RegisterConfig(hero3D);
```

### 局限 / 注意事项

- **Animator 同步**：多部件时所有部件被 `CharacterView.Play(action)` 同时切到同名 state；如果武器 Prefab 上无 Animator，`Play` 静默失败但其它部件仍正常播放（不阻塞整组）。
- **OnActionComplete pivot**：3D 部件 `CanPivotComplete = true`，所以第一个被 Build 的 3D 部件就是 pivot；其余部件的完成时间被忽略。
- **不预置 3D 默认模板**：`CharacterManager._registerDebugTemplates` 只注册 2D 的 Warrior/Mage/Tree，3D 角色全部由项目方按需 `Register*Prefab3D` 注册。
- **预览面板暂仅支持 2D**：`CharacterPreviewPanel` 当前只能切换 Warrior/Mage 的 Sprite 变体；3D 预览待补。

## 3D Playables 模式（FBX + AnimationClip 直接播放，零配置）

适用于"导入大量 FBX、不想为每个 FBX 建 AnimatorController"的场景。

### 工作原理

| 阶段 | 行为 |
|---|---|
| **Build** | `CharacterView` 看到 `RenderMode == Prefab3DClips`，每个 Part 挂 `CharacterPartView3DClips` |
| **Setup** | `EVT_GET_RESOURCE`（type=`"Prefab"`）加载 FBX，`Instantiate` 后 `GetComponentInChildren<Animator>`（无则自动 `AddComponent`），并把 `runtimeAnimatorController` 置 null —— Playables 模式不需要 |
| **Graph** | 每个部件构建一个 `PlayableGraph` + 2 输入的 `AnimationMixerPlayable`，输出连到 `AnimationPlayableOutput` 驱动 Animator |
| **Play** | 解析 `ActionConfig.ResolveAnimatorState()` 当作 clip 名 → `EVT_GET_RESOURCE`（type=`"AnimationClip"`）取出 → 创建 `AnimationClipPlayable` 推到 mixer input 1，旧的转到 input 0 → CrossFade 由 Update 内 weight 渐变实现 |
| **完成检测** | `Loop=false` 的动作，Update 内 `time >= clip.length` 一次性触发 `OnActionComplete` |
| **帧事件** | 复用 `NormalizedTimeEvents`（dict<float,string>）；按当前 active clip 的 `time / clip.length` 跨越阈值时通过 `EVT_FRAME_EVENT` 广播 |

### 资源约定

- **FBX 必须放在 `Resources/`**（推荐 `Resources/Models/Characters3D/`），启动时 `ResourceService.AutoLoadAllResources` 会把 FBX 根（GameObject）和内部 AnimationClip 子资产（按 clip name）双索引到缓存。
- **clip 名全局唯一**：因为按 `clip.name` 入 cache，所以不同 FBX 内的 clip 名重复会以最后一个为准。冲突时把 FBX 内的 take 重命名为 `{model}_{action}` 避免碰撞。
- **不混用渲染模式**：一个 `CharacterConfig` 要么全 `Prefab3DClips`，要么全 `Prefab3D`，要么全 `Sprite2D`。

### 用法示例

#### 一行注册（Editor 自动扫描 FBX 内 clip 名）

```csharp
// Editor 下：自动扫描 zombie.fbx 内所有 clip，全部当作 loop 动作
CharacterConfigFactory.RegisterFBXModelAuto("zombie", "Models/Characters3D/zombie", defaultAction: "Idle");
```

#### 启动时批量注册 Resources/ 下所有 FBX（推荐）

`CharacterManager` Inspector：
- `_autoRegisterAllFBX = true`（默认）
- `_autoRegisterFBXSubFolder = "Models"`（仅扫该子目录；为空则扫整个 `Resources/`）

实际触发：CharacterManager 监听 `ResourceManager.EVT_RESOURCES_LOADED`，等 `ResourceService` 完成所有索引后再调 `RegisterAllFBXInResources` —— **复用其 `_modelClipNames` 缓存（O(1) 查表）**，不重复跑 AssetDatabase。

工作流：
- 用 `EVT_GET_ALL_MODEL_PATHS` 拿到所有 FBX 路径列表
- 对每个 FBX：用 `EVT_GET_MODEL_CLIPS` 取 clip 名 → 选 defaultAction（优先级 `Idle` → `Idle_01` → `idle` → clip[0]）
- **无 clip 的 FBX 自动跳过**（静态道具）
- `configId = FBX 文件名`，`fbxPath = Resources 相对路径`，`RenderMode = Prefab3DClips`

```csharp
// 把 FBX 丢进 Assets/Resources/Models/ 任意层级，启动后：
var z = CharacterService.Instance.CreateCharacter("zombie", "z_001");
z.View.Play("Walk");
```

##### 跨 FBX 同名 clip 不再碰撞

`CharacterPartView3DClips` 在 `OnSetup` 时通过 `EVT_GET_MODEL_CLIPS` 把**本 FBX 的 clip 列表缓存到本地字典**，`Play` 优先在本地查 —— 即使 `zombie.fbx` 和 `goblin.fbx` 都有 `Idle` clip，各自实例都会拿到正确的那个，不会互相覆盖。仅当本地未命中才回退到全局缓存。

##### Build 兼容（manifest 流程）

`AssetDatabase` 在 Build 不可用 → 通过预生成的 manifest 提供数据：

1. **Editor**：菜单 `Tools/Character/Rebuild FBX Manifest`，或 Build 时自动触发（`FBXManifestBuilder` 实现 `IPreprocessBuildWithReport`）
2. 生成 `Assets/Resources/CharacterFBXManifest.json`（每个 FBX 的 path + clip 名列表）
3. Build 启动时 `ResourceService.LoadFBXManifestIfPresent` 读取 manifest 填充 `_modelClipNames`
4. `RegisterAllFBXInResources` 走相同逻辑，Build 也能跑

> 直接修改 / 新增 FBX 后**不需要**手动重建 manifest（Editor 启动时 AssetDatabase 索引会动态更新）；只需在打 Build 前确保 manifest 是最新的（Build 预处理会自动重建）。

#### 显式声明动作（推荐，Build 兼容）

```csharp
CharacterConfigFactory.RegisterFBXModel(
    configId:       "zombie",
    fbxPath:        "Models/Characters3D/zombie",
    defaultAction:  "Idle",
    loopActions:    new[] { "Idle", "Walk", "Run" },
    oneShotActions: new[] { "Attack01", "Death" });

var z = CharacterService.Instance.CreateCharacter("zombie", "z_001", parent: null, worldPosition: Vector3.zero);
z.View.Play("Walk");
z.View.PlayThenReturn("Death", "Idle");   // Death loop=false → 自动回 Idle
```

#### 手写 Config（多部件 + CrossFade + 帧事件）

```csharp
var hero = new CharacterConfig("Hero3D", "勇者(FBX)")
    .WithRenderMode(CharacterRenderMode.Prefab3DClips)
    .WithPart(new CharacterPartConfig("Body", CharacterPartType.Dynamic)
        .WithPrefab("Models/Characters3D/hero")
        .WithDynamic("Idle",
            new CharacterActionConfig("Idle").WithAnimatorState("Idle"),
            new CharacterActionConfig("Run") .WithAnimatorState("Run"),
            new CharacterActionConfig("Atk") .WithAnimatorState("Attack01")
                .WithLoop(false)
                .WithAnimatorState("Attack01", layer: 0, crossFadeDuration: 0.15f)
                .WithNormalizedTimeEvent(0.45f, "HitCheck")));
CharacterService.Instance.RegisterConfig(hero);
```

### Editor 工具：FBX → AnimatorController（仅 Prefab3D 模式需要）

如果你坚持用经典 `Prefab3D` 模式（AnimatorController），可用菜单：

> **Tools/Character/Build AnimatorController From Selected FBX**

选中一个或多个 FBX 后点击：在 FBX 同目录生成 `{fbxName}.controller`，把 FBX 内每个 AnimationClip 加为 state（第一个为默认）。**注意**：Unity 不会自动把 .controller 绑到 FBX 实例化出来的 Animator —— 需要：
- 在场景中给 Animator 组件手动指定 Controller，或
- 在 `Prefab3D` 模式下让用户自己保证 Prefab 的 Animator 已绑好 Controller。

> **若不想做这一步**，请用 `Prefab3DClips` 模式，纯 Playables 驱动，**完全无需 .controller**。

### 局限

- **Avatar / Humanoid retargeting**：Playables `AnimationClipPlayable` 默认走 Generic 通道。Humanoid retargeting 需要 Avatar；当前实现没有显式设置 Avatar，FBX 自身的 Avatar 由 Animator 内部使用，常见情况下能工作，但跨 FBX 共享一套人形动画时建议改用 `AnimatorOverrideController`（C 路径，未实现）。
- **CrossFade 模型**：仅 2 输入 mixer，不支持同时混合 ≥3 个 clip。
- **Build 期 `EVT_GET_MODEL_CLIPS` 返回空**：因此 `RegisterFBXModelAuto` 仅 Editor 可用，发布前请改用显式 `RegisterFBXModel`。

## 待补充 / TODO

- [ ] 渲染模式扩展：现支持 SpriteRenderer + Prefab，UI 模式（`Image`）按需添加
- [ ] Sprite 加载：当前每个 2D PartView 内有 1 级缓存，跨实例缓存可放到 `ResourceManager`
- [ ] 2D 动作过渡 / 混合：当前为硬切换；3D 已支持 `CrossFadeDuration`
- [ ] 持久化角色实例（如果需要存档恢复运行时角色，需要为 `Character` 提供反序列化构造）
- [ ] 优先级系统：`PlayWithPriority(action, priority)` 防止 Damage 打断 Death（目前由调用方自行判断）
- [ ] 3D 预览面板：在 `CharacterPreviewPanel` 加 RenderMode 切换，支持枚举注册的 3D Config
- [ ] 3D 部件挂点：当前部件按 PartView GameObject 的 Transform 摆放；如需挂到 Body Prefab 的 bone（如手部 socket），后续可加 `AttachBoneName` 字段
