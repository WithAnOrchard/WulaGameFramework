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
│   │   ├── DamageEffect.cs       伤害效果（调 EntityService.TryDamage）
│   │   ├── HealEffect.cs         治疗效果（调 IDamageable.Heal）
│   │   ├── BuffEffect.cs         施加 Buff（自定义 BuffFactory）
│   │   ├── AoeEffect.cs          范围效果（Physics2D 检测 + 子效果链）
│   │   ├── DashEffect.cs         冲刺 + 可选无敌帧
│   │   ├── JumpSlashEffect.cs    跳起 → 滞空 → 砸地 AOE
│   │   ├── SummonEntityEffect.cs 环形召唤 N 个 EntityConfig
│   │   ├── TeleportEffect.cs     瞬移（绝对位置 / 相对方向）
│   │   ├── KnockbackEffect.cs    击退（写 Rigidbody2D.velocity）
│   │   ├── ShieldEffect.cs       减伤护盾（叠加取较高值）
│   │   ├── DotEffect.cs          持续伤害（OnTick + TryDamage）
│   │   ├── HotEffect.cs          持续治疗（OnTick + Heal）
│   │   ├── WhirlwindEffect.cs    旋风斩（每 tick 自身周围 AOE）
│   │   ├── ProjectileEffect.cs   投射物（生成 SkillProjectile MB 直线飞）
│   │   ├── ChainLightningEffect.cs 链式闪电（最近邻跳跃 + 伤害衰减）
│   │   ├── MeteorEffect.cs       陨石术（PointTarget 延迟 AOE）
│   │   ├── LifeDrainEffect.cs    吸血（TryDamage + Heal 比例回血）
│   │   ├── CleaveEffect.cs       锥形挥砍（角度 + 半径过滤 + 子效果链）
│   │   ├── MultiShotEffect.cs    扇形多重投射
│   │   ├── CleanseEffect.cs      净化 Buff（按 ID 列表 / 全部）
│   │   ├── SlowEffect.cs         减速 / 加速（写 IMovable.SpeedMultiplier）
│   │   ├── StunEffect.cs         眩晕（Push IControllable.Stun，过期 Pop）
│   │   ├── SilenceEffect.cs      沉默（Push IControllable.Silence，过期 Pop）
│   │   └── DamageReflectEffect.cs 反伤（订阅 IDamageable.Damaged 事件）
│   ├── Buffs/
│   │   └── BuffInstance.cs       Buff 运行时实例（持续时间、Tick 回调）
│   └── Skills/
│       └── CommonSkills.cs       通用技能定义工厂（BuildXxx + EnsureRegistered）
└── Runtime/
    ├── SkillExecutor.cs          执行管线（Idle → Casting → Execute → [Channeling] → Recovery → Done）
    ├── SkillProjectile.cs        投射物运行时（直线飞行 + 命中检测）
    └── ComboTracker.cs           连招追踪器（滚动历史 + 时间窗匹配 → 触发 finisher 技能）
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
## Event API

> Full Event definitions (params / return / side effects / usage) live in root Events.md -> section: **SkillManager Event**.

- `SkillManager.EVT_CAST_SKILL`
- `SkillManager.EVT_LEARN_SKILL`
- `SkillManager.EVT_REGISTER_SKILL`

## 通用技能目录（CommonSkills）
`Dao/Skills/CommonSkills.cs` 提供市面 RPG/动作 / MOBA 常见技能的开箱即用定义。
全部为**纯 SkillManager 内部**实现 —— 不依赖任何 Demo 命名空间，业务侧可自由复用。

| 技能 ID | 类别 | 模式 | 默认 CD | 关键参数 / 效果链 |
|---|---|---|---|---|
| `common_dash` | 位移 | Directional | 4s | `DashEffect`：水平 14 速度 + 0.2s 无敌帧（DamageReduction=1） |
| `common_teleport` | 位移 | Directional | 6s | `TeleportEffect`：朝向位移 5 单位，瞬时同步 RB.velocity=0 |
| `common_jump_slash` | 攻击 | Directional | 8s | `JumpSlashEffect`：起跳→0.45s 滞空→砸地 AOE（半径 2.5、伤害 20）|
| `common_whirlwind` | 攻击 | None | 12s | `WhirlwindEffect`：3s 内每 0.4s 周围 2.5 半径打 6 伤 |
| `common_fireball` | 攻击 | Directional | 6s | `ProjectileEffect`：14 速度直线火球，命中 15 伤 |
| `common_burn` | 减益 | Targeted | 5s | `DotEffect`：5s 内每秒 4 火属性伤害 |
| `common_shockwave` | 攻击+控制 | None | 10s | `AoeEffect(3)` ⊃ `DamageEffect(10) + KnockbackEffect(12)` |
| `common_shield` | 防御 | None | 15s | `ShieldEffect`：5s 内 DamageReduction +50%（叠加取较高） |
| `common_regen` | 治疗 | None | 15s | `HotEffect`：6s 内每秒回 5 血 |
| `common_summon` | 召唤 | None | 12s | `SummonEntityEffect`：环形 N 个 EntityConfig（**业务必须传入 configId**） |
| `common_chain_lightning` | 攻击 | Targeted | 8s | `ChainLightningEffect`：最近邻 4 跳，每跳衰减 20% |
| `common_meteor` | 攻击 | PointTarget | 12s | `MeteorEffect`：1.2s 后落点 3 半径 AOE 35 伤 |
| `common_life_drain` | 攻击+治疗 | Targeted | 6s | `LifeDrainEffect`：14 伤害，50% 回血给施法者 |
| `common_cleave` | 攻击 | Directional | 4s | `CleaveEffect(3, 45°)` ⊃ `DamageEffect(12)` 锥形挥砍 |
| `common_multishot` | 攻击 | Directional | 5s | `MultiShotEffect`：扇形 3 连射，每发 7 伤 |
| `common_cleanse` | 解控 | None | 20s | `CleanseEffect`：移除自身所有 Buff（或指定 ID 列表） |
| `common_frost_nova` | 攻击+控制 | None | 10s | `AoeEffect(3.5)` ⊃ `DamageEffect(8, frost) + SlowEffect(×0.4, 3s)` |
| `common_haste` | 增益 | None | 18s | `SlowEffect(×1.6, 5s, self)` 借用同一字段做正向加速 |
| `common_stun` | 控制 | Targeted | 8s | `DamageEffect(6) + StunEffect(1.5s)` —— 写 IControllable.Stun 计数 |
| `common_silence` | 控制 | Targeted | 12s | `SilenceEffect(3s)` —— 写 IControllable.Silence 计数 |
| `common_reflect_shield` | 防御 | None | 20s | `DamageReflectEffect(50%, 6s)` 订阅 IDamageable.Damaged 反向 TryDamage |
| `common_channel_drain` | 引导 | Targeted | 10s | 3s 引导，每 0.5s 触发 `LifeDrainEffect` —— 利用 SkillExecutor.Channeling 阶段 |
### 设计模式
- **Buff 调度替代 Coroutine**：滞空 / DoT / HoT / Whirlwind / Shield / Meteor / Slow / Haste
  全部走 `BuffInstance.OnTick + OnExpire`，不引入额外 MonoBehaviour 调度器，纯 Dao 层逻辑。
- **AoeEffect / CleaveEffect 子效果组合**：Shockwave、FrostNova、Cleave 通过 `SubEffects`
  串接基础效果（`DamageEffect + KnockbackEffect + SlowEffect`），范围 + 锥形检测复用同一套
  Physics2D 查询 + EntityHandle 解析逻辑。
- **DamageReduction 复用**：Dash 的无敌帧、Shield 的减伤、巨大化的护甲都走
  `DamageableComponent.DamageReduction`（值类型 0..1），无需新 capability。
- **SpeedMultiplier 复用**：Slow / Haste / 群体减速 都写 `IMovable.SpeedMultiplier`
  （新加在 `MovableComponent` 和 `Rigidbody2DMoverComponent` 上，缺省 1），
  与 Sprinting / SprintMultiplier 串联相乘，不互相覆盖。
- **物理一致性**：Teleport 同步 `WorldPosition` + `transform.position` + `Rigidbody2D.velocity=0`，
  避免横版下 ParallaxLayer / 摄像机帧间错位。
### 已知约束
- `common_fireball` 当前只单点伤害，**不自动叠 Burn**：`SkillProjectile` 没有命中事件回调钩子。
  如需"火球+灼烧"组合，可在业务层扩展：让 `SkillProjectile` 在命中时拿到 `DotEffect` 实例并 Apply
  到一个临时 SkillEffectContext。占位 TODO 留在 `BuildFireball` 方法注释里。
- `SKILL_SUMMON` 没有默认 ConfigId，`EnsureRegistered` 不会注册。需要业务先调
  `BuildSummon("your_entity_config")` 然后自己 RegisterSkill。
## 引导施法（Channeling）
`SkillDefinition.ChannelTime > 0` 时，`SkillExecutor` 在 Execute 阶段执行一次 Effects 后进入
`Phase.Channeling`，每 `ChannelTickInterval` 秒重新走一次同一条 Effects 链，直到时长耗尽。
`Channeling` 中 `Interrupt()` 同样能即时打断（典型触发：受击 / 玩家移动取消）。
应用场景：暗影抽取、引导射线、持续治疗大法术、敌人蓄力 BOSS 技能。
**注意**：因为 Effects 被反复 Apply，要小心带"挂一次性 Buff"的 effect —— 反复 Apply 会反复叠加 Buff。
推荐组合：`LifeDrainEffect` / `DamageEffect` / `HealEffect` / `ProjectileEffect`（每 tick 射一发）。
## 控制状态（Stun / Silence）
新增 `IControllable` capability（位于 `EntityManager/Capabilities/Effect/`），默认实现
`ControllableComponent` 通过引用计数（Push/Pop）管理叠加。
- **Stunned**: `MovableComponent.Move()` 与 `Rigidbody2DMoverComponent.Move()` 命中即清横向速度 + 直接 return。
- **Silenced**: `SkillService.CastSkill` 命中即拒绝施法（Stunned 也禁止主动技能）。
- 实体需挂载 `ControllableComponent` 才能受 Stun/Silence 影响 —— 老 Entity 没挂的直接跳过（向后兼容）。
## 连招系统（ComboTracker）
`Runtime/ComboTracker.cs` 维护 `[entityId → 最近 16 个 skillId + 时间戳]` 的滚动缓冲。
注册 `ComboDefinition(Sequence, WindowSeconds, FinisherSkillId, ComboCooldown)` 后，每次
`SkillService.CastSkill` 成功就调一次 `OnSkillCast`；尾段匹配且在时间窗内 → 立即 Cast finisher
并清空该实体缓冲。
注意：Finisher 本身的冷却仍走 SkillInstance.IsReady；ComboCooldown 是连招额外的"防抖" CD。
## 扩展点
- **EntityManager 解耦状态（2026-06-06）**: SkillManager 内部上下文使用 `entityId`，所有 Entity 交互集中到 `SkillEntityProxy` 并通过 EventProcessor 调用 EntityManager 事件。不要在本模块新增 `EntityManager.Dao / Capabilities / Runtime` using。
- **SkillTree**: 技能树 / 前置条件 / Tier 解锁
- **MP/资源系统**: 接入 INeeds 或独立 ManaComponent，CastSkill 时扣资源
- **命中事件**: `SkillProjectile.OnHit` 钩子，让飞行系技能能链式触发 DoT / Knockback / Buff
- **打断条件**: 受击 / 移动自动 Interrupt Channeling —— 当前由调用方手动调 `Interrupt()`
- **AttackPower Buff（狂暴）**: 需 `AttackerComponent` 加公共 setter / Multiplier 字段
