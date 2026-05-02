# CharacterManager 指南

## 概述

`CharacterManager`（`[Manager(11)]`，薄门面）+ `CharacterService`（业务核心 + 配置持久化）提供通用的角色系统：

- 一个 Character 由多个**部件**组成（如 Body / Head / Weapon）
- 每个部件可独立配置为：
  - **Static** — 固定一张 Sprite，永不切换
  - **Dynamic** — 按帧序列播放动画，可在多个动作间切换（Idle / Walk / Attack ...）
- 配置持久化（JSON），实例运行时仅在内存
- Sprite 加载走 `ResourceManager` 的 `GetResource` Event，**完全解耦**

## 文件结构

```
CharacterManager/
├── CharacterManager.cs              薄门面：默认注册（无 Event 暴露）
├── CharacterService.cs              业务核心 + 持久化（纯 C# API，无 [Event] 包装）
├── Agent.md                         本文档
├── Runtime/
│   ├── CharacterView.cs             角色根 MonoBehaviour（持有所有部件 View + OnActionComplete / PlayThenReturn）
│   ├── CharacterPartView.cs         单个部件 MonoBehaviour（SpriteRenderer + 帧动画 + 帧事件广播）
│   └── Preview/
│       └── CharacterPreviewPanel.cs 运行时预览/调试 uGUI 面板
├── Editor/
│   └── CharacterSpriteSheetSlicer.cs  切片工具：Tools/Character/Slice Selected Sprite Sheets (8x6)
└── Dao/
    ├── Character.cs                 运行时实例（非持久化）
    ├── DefaultCharacterConfigs.cs   内置默认配置（Warrior / Mage）—— Dynamic 部件 + 8 动作
    ├── CharacterConfigFactory.cs    快速生成 Config（MakeSimpleMonster / MakeLayered，给 EntityManager 用）
    ├── CharacterVariantPools.cs     给预览面板用：枚举每个部件可选 sheet 前缀
    └── Config/
        ├── CharacterConfig.cs           配置：ConfigId + DisplayName + Parts[]
        ├── CharacterPartConfig.cs       单部件：PartId + Static/Dynamic + 变换
        ├── CharacterPartType.cs         enum：Static / Dynamic
        └── CharacterActionConfig.cs     单动作：ActionName + Sprite 序列 + FPS + Loop
```

## 内置默认配置

启动时 `CharacterManager` 会自动注册两个示例 Model（若同 ConfigId 不存在）：

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

业务侧可用 **同 ConfigId + `RegisterConfig`** 覆盖默认，或关闭 `CharacterManager._registerDebugTemplates` 自己从零构建。

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

## Event API

**本模块不暴露任何 EVT_ 常量。** 业务层直接调用 `CharacterService.Instance` 的 C# API 即可：

- 创建 / 销毁：`CharacterService.Instance.CreateCharacter(...)` / `DestroyCharacter(...)`
- 配置：`RegisterConfig(...)` / `GetConfig(...)` / `GetAllConfigs()`
- 动作控制：`PlayAction(...)` / `StopAction(...)`、或直接 `character.View.Play(...)` / `Stop(...)` / `PlayThenReturn(...)`

**一个跨模块事件**：`"CharacterFrameEvent"`——动作某帧触发的广播（详见下文“动画事件 / 回弹”章节）。该事件是`EVT_*` 之外唯一的外部接口。

后续若需要跨模块通过 `EventProcessor` 调度，再按需在 Service 上补 `[Event(EVT_XXX)]` 包装即可。

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

事件名：`"CharacterFrameEvent"`，参数 `[GameObject owner, string eventName, string actionName, int frameIndex]`。业务层按需监听：

```csharp
[Event("CharacterFrameEvent")]
public List<object> OnCharacterFrameEvent(List<object> data) {
    var owner      = data[0] as GameObject;
    var evtName    = data[1] as string;
    var actionName = data[2] as string;
    var frameIndex = (int)data[3];
    if (evtName == "HitCheck") DoHitCheck(owner);
    return ResultCode.Ok();
}
```

为避免没人监听时浪费，`CharacterPartView` 先 `EventProcessor.HasListener("CharacterFrameEvent")` 再广播。

## 待补充 / TODO

- [ ] 渲染模式扩展：现仅支持 `SpriteRenderer`，UI 模式（`Image`）按需添加
- [ ] Sprite 加载：当前每个 PartView 内有 1 级缓存，跨实例缓存可放到 `ResourceManager`
- [ ] 动作过渡 / 混合：当前为硬切换，需要的话可加 crossfade 时长
- [ ] 持久化角色实例（如果需要存档恢复运行时角色，需要为 `Character` 提供反序列化构造）
- [ ] 优先级系统：`PlayWithPriority(action, priority)` 防止 Damage 打断 Death（目前由调用方自行判断）
