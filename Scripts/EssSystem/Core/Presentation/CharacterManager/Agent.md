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
│   │   ├── CharacterConfigFactory2D.cs    工厂（partial，含 MakeSimpleMonster / MakeLayered / MakeSheetCreature 等）
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
`CharacterView.Build` 按 `config.RenderMode` 派遣：
| RenderMode | 实例化的 PartView | 来自子目录 |
|---|---|---|
| `Sprite2D`（默认） | `CharacterPartView2D` | `Sprite2D/Runtime/` |
| `Sprite2DAnimator` | `CharacterPartView2DAnimator` | `Sprite2D/Runtime/` |
| `Prefab3D` | `CharacterPartView3D` | `Prefab3D/Runtime/` |
| `Prefab3DClips` | `CharacterPartView3DClips` | `Prefab3D/Runtime/` |
> **无 RenderMode 分支** 的代码（`CharacterPartView` 基类、`CharacterView` 派遣、所有 DAO Config）都在 `Common/`。
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **CharacterManager Event**.

- `CharacterManager.EVT_CREATE_CHARACTER`
- `CharacterManager.EVT_DESTROY_CHARACTER`
- `CharacterManager.EVT_GET_PART_SPRITE_ID`
- `CharacterManager.EVT_MOVE_CHARACTER`
- `CharacterManager.EVT_PLAY_ACTION`
- `CharacterManager.EVT_PLAY_LOCOMOTION`
- `CharacterManager.EVT_SET_CHARACTER_POSITION`
- `CharacterManager.EVT_SET_CHARACTER_SCALE`
- `CharacterManager.EVT_SET_DIRECTION`
- `CharacterManager.EVT_SET_FACING`
- `CharacterManager.EVT_STOP_ACTION`
- `CharacterManager.EVT_TRIGGER_ATTACK`
- `CharacterService.EVT_FRAME_EVENT`
- `DialogueManager.EVT_SET_PORTRAIT_SPRITE`

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
## Sprite 预加载指南
### 职责分离
- **CharacterManager**：提供 `PreloadCharacterSprites(basePath)` 公开方法
- **业务方（如 DobeCat）**：负责调用预加载，传入正确的资源路径
### 预加载流程
1. **业务方扫描配置**：遍历已注册的 CharacterConfig，收集需要的 Sprite Sheet 路径
2. **业务方加载 Sheet**：`Resources.LoadAll<Sprite>(sheetPath)` 加载整个 Sprite Sheet
3. **业务方注册子图**：逐个调用 `SpriteService.EVT_REGISTER_SPRITE_TO_CACHE` 将子图注册到缓存
4. **运行时查询**：CharacterPartView 调用 `SpriteService.EVT_GET_SPRITE_ASYNC` 直接从缓存命中
### 使用示例
### 路径规范
- **Sprite ID 格式**：`{category}_{variant}_{action}_{frameIndex}`
  - 例：`Skin_warrior_1_Idle_0` → 类别 `Skin`，变体 `warrior_1`，动作 `Idle`，帧 `0`
- **Sprite Sheet 路径**：`{basePath}/{category}/{variant}`
  - 例：`Characters/PixArt/Skin/warrior_1` → 对应 `Resources/Characters/PixArt/Skin/warrior_1.png`
## 注意事项
- **跨模块只走 bare-string**（§4.1）；返 `Transform` / `GameObject` 等 Unity 中立类型，不返 `Character` / `CharacterView`（§A7）
- **`Character` 不可序列化**：持有 Unity Object 引用；`IsTransientCategory(CAT_INSTANCES) = true` 防止误写盘（Anti-Patterns §A3）
- **2D / 3D 互斥**：一个 `Character` 实例只走一种 RenderMode；Mix 同 Character 内不同部件不同 mode 的需求当前不支持
- **FBX 工作流**：
  - Editor 期 `RegisterAllFBXInResources` 自动调 `EnsureControllerForFBX` 生成同目录 `.controller`
  - Build 期依赖 `Resources/CharacterFBXManifest.json`（`FBXManifestBuilder` 生成）
- **`CharacterAnimatorBinder` 是 3D 专属**（在 `Prefab3D/Runtime/`）—— 通过事件创建 Character 并锁外层位姿，2D 项目不需要
- **预览面板** `CharacterPreviewPanel` 在 `Common/Runtime/Preview/`，2D/3D 都能预览
- **Sprite 预加载不硬编码**：不应在 CharacterManager 中硬编码资源路径，而应由业务方根据自己的资源结构调用 `PreloadCharacterSprites(basePath)` 传入正确的路径
