# SkillManager

**Priority**: `[Manager(15)]` — 在 EntityManager(13) / BuildingManager(14) 之后

## 职责

技能/能力系统 —— RPG 攻击、魔法、Buff 的数据驱动管理与执行。

## 架构

```
SkillManager/
├── SkillManager.cs          Manager 薄壳（驱动 Tick + bare-string 事件接口）
├── SkillService.cs          业务核心（注册、查询、释放、Buff 管理）
├── Agent.md                 本文档
├── Dao/
│   ├── SkillDefinition.cs   技能静态定义（ID、消耗、冷却、效果链、目标模式）
│   ├── SkillInstance.cs     技能运行时实例（等级、冷却计时）
│   ├── SkillSlot.cs         快捷栏槽位
│   ├── ISkillEffect.cs      技能效果接口 + SkillEffectContext
│   ├── Effects/
│   │   ├── DamageEffect.cs  伤害效果（调 EntityService.TryDamage）
│   │   ├── HealEffect.cs    治疗效果（调 IDamageable.Heal）
│   │   ├── BuffEffect.cs    施加 Buff
│   │   └── AoeEffect.cs     范围效果（Physics2D 检测 + 子效果链）
│   └── Buffs/
│       └── BuffInstance.cs   Buff 运行时实例（持续时间、Tick 回调）
└── Runtime/
    └── SkillExecutor.cs     执行管线（Idle → Casting → Execute → Recovery → Done）
```

## 数据流

```
SkillDefinition（静态）
    └── Effects: List<ISkillEffect>         ← 效果链（组合模式）
        ├── DamageEffect → EntityService.TryDamage
        ├── HealEffect → IDamageable.Heal
        ├── BuffEffect → SkillService.ApplyBuff
        └── AoeEffect → Physics2D.OverlapCircle → 子效果链

SkillInstance（运行时）
    ├── Level, CooldownRemaining
    └── 关联 SkillDefinition

SkillSlot（快捷栏）
    └── 关联 SkillInstance
```

## 释放流程

```
CastSkill(caster, skillId, target?, dir?, pos?)
  1. 查 SkillInstance → 检查 IsReady（冷却+解锁）
  2. 消耗检查（MP/HP）── TODO: 接入 INeeds
  3. 创建 SkillEffectContext
  4. SkillExecutor.Begin(ctx)
     a. CastTime > 0 → Phase.Casting（前摇，可被 Interrupt 打断）
     b. CastTime = 0 → 直接 ExecuteEffects
  5. ExecuteEffects：遍历 Definition.Effects，逐一 Apply(ctx)
  6. StartCooldown
  7. RecoveryTime > 0 → Phase.Recovery（后摇）
  8. Phase.Done
```

## Buff 生命周期

```
ApplyBuff(target, buff)
  → _entityBuffs[entityId].Add(buff)
  → 每帧 Tick：buff.Tick(dt)
    → OnTick 回调（持续伤害/回复等）
  → IsExpired → OnExpire 回调 → 移除
```

## Event API（bare-string §4.1）

| Event 常量 | bare-string | data 参数 | 返回 |
|---|---|---|---|
| `SkillManager.EVT_REGISTER_SKILL` | `"RegisterSkill"` | `[SkillDefinition]` | — |
| `SkillManager.EVT_LEARN_SKILL` | `"LearnSkill"` | `[entityId, skillId]` | — |
| `SkillManager.EVT_CAST_SKILL` | `"CastSkill"` | `[Entity caster, skillId, target?, Vector3 dir?, Vector3 pos?]` | `Ok` / `Fail(msg)` |

## C# API（直接调用）

```csharp
SkillService.Instance.RegisterDefinition(def);
SkillService.Instance.LearnSkill(entityId, skillId);
SkillService.Instance.CastSkill(caster, skillId, target, dir, pos);
SkillService.Instance.ApplyBuff(target, buff);
SkillService.Instance.GetBuffs(entityId);
SkillService.Instance.InitSlots(entityId, 4);
SkillService.Instance.BindSlot(entityId, 0, skillId);
```

## 扩展点

- **ProjectileEffect**: 生成投射物 Entity（EntityManager.EVT_REGISTER_SCENE_ENTITY）
- **SummonEffect**: 召唤怪物/援军
- **TeleportEffect**: 瞬移
- **ComboSystem**: 连击链管理
- **SkillTree**: 技能树/前置条件
- **MP/资源系统**: 接入 INeeds 或独立 ManaComponent
