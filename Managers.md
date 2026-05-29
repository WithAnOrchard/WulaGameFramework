# Manager 完整清单

> **必读**：所有框架 Manager 的优先级、路径、职责和文档。

---

## Manager 优先级表

| 优先级 | Manager | 分组 | 路径 | 职责 | 文档 |
|---|---:|---|---|---|---|
| **-30** | `EventProcessor` | Core/Event | `Scripts/EssSystem/Core/Event/` | 事件分派中心 | `Scripts/EssSystem/Core/Event/Agent.md` |
| **-20** | `DataManager` | Core/Foundation | `Scripts/EssSystem/Core/Foundation/DataManager/` | 数据持久化 + Service 自动注册 | `Scripts/EssSystem/Core/Foundation/DataManager/Agent.md` |
| **0** | `ResourceManager` | Core/Foundation | `Scripts/EssSystem/Core/Foundation/ResourceManager/` | 资源加载/缓存/预加载 | `Scripts/EssSystem/Core/Foundation/ResourceManager/Agent.md` |
| **2** | `NetworkManager` | Core/Foundation | `Scripts/EssSystem/Core/Foundation/NetworkManager/` | 多人联机网络通讯（Mirror） | `Scripts/EssSystem/Core/Foundation/NetworkManager/Agent.md` |
| **2** | `InputManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/InputManager/` | 输入抽象（Action + Axis + 鼠标） | `Scripts/EssSystem/Core/Presentation/InputManager/Agent.md` |
| **3** | `AudioManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/AudioManager/` | BGM/SFX 音频管理 | `Scripts/EssSystem/Core/Presentation/AudioManager/Agent.md` |
| **4** | `CameraManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/CameraManager/` | 相机控制（跟随/缩放/震动） | `Scripts/EssSystem/Core/Presentation/CameraManager/Agent.md` |
| **5** | `UIManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/UIManager/` | UI 实体管理中心 | `Scripts/EssSystem/Core/Presentation/UIManager/Agent.md` |
| **6** | `EffectsManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/EffectsManager/` | 特效播放 + 屏幕闪光 | `Scripts/EssSystem/Core/Presentation/EffectsManager/Agent.md` |
| **7** | `LightManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/LightManager/` | 灯光管理（URP 专用） | `Scripts/EssSystem/Core/Presentation/LightManager/Agent.md` |
| **10** | `InventoryManager` | Core/Application | `Scripts/EssSystem/Core/Application/SingleManagers/InventoryManager/` | 背包系统 | `Scripts/EssSystem/Core/Application/SingleManagers/InventoryManager/Agent.md` |
| **11** | `CharacterManager` | Core/Presentation | `Scripts/EssSystem/Core/Presentation/CharacterManager/` | 角色外观工厂（2D/3D） | `Scripts/EssSystem/Core/Presentation/CharacterManager/Agent.md` |
| **12** | `MapManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/MapManager/TopDown2D/` | 2D 地图生成/管理 | `Scripts/EssSystem/Core/Application/MultiManagers/MapManager/Agent.md` |
| **13** | `EntityManager` | Core/Application | `Scripts/EssSystem/Core/Application/SingleManagers/EntityManager/` | 实体管理系统 + Utility AI | `Scripts/EssSystem/Core/Application/SingleManagers/EntityManager/Agent.md` |
| **13** | `Voxel3DMapManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/MapManager/Voxel3D/` | 3D 体素地图生成/管理 | `Scripts/EssSystem/Core/Application/MultiManagers/MapManager/Agent.md` |
| **14** | `BuildingManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/BuildingManager/` | 建筑系统 | `Scripts/EssSystem/Core/Application/MultiManagers/BuildingManager/Agent.md` |
| **14** | `VoxelLightingManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/MapManager/Voxel3D/Lighting/` | 体素地图光照传播 | `Scripts/EssSystem/Core/Application/MultiManagers/MapManager/Agent.md` |
| **15** | `DialogueManager` | Core/Application | `Scripts/EssSystem/Core/Application/SingleManagers/DialogueManager/` | 对话系统 | `Scripts/EssSystem/Core/Application/SingleManagers/DialogueManager/Agent.md` |
| **15** | `SkillManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/SkillManager/` | 技能系统 | `Scripts/EssSystem/Core/Application/MultiManagers/SkillManager/Agent.md` |
| **16** | `SceneInstanceManager` | Core/Application | `Scripts/EssSystem/Core/Application/SingleManagers/SceneInstanceManager/` | 子场景/副本管理（骨架） | `Scripts/EssSystem/Core/Application/SingleManagers/SceneInstanceManager/Agent.md` |
| **17** | `NpcManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/NpcManager/` | NPC 配置/互动（骨架） | `Scripts/EssSystem/Core/Application/MultiManagers/NpcManager/Agent.md` |
| **18** | `CraftingManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/CraftingManager/` | 装备制作系统（骨架） | `Scripts/EssSystem/Core/Application/MultiManagers/CraftingManager/Agent.md` |
| **18** | `FarmManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/FarmManager/` | 农场系统 | `Scripts/EssSystem/Core/Application/MultiManagers/FarmManager/Agent.md` |
| **19** | `ShopManager` | Core/Application | `Scripts/EssSystem/Core/Application/MultiManagers/ShopManager/` | 商店交易系统 | `Scripts/EssSystem/Core/Application/MultiManagers/ShopManager/Agent.md` |
| **50** | `BilibiliDanmuManager` | Manager/扩展 | `Scripts/EssSystem/Manager/DanmuManager/` | B 站弹幕直播（可选） | `Scripts/EssSystem/Manager/DanmuManager/Agent.md` |
| **50** | `LiveStatusManager` | Manager/扩展 | `Scripts/EssSystem/Manager/LiveStatusManager/` | B 站直播间开播状态轮询（可选） | `Scripts/EssSystem/Manager/LiveStatusManager/Agent.md` |

---

## Manager 分组说明

### Core/Event — 事件系统
- **EventProcessor** (优先级 -30)：事件分派中心，所有 Manager 的通信枢纽

### Core/Base — 基础设施
- **DataManager** (优先级 -20)：数据持久化，监听 Service 初始化事件

### Core/Foundation — 基础服务
- **ResourceManager** (优先级 0)：资源加载/缓存，所有资源的来源

### Core/Presentation — 表现层
| Manager | 优先级 | 职责 |
|---|---|---|
| InputManager | 2 | 输入抽象（Action + Axis + 鼠标） |
| AudioManager | 3 | 音频管理（BGM / SFX） |
| UIManager | 5 | UI 实体管理中心 |
| CameraManager | 6 | 相机控制（跟随/缩放/震动） |
| LightManager | 7 | 灯光管理（天空盒/动态光） |
| EffectsManager | 8 | 特效播放（粒子系统） |
| CharacterManager | 11 | 角色外观工厂（2D Sprite / 3D Prefab） |

### Core/Application — 业务逻辑
| Manager | 优先级 | 职责 | 依赖 |
|---|---|---|---|
| InventoryManager | 10 | 背包系统 | - |
| MapManager | 12 | 地图生成/管理 | - |
| EntityManager | 13 | 实体管理系统 | CharacterManager + MapManager |
| BuildingManager | 14 | 建筑系统 | EntityManager |
| DialogueManager | 15 | 对话系统 | - |
| SkillManager | 15 | 技能系统 | - |
| SceneInstanceManager | 16 | 子场景/副本管理（骨架） | - |
| NpcManager | 17 | NPC 配置/互动（骨架） | - |
| CraftingManager | 18 | 装备制作（骨架） | - |
| ShopManager | 19 | 商店交易（骨架） | - |

### Manager/扩展 — 第三方模块
- **DanmuManager** (优先级 50)：B 站弹幕直播集成（可选）

---

## Manager 快速参考

### InputManager（优先级 2）
- **路径**：`Scripts/EssSystem/Core/Presentation/InputManager/`
- **职责**：统一输入抽象，支持 Action 绑定、Axis 查询、鼠标输入
- **核心 Event**：`BindInputAction` / `IsInputPressed` / `GetInputAxis` / `GetMouseScreenPosition`
- **当前实现**：基于 Legacy Input Manager；支持切换到 New Input System
- **详见**：`Scripts/EssSystem/Core/Presentation/InputManager/Agent.md`

### AudioManager（优先级 3）
- **路径**：`Scripts/EssSystem/Core/Presentation/AudioManager/`
- **职责**：BGM / SFX 管理，音量持久化，SFX 对象池
- **核心 Event**：`PlayBGM` / `PlaySFX` / `SetMasterVolume` / `SetBGMVolume` / `SetSFXVolume`
- **资源加载**：通过 ResourceManager（bare-string `"GetAudioClip"`）
- **详见**：`Scripts/EssSystem/Core/Presentation/AudioManager/Agent.md`

### UIManager（优先级 5）
- **路径**：`Scripts/EssSystem/Core/Presentation/UIManager/`
- **职责**：UI 实体管理中心，Canvas 自动建立，UGUI 组件树
- **核心 Event**：`RegisterUIEntity` / `UnregisterUIEntity` / `GetUIGameObject` / `HotReloadUIConfigs`
- **架构**：UIComponent DAO + UIEntity 运行时分离
- **约束**：禁止业务方直接创建 UI，只构造 DAO
- **详见**：`Scripts/EssSystem/Core/Presentation/UIManager/Agent.md`

### CameraManager（优先级 6）
- **路径**：`Scripts/EssSystem/Core/Presentation/CameraManager/`
- **职责**：相机控制，跟随/缩放/震动等效果
- **核心 Event**：`SetCameraTarget` / `SetCameraZoom` / `ShakeCamera`
- **详见**：`Scripts/EssSystem/Core/Presentation/CameraManager/Agent.md`

### LightManager（优先级 7）
- **路径**：`Scripts/EssSystem/Core/Presentation/LightManager/`
- **职责**：光照管理，天空盒、动态光控制（URP 专用）
- **核心 Event**：`SetSkybox` / `SetAmbientLight` / `AddDynamicLight`
- **详见**：`Scripts/EssSystem/Core/Presentation/LightManager/Agent.md`

### EffectsManager（优先级 8）
- **路径**：`Scripts/EssSystem/Core/Presentation/EffectsManager/`
- **职责**：特效播放，粒子系统管理
- **核心 Event**：`PlayEffect` / `StopEffect` / `ClearAllEffects`
- **详见**：`Scripts/EssSystem/Core/Presentation/EffectsManager/Agent.md`

### InventoryManager（优先级 10）
- **路径**：`Scripts/EssSystem/Core/Application/InventoryManager/`
- **职责**：背包系统，物品管理，快捷栏
- **核心 Event**：`OpenInventoryUI` / `RegisterItem` / `SpawnPickableItem`
- **详见**：`Scripts/EssSystem/Core/Application/InventoryManager/Agent.md`

### CharacterManager（优先级 11）
- **路径**：`Scripts/EssSystem/Core/Presentation/CharacterManager/`
- **职责**：角色外观工厂，2D Sprite / 3D Prefab 统一管理
- **核心 Event**：`CreateCharacter` / `DestroyCharacter` / `PlayCharacterAction` / `SetCharacterScale`
- **新增方法**：`PreloadCharacterSprites(basePath)` —— 业务方负责调用
- **详见**：`Scripts/EssSystem/Core/Presentation/CharacterManager/Agent.md`

### MapManager（优先级 12）
- **路径**：`Scripts/EssSystem/Core/Application/MapManager/`
- **职责**：地图生成/管理，支持 2D Perlin + 3D Voxel
- **当前实现**：纯 C# API（`MapService.Instance.XXX`），不暴露 Event
- **详见**：`Scripts/EssSystem/Core/Application/MapManager/Agent.md`

### EntityManager（优先级 13）
- **路径**：`Scripts/EssSystem/Core/Application/EntityManager/`
- **职责**：实体管理系统，能力系统，Utility AI
- **核心 Event**：`CreateEntity` / `DestroyEntity` / `DamageEntity` / `RegisterSceneEntity`
- **依赖**：CharacterManager + MapManager
- **详见**：`Scripts/EssSystem/Core/Application/EntityManager/Agent.md`

### BuildingManager（优先级 14）
- **路径**：`Scripts/EssSystem/Core/Application/BuildingManager/`
- **职责**：建筑系统，建造/销毁/补给
- **核心 Event**：`PlaceBuilding` / `SupplyBuilding` / `DestroyBuilding`
- **依赖**：EntityManager
- **详见**：`Scripts/EssSystem/Core/Application/BuildingManager/Agent.md`

### DialogueManager（优先级 15）
- **路径**：`Scripts/EssSystem/Core/Application/DialogueManager/`
- **职责**：对话系统，分支对话，选项选择
- **核心 Event**：`OpenDialogueUI` / `AdvanceDialogue` / `SelectDialogueOption`
- **详见**：`Scripts/EssSystem/Core/Application/DialogueManager/Agent.md`

### SkillManager（优先级 15）
- **路径**：`Scripts/EssSystem/Core/Application/SkillManager/`
- **职责**：技能系统，技能学习/释放
- **核心 Event**：`RegisterSkill` / `LearnSkill` / `CastSkill`
- **详见**：`Scripts/EssSystem/Core/Application/SkillManager/Agent.md`

### DanmuManager（优先级 50）
- **路径**：`Scripts/EssSystem/Manager/DanmuManager/`
- **职责**：B 站弹幕直播集成（可选第三方模块）
- **核心 Event**：`OnDanmuConnected` / `OnDanmuComment` / `OnDanmuGift` / `OnDanmuSuperChat`
- **详见**：`Scripts/EssSystem/Manager/DanmuManager/Agent.md`

---

## Manager 初始化顺序

### 自动发现与初始化流程

1. **EventProcessor** (-30) ⚠️ 必须最先
   - 初始化事件系统
   - 为其他 Manager 提供通信枢纽

2. **DataManager** (-20)
   - 初始化数据持久化
   - 监听 Service 初始化事件

3. **ResourceManager** (0)
   - 初始化资源加载系统
   - 预加载资源

4. **Presentation 层** (2-11)
   - InputManager (2)
   - AudioManager (3)
   - UIManager (5)
   - CameraManager (6)
   - LightManager (7)
   - EffectsManager (8)
   - CharacterManager (11)

5. **Application 层** (10-19)
   - InventoryManager (10)
   - MapManager (12)
   - EntityManager (13) —— 依赖 CharacterManager + MapManager
   - BuildingManager (14) —— 依赖 EntityManager
   - DialogueManager (15)
   - SkillManager (15)
   - 其他 Manager (16+)

6. **第三方模块** (50+)
   - DanmuManager (50)

---

## Manager 通信约定

### 同模块通信
```csharp
// ✅ 推荐：直接调用 Service
UIService.Instance.RegisterUIEntity(daoId, component);
```

### 跨模块通信
```csharp
// ✅ 推荐：通过 EventProcessor 走 bare-string
EventProcessor.Instance.TriggerEventMethod(
    "RegisterUIEntity", 
    new List<object> { daoId, component });
```

### 禁止事项
```csharp
// ❌ 禁止：跨模块直接 using 其他 Manager
using EssSystem.Core.Presentation.UIManager;
UIManager.Instance.RegisterUIEntity(...);

// ❌ 禁止：为读常量而 using 其他模块
using EssSystem.Core.Presentation.UIManager;
EventProcessor.Instance.TriggerEventMethod(
    UIManager.EVT_REGISTER_ENTITY, ...);
```

---

## 后续优化方向

1. **InputManager 优化**：完全支持 New Input System，提供兼容层
2. **AudioManager 优化**：音频混音器集成，动态压缩
3. **CameraManager 优化**：相机动画曲线库，视锥剔除优化
4. **EffectsManager 优化**：特效对象池 + 自动清理机制
5. **LightManager 优化**：动态光阴影优化，烘焙集成
6. **MapManager 优化**：Event API 暴露，支持更多生成算法
7. **EntityManager 优化**：性能优化，大规模实体支持
8. **新增 Manager**：根据项目需求添加新的业务 Manager

---

## 注意事项

- **优先级不可改**：标记为"不可改"的 Manager 优先级是框架硬约束
- **依赖关系**：必须遵守 Manager 间的依赖关系，否则初始化失败
- **事件常量化**：所有 Event 都必须在定义方用常量，消费方用 bare-string
- **文档同步**：修改 Manager 时必须同步更新 Agent.md 和根目录 Events.md
- **lint 检查**：提交前必须运行 `agent_lint.ps1 -Strict` 检查 Event 一致性
