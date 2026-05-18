# Tribe Demo 模块指南

## 概述

**Tribe** 是一个基于 EssSystem 框架的横版 2D 部落生存 Demo，演示：
- 框架启动入口（`AbstractGameManager` 继承）
- 实体系统（Capability 组合 + Brain AI）
- 横版地图生成 + 视差背景
- 玩家移动/战斗/HUD/重生
- 多种动物/怪物的随机生成与 AI 行为

**入口**：`TribeGameManager.cs`（继承 `AbstractGameManager`）

## 目录结构

```
Demo/Tribe/
├── Tribe.unity                          演示场景
├── TribeGameManager.cs                  总控（继承 AbstractGameManager）
├── TribeCollisionLayers.cs              碰撞矩阵助手（被 Entity / Resource 共享）
│
├── Player/                              玩家系统
│   ├── TribePlayer.cs                       玩家编排器（HP/MP/Exp/金币/死亡重生）
│   ├── TribePlayerDamageEffect.cs           受伤闪烁 + 击退
│   └── Component/                           子模块组件（physical-only 子目录）
│       ├── TribePlayerMovement.cs               能力装配（IMovable/IJumpable/IFacing/...）
│       ├── TribePlayerCombat.cs                 近战攻击（鼠标左键 + OverlapBox）
│       ├── TribePlayerHud.cs                    HUD（血/蓝/经验条 + 金币 + 头像）
│       ├── TribePlayerInteraction.cs            交互（B 键背包 / I 键对话）
│       └── TribePlayerCameraFollow.cs           镜头跟随
│
├── Entity/                              生物系统（动物 + 怪物，配置驱动）
│   ├── Creature/                            生物核心（不跟随具体物种）
│   │   ├── TribeCreature.cs                     生物编排器（配置驱动，动物 + 怪物）
│   │   ├── TribeCreatureConfig.cs               生物配置数据类
│   │   └── TribeCreatureSpawner.cs              周期性刷新点 MB
│   ├── Combat/                              战斗辅助
│   │   ├── TribeCreatureContactDamager.cs       接触伤害组件（怪物之间互不伤害）
│   │   └── TribeCreatureHealthUI.cs             头顶血条 UI（World→Screen 投影）
│   └── Creatures/                           **按物种一个文件 / 一个文件夹**
│       ├── Cow.cs                               Preset()
│       ├── Hen.cs                               Preset()
│       ├── Pig.cs                               Preset()
│       ├── Skeleton.cs                          Preset()
│       ├── Wolf.cs                              Preset()
│       ├── Bat.cs                               Preset()
│       ├── Mushy.cs                             Mushy01/02/03/04() —— 4 种共一族
│       ├── Ogre.cs                              Preset()
│       └── Slime/                               史莱姆 —— 含专属 MB + 技能注册
│           ├── Slime.cs                            Preset() + EnsureSkillsRegistered()
│           │                                          · SKILL_BIG_HOP   → 框架 DashEffect
│           │                                          · SKILL_GIANT     → 框架 BuffEffect + BuildGiantBuff 闭包
│           ├── TribeSlimeHopBehavior.cs             小蹦自驱 + 大蹦 / 巨大化 cast 触发
│           └── GiantSlimeState.cs                   "巨大化" 运行时 MB（视觉 + 属性 + 跳跃倍率）
│
├── Interaction/                         玩家与世界互动 UI
│   ├── TribeInteractable.cs                 通用 IInteractable 适配
│   └── TribeCraftingPanel.cs                合成面板
│
├── World/                               世界生成 + 设施
│   ├── TribeWorldSpawner.cs                 世界随机生成器（泊松采样 + 加权随机）
│   ├── TribeWorldBoundary.cs                世界左/右边界墙
│   ├── TribeCampfire.cs                     营火设施（动画 + 距离音量）
│   ├── TribeBiomeConfig.cs                  Biome 静态定义
│   ├── TribeBiomeContext.cs                 Biome 生成期上下文
│   ├── TribeBiomeRegistry.cs                Biome 注册表
│   ├── TribeFeatureSpec.cs                  Feature 接口
│   ├── TribeFarmCoordinator.cs              农场协调（FarmManager bare-string 路由）
│   ├── Features/                            具体 IFeature 实现（营火 / 帐篷 / 资源点 / 生物点）
│   └── Presets/                             默认 Biome 组合（TribeDefaultBiomes 等）
│
├── Resource/                            可采集资源
│   └── PickableDropEntity.cs                可被攻击的掉落实体（树/蘑菇/浆果丛等）
│
└── Background/                          背景
    └── ParallaxLayer.cs                     视差背景单层（循环平铺）
```

> 本次结构调整（2026-05）：
> - **动画走 CharacterManager**：删除 `TribeSpriteAnimator` 与 `SpritePivot`（连同 `Entity/Common/` 子目录），所有生物视觉改走框架 `CharacterPartView2D`（Sprite2D 模式）+ 新增的 sheet 模式（`CharacterActionConfig.SheetResourcePath` + `DirectionalFrameIndices`）。每个物种 `EnsureCharacterRegistered()` 调 `CharacterConfigFactory.RegisterSheetCreature(...)` 一次性注册到 `CharacterManager.CharacterService`。`TribeCreature` 通过 `CharacterViewBridge.CreateCharacter` 创建视觉子节点，运行时用 `SetDirection` / `PlayLocomotion` 驱动。
> - **Entity 命名空间防 shadow**：物理目录保留 `Entity/`，但 namespace 改为 `Demo.Tribe.Entities`（复数）—— 避免与框架 `Entity` 类型在 `Demo.Tribe.*` 子命名空间内被解析为命名空间而导致 CS0118。`TribePlayerMovement.Entity` 等所有 Player / Resource 侧的"裸 Entity 类型"现在能稳定解析到框架类型。
> - **TribeCreatureConfig 瘦身**：删除 `IdleResourcePath / WalkResourcePath / VisualScale / VisualYOffset / FrameTime / Pivot`；新增 `CharacterConfigId` 指向已注册的角色配置。
> - **历史 — 按物种拆分**：原 `TribeCreaturePresets.cs`（12 个 preset 集中文件）拆为 `Entity/Creatures/{Cow,Hen,Pig,Skeleton,Wolf,Bat,Mushy,Ogre,Slime/}`，每个物种一个文件 / 文件夹。
> - **历史 — 技能并入物种文件**：原 `Entity/Skills/TribeSlimeSkills.cs` 并入 `Entity/Creatures/Slime/Slime.cs` 的 `EnsureSkillsRegistered()` 与 `BuildGiantBuff()` 静态方法。
> - **历史 — Enemy→Entity 重命名 + 类名对齐**：`Enemy/` → `Entity/`；`TribeEnemyContactDamager` / `TribeEnemyHealthUI` → `TribeCreatureContactDamager` / `TribeCreatureHealthUI`；`TribeCampfire.cs` / `TribeWorldSpawner.cs` 从根迁入 `World/`。
> - **历史 — 业务侧不实现 `ISkillEffect`**：`SlimeBigHopEffect.cs` / `GiantBuffEffect.cs` 已删除，大蹦走框架 `DashEffect`、巨大化走框架 `BuffEffect` + `BuffFactory` 闭包。

## 技能架构原则（业务侧规则）

**所有技能必须通过 `SkillManager` 注册 / 学习 / 施放，业务侧不实现 `ISkillEffect` 接口。**

- **通用效果**（`DamageEffect` / `BuffEffect` / `DashEffect` / `ProjectileEffect` / ...）都住在 `SkillManager/Dao/Effects/`。
- **Tribe 专属内容**（具体 `SkillDefinition`、Tribe 特有的 `MonoBehaviour` 状态如 `GiantSlimeState`）按物种住在 `Entity/Creatures/<Species>(/)*.cs`。
- **跨层接口**：业务侧通过 `BuffEffect.BuffFactory`（`Func<SkillEffectContext, float, BuffInstance>`）这种闭包入口注入特定逻辑（视觉 / 属性变换），保持框架与业务的单向依赖。

参考：`Entity/Creatures/Slime/Slime.BuildGiantBuff` 是 BuffFactory 闭包的标准范例。

## 动画架构原则（业务侧规则）

**所有动画必须通过 `CharacterManager` 创建 / 驱动，业务侧不实现 SpriteRenderer 帧序列。**

- **物种视觉注册**：每个物种的静态类（`Cow` / `Slime` / ...）暴露 `EnsureCharacterRegistered()`；内部调 `CharacterConfigFactory.RegisterSheetCreature(...)` 把 4 行 × 4 列 spritesheet 转为标准 `CharacterConfig`（行 1=左，行 2=右，4 帧 idle / walk）。`Preset()` 在返回 `TribeCreatureConfig` 前会先触发它，业务调用方零额外步骤。
- **视觉创建**：`TribeCreature.BuildCharacterView()` 走 `CharacterViewBridge.CreateCharacter(configId, instanceId, parent, position)`，与 Player / Campfire 同一管线。
- **运行时驱动**：`CharacterViewBridge.SetDirection(instanceId, ±1)` 选朝向行的帧序列；`PlayLocomotion(instanceId, moving, grounded)` 切 Idle/Walk action。Slime 因为没有 Brain，由 `TribeSlimeHopBehavior.DoSmallHop` 自驱动 SetDirection。
- **业务专属变换**：临时性的视觉调整（如巨大化的 scale × 染色）走 `MonoBehaviour` 状态类（如 `GiantSlimeState`），通过 `TribeCreature.CharacterRoot` 拿到 character 根节点后操作；不再走 `transform.Find("Visual")` 这种约定路径。

参考：`Entity/Creatures/Cow.EnsureCharacterRegistered` / `Entity/Creatures/Slime/GiantSlimeState.GetVisualRoot` 是新管线两端的标准范例。

## 启动流程

```
TribeGameManager.Awake()  ← 继承自 AbstractGameManager
  ├── (基类) EnsureBaseManagers              确保框架基础 Manager 就绪
  ├── (基类) 反射扫描场景 Manager → 按优先级 Initialize
  └── (子类重载) Initialize / Start 阶段：
      1. EnsurePhysicsLayers()              配置 BuildingBarrier / Enemy 碰撞矩阵
      2. RegisterTribeInventoryContent()    注册物品到 InventoryManager
      3. SpawnStartupMap()                  生成地图 + Player
         ├── ApplyCameraSize()                  相机正交大小
         ├── SpawnBackground()                  视差背景（Resources/Tribe/Background/）
         ├── SpawnTerrainAndPlayer()            生成地形（可选）+ 玩家
         ├── EnsureFallbackGround()             无地形时的隐形地板
         └── EnsureLeftBoundaryWall()           左边界墙
      4. SpawnTribeBuildings()              帐篷/营火/篱笆/大门
         └── player.SetRespawnPosition(tentPos) 设置玩家死亡重生点
      5. RunWorldSpawner()                  泊松采样撒播资源 / 敌人 / 动物
```

## 核心模块

### TribeGameManager（总控）

继承 `AbstractGameManager`，提供横版 Demo 专属配置。负责：

| Inspector 分组 | 主要字段 |
|---|---|
| **Map** | `_generateTerrain` `_mapId` `_mapConfigId` |
| **Camera** | `_cameraOrthographicSize` `_lockCameraY` `_cameraLockY` |
| **Background** | `_backgroundResourceFolder` `_foregroundStartIndex` `_minParallax/_maxParallax` `_groundLayerIndex` |
| **Floor** | `_spawnFallbackGround` `_fallbackGroundY` `_fallbackGroundWidth` |
| **World Boundary** | `_enableLeftBoundary` `_leftBoundaryX` |
| **World Spawner** | `_worldSeed` `_spawnZones` |
| **Player Spawn** | `_autoSpawnPlayer` `_playerSpawnX` `_playerSpawnHeightAboveSurface` |

### Player（玩家）

`TribePlayer` 是 **编排器**，把行为切分到子组件：

```
TribePlayer (MonoBehaviour, 编排器)
  ├── 数值状态        HP / MP / Exp / Coins（权威数据）
  ├── 死亡重生        OnEntityDamaged → IsDead → Respawn() → 传送至 _respawnPosition
  ├── TribePlayerMovement       Entity + Capabilities 装配（IMovable/IJumpable/IFacing）
  ├── TribePlayerCombat         鼠标左键 + OverlapBox → handle.TakeDamage
  ├── TribePlayerHud            HUD 显示
  ├── TribePlayerInteraction    B 键背包 / I 键对话
  ├── TribePlayerCameraFollow   镜头跟随（LateUpdate）
  └── TribePlayerDamageEffect   受伤闪烁 + 击退
```

### Enemy（敌人/动物）

两条线：

1. **`TribeCreature`**（通用，推荐）—— 配置驱动
   - 由 `TribeCreatureConfig` 描述所有参数（HP / 速度 / 能否攻击 / 击退力等）
   - `TribeCreaturePresets` 提供 Cow / Chicken / Wolf / Ogre / Skeleton 等预设
   - 自动构建：Visual + Animator + HealthUI（如可攻击）+ ContactDamager（如可攻击）+ Brain AI

2. **骷髅**已并入 `TribeCreature` —— 调用 `AddComponent<TribeCreature>().Configure(Skeleton.Preset())` 即可生成，与其它怀物 / 动物走同一条 Brain + Capability 链路。

### Enemy AI（Brain）

| Consideration | 优先级 | 触发条件 | 行为 |
|---|---|---|---|
| **Flee_LowHp** | 高 | HP < 阈值 | `FleeAction` 远离威胁 |
| **Chase_Aggro** | 中-高 (0.75) | 受到攻击 → `ThreatSource` 被设置 | `ChaseAction` 追击攻击者 |
| **Patrol** | 低 | 默认 | `PatrolAction` 巡逻 |

### TribeWorldSpawner（世界生成）

- **泊松圆盘采样**：1D 简化版，保证生成物有最小间距
- **加权随机表**：每个 `SpawnZone` 配置多个 `SpawnEntry`（权重 + Y 偏移 + 资源路径）
- **分区段**：例如 近郊 / 荒野 / 深处，每段不同生成规则
- **生成种类**：Resource（可采集）/ Enemy（怪物）/ Animal（动物）/ Decoration

### Background（视差背景）

`ParallaxLayer` 单层组件：
- 跟随相机：`localX = -Repeat(camX * factor, wrapWidth)`
- 多副本横向循环（首/中/尾）
- 由 `TribeGameManager.SpawnBackground` 按 `_minParallax → _maxParallax` 线性分配各层视差强度

## 关键交互

```
TribePlayerCombat.OnAttack()
  → Physics2D.OverlapBoxAll
  → 遍历 EntityHandle
  → handle.TakeDamage(damage, sourceEntity: _selfHandle.Entity)
  → EntityService.TryDamage
  → IDamageable.TakeDamage → Damaged 事件
  → IBrain.OnDamaged → ThreatSource 设置 → 触发 ChaseAction

TribeCreatureContactDamager (OnTriggerStay2D)
  → 反查 EntityHandle
  → handle.TakeDamage(damage, sourceEntity: 自身 Entity)
  → 同上流水线

玩家死亡：
  TribePlayer.OnEntityDamaged → IsDead → Invoke(Respawn, 1s)
  Respawn() → dmg.Revive() + transform.position = _respawnPosition（帐篷处）
```

## 与框架的依赖

| Demo 调用 | 框架模块 |
|---|---|
| `TribeGameManager : AbstractGameManager` | Core |
| `MapManager.Service.LoadConfig / SpawnMap` | MapManager |
| `EntityService.TryDamage / Tick / RegisterEntity` | EntityManager |
| `EntityHandle.TakeDamage` | EntityManager.Runtime |
| `CharacterService.GetCharacterRoot` | CharacterManager |
| `InventoryManager.EVT_REGISTER_INVENTORY_ITEM` | InventoryManager |
| `InventoryManager.EVT_ITEM_USED` | InventoryManager |
| `AudioManager.Instance.SFXVolume` | AudioManager |
| `EventProcessor.TriggerEventMethod("PlayDamageSFX")` | Core/Event |

## 物理 Layer 约定

| Layer | 用途 |
|---|---|
| **Default** | 玩家 / 地面 / 通用 |
| **Enemy** | 敌人（与 BuildingBarrier 碰撞） |
| **BuildingBarrier** | 篱笆等建筑墙（只挡 Enemy，不挡玩家） |

## 资源路径约定

```
Resources/
├── Sound/                            通用音频（含 feuer.wav 营火声）
└── Tribe/
    ├── Background/                  视差背景图（按文件名排序为各层）
    ├── Entity/                      生物 spritesheet（骷髅 / 牛 / 狼 / Ogre / ... 16/20/32 px）
    ├── Objects/                     场景物件素材（campfire / Crops / Plants / Mushroom_2 / ...）
    └── Items/                       物品图标（按功能分类的子目录）
        ├── Weapons/                     刀剑 / 弓 / 法杖 / 锤斧 / 投掷物
        ├── Armor/                       盾牌 / 铠甲 / 头盔 / 法袍 / 面具
        ├── Accessories/                 戒指 / 项链 / 宝石 / 灵球 / 翼饰
        ├── Consumables/                 药水 / 食物 / 浆果 / 蘑菇 / 草药 / 花卉 / 橡果
        ├── Materials/                   骨头 / 獠牙 / 眼球 / 蛋 / 木头 / 石头 / 史莱姆碎块
        ├── Currency/                    金币袋 / 铜币堆 / 宝箱币
        ├── Tools/                       铲 / 镐 / 钥匙 / 铁砧 / 钓具 / 箱子 / 卷轴
        ├── Magic/                       月相 / 元素弹 / 风波 / 星光 / 心粒
        └── UI/                          数字字符 / 槽位框 / 标记 / 鼠标 / 浮窗
```

> 代码引用 Item 时使用完整路径：`"Tribe/Items/<分类>/<名称>"`，例如 `Tribe/Items/Consumables/carrot`。

## 扩展建议

- **新增动物 / 怪物**：在 `TribeCreaturePresets` 添加配置 + 在 `_spawnZones` 引用
- **建筑系统**：当前帐篷/营火/篱笆是手写代码，可迁移到 `BuildingManager`
- **技能系统**：玩家攻击当前是直接近战 OverlapBox，可改用新的 `SkillManager` 体系
