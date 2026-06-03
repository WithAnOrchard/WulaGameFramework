# Cubic - 方块世界的冒险之旅

## 项目简介

**Cubic** 是一款纯方块（Cube/Voxel）美术风格的RPG游戏，采用Unity引擎 + WulaGameFramework 框架开发。

### 核心特色
- 🎮 **纯方块视觉风格** - 不同颜色方块代表不同职业
- ⚔️ **RPG冒险与战斗系统** - 8大职业，丰富的技能树
- 🧙 **职业技能特效** - 基于 EffectsManager 的华丽技能特效
- 🎯 **即时战斗模式** - 流畅的技能释放和连招系统

## 项目结构

```
Cubic/
├── Scripts/              # 游戏脚本
│   ├── CubicGameManager.cs      # 游戏主管理器
│   ├── CubicCharacter.cs         # 角色组件
│   ├── CubicSkillDefs.cs         # 职业技能定义
│   ├── CubicVFXDefs.cs           # VFX特效定义
│   └── CubicClassColors.cs       # 职业颜色配置
├── Resources/            # 游戏资源
│   └── Prefabs/         # 预制体
│       ├── Characters/  # 角色预制体
│       └── VFX/        # 特效预制体
├── Scenes/             # 游戏场景
├── Art/                # 美术资源
│   └── Sprites/       # 精灵图
├── Audio/             # 音频资源
├── DESIGN.md          # 游戏设计文档
└── CLASSES.md         # 职业系统详细设计
```

## 职业系统

### 8 大职业 - 颜色标识

| 职业 | 颜色 | 代码 | 定位 |
|-----|------|------|------|
| 战士 | 铁灰色 | `#708090` | 近战物理 |
| 魔法师 | 紫色 | `#9932CC` | 远程魔法 |
| 弓箭手 | 绿色 | `#32CD32` | 远程物理 |
| 圣骑士 | 蓝色 | `#4169E1` | 近战辅助 |
| 刺客 | 黑色 | `#1C1C1C` | 近战爆发 |
| 工程师 | 橙色 | `#FF8C00` | 远程召唤 |
| 死灵法师 | 深紫色 | `#4B0082` | 召唤/诅咒 |
| 圣职者 | 金色 | `#FFD700` | 治疗/辅助 |

详细职业设计请查看 [CLASSES.md](CLASSES.md)

## 技术框架

### WulaGameFramework 集成

本项目基于 WulaGameFramework 框架开发，使用了以下核心系统：

- **CharacterManager** - 角色视觉管理
- **SkillManager** - 技能系统
- **EffectsManager** - 特效和屏幕闪光
- **EntityManager** - 实体和AI
- **InventoryManager** - 背包系统
- **AudioManager** - 音频管理

### 技能系统

使用框架的 SkillManager 管理所有职业技能：

```csharp
// 注册技能
CubicSkillDefs.EnsureRegistered();

// 使用技能
SkillService.Instance.CastSkill(caster, "cubic_warrior_slash", target, direction);
```

### 特效系统

使用框架的 EffectsManager 管理所有 VFX 特效：

```csharp
// 播放VFX特效
CubicVFXDefs.PlaySkillVFX("warrior_slash", transform.position);

// 屏幕闪光反馈
CubicVFXDefs.PlayScreenFlash(ScreenFlashType.Damage);
```

## 开发状态

**当前阶段**：🟡 基础框架搭建中

| 模块 | 状态 | 说明 |
|-----|------|------|
| 基础框架 | ✅ 完成 | GameManager、Character组件 |
| 职业系统 | ✅ 完成 | 8职业定义、颜色配置 |
| 技能系统 | 🔧 开发中 | 框架集成、技能定义 |
| 特效系统 | 🔧 开发中 | VFX定义、特效管理 |
| 地图系统 | 🔲 待开发 | 体素地图生成 |
| 战斗系统 | 🔲 待开发 | 实时战斗实现 |
| AI系统 | 🔲 待开发 | 敌人AI |

## 如何开始

1. 确保 WulaGameFramework 已正确配置
2. 打开 `Scenes/` 下的游戏场景
3. 运行游戏，选择职业开始冒险

## 职业示例代码

```csharp
// 创建角色
var character = gameObject.AddComponent<CubicCharacter>();
character.CharacterClass = CubicCharacterClass.Warrior;
character.ApplyClassColor();

// 使用技能
var skillResult = SkillService.Instance.CastSkill(
    character,
    "cubic_warrior_slash",
    target,
    transform.forward
);

// 播放技能特效
CubicVFXDefs.PlaySkillVFX("warrior_slash", hitPoint);
CubicVFXDefs.PlayScreenFlash(ScreenFlashType.Crit);
```

## 文档

- [游戏设计文档](DESIGN.md) - 完整的游戏设计
- [职业系统详解](CLASSES.md) - 8大职业详细设计
- [框架文档](../../Scripts/EssSystem/Core/) - WulaGameFramework 文档

## 版本历史

| 版本 | 日期 | 更新内容 |
|-----|------|---------|
| 0.1.0 | 2026-05-31 | 基础框架、职业系统、技能和特效定义 |
