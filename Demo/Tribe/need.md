# Tribe Demo 素材需求清单

> 本文档记录 Tribe Demo 场景生成测试期间被 `PlaceholderFeature`（色块 + 中文标签）替代的
> 所有素材，以及成品到位后应放到的目录。
>
> 当前阶段：#1.M1 框架已落地（`Demo/Tribe/World/`），7 个 biome 横向带状世界已可在场景中
> 跑通；缺素材的对象用纯色块 + 标签代替。下一步按本清单逐项替换。

## 文件命名约定

- **路径根**：`Resources/Tribe/<分类>/<sprite名>.png`
- **PPU**：64（与现有 `Tribe/Objects/*.png` 一致）
- **可破坏物**：3 帧 sprite 序列（默认 / 受击 / 损坏），脚本读取第 3 帧作展示
- **动画对象**：N 帧序列（参考 `Tribe/Entity/*_idle (16x16).png` 命名格式）
- **PSD 不进 Resources**：先导出 PNG 切片再放入

---

## 优先级

- 🔴 **P0** — 阻塞测试可玩性（玩家明显感知缺失）
- 🟡 **P1** — 影响美术一致性，但占位可凑合
- 🟢 **P2** — 锦上添花

---

## 1. 水体类（meadow / swamp / snow）

放到：`Resources/Tribe/Sprites/Water/`

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🟡 P1 | `puddle.png` | 1-2 格浅水洼（meadow） | meadow X+15 | 蓝色 2×0.4 色块 |
| 🟡 P1 | `pond.png` | 3-5 格池塘 | (M2 阶段引入) | — |
| 🔴 P0 | `lake.png` | 8-15 格大型湖泊（swamp 主特征） | swamp X+30 | 蓝色 12×0.6 色块 |
| 🟢 P2 | `lake_island.png` | 湖中小岛（可选） | swamp 内 | — |
| 🟡 P1 | `ice_lake.png` | 冻结湖面（snow） | snow X+60 | 浅蓝 8×0.4 色块 |
| 🟡 P1 | `mud_pit.png` | 泥潭（swamp） | swamp X+75 | 棕色 2×0.3 色块 |

## 2. 岩石类（rocky）

放到：`Resources/Tribe/Sprites/Rocks/`

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🔴 P0 | `stone_cluster_small.png` | 3 块石堆（小） | rocky X+10 | 灰 2×1.5 色块 |
| 🔴 P0 | `stone_cluster_large.png` | 5-7 块石堆（大） | rocky X+30 | 灰 3×2 色块 |
| 🟡 P1 | `ore_iron.png` | 铁矿脉 | rocky X+50 | 棕黄 1.5×1.5 色块 |
| 🟡 P1 | `ore_coal.png` | 煤矿脉 | rocky X+95 | 黑色 1.5×1.5 色块 |
| 🟡 P1 | `cliff_face.png` | 断崖（4×4 格） | rocky X+80 | 灰 4×4 色块 |
| 🟢 P2 | `ice_pillar.png` | 冰柱（snow） | snow X+80,95 | 浅蓝 0.5×3 色块 |

> 已有 `Tribe/Objects/stone.png` 可作为单块石头复用（不属本清单）。

## 3. 聚落 / 建筑（town）

放到：`Resources/Tribe/Sprites/Buildings/`

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🔴 P0 | `house_small_a.png` | 农舍 A（4×4 格） | town X+10 | 棕色 4×4 色块 |
| 🔴 P0 | `house_small_b.png` | 农舍 B（变体） | town X+28 | 棕色 4×4 色块 |
| 🔴 P0 | `house_small_c.png` | 农舍 C（变体） | town X+50 | 棕色 4×4 色块 |
| 🔴 P0 | `well.png` | 水井 | town X+18 | 蓝灰 1.5×2 色块 |
| 🟢 P2 | `tent_small.png` | 营地帐篷 | (M4 阶段) | — |
| 🟡 P1 | `ruin_pillar_tall.png` | 高残柱（5 格） | ruins X+15 | 紫 1×5 色块 |
| 🟡 P1 | `ruin_pillar_short.png` | 矮残柱（4 格） | ruins X+40 | 紫 1×4 色块 |

> 营火已有素材：`Tribe/Objects/campfire.png`（town X+38 应改用，当前用色块占位中文"营火"）。

## 4. 传送门（meadow / ruins）

放到：`Resources/Tribe/Sprites/Portals/`

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🔴 P0 | `portal_blue.png` | 安全采集点入口 | meadow X+40 | 紫 1.5×3 色块 |
| 🟡 P1 | `portal_purple.png` | 副本入口 | ruins X+100 | 紫 1.5×3 色块 |
| 🟢 P2 | `portal_glow_particle.png` | 粒子光晕 | 二者顶部 | — |

## 5. 植被装饰（forest / snow）

放到：`Resources/Tribe/Sprites/Decor/`

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🔴 P0 | `tree_oak_a.png` | 高树 A（5 格） | forest X+10,35,80 | 深绿 2×5 色块 |
| 🟡 P1 | `tree_oak_b.png` | 高树 B（变体） | forest 间隔 | — |
| 🔴 P0 | `tree_pine.png` | 雪松（5 格） | snow X+15,40 | 暗绿 2×5 色块 |
| 🟢 P2 | `bush_normal.png` | 灌木 | forest / meadow | — |
| 🟢 P2 | `reed_swamp.png` | 沼泽芦苇 | swamp X+25,50 | 黄绿 0.6×2 色块 |
| 🟡 P1 | `bones_pile.png` | 骨堆 | ruins X+60 | 米色 1.5×1 色块 |
| 🟢 P2 | `firefly_glow.png` | 萤火虫粒子（夜间） | (M4 #3 SafeGrove) | — |

## 6. 互动点（chest / npc 占位）

放到：`Resources/Tribe/Sprites/Interactives/`

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🔴 P0 | `chest_wood.png` | 木宝箱 | ruins X+80 | 棕黄 1.2×1.2 色块 |
| 🟡 P1 | `chest_iron.png` | 铁宝箱（升级） | (M5 阶段) | — |
| 🟢 P2 | `sign_wooden.png` | 路牌 | biome 边界 | — |

## 7. NPC 视觉（town）

放到：`Resources/Tribe/Entity/`（与现有动物动画素材同根，命名遵循 `_idle (16x16).png` 格式）

| 优先级 | 文件名 | 用途 | biome 出现位置 | 当前占位 |
|---|---|---|---|---|
| 🔴 P0 | `Alice 01_idle (16x16).png` | 商人艾丽丝 idle 序列帧 | town X+22 | 黄色 0.8×1.6 色块 |
| 🟡 P1 | `Alice 01_walk (16x16).png` | 商人 walk（巡店动画） | town | — |
| 🟡 P1 | `Guard 01_idle (16x16).png` | 守卫 idle | town X+60 | 黄色 0.8×1.6 色块 |
| 🟢 P2 | `Quester 01_idle (16x16).png` | 任务 NPC | (M3 阶段) | — |

## 8. 环境音（biome ambient）

放到：`Resources/Sound/`（已是 ResourceManager hint 子目录）

| 优先级 | 文件名 | 用途 | 长度 |
|---|---|---|---|
| 🟡 P1 | `bird_meadow_loop.wav` | 草地鸟鸣环境音 | 30s loop |
| 🟡 P1 | `forest_ambient_loop.wav` | 森林环境音 | 30s loop |
| 🟡 P1 | `town_chatter_loop.wav` | 小镇人群嘈杂 | 30s loop |
| 🟡 P1 | `swamp_water_loop.wav` | 沼泽水声 + 蛙鸣 | 30s loop |
| 🟡 P1 | `wind_rocky_loop.wav` | 荒原风声 | 30s loop |
| 🟡 P1 | `wind_snow_loop.wav` | 雪原风暴 | 30s loop |
| 🟡 P1 | `ruins_eerie_loop.wav` | 遗迹诡异嗡鸣 | 30s loop |

## 9. 背景图（每 biome 一组视差图）

放到：`Resources/Tribe/Background/<biomeId>/`（按 0~4 命名：0=最远 / 4=踩脚层）

> 当前所有 biome 共用 `Tribe/Background/*.png` 一套图。M5 阶段每 biome 拆分独立背景。

| 优先级 | 路径 | 5 张文件 | biome |
|---|---|---|---|
| 🟢 P2 | `Tribe/Background/meadow/` | 0~4.png | meadow |
| 🟢 P2 | `Tribe/Background/forest/` | 0~4.png | forest |
| 🟢 P2 | `Tribe/Background/town/` | 0~4.png | town |
| 🟢 P2 | `Tribe/Background/swamp/` | 0~4.png | swamp |
| 🟢 P2 | `Tribe/Background/rocky/` | 0~4.png | rocky |
| 🟢 P2 | `Tribe/Background/snow/` | 0~4.png | snow |
| 🟢 P2 | `Tribe/Background/ruins/` | 0~4.png | ruins |

## 10. 物品图标（已分类，但部分缺货）

放到：`Resources/Tribe/Items/<分类>/`（已有 9 个分类目录）

| 优先级 | 路径 | 缺什么 | 用途 |
|---|---|---|---|
| 🟡 P1 | `Tribe/Items/Currency/gold_coin.png` | 金币图标 | #4 ShopManager 货币 |
| 🟡 P1 | `Tribe/Items/Currency/silver_coin.png` | 银币图标 | 多货币体系 |
| 🟢 P2 | `Tribe/Items/Magic/blueprint_scroll.png` | 蓝图卷轴通用图 | #5 蓝图物品基础图 |
| 🟢 P2 | `Tribe/Items/Materials/wood_log.png` | 木材 | 制作系统 |
| 🟢 P2 | `Tribe/Items/Materials/stone_chunk.png` | 石块 | 制作系统 |
| 🟢 P2 | `Tribe/Items/Materials/iron_ingot.png` | 铁锭 | 制作系统 |
| 🟢 P2 | `Tribe/Items/Materials/coal_lump.png` | 煤块 | 制作系统 |

> 已有 220 张物品图按 9 类分好，本表只列**新增**类别。

---

## 提交流程建议

1. 美术导出 PNG 后放对应目录
2. Unity 自动生成 .meta（首次导入需在编辑器走一遍 Refresh）
3. 在对应 `*Feature.cs`（PuddleFeature / StoneClusterFeature / ...）替换 PlaceholderFeature
4. 提交时 commit 信息体例：`feat(Tribe): 添加 <分类> 素材（<biome> 用）`

## 当前状态总览

- 测试场景已生成：7 段 biome 串联（X=0~760，约 12 屏宽）
- 占位元素：约 **40 个** PlaceholderFeature 色块 + 标签
- 实素材已用：胡萝卜 / 向日葵 / 红蘑菇 / 浆果 / 现有 9 种 TribeCreature 预设
- 阻塞 P0 项：约 **15 个**（水体 3 + 岩石 2 + 建筑 4 + 传送门 1 + 植被 2 + 宝箱 1 + Alice 1 + 守卫 1）

填齐 P0 项后，Tribe Demo 视觉占位就能换成正式素材；P1/P2 后续按里程碑迭代。
