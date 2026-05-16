# CharacterManager 指南

## 概述

`Presentation/CharacterManager`（`[Manager(11)]`）—— 角色外观资源工厂 + 动作运行时。**统一管理 2D Sprite 与 3D Prefab/FBX 两条渲染路径**，按 `CharacterConfig.RenderMode` 分派。

| | 类 | 角色 |
|---|---|---|
| Manager | `CharacterManager` | MonoBehaviour 单例：默认配置注册、FBX 自动扫描、10 个 [Event] 入口 |
| Service | `CharacterService` | 持久化 `CharacterConfig` + 运行时 `Character` 实例（Transient）+ 业务 API |

跨模块消费典型路径：`EntityManager` 在 `EntityService.OnEntityCreated` 时通过 `CharacterViewBridge` 触发 `EVT_CREATE_CHARACTER`，把 Character 根节点 SetParent 到 Entity GameObject 之下。

## 文件结构

```
Presentation/CharacterManager/
├── CharacterManager.cs        统一管理（不分 2D/3D；门面 + 10 个 [Event]）
├── CharacterService.cs        统一管理（持久化 + 运行时 API；不分 2D/3D）
├── Agent.md                   本文档
│
├── Common/                    2D / 3D 共享合约（DAO + 基类 + 派遣）
│   ├── Dao/
│   │   ├── Character.cs                  运行时 Character 实例（Transient）
│   │   └── Config/
│   │       ├── CharacterConfig.cs        角色配置（含 RenderMode 分派 enum）
│   │       ├── CharacterPartConfig.cs    部件配置
│   │       ├── CharacterPartType.cs      Static/Dynamic
│   │       ├── CharacterActionConfig.cs  动作 + FrameEvents
│   │       ├── CharacterLocomotionRole.cs Walk/Idle/Jump/Attack 分派角色
│   │       └── CharacterRenderMode.cs    Sprite2D / Prefab3D / Sprite2DAnimator / Prefab3DClips
│   └── Runtime/
│       ├── CharacterPartView.cs          抽象基类
│       ├── CharacterView.cs              派遣中心：按 RenderMode 实例化对应 PartView
│       └── Preview/CharacterPreviewPanel.cs  编辑器预览（2D/3D 通用）
│
├── Sprite2D/                  纯 2D Sprite 帧序列实现
│   ├── Dao/
│   │   ├── CharacterConfigFactory2D.cs    工厂（partial，含 MakeSimpleMonster / MakeLayered 等）
│   │   ├── CharacterVariantPools.cs       变体池（颜色/装饰组合）
│   │   ├── DefaultCharacterConfigs.cs     Warrior / Mage 等内置示例
│   │   └── DefaultTreeCharacterConfigs.cs 树系列 8 份示例
│   ├── Runtime/
│   │   ├── CharacterPartView2D.cs         SpriteRenderer + 帧序列（默认 Sprite2D 模式）
│   │   └── CharacterPartView2DAnimator.cs SpriteRenderer + AnimatorOverrideController（Sprite2DAnimator 模式）
│   └── Editor/
│       ├── CharacterAnimatorBaseControllerBuilder.cs  Sprite2DAnimator 的 base controller 生成器（一次性）
│       └── CharacterSpriteSheetSlicer.cs  Sprite sheet 切片菜单
│
└── Prefab3D/                  纯 3D Prefab/FBX + Animator 实现
    ├── Dao/
    │   └── CharacterConfigFactory3D.cs    工厂（partial，含 MakeSimplePrefab3D / MakeFBXModel / RegisterAllFBXInResources / EnsureControllerForFBX）
    ├── Runtime/
    │   ├── CharacterPartView3D.cs         Prefab 实例化 + AnimatorController 状态切换（Prefab3D 模式）
    │   ├── CharacterPartView3DClips.cs    FBX + Playables 直播 AnimationClip（Prefab3DClips 模式）
    │   └── CharacterAnimatorBinder.cs     场景挂载点：自动 Spawn + 锁外层位姿
    └── Editor/
        ├── FBXAnimatorControllerBuilder.cs  FBX 同目录 .controller 生成器
        └── FBXManifestBuilder.cs            Build 期 FBX manifest 生成（菜单 + IPreprocessBuildWithReport）
```

> **目录 ≠ namespace**：物理上拆 2D/3D，但所有文件保留原 namespace（`...CharacterManager.Dao` / `.Runtime` / `.Editor`）。这样调用方零修改 using，Manager / Service 仍然统一管理两套实现。`CharacterConfigFactory` 用 C# `partial class` 横跨 2D / 3D 两文件。

## 渲染模式分派（核心机制）

```csharp
public enum CharacterRenderMode
{
    Sprite2D         = 0,   // 默认 SpriteRenderer + 帧序列（手动 Update 切帧）
    Prefab3D         = 1,   // Prefab/FBX + AnimatorController state 切换
    Prefab3DClips    = 2,   // FBX + Playables 直播 AnimationClip（零配置 controller）
    Sprite2DAnimator = 3,   // SpriteRenderer + AnimatorOverrideController（运行时覆盖）
}
```

`CharacterView.Build` 按 `config.RenderMode` 派遣：

| RenderMode | 实例化的 PartView | 来自子目录 |
|---|---|---|
| `Sprite2D`（默认） | `CharacterPartView2D` | `Sprite2D/Runtime/` |
| `Sprite2DAnimator` | `CharacterPartView2DAnimator` | `Sprite2D/Runtime/` |
| `Prefab3D` | `CharacterPartView3D` | `Prefab3D/Runtime/` |
| `Prefab3DClips` | `CharacterPartView3DClips` | `Prefab3D/Runtime/` |

> **无 RenderMode 分支** 的代码（`CharacterPartView` 基类、`CharacterView` 派遣、所有 DAO Config）都在 `Common/`。

## Event API

> 共 10 个命令 + 1 个广播。所有命令返回 `Ok(instanceId)` 或 `Ok(Transform root)` 表示成功，`Fail(msg)` 表示失败。跨模块调用走 bare-string（§4.1）。

### 命令类（生命周期）

#### `CharacterManager.EVT_CREATE_CHARACTER` — 创建 Character
- **常量**: `CharacterManager.EVT_CREATE_CHARACTER` = `"CreateCharacter"`
- **参数**: `[string configId, string instanceId, Transform parent?, Vector3 worldPosition?]`
- **返回**: `ResultCode.Ok(Transform root)` / `ResultCode.Fail("...")`
- **副作用**: 按 `RenderMode` 派遣实例化 PartView，挂载到 `parent`（若提供）；`Character` 实例存入 `CAT_INSTANCES`（**Transient 不写盘**）
- **示例**:
  ```csharp
  var r = EventProcessor.Instance.TriggerEventMethod(
      "CreateCharacter",
      new List<object> { "Warrior", "player_001", playerTransform, Vector3.zero });
  if (ResultCode.IsOk(r)) { var characterRoot = r[1] as Transform; }
  ```

#### `CharacterManager.EVT_DESTROY_CHARACTER` — 销毁 Character
- **常量**: `CharacterManager.EVT_DESTROY_CHARACTER` = `"DestroyCharacter"`
- **参数**: `[string instanceId]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: `Object.Destroy(View.gameObject)` + 从 `CAT_INSTANCES` 移除

### 命令类（动作 / 运动）

#### `CharacterManager.EVT_PLAY_ACTION` — 播放动作
- **常量**: `CharacterManager.EVT_PLAY_ACTION` = `"PlayCharacterAction"`
- **参数**: `[string instanceId, string actionName, string partId?]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: `partId` 为空时对所有 Dynamic 部件生效；按 `RenderMode` 走对应 PartView 的 Play 实现

#### `CharacterManager.EVT_STOP_ACTION` — 停止动作
- **常量**: `CharacterManager.EVT_STOP_ACTION` = `"StopCharacterAction"`
- **参数**: `[string instanceId, string partId?]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: `partId` 为空则停止所有部件

#### `CharacterManager.EVT_PLAY_LOCOMOTION` — 分发运动状态
- **常量**: `CharacterManager.EVT_PLAY_LOCOMOTION` = `"PlayCharacterLocomotion"`
- **参数**: `[string instanceId, bool moving, bool grounded?=true]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: 按部件 `LocomotionRole` 路由到 Walk/Idle/Jump/Attack
- **示例**: 由 `PickableDropEntity` / 玩家移动逻辑每帧驱动

#### `CharacterManager.EVT_TRIGGER_ATTACK` — 触发攻击锁定
- **常量**: `CharacterManager.EVT_TRIGGER_ATTACK` = `"TriggerCharacterAttack"`
- **参数**: `[string instanceId, float duration?=0.4f]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: 在锁定窗口内 Attack 角色部件播放 `Attack` 动作；窗口结束自动恢复 Locomotion

### 命令类（变换）

#### `CharacterManager.EVT_SET_CHARACTER_SCALE` — 设置根 localScale
- **常量**: `CharacterManager.EVT_SET_CHARACTER_SCALE` = `"SetCharacterScale"`
- **参数**: `[string instanceId, Vector3 scale]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: `View.transform.localScale = scale`（不影响 Entity 碰撞体）

#### `CharacterManager.EVT_SET_CHARACTER_POSITION` — 设置世界坐标
- **常量**: `CharacterManager.EVT_SET_CHARACTER_POSITION` = `"SetCharacterPosition"`
- **参数**: `[string instanceId, Vector3 worldPosition]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: `View.transform.position = worldPosition`。Entity 驱动场景下通常由 `EntityService.Tick` 同步，**手动控制 Character 时使用**

#### `CharacterManager.EVT_MOVE_CHARACTER` — 平移
- **常量**: `CharacterManager.EVT_MOVE_CHARACTER` = `"MoveCharacter"`
- **参数**: `[string instanceId, Vector3 delta]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: `View.transform.position += delta`

#### `CharacterManager.EVT_SET_FACING` — 设置面朝
- **常量**: `CharacterManager.EVT_SET_FACING` = `"SetCharacterFacing"`
- **参数**: `[string instanceId, bool facingRight]`
- **返回**: `ResultCode.Ok(instanceId)` / `ResultCode.Fail`
- **副作用**: 翻转 `View.transform.localScale.x`

### 广播类

#### `CharacterService.EVT_FRAME_EVENT` — 角色动画帧事件
- **常量**: `CharacterService.EVT_FRAME_EVENT` = `"OnCharacterFrameEvent"`
- **触发时机**: `CharacterPartView` 播放到 `CharacterActionConfig.FrameEvents` 登记的帧时发出
- **data**: `[GameObject owner, string eventName, string actionName, int frameIndex]`
- **订阅示例**:
  ```csharp
  [EventListener("OnCharacterFrameEvent")]
  public List<object> OnFrameEvent(List<object> data)
  {
      var owner = data[0] as GameObject;
      var eventName = data[1] as string;
      // ... 例如 "Hit" 帧触发伤害判定
      return ResultCode.Ok();
  }
  ```

### 监听器（CharacterManager 内部）

#### `OnResourcesLoaded` — 等 ResourceManager 资源就绪后批量注册 FBX
- **订阅**: `[EventListener("OnResourcesLoaded")]`（§4.1 跨模块 bare-string）
- **行为**: 启动时若 `_autoRegisterAllFBX = true`，等 ResourceService 完成 FBX 索引后调 `CharacterConfigFactory.RegisterAllFBXInResources(_autoRegisterFBXSubFolder)`
- **目的**: 走 `_modelClipNames` O(1) 查表，避免重复跑 AssetDatabase

## Inspector 字段

| 字段 | 默认 | 说明 |
|---|---|---|
| `_registerDebugTemplates` | `true` | 启动时注册内置示例配置（Warrior / Mage / 8 种树）；同 ConfigId 业务可覆盖 |
| `_autoRegisterAllFBX` | `true` | 启动时扫描 Resources/ 下所有 FBX/Model 自动注册为 Prefab3D Config |
| `_autoRegisterFBXSubFolder` | `""` | 配合上一项：仅扫该子目录（如 `"Models/Characters3D"`）；空则扫全部 |

## Service 持久化

| 分类 | 类型 | 说明 |
|---|---|---|
| `CAT_CONFIGS` = `"Configs"` | `CharacterConfig` | **持久化**到 `CharacterService/Configs.json`；下次启动自动恢复 |
| `CAT_INSTANCES` = `"Characters"` | `Character` | **Transient**（`IsTransientCategory` 返 true，**绝不写盘**）；持有 Unity View 引用，序列化会变僵尸数据 |

## 跨模块工厂调用示例

`CharacterConfigFactory` 是 `partial class`，**不需要区分 2D/3D 文件名**：

```csharp
// 2D Sprite 模式（来自 Sprite2D/Dao/CharacterConfigFactory2D.cs）
CharacterConfigFactory.RegisterSimpleMonster("slime_green", "slime_green");
CharacterConfigFactory.RegisterLayered("goblin",
    ("Body","goblin_body",0), ("Armor","goblin_armor",1));

// 3D FBX 模式（来自 Prefab3D/Dao/CharacterConfigFactory3D.cs）
CharacterConfigFactory.RegisterFBXModel("zombie", "Models/Characters3D/zombie",
    defaultAction: "Idle",
    loopActions: new[] { "Idle", "Walk", "Run" },
    oneShotActions: new[] { "Attack", "Death" });

// 一行扫整个目录（启动时 _autoRegisterAllFBX 走这条）
CharacterConfigFactory.RegisterAllFBXInResources("Models/Characters3D");
```

## 注意事项

- **跨模块只走 bare-string**（§4.1）；返 `Transform` / `GameObject` 等 Unity 中立类型，不返 `Character` / `CharacterView`（§A7）
- **`Character` 不可序列化**：持有 Unity Object 引用；`IsTransientCategory(CAT_INSTANCES) = true` 防止误写盘（Anti-Patterns §A3）
- **2D / 3D 互斥**：一个 `Character` 实例只走一种 RenderMode；Mix 同 Character 内不同部件不同 mode 的需求当前不支持
- **FBX 工作流**：
  - Editor 期 `RegisterAllFBXInResources` 自动调 `EnsureControllerForFBX` 生成同目录 `.controller`
  - Build 期依赖 `Resources/CharacterFBXManifest.json`（`FBXManifestBuilder` 生成）
- **`CharacterAnimatorBinder` 是 3D 专属**（在 `Prefab3D/Runtime/`）—— 通过事件创建 Character 并锁外层位姿，2D 项目不需要
- **预览面板** `CharacterPreviewPanel` 在 `Common/Runtime/Preview/`，2D/3D 都能预览
