# ESS 技能库目录

- 技能总数：107
- Tribe Demo 中按 `U` 打开技能测试面板，可滚动浏览、拖拽到槽位覆盖绑定，绑定后通过 `J/K/L/;` 释放。
- 面板显示：中文名、图标、魔力消耗、冷却时间、技能描述。

## 分类统计

- 战士/武技 (Martial)：16
- 防御 (Defense)：8
- 刺客 (Assassin)：7
- 位移 (Mobility)：1
- 元素法术 (Elemental)：23
- 圣光 (Light)：2
- 暗影 (Dark)：4
- 辅助治疗 (Support)：8
- 召唤 (Summon)：5
- 枪械 (Gunner)：9
- 工程 (Engineer)：2
- 陷阱 (Trap)：9
- 爆破 (Bomb)：2
- 部署物 (Deployable)：5
- 弹幕火力 (Barrage)：4
- 控制 (Control)：2

## 战士/武技

- `common_dash` | 冲刺 | 魔力 0 | 冷却 4秒 | effects: Cue, Dash | 发动冲刺向前突进，兼具位移和进攻节奏。
- `common_jump_slash` | 跳斩 | 魔力 10 | 冷却 8秒 | effects: Cue, JumpSlash | 发动跳斩跃向目标区域，落地时造成冲击伤害。
- `common_cleave` | 横扫斩 | 魔力 4 | 冷却 3.5秒 | effects: Cue, Cleave | 挥出横扫斩打击前方范围敌人，适合近战清场。
- `common_shockwave` | 震荡波 | 魔力 12 | 冷却 9秒 | effects: Cue, Aoe | 释放震荡波影响周围区域，对范围内目标造成效果。
- `common_whirlwind` | 旋风斩 | 魔力 14 | 冷却 12秒 | effects: Cue, Whirlwind | 发动旋风斩连续旋转攻击，扫击周围敌人。
- `martial_berserker_roar` | 狂战怒吼 | 魔力 12 | 冷却 11秒 | effects: Cue, Aoe | 释放狂战怒吼影响周围区域，对范围内目标造成效果。
- `martial_counter_spin` | 反击旋舞 | 魔力 13 | 冷却 13秒 | effects: Cue, DamageReflect, Whirlwind | 发动反击旋舞连续旋转攻击，扫击周围敌人。
- `martial_earth_splitter` | 裂地斩 | 魔力 13 | 冷却 9秒 | effects: Cue, Cleave | 挥出裂地斩打击前方范围敌人，适合近战清场。
- `martial_execute_arc` | 处决弧光 | 魔力 15 | 冷却 12秒 | effects: Cue, Cleave | 挥出处决弧光打击前方范围敌人，适合近战清场。
- `martial_guard_break` | 破防斩 | 魔力 8 | 冷却 7秒 | effects: Cue, Cleave | 挥出破防斩打击前方范围敌人，适合近战清场。
- `martial_leap_crash` | 跃击震地 | 魔力 12 | 冷却 9秒 | effects: Cue, JumpSlash | 发动跃击震地跃向目标区域，落地时造成冲击伤害。
- `martial_lunging_thrust` | 突刺冲锋 | 魔力 8 | 冷却 6.5秒 | effects: Cue, Dash, Cleave | 发动突刺冲锋向前突进，兼具位移和进攻节奏。
- `martial_power_slash` | 蓄力斩 | 魔力 4 | 冷却 3.2秒 | effects: Cue, Cleave | 挥出蓄力斩打击前方范围敌人，适合近战清场。
- `martial_rising_cut` | 升龙斩 | 魔力 7 | 冷却 5.2秒 | effects: Cue, Cleave | 挥出升龙斩打击前方范围敌人，适合近战清场。
- `martial_shoulder_charge` | 肩撞冲锋 | 魔力 9 | 冷却 7.5秒 | effects: Cue, Dash, Cleave | 发动肩撞冲锋向前突进，兼具位移和进攻节奏。
- `martial_whirlwind_burst` | 爆裂旋风 | 魔力 14 | 冷却 10秒 | effects: Cue, Whirlwind | 发动爆裂旋风连续旋转攻击，扫击周围敌人。

## 防御

- `common_shield` | 能量护盾 | 魔力 10 | 冷却 15秒 | effects: Cue, Shield | 启动能量护盾，获得短时间防护能力。
- `common_reflect_guard` | 反射护卫 | 魔力 14 | 冷却 18秒 | effects: Cue, DamageReflect | 启动反射护卫，将部分受到的伤害反弹给攻击者。
- `defense_bulwark_aura` | 壁垒光环 | 魔力 14 | 冷却 18秒 | effects: Cue, Shield, Aoe | 启动壁垒光环，获得短时间防护能力。
- `defense_iron_wall` | 铁壁姿态 | 魔力 10 | 冷却 16秒 | effects: Cue, Shield | 启动铁壁姿态，获得短时间防护能力。
- `defense_rallying_cry` | 振奋战吼 | 魔力 11 | 冷却 18秒 | effects: Cue, Hot, Shield | 启动振奋战吼，获得短时间防护能力。
- `defense_sanctuary_guard` | 圣域守护 | 魔力 15 | 冷却 20秒 | effects: Cue, Zone | 展开圣域守护区域，持续影响范围内的敌人或友方。
- `defense_shield_bash` | 盾牌猛击 | 魔力 9 | 冷却 8.5秒 | effects: Cue, Cleave | 挥出盾牌猛击打击前方范围敌人，适合近战清场。
- `defense_thorns_guard` | 荆棘守卫 | 魔力 13 | 冷却 18秒 | effects: Cue, DamageReflect | 启动荆棘守卫，将部分受到的伤害反弹给攻击者。

## 刺客

- `assassin_bleeding_cross` | 血十字 | 魔力 9 | 冷却 6.5秒 | effects: Cue, Cleave | 挥出血十字打击前方范围敌人，适合近战清场。
- `assassin_crippling_dash` | 断筋突袭 | 魔力 10 | 冷却 8秒 | effects: Cue, Dash, Cleave | 发动断筋突袭向前突进，兼具位移和进攻节奏。
- `assassin_fan_of_knives` | 飞刀扇 | 魔力 12 | 冷却 7秒 | effects: Cue, MultiShot | 释放飞刀扇，以多段弹幕覆盖前方区域。
- `assassin_poison_blade` | 淬毒刃 | 魔力 8 | 冷却 5.5秒 | effects: Cue, Cleave | 挥出淬毒刃打击前方范围敌人，适合近战清场。
- `assassin_shadow_step` | 暗影步 | 魔力 11 | 冷却 7秒 | effects: Cue, Teleport, Aoe | 施展暗影步迅速调整站位，并在落点制造战斗收益。
- `assassin_smoke_bomb` | 烟雾弹 | 魔力 13 | 冷却 14秒 | effects: Cue, Zone | 展开烟雾弹区域，持续影响范围内的敌人或友方。
- `assassin_vampiric_cut` | 嗜血 | 魔力 10 | 冷却 8秒 | effects: Cue, LifeStealBuff | 进入嗜血状态，持续 10 秒。期间造成的物理伤害会按 10% 转化为生命回复，每次至少回复 1 点。

## 位移

- `common_blink` | 闪现 | 魔力 8 | 冷却 7秒 | effects: Cue, Teleport | 施展闪现迅速调整站位，并在落点制造战斗收益。

## 元素法术

- `common_fireball` | 火球术 | 魔力 12 | 冷却 6秒 | effects: Projectile | 发射火球命中目标并引发小范围爆炸，适合清理密集敌人。
- `common_ice_shard` | 冰霜碎片 | 魔力 10 | 冷却 5秒 | effects: Projectile | 射出冰霜碎片，命中后造成寒冰伤害并短暂冻结目标。
- `common_thunder_spear` | 雷霆长矛 | 魔力 16 | 冷却 8秒 | effects: Projectile | 投射雷霆长矛，命中后在附近敌人之间跳跃闪电。
- `common_arcane_bomb` | 奥术飞弹 | 魔力 20 | 冷却 10秒 | effects: HomingMultiProjectile | 从身旁唤出多枚奥术飞弹，圆滑转向并追踪范围内敌人。
- `common_frost_nova` | 冰霜新星 | 魔力 14 | 冷却 10秒 | effects: Cue, Aoe | 释放冰霜新星影响周围区域，对范围内目标造成效果。
- `common_meteor` | 陨石术 | 魔力 18 | 冷却 14秒 | effects: Cue, Meteor | 呼叫陨石术轰击目标区域，造成高威力范围打击。
- `common_burning_dart` | 燃烧飞镖 | 魔力 9 | 冷却 6秒 | effects: Projectile | 发射燃烧飞镖命中目标，造成对应属性伤害。
- `elemental_arcane_shards` | 奥术碎晶 | 魔力 14 | 冷却 7.5秒 | effects: Cue, MultiShot | 释放奥术碎晶，以多段弹幕覆盖前方区域。
- `elemental_blizzard` | 暴风雪 | 魔力 22 | 冷却 18秒 | effects: Cue, Zone | 展开暴风雪区域，持续影响范围内的敌人或友方。
- `elemental_chain_spark` | 连锁电火花 | 魔力 16 | 冷却 10秒 | effects: Cue, Projectile | 发射连锁电火花命中目标，造成对应属性伤害。
- `elemental_earth_spikes` | 大地尖刺 | 魔力 16 | 冷却 12秒 | effects: Cue, Cleave | 挥出大地尖刺打击前方范围敌人，适合近战清场。
- `elemental_fire_lance` | 火焰长枪 | 魔力 10 | 冷却 5.5秒 | effects: Cue, Projectile | 发射火焰长枪命中目标，造成对应属性伤害。
- `elemental_fire_nova` | 烈焰新星 | 魔力 14 | 冷却 9秒 | effects: Cue, Aoe | 释放烈焰新星影响周围区域，对范围内目标造成效果。
- `elemental_frost_field` | 霜冻力场 | 魔力 15 | 冷却 12秒 | effects: Cue, Zone | 展开霜冻力场区域，持续影响范围内的敌人或友方。
- `elemental_ice_lance` | 冰枪术 | 魔力 10 | 冷却 5.5秒 | effects: Cue, Projectile | 发射冰枪术命中目标，造成对应属性伤害。
- `elemental_meteor_rain` | 流星雨 | 魔力 24 | 冷却 20秒 | effects: Cue, Meteor | 呼叫流星雨轰击目标区域，造成高威力范围打击。
- `elemental_orbital_arcana` | 环绕奥能 | 魔力 20 | 冷却 12秒 | effects: Cue, HomingMultiProjectile | 释放多枚环绕奥能，自动寻找附近敌人并追踪命中。
- `elemental_poison_cloud` | 毒云术 | 魔力 15 | 冷却 13秒 | effects: Cue, Zone | 展开毒云术区域，持续影响范围内的敌人或友方。
- `elemental_poison_spit` | 毒液喷吐 | 魔力 9 | 冷却 5.5秒 | effects: Cue, Projectile | 发射毒液喷吐命中目标，造成对应属性伤害。
- `elemental_thunder_bolt` | 雷电箭 | 魔力 11 | 冷却 6秒 | effects: Cue, Projectile | 发射雷电箭命中目标，造成对应属性伤害。
- `elemental_thunder_storm` | 雷暴领域 | 魔力 20 | 冷却 16秒 | effects: Cue, Zone | 展开雷暴领域区域，持续影响范围内的敌人或友方。
- `elemental_tidal_ring` | 潮汐圆环 | 魔力 14 | 冷却 11秒 | effects: Cue, Aoe | 释放潮汐圆环影响周围区域，对范围内目标造成效果。
- `elemental_wind_cutter` | 风刃切割 | 魔力 12 | 冷却 8秒 | effects: Cue, Cleave | 挥出风刃切割打击前方范围敌人，适合近战清场。

## 圣光

- `light_holy_bolt` | 圣光弹 | 魔力 9 | 冷却 5.5秒 | effects: Cue, Projectile | 发射圣光弹命中目标，造成对应属性伤害。
- `light_holy_nova` | 圣光新星 | 魔力 16 | 冷却 13秒 | effects: Cue, Aoe, Heal | 释放圣光新星恢复生命，提升持续作战能力。

## 暗影

- `common_life_drain` | 生命汲取 | 魔力 13 | 冷却 9秒 | effects: Projectile | 发射生命汲取命中目标，造成对应属性伤害。
- `dark_curse_field` | 诅咒力场 | 魔力 18 | 冷却 16秒 | effects: Cue, Zone | 展开诅咒力场区域，持续影响范围内的敌人或友方。
- `dark_void_burst` | 虚空爆裂 | 魔力 17 | 冷却 14秒 | effects: Cue, Aoe | 释放虚空爆裂影响周围区域，对范围内目标造成效果。
- `elemental_shadow_bolt` | 暗影箭 | 魔力 12 | 冷却 7秒 | effects: Cue, Projectile | 发射暗影箭命中目标，造成对应属性伤害。

## 辅助治疗

- `common_first_aid` | 急救术 | 魔力 8 | 冷却 9秒 | effects: Cue, Heal | 释放急救术恢复生命，提升持续作战能力。
- `common_regen` | 再生术 | 魔力 12 | 冷却 16秒 | effects: Cue, Hot | 释放再生术恢复生命，提升持续作战能力。
- `common_cleanse` | 净化术 | 魔力 8 | 冷却 20秒 | effects: Cue, Cleanse | 释放净化术清除负面状态，稳定战斗节奏。
- `support_arcane_barrier` | 奥术屏障 | 魔力 13 | 冷却 18秒 | effects: Cue, Shield | 启动奥术屏障，获得短时间防护能力。
- `support_cleanse_ring` | 净化圆环 | 魔力 12 | 冷却 18秒 | effects: Cue, Cleanse, Aoe | 释放净化圆环清除负面状态，稳定战斗节奏。
- `support_group_heal` | 群体治疗 | 魔力 18 | 冷却 15秒 | effects: Cue, Aoe | 释放群体治疗影响周围区域，对范围内目标造成效果。
- `support_haste` | 急速祝福 | 魔力 10 | 冷却 16秒 | effects: Cue, Slow | 释放急速祝福，提供一项通用 RPG 战斗技能效果。
- `support_regrowth_field` | 复苏领域 | 魔力 18 | 冷却 18秒 | effects: Cue, Zone | 展开复苏领域区域，持续影响范围内的敌人或友方。

## 召唤

- `common_summon_guard` | 召唤卫士 | 魔力 18 | 冷却 16秒 | effects: Cue, SummonEntity | 召唤卫士协助作战，持续压制附近敌人。
- `summon_arcane_circle` | 奥术召唤阵 | 魔力 28 | 冷却 30秒 | effects: Cue, SummonEntity, Aoe | 召唤奥术阵协助作战，持续压制附近敌人。
- `summon_mage_apprentice` | 召唤法师学徒 | 魔力 18 | 冷却 22秒 | effects: Cue, SummonEntity | 召唤法师学徒协助作战，持续压制附近敌人。
- `summon_spirit_burst` | 灵体爆发 | 魔力 24 | 冷却 28秒 | effects: Cue, SummonEntity, Aoe | 召唤灵体爆发协助作战，持续压制附近敌人。
- `summon_warrior_pair` | 召唤双战士 | 魔力 22 | 冷却 26秒 | effects: Cue, SummonEntity | 召唤双战士协助作战，持续压制附近敌人。

## 枪械

- `common_multishot` | 多重射击 | 魔力 12 | 冷却 7秒 | effects: Cue, MultiShot | 释放多重射击，以多段弹幕覆盖前方区域。
- `lib_gunner_cryo_round` | 寒冻弹 | 魔力 9 | 冷却 3.6秒 | effects: Projectile | 发射寒冻弹命中目标，造成对应属性伤害。
- `lib_gunner_explosive_round` | 爆裂弹 | 魔力 11 | 冷却 3.8秒 | effects: Projectile | 发射爆裂弹命中目标，造成对应属性伤害。
- `lib_gunner_grenade_launcher` | 榴弹发射 | 魔力 16 | 冷却 6.5秒 | effects: Projectile | 发射榴弹发射命中目标，造成对应属性伤害。
- `lib_gunner_pistol_burst` | 手枪连射 | 魔力 5 | 冷却 1.1秒 | effects: Cue, MultiShot | 释放手枪连射，以多段弹幕覆盖前方区域。
- `lib_gunner_rocket_shell` | 火箭弹 | 魔力 22 | 冷却 9秒 | effects: Projectile | 发射火箭弹命中目标，造成对应属性伤害。
- `lib_gunner_scatter_blast` | 霰弹爆射 | 魔力 9 | 冷却 3秒 | effects: Cue, MultiShot | 释放霰弹爆射，以多段弹幕覆盖前方区域。
- `lib_gunner_shock_round` | 电击弹 | 魔力 11 | 冷却 5秒 | effects: Projectile | 发射电击弹命中目标，造成对应属性伤害。
- `lib_gunner_sniper_round` | 狙击弹 | 魔力 12 | 冷却 5.5秒 | effects: Cue, Projectile | 发射狙击弹命中目标，造成对应属性伤害。

## 工程

- `lib_engineer_electro_net` | 电磁网 | 魔力 17 | 冷却 8秒 | effects: Aoe, Cue | 释放电磁网影响周围区域，对范围内目标造成效果。
- `lib_engineer_flame_canister` | 燃焰罐 | 魔力 15 | 冷却 5.5秒 | effects: Projectile | 发射燃焰罐命中目标，造成对应属性伤害。

## 陷阱

- `lib_engineer_cluster_mine` | 集束地雷 | 魔力 21 | 冷却 9.5秒 | effects: MultiShot | 布设集束地雷，触发后对范围内敌人造成效果。
- `lib_engineer_frag_mine` | 破片地雷 | 魔力 13 | 冷却 5.8秒 | effects: Cue, Aoe | 布设破片地雷，触发后对范围内敌人造成效果。
- `lib_engineer_frost_mine` | 冰霜地雷 | 魔力 14 | 冷却 6.2秒 | effects: Aoe, Cue | 布设冰霜地雷，触发后对范围内敌人造成效果。
- `lib_engineer_poison_mine` | 毒雾地雷 | 魔力 15 | 冷却 6.8秒 | effects: Aoe, Cue | 布设毒雾地雷，触发后对范围内敌人造成效果。
- `lib_engineer_shock_mine` | 电击地雷 | 魔力 16 | 冷却 7.5秒 | effects: Aoe, Cue | 布设电击地雷，触发后对范围内敌人造成效果。
- `lib_trap_launch_pad` | 弹射陷阱 | 魔力 16 | 冷却 7秒 | effects: Aoe | 布设弹射陷阱，触发后对范围内敌人造成效果。
- `lib_trap_silence_field` | 静默陷阱 | 魔力 17 | 冷却 8秒 | effects: Aoe, Cue | 布设静默陷阱，触发后对范围内敌人造成效果。
- `lib_trap_spike_plate` | 尖刺踏板 | 魔力 10 | 冷却 4.5秒 | effects: Aoe | 布设尖刺踏板，触发后对范围内敌人造成效果。
- `lib_trap_tar_pit` | 焦油陷坑 | 魔力 12 | 冷却 6秒 | effects: Cue, Aoe | 布设焦油陷坑，触发后对范围内敌人造成效果。

## 爆破

- `lib_engineer_demolition_charge` | 爆破装药 | 魔力 25 | 冷却 12秒 | effects: Cue, Aoe | 释放爆破装药影响周围区域，对范围内目标造成效果。
- `lib_engineer_timed_bomb` | 定时炸弹 | 魔力 19 | 冷却 8.5秒 | effects: Cue, Meteor | 呼叫定时炸弹轰击目标区域，造成高威力范围打击。

## 部署物

- `lib_engineer_assault_bot` | 突击机器人 | 魔力 26 | 冷却 16秒 | effects: Cue, SummonEntity | 召唤突击机器人协助作战，持续压制附近敌人。
- `lib_engineer_force_generator` | 力场发生器 | 魔力 22 | 冷却 14秒 | effects: Cue, Shield, DamageReflect | 启动力场发生器，提供护盾并反弹部分伤害。
- `lib_engineer_guard_drone` | 守卫无人机 | 魔力 28 | 冷却 18秒 | effects: Cue, SummonEntity | 召唤守卫无人机协助作战，持续压制附近敌人。
- `lib_engineer_repair_beacon` | 维修信标 | 魔力 18 | 冷却 12秒 | effects: Cue, Hot | 释放维修信标恢复生命，提升持续作战能力。
- `lib_engineer_sentry_turret` | 哨戒炮台 | 魔力 24 | 冷却 14秒 | effects: Cue, SummonEntity | 召唤哨戒炮台协助作战，持续压制附近敌人。

## 弹幕火力

- `lib_barrage_mortar_rain` | 迫击炮雨 | 魔力 20 | 冷却 8.5秒 | effects: Cue, Meteor | 呼叫迫击炮雨轰击目标区域，造成高威力范围打击。
- `lib_barrage_ring_fire` | 环形火力 | 魔力 18 | 冷却 7.5秒 | effects: Cue, MultiShot | 释放环形火力，以多段弹幕覆盖前方区域。
- `lib_barrage_rocket_swarm` | 火箭蜂群 | 魔力 24 | 冷却 10秒 | effects: MultiShot | 释放火箭蜂群，以多段弹幕覆盖前方区域。
- `lib_gunner_bullet_fan` | 扇形弹幕 | 魔力 14 | 冷却 4.8秒 | effects: Cue, MultiShot | 释放扇形弹幕，以多段弹幕覆盖前方区域。

## 控制

- `common_stunning_blow` | 眩晕重击 | 魔力 8 | 冷却 8秒 | effects: Cue, Cleave | 挥出眩晕重击打击前方范围敌人，适合近战清场。
- `common_silence` | 沉默领域 | 魔力 10 | 冷却 12秒 | effects: Cue, Aoe | 释放沉默领域影响周围区域，对范围内目标造成效果。
