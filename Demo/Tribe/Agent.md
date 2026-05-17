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
├── TribeWorldSpawner.cs                 世界随机生成器（泊松采样 + 加权随机）
├── TribeCampfire.cs                     营火（动画 + 距离音量）
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
├── Enemy/                               敌人/动物系统
│   ├── TribeCreature.cs                     通用生物（配置驱动，支持动物 + 怪物）
│   ├── TribeCreatureConfig.cs               生物配置数据类
│   ├── TribeCreaturePresets.cs              预定义配置（Cow/Chicken/Wolf/Ogre/...）
│   ├── TribeEnemyContactDamager.cs          接触伤害组件
│   ├── TribeEnemyHealthUI.cs                血条 UI
│   └── TribeSpriteAnimator.cs               通用 4行×4列 16帧 spritesheet 动画器
│
├── Resource/                            可采集资源
│   └── PickableDropEntity.cs                可被攻击的掉落实体（树/蘑菇/浆果丛等）
│
└── Background/                          背景
    └── ParallaxLayer.cs                     视差背景单层（循环平铺）
```

> 本次结构调整：
> - `Player/Compoment/` 拼写修正 → `Player/Component/`
> - `TribeSkeletonEnemy.cs` + `TribeSkeletonAnimator.cs` 已删：骷髅走通用 `TribeCreature` + `TribeCreaturePresets.Skeleton()` 驱动，专用骨架动画器被 `TribeSpriteAnimator` 覆盖
> - `PickableDropEntity.cs` 根目录 → `Resource/`（资源采集实体有专属包）

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

2. **骷髅**已并入 `TribeCreature` —— 调用 `AddComponent<TribeCreature>().Configure(TribeCreaturePresets.Skeleton())` 即可生成，与其它怀物 / 动物走同一条 Brain + Capability 链路。

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

TribeEnemyContactDamager (OnTriggerStay2D)
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
├── Tribe/
│   ├── Background/                  视差背景图（按文件名排序为各层）
│   └── Entity/
│       ├── Skeleton 01_idle (16x16) 骷髅 idle spritesheet
│       └── Skeleton 01_walk (16x16) 骷髅 walk spritesheet
```

## 扩展建议

- **新增动物 / 怪物**：在 `TribeCreaturePresets` 添加配置 + 在 `_spawnZones` 引用
- **建筑系统**：当前帐篷/营火/篱笆是手写代码，可迁移到 `BuildingManager`
- **技能系统**：玩家攻击当前是直接近战 OverlapBox，可改用新的 `SkillManager` 体系
