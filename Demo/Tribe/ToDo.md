# Tribe Demo 设计 ToDo

> 本文档记录 Tribe 演示场景的设计决策、待办事项与历史变更。
> 每条新设计要点请按时间倒序追加到顶部，遵循下方模板。

## 使用约定

- **当前进行中** → `[~]`
- **已完成** → `[x]`
- **待办** → `[ ]`
- **已废弃 / 改方案** → `[-]`（保留记录但删除线）
- 设计变更时**不要**直接覆盖旧条目；新建条目并在旧条目末尾标注 "→ 见 #N"。
- 跨模块依赖请同步在对应模块的 `Agent.md` 注明。

## 条目模板

```markdown
### N. <简短主题>  <YYYY-MM-DD>

- **状态**: `[~]` / `[x]` / `[ ]` / `[-]`
- **背景**: 为什么要做（1-2 句）
- **方案**: 具体设计（数据结构 / 流程 / 涉及文件 / 接口）
- **影响范围**: 哪些文件 / 模块 / 资源被修改
- **后续**: 待办子项 / 风险点 / 验证方式
```

---

## 设计记录

<!-- 新条目追加在此处，最新在最上 -->

### 5. 装备制作 + 蓝图系统（CraftingManager EssManager） v1 草案  2026-05-17

- **状态**: `[ ]` 草案已确认，待逐里程碑实施
- **背景**: 项目零相关代码，新建框架级 `CraftingManager`。
  支持配方驱动的装备制作 + 蓝图解锁机制 + 工作台门槛 + 品质系统。
  与 InventoryManager / IStats(#2) / ShopManager(#4) / World Features(#1) 多向联动。

- **方案**:

  **(1) 总体架构**：
  ```
  CraftingManager
   ├ RecipeRegistry      (所有配方目录)
   ├ KnownRecipes        (玩家已学会的配方表，持久化)
   ├ WorkstationRegistry (工作台类型与可制作配方关联)
   ├ CraftingSession     (运行中的制作任务)
   └ CraftQuality        (品质判定 + Modifier 写入装备实例)
        │
   ┌────┴────┬───────────┬──────────────┐
   InvMgr   IStats(#2)   #4 Shop       #1 World
   消耗/产出  CraftSkill   蓝图售卖       WorkstationFeature
              CHA/INT/STR
  ```
  **职责单一**: CraftingManager 管"做什么/怎么做/谁会做"；地图工作台是 #1 Feature；
  蓝图是特殊 Item；品质走 Modifier 体系（与 #2 同型）。

  **(2) Recipe 配方 Dao**：
  ```csharp
  CraftingRecipe {
    Id, DisplayName, Description,
    CraftIngredient[] Ingredients,       // 输入材料
    CraftOutput[] Outputs,                // 输出（通常 1 个，带 Chance）
    CraftIngredient[] CatalystKeep,       // 不消耗的"催化剂"（如锤子）
    string WorkstationId,                 // "" = 手搓
    int Tier (1~5),
    float CraftSeconds,
    RecipeSkillRequirement Skill,         // CraftSkillMin / IntMin / StrMin
    bool LearnedByDefault,                // false = 需蓝图解锁
    string BlueprintItemId,
    string CategoryId                     // "weapon"/"armor"/"consumable"/"tool"...
  }
  CraftIngredient { ItemId, Count }
  CraftOutput     { ItemId, Count, Chance=1f }
  ```

  **(3) Blueprint 蓝图机制**：

  蓝图 = 特殊 InventoryItem（`InventoryItemType.Blueprint` 新枚举值）。
  ```
  约定：Item.Id = "blueprint_X" → 解锁 RecipeId = "recipe_X"

  LearnBlueprint(playerId, blueprintItemId):
    1. 校验持有蓝图
    2. 查 RecipeRegistry 找 RecipeId
    3. 校验未学过
    4. 加入 KnownRecipes
    5. 消耗蓝图（一次性物品）
    6. 广播 EVT_RECIPE_LEARNED
  ```
  蓝图来源：商店购买（接 #4） / 怪物掉落 / 任务奖励 / 宝箱。
  设计选项："消耗永久解锁"（默认）vs"常驻物品"（rare 蓝图扩展）。

  **(4) Workstation 工作台**：
  ```csharp
  WorkstationDefinition {
    Id,                  // "workbench_basic"/"anvil_iron"/"furnace"/"alchemy_table"
    DisplayName,
    Tier,                // 决定可制作配方 Tier 上限
    string[] Categories  // 仅支持哪些 Recipe.CategoryId
  }
  ```
  地图上的物理工作台 = `WorkstationFeature : TribeFeatureSpec`（接 #1）。
  玩家走到 → 按 E → 打开 CraftingUI 锁定该 WorkstationId。

  **(5) CraftQuality 品质系统**：

  ```csharp
  enum CraftQuality { Crude, Common, Fine, Superior, Masterwork, Legendary }

  // 判定公式：
  roll = baseSuccess + (CraftSkill - RecipeTier*5) * 0.02 + INT * 0.005
  // 落入 [0, 0.4, 0.7, 0.85, 0.95, 0.99] 分布对应六档

  // 品质对装备的 Modifier：
  Crude     → -10% 装备属性
  Common    →  无
  Fine      → +10%
  Superior  → +25%
  Masterwork→ +50% + 1 词缀
  Legendary → +100% + 2 词缀
  ```

  实现需 `InventoryItem` 加 `RuntimeModifiers` 字段（与 #2 IStats Modifier 同型），
  装备穿戴时 Modifier 注入到 IStats。

  **(6) 制作流程 (CraftingSession)**：
  ```
  StartCraft(playerId, recipeId, workstationId, quantity):
    1. 校验 KnownRecipes 包含 recipeId
    2. 校验 workstationId 匹配
    3. 校验 IStats 技能/属性门槛
    4. 校验背包材料足够
    5. 校验背包能容纳输出（双层：槽位+重量，接 #2）
    6. 预扣材料（标记保留）
    7. 创建 CraftingSession（playerId + endTime）
    8. UI 进度条
    9. 时间到 CompleteCraft：
       a. 真正消耗材料
       b. 计算品质 roll
       c. new InventoryItem with Modifiers
       d. 推入背包
       e. 增 CraftSkill 经验
       f. 广播 EVT_CRAFT_COMPLETED
    10. 取消 → 退还材料 + 销毁 session

  取消条件：移出工作台范围 / 主动取消 / Instance 切换
  退还困境：背包满 → 材料丢地面（用 PickableDropEntity）
  ```

  **(7) CraftSkill 制作技能**（独立 Capability）：
  ```csharp
  CraftSkillSet {
    Smithing,    // 冶炼/铁器
    Carpentry,   // 木工
    Tailoring,   // 缝纫
    Alchemy,     // 炼金
    Cooking      // 烹饪
  }
  // 每 Recipe.Category 对应一个 Skill 轴
  // 制作成功 +10~50 xp（按 Tier）
  // 等级公式：xp_to_next = 100 * level^1.5
  ```
  归属：独立 `ICraftSkill` Capability（避免 #2 IStats 膨胀），但走相同 Modifier 体系。

  **(8) 事件 API**：
  ```
  EVT_REGISTER_RECIPE = "RegisterRecipe"            [CraftingRecipe]
  EVT_REGISTER_WORKSTATION = "RegisterWorkstation"  [WorkstationDefinition]

  EVT_LEARN_BLUEPRINT = "LearnBlueprint"            [playerId, blueprintItemId]
  EVT_RECIPE_LEARNED = "RecipeLearned"              (广播)

  EVT_START_CRAFT = "StartCraft"                    [playerId, recipeId, ws, count]
  EVT_CANCEL_CRAFT = "CancelCraft"                  [playerId, sessionId]
  EVT_CRAFT_PROGRESS = "CraftProgress"              (广播 [sessionId, ratio])
  EVT_CRAFT_COMPLETED = "CraftCompleted"            (广播)
  EVT_CRAFT_FAILED = "CraftFailed"                  (广播)

  EVT_OPEN_CRAFTING_UI = "OpenCraftingUI"           [playerId, workstationId]
  EVT_CLOSE_CRAFTING_UI = "CloseCraftingUI"         [playerId]
  EVT_QUERY_KNOWN_RECIPES = "QueryKnownRecipes"     [playerId] → string[]
  ```

  **(9) UI 设计**：
  - **CraftingUI**: 4 个 Tab（武器/护甲/消耗/工具）+ 配方列表（锁定带🔒，未学带 ?）
    \+ 详情面板（材料检查、技能门槛、时长）+ 数量按钮（×1/×5/全部）
  - **KnownRecipesUI**: 配方书页面，按类别分组 + 进度统计
  - 走 UIManager.Specs 风格统一

- **影响范围**:
  - 新增框架：`MultiManagers/CraftingManager/`（依赖 InventoryManager/IStats，归 MultiManagers）
    - `CraftingManager.cs / CraftingService.cs`
    - `Dao/CraftingRecipe / CraftIngredient / CraftOutput / RecipeSkillRequirement`
    - `Dao/WorkstationDefinition / CraftQuality / CraftingSession / KnownRecipesData`
    - `UI/CraftingUIBuilder.cs / KnownRecipesUIBuilder.cs`
    - `Agent.md`
  - 修改：`InventoryManager/Dao/Item.cs`
    - `InventoryItemType` 增 `Blueprint` 值
    - 加 `RuntimeModifiers` 字段（与 #2 IStats Modifier 同型）
  - 新增框架：`EntityManager/Capabilities/CraftSkill/`
    - `ICraftSkill / CraftSkillSet / CraftSkillComponent`
  - 新增 Tribe：`Demo/Tribe/Crafting/`
    - `TribeRecipeRegistry.cs`
    - `Presets/{BasicWeaponRecipes, BasicArmorRecipes, ConsumableRecipes, ToolRecipes}.cs`
  - 新增 Tribe：`Demo/Tribe/Workstations/TribeWorkstationRegistry.cs`
  - 新增 Tribe Feature：`Demo/Tribe/World/Features/WorkstationFeature.cs`（在 #1 框架下）
  - Manager 优先级：`CraftingManager = 14`
  - Agent.md 全局清单同步

- **里程碑**:
  - **M1 CraftingManager 骨架** `[ ]` — Manager + Service + RecipeRegistry +
    WorkstationRegistry + 全部 Dao；事件 API 定义；空注册无业务
  - **M2 制作流程 + UI** `[ ]` — CraftingSession + StartCraft/Cancel/Complete +
    CraftingUIBuilder + 默认全配方解锁（暂不做蓝图）；木剑/木盾端到端
  - **M3 蓝图系统** `[ ]` — `Blueprint` ItemType + LearnBlueprint 流程 +
    KnownRecipes 持久化 + UI 锁定配方显示策略
  - **M4 工作台 + Tribe 集成** `[ ]` — `WorkstationFeature` + 4 个 Tribe Workstation
    (basic/anvil/furnace/alchemy) + Tribe RecipeRegistry 填充
  - **M5 品质 + 技能** `[ ]` — CraftQuality 判定 + Modifier 写入 +
    `ICraftSkill` Capability + 经验/等级；与 #2 IStats 装备 modifier 协同

- **风险 / 开放问题**:
  - **依赖链**：
    - CraftingManager: InventoryManager (已有) + #2 IStats (软依赖) + #4 Shop (蓝图售卖)
    - 业务粘合: #1 World/Features WorkstationFeature
  - **Item.RuntimeModifiers**：现 InventoryItem 是模板/实例共用类，加运行时 Modifier
    需与 #2.M1 同步设计；本设计标记此前置扩展
  - **蓝图持久化**：玩家已学 RecipeId 列表存盘，配方改材料时按 Id 兼容；
    删除旧配方 → KnownRecipes 自动清理
  - **多玩家并发**：CraftingSession 按 playerId 隔离，单工作台不互斥（多人共用）
  - **退还材料**：取消时背包满 → 材料丢地面（复用 PickableDropEntity）
  - **配方树**：铁锭→铁剑 中间产物链，M2 不做前置缺失提示，M5 可加
  - **CraftSkill 归属**：独立 Capability vs IStats 派生轴 → 选独立（避免 IStats 膨胀）
  - **资源占位**：配方图标 = Output Item 的图标；工作台 Sprite 用色块占位

- **后续 / 验证**:
  - M1：注册 1 配方 + 1 工作台，事件 API 调通，无业务行为
  - M2：CraftingUI 打开看到木剑/木盾配方，材料够即可制作，进度条→产出
  - M3：蓝图物品出现在背包，使用后配方解锁，再次使用提示已学过
  - M4：town biome 出现工作台 sprite，按 E 打开 CraftingUI 自动选中类别；
    多个工作台 Tier 限制生效
  - M5：CraftSkill=1 制作铁剑全 Crude；CraftSkill=15 出 Superior+；装备穿戴后
    IStats 派生属性正确叠加 Modifier

---

### 4. NPC 系统 + 商店交易系统（两个新 EssManager） v1 草案  2026-05-17

- **状态**: `[ ]` 草案已确认，待逐里程碑实施
- **背景**: 项目零 NPC / Shop / Currency 相关代码，需新建两个框架级 EssManager。
  Shop 强依赖 NPC（商人扮演）→ NPC 必须先落地。两者同时在 Tribe 小镇 biome 首发。
  与 #2 IStats（CHA 折扣）弱依赖，与 #1 town biome / #3 SceneInstance 协同。

- **方案**:

  **(1) 总体架构**：
  ```
  NpcManager (前置)              ShopManager (后置)
   ├─ NpcConfig 注册              ├─ ShopConfig 注册
   ├─ Spawn/Despawn               ├─ 货币 / 钱包 / 事务
   ├─ InteractionMenu(自动)       ├─ 价格公式（含 CHA 折扣）
   └─ Role=Merchant ─┐            ├─ Buy / Sell 原子事务
                     └─触发 OpenShop→  └─ ShopUI

  业务侧（Tribe）：把两者粘合 → Alice 商人扮演 + AliceGeneralStore 商品表
  ```
  **职责划分**：NpcManager 只管"NPC 是谁/在哪"；ShopManager 只管"卖什么/多少钱"；
  互不知对方细节，由业务 Demo 通过 NpcConfig.ShopId 字段粘合。

  **(2) NpcManager Dao**：
  ```csharp
  NpcConfig {
    Id, DisplayName, CharacterConfigId,
    NpcRole Role,            // Generic/Merchant/Quester/Trainer/Storyteller/Guard/Banker
    string DialogueId,
    string[] Tags,
    NpcInteractionFlags Interactions,  // [Flags] Talk/Trade/Quest/Train/Bank
    string ShopId            // Role=Merchant 时必填
  }
  NpcInstance {
    InstanceId, ConfigId, WorldPosition,
    string SceneInstanceId,  // 在哪个 SceneInstance（接 #3）
    bool IsAlive,
    Dict<string,object> RuntimeFlags
  }
  ```

  **(3) NPC InteractionMenu**：

  按 E 自动弹菜单，菜单项由 `NpcInteractionFlags` 位标志生成：
  ```
  ┌──────────────┐
  │ 与艾丽丝交谈 │ (Talk)
  │ 商店         │ (Trade)
  │ 任务         │ (Quest)
  │ 离开         │
  └──────────────┘
  ```
  仅一项时按 E 直接进入。

  **(4) ShopManager Dao**：
  ```csharp
  ShopConfig {
    Id, DisplayName, OwnerNpcConfigId,
    ShopType Type,           // General/Weapon/Armor/Magic/BlackMarket
    List<ShopStock> Stock,
    ShopPolicy Policy,
    string CurrencyId        // 默认 "gold"
  }
  ShopStock {
    string ItemId,
    int BasePrice,           // ≤0 = 用 Item.Value
    int Stock,               // -1 = 无限
    float SellbackRatio = 0.5,
    bool RestockEnabled, float RestockSeconds, int RestockAmount
  }
  ShopPolicy {
    float BuyMarkupRatio = 1.2,
    float SellMarkdownRatio = 0.5,
    InventoryItemType[] AcceptedSellTypes,
    int PlayerInitialDiscountChaThreshold = 12
  }
  CurrencyEntry { Id, DisplayName, IconSpriteId }   // gold/silver/rune_token
  ```

  **(5) 价格公式**：
  ```
  基础买入价 = stock.BasePrice * Policy.BuyMarkupRatio
  CHA 折扣  = max(0, (CHA - 10) * 0.01)              // 每点 CHA -1%
  最终买入价 = round(基础买入价 * (1 - CHA折扣))

  卖出价 = item.Value * stock.SellbackRatio * Policy.SellMarkdownRatio
         * (1 + (CHA - 10) * 0.005)                  // CHA 提升卖价
  ```

  **(6) 货币 / 钱包**：

  货币 = 特殊 InventoryItem（`Type=Currency` 新枚举值），存于
  `wallet_{playerId}` 命名 Inventory；ShopManager 提供 Wallet 抽象：
  ```
  GetBalance(playerId, currencyId) → int
  Adjust(playerId, currencyId, delta) → bool       (原子)
  Transfer(fromId, toId, currencyId, amount)        (多人扩展)
  ```

  **(7) 事务原子性**（Buy/Sell 必须全成功或全回滚）：
  ```
  BuyItem 流程：
    1. 校验商品存在、库存足够
    2. 校验玩家钱包余额 >= 总价
    3. 校验玩家背包能容纳（双层：槽位 + 重量，接 #2）
    4. 扣钱 → 加物品 → 减库存
    5. 任一步失败立即回滚已做操作 → ResultCode.Fail
    6. 成功广播 EVT_SHOP_TRANSACTION
  ```

  **(8) 事件 API**：

  NpcManager:
  - `EVT_REGISTER_NPC = "RegisterNpc"`            `[NpcConfig]`
  - `EVT_SPAWN_NPC = "SpawnNpc"`                  `[configId, instanceId, worldPos, sceneInstanceId?]`
  - `EVT_DESPAWN_NPC = "DespawnNpc"`              `[instanceId]`
  - `EVT_INTERACT_NPC = "InteractNpc"`            `[npcInstanceId, playerId]`
  - `EVT_NPC_INTERACTION_OPENED / CLOSED`         (广播)

  ShopManager:
  - `EVT_REGISTER_SHOP = "RegisterShop"`          `[ShopConfig]`
  - `EVT_OPEN_SHOP = "OpenShop"`                  `[shopId, playerId]`
  - `EVT_CLOSE_SHOP = "CloseShop"`                `[shopId, playerId]`
  - `EVT_BUY_ITEM = "ShopBuyItem"`                `[shopId, playerId, itemId, count]`
  - `EVT_SELL_ITEM = "ShopSellItem"`              `[shopId, playerId, itemId, count]`
  - `EVT_REGISTER_CURRENCY = "RegisterCurrency"`  `[CurrencyEntry]`
  - `EVT_GET_PLAYER_CURRENCY = "GetPlayerCurrency"` `[playerId, currencyId] → int`
  - `EVT_SHOP_TRANSACTION = "ShopTransaction"`    (广播)

  **(9) UI 设计**：
  - **NpcInteractionPanel**: NPC 头像 + 名字 + 菜单项 + 取消（走 UIManager.Specs）
  - **ShopUI**: 左商品列表 / 右玩家背包 / 底部金币显示 + Buy/Sell Tab 切换
  - 风格与 InventoryUI / DialogueUI 一致

- **影响范围**:
  - 新增框架：`SingleManagers/NpcManager/`
    - `NpcManager.cs / NpcService.cs`
    - `Dao/NpcConfig / NpcInstance / NpcRole / NpcInteractionFlags`
    - `UI/NpcInteractionUIBuilder.cs`
    - `Agent.md`
  - 新增框架：`MultiManagers/ShopManager/`（依赖 InventoryManager/IStats，归 MultiManagers）
    - `ShopManager.cs / ShopService.cs`
    - `Dao/ShopConfig / ShopStock / ShopPolicy / ShopType / CurrencyEntry`
    - `UI/ShopUIBuilder.cs`
    - `Agent.md`
  - 修改：`InventoryManager/Dao/Item.cs` (`InventoryItemType` 加 `Currency` 值)
  - 新增 Tribe：`Demo/Tribe/Npc/` (TribeNpcRegistry + TribeNpc + Presets/AliceMerchantPreset)
  - 新增 Tribe：`Demo/Tribe/Shop/` (TribeShopRegistry + Presets/AliceGeneralStorePreset)
  - Manager 优先级：`NpcManager=11`, `ShopManager=16`
  - Agent.md 全局清单同步（Core/EssManagers/Agent.md + 总览）

- **里程碑**:

  **前置（NPC）**：
  - **M1 NpcManager 骨架** `[ ]` — Manager + Service + NpcConfig + Spawn/Despawn 事件 +
    Static Entity 注册
  - **M2 NPC 视觉 + 互动** `[ ]` — 接 CharacterManager 视觉 + 按 E 触发 +
    InteractionPanel 自动菜单生成
  - **M3 NPC ↔ Dialogue** `[ ]` — Talk 项打开 DialogueManager；
    Tribe Alice NPC 在 town biome 出现并可对话

  **后置（Shop）**：
  - **M4 ShopManager 骨架** `[ ]` — Manager + Service + ShopConfig + 货币 + 钱包基础；
    注册 `gold` 货币
  - **M5 Shop 事务 + UI** `[ ]` — Buy/Sell 事务 + 校验 + 回滚 + ShopUIBuilder；
    价格公式（含 CHA 折扣，软依赖 #2.M1，缺席时退化为固定 markup）
  - **M6 NPC 商人接入** `[ ]` — Alice 设为 Merchant + 注册 AliceGeneralStore +
    互动菜单触发"商店" → ShopUI；端到端买卖跑通

- **风险 / 开放问题**:
  - **依赖链**：
    - NpcManager: DialogueManager (已有) + CharacterManager (已有)
    - ShopManager: InventoryManager (已有) + #2 IStats (软依赖) + NpcManager
    - 业务粘合点: #1 town biome（首发地点） / #3 SceneInstance（NPC 归属）
  - **货币序列化**: 钱包做特殊 Inventory（复用持久化） vs 独立结构 → 倾向前者
  - **CHA 折扣可选**: M5 若 #2.M1 未落地，先固定 markup 跑通；M6 再补
  - **NPC 物理**: NPC 注册为 `EntityKind.Static`（不参 AI Tick / 不可推），
    通过 EntityManager.EVT_REGISTER_SCENE_ENTITY 接入
  - **本地化**: Alice 对白暂硬编码，未来接 LocalizationManager（不在本设计内）
  - **拒收物品**: Quest 物品不可卖，靠 `ShopPolicy.AcceptedSellTypes` 白名单
  - **多人交易**: M6 仅 Player ↔ Shop；玩家间走未来的 TradeManager（不在范围）
  - **多商店类型**: 先做 General，Weapon/Armor 只是 ShopType 标签 + 商品表差异

- **后续 / 验证**:
  - M1：注册一个 NpcConfig + spawn 一个 NpcInstance，能在场景看到占位
  - M2：玩家走近按 E 弹出菜单（仅"离开"项），关闭正常
  - M3：Alice 在 town biome 出现，按 E → "交谈" → DialogueManager 弹对白
  - M4：钱包 +/- 金币事件正确，余额查询正确
  - M5：买入 / 卖出走完整事务，背包不下时回滚不扣钱；CHA=15 价格降低 5%
  - M6：从 town biome 走到 Alice 按 E → 商店 → 买胡萝卜 → 背包出现 + 金币减少 +
    库存减少；再卖回获得 50% 金币

---

### 3. 传送门 + 子场景实例（多人在线）系统设计（v1 草案）  2026-05-17

- **状态**: `[ ]` 草案已确认，待逐里程碑实施
- **背景**: 主世界单一连续场景缺乏深度。需要在某些特殊地点（如 meadow biome 中段）
  放置传送门，进入"安全采集点"等独立子场景。
  **关键约束**：场景需要支持多人同时在线 —— 不能用 SetActive 冻结 OverWorld，
  Instance 也必须始终活着以支持任意玩家随时进出，且不同玩家可分布在不同 instance。

- **方案**:

  **(1) 空间偏移并存 (Spatial Partitioning)**：

  所有 Instance 与 OverWorld 共存于同一 Unity 场景，以巨大坐标偏移分隔。
  ```
  OverWorld:        X = 0     ~ 760
  SafeGroveMeadow:  X = 50000 ~ 50120
  CombatGoblinCamp: X = 60000 ~ 60100
  ...
  ```
  - 玩家"进入"= 瞬移到目标 instance origin + entryPosition
  - 物理 / 相机自然隔离（>20000 单位远超视口）
  - 不切 Unity Scene，不卸载/加载，最简实现
  - 候选改进：用 Physics2D Layer 进一步隔离碰撞（避免 instance 间穿越交互）

  **(2) Instance 始终活着**（多人核心）：

  - OverWorld 与所有 Instance 的 spawner / ticker / NPC AI 全程运行
  - 玩家只是 GameObject 物理位置变化，世界其它实体不感知
  - 任意时刻多个玩家可分布在 OverWorld + 多个 Instance —— 互相不冻结
  - 同 Instance 内玩家互相可见（多人合作采集 / 战斗）

  **(3) Instance 类型（Theme）**：

  | Theme | 用途 | 示例 | 规则差异 |
  |---|---|---|---|
  | `safe` | 采集 / 休整 | 安全采集点、绿洲 | 禁敌人、HP 缓回 |
  | `event` | 一次性剧情 | 神秘洞窟 | 进入锁门，完成开 |
  | `combat` | 副本 / Boss | 哥布林营地 | 敌人密度高，掉落加成 |
  | `puzzle` | 解谜 | 古代神殿 | 触发器驱动 |
  | `social` | 高密度 NPC | 王城 | 强制和平 + NPC 列表 |

  > 用户首要需求 = `safe` theme 的 **静谧采集林**（safe gathering #1）。

  **(4) 静谧采集林（Safe Grove Meadow）具体内容**：

  - **入口**: meadow biome `X=40` 处的 `PortalFeature`
  - **场景尺寸**: 120 × 20 格（约 8 屏宽）
  - **背景**: 单层暖绿光晕（无视差或低视差）
  - **布局**:
    ```
    [P_in]  胡萝卜x4向日葵x3   [pond+鸭子]   浆果x6蘑菇x4   [campfire + P_out]
     X=0     X=10~30           X=40~55       X=60~85         X=100~120
    ```
  - **可采集物** (高密度 + 重生)：胡萝卜 / 向日葵 / 红蘑菇 / 浆果 / 野草 / 树枝
  - **无害动物**：
    ```
    Rabbit    Hp=3 MoveSpeed=4 FleeRange=5  Loot:[meat_raw, fur(rare)]
    Duck      Hp=4 FleeRange=3              Loot:[meat_raw, feather, egg(rare)]
    Butterfly Hp=∞ 装饰无碰撞
    Firefly   仅夜间显示（占位）
    ```
  - **安全规则**: 进入无敌 0.5s + HP 每秒 +0.5 + 禁敌人 spawn

  **(5) 多人会话模型 (Membership)**：
  ```csharp
  SceneInstanceService:
    instanceId → HashSet<playerId>     # 当前在场玩家
    playerId   → instanceId            # 反向索引

  EnterInstance(playerId, instanceId):
    1. 从旧 instance membership 移除
    2. 加入新 instance membership
    3. 玩家瞬移到 instance.origin + entryPosition
    4. 应用 InstanceRules（HpRegen / 锁敌人）
    5. 广播 EVT_INSTANCE_PLAYER_ENTERED

  ExitInstance(playerId):
    与 Enter 对称，归还 lastOverWorldPos
  ```

  **(6) Instance Hibernation（降耗）**：

  - 所有玩家离开后启动 60s 定时器
  - 超时 → ticker 频率降至 1Hz + 隐藏视觉（不 Destroy）
  - 任意玩家再次进入 → 立即恢复全速 + 视觉
  - 采集物重生计时不停（保持世界一致），仅渲染休眠

  **(7) 数据结构**：
  ```csharp
  InstanceConfig {
    Id, DisplayName, Theme,
    Vector2 OriginOffset,        // 在世界中的偏移基点 (e.g. 50000, 0)
    Vector2 EntryPosition,        // 进入后玩家落点（相对 origin）
    InstanceRules Rules,          // DisableEnemySpawn / HpRegenPerSec / LockTimeOfDay
    List<PortalSpec> ExitPortals, // 通常 1 个回 OverWorld
    string AmbientSoundId,
    string InstanceBuilderId      // 标识用哪个 builder 构造内容
  }

  PortalFeature : TribeFeatureSpec {
    string TargetInstanceId,      // null = 通往 OverWorld
    Vector2 ReturnPosition,
    string PromptText = "按 E 进入",
    string PortalSpriteId
  }
  ```

  **(8) 框架归属**：

  Manager 放在 `EssSystem/Core/Application/SingleManagers/SceneInstanceManager/`
  （框架级，其他 Demo 可复用）。Tribe 侧只持有 InstanceBuilder 与 Preset。

  事件 API：
  - `EVT_REGISTER_INSTANCE = "RegisterInstance"` — `[InstanceConfig]`
  - `EVT_ENTER_INSTANCE = "EnterInstance"` — `[playerId, instanceId]`
  - `EVT_EXIT_INSTANCE = "ExitInstance"` — `[playerId]`
  - `EVT_INSTANCE_PLAYER_ENTERED = "InstancePlayerEntered"`（广播）
  - `EVT_INSTANCE_PLAYER_EXITED = "InstancePlayerExited"`（广播）
  - `EVT_INSTANCE_HIBERNATED` / `EVT_INSTANCE_AWOKE`

- **影响范围**:
  - 新增框架：`SingleManagers/SceneInstanceManager/`
    - `SceneInstanceManager.cs / Service.cs`
    - `Dao/InstanceConfig / InstanceTheme / InstanceRules / PortalSpec`
  - 新增 Tribe：`Demo/Tribe/Instances/`
    - `TribeInstanceRegistry.cs`
    - `Builders/ITribeInstanceBuilder.cs` + `SafeGroveMeadowBuilder.cs`
    - `Presets/SafeGroveMeadowPreset.cs`
  - 新增 Tribe Feature：`Demo/Tribe/World/Features/PortalFeature.cs`
  - 扩展：`TribeCreaturePresets.cs` (+Rabbit / Duck / Butterfly / Firefly)
  - 扩展：`TribePlayer` 加 `CurrentInstanceId` 字段 + 进出回调
  - 资源：portal sprite / pond / 篝火安全林背景（先占位色块）

- **里程碑**:
  - **M1 框架骨架** `[ ]` — `SceneInstanceManager` + `InstanceConfig` + 空间偏移瞬移 +
    Membership 维护；写空 Instance 验证多玩家进出
  - **M2 Portal Feature** `[ ]` — `PortalFeature` + 互动 trigger + 提示 UI；
    放 Portal 到 meadow，按 E 进入空 Instance
  - **M3 静谧采集林内容** `[ ]` — `SafeGroveMeadowBuilder` 布置采集物 + 池塘装饰 +
    Exit Portal + 重生计时器；进出循环
  - **M4 无害动物 + Hibernation** `[ ]` — Rabbit / Duck / Butterfly 三种 Preset +
    逃跑 AI + Instance Hibernation 节能
  - **M5 多人验证** `[ ]` — 第二玩家加入测试（哪怕本地两个 TribePlayer 实例）：
    分布在 OverWorld + Instance 时世界保持运行，可见性正确隔离

- **风险 / 开放问题**:
  - **空间偏移上限**：Unity 单精度 float 在 X>100000 处精度下降，单 Instance 内定位
    需相对 origin 计算，避免直接用绝对世界坐标做物理细节
  - **多人架构基础**：当前项目无网络层。本设计先按"本地多 TribePlayer 实例"准备，
    将来接网络时仅需把 EnterInstance/ExitInstance 走网络消息
  - **物理 Layer 隔离**：仅靠距离不保证 100% 隔离（远距投射物等）。M5 时考虑用
    Physics2D Layer 给每 instance 独立 layer
  - **Hibernation 一致性**：休眠期间采集物重生计时如何保持？方案：用 `Time.time`
    时间戳记录最后状态，醒来差量补算（不依赖每帧 tick）
  - **死亡 / 强制踢出**：Instance 内死亡 → 回 OverWorld + HP 50%；网络掉线 → 自动踢
  - **依赖 #1**：依赖 #1.M1 BiomeRegistry 与 FeatureSpec 基类
  - **资源占位**：portal / pond / safe grove 背景首版用色块，后续替换

- **后续 / 验证**:
  - M1：两个 TribePlayer 同时存在，分别 EnterInstance("A") 和留 OverWorld，
    各自看到对方数量正确（同 instance 互见 / 不同不可见）
  - M2：玩家走到 Portal 触发器看到提示，按 E 瞬移到 Instance origin
  - M3：进入 Instance 看到完整布局，采集物可破坏掉落，Exit Portal 能回主世界
  - M4：兔子见玩家逃跑、鸭子受击掉羽毛；离开 60s 后 Instance hibernate（log）
  - M5：模拟两玩家穿插进出，OverWorld spawner 不被打断，Instance ticker 持续

---

### 2. RPG 属性 + 重量 + 背包切换 系统设计（v1 草案）  2026-05-17

- **状态**: `[ ]` 草案已确认，待逐里程碑实施
- **背景**: 当前缺少 (1) 实体属性系统 (2) 物品/容器重量约束 (3) 多背包切换 UX。
  目标：力量影响携带重量，重量影响移动速度，背包品类决定基础容量，背包间可切换。

- **方案**:

  **(1) 三层职责分离**：
  ```
  EntityManager (属性源)         InventoryManager (容量)        Tribe Player (消费)
   AttributeSet  ──Strength→     Inventory.MaxWeight  ──Weight→   SpeedMultiplier
   IStats Capability             AddItem 双层校验                 HUD 重量条
  ```
  - 属性 = Entity Capability（玩家/怪物/NPC 通用）
  - 重量 = Item.Weight + Inventory.MaxWeight（与属性解耦，单独可玩）
  - 速度惩罚 = Tribe 业务侧（框架不规定公式）

  **(2) RPG 属性系统（EntityManager 新 Capability）**：

  6 Primary：

  | 字段 | 中文 | 派生影响 |
  |---|---|---|
  | `STR` | 力量 | **CarryCapacity** + 近战伤害 + 击退抗性 |
  | `DEX` | 敏捷 | 攻速 + 闪避 + 暴击率 |
  | `CON` | 体质 | MaxHP + HPRegen + 抗毒 |
  | `INT` | 智力 | MaxMP + 法术伤害 + 学习速度 |
  | `WIS` | 感知 | 视野 + MPRegen + 命中 |
  | `CHA` | 魅力 | NPC 价格折扣 + 对话选项 |

  派生公式（集中在 `StatFormulas.cs`）：
  ```
  CarryCapacity (kg) = 10 + STR * 5    // STR 10 → 60kg, STR 20 → 110kg
  MaxHp              = 50 + CON * 10
  MaxMp              = 20 + INT * 8
  HpRegen (/s)       = CON * 0.05
  AttackPower        = STR * 1.5 + 武器加成
  AttackSpeed        = 1.0 + DEX * 0.02
  DodgeChance        = DEX * 0.005   (cap 0.5)
  CritChance         = DEX * 0.003
  ViewRange          = 8 + WIS * 0.3
  ```

  Modifier 系统（装备/Buff 叠加）：
  ```csharp
  enum StatModifierOp { Flat, PercentAdd, PercentMul }
  StatModifier { SourceId, Target, Op, Value, Duration }
  // final = (base + ΣFlat) * (1 + ΣPercentAdd) * Π(1 + PercentMul)
  ```

  事件：`EntityManager.EVT_STATS_CHANGED = "EntityStatsChanged"`
  → `[entityId, statId, oldValue, newValue]`

  **(3) 重量系统（InventoryManager 扩展）**：
  ```csharp
  // Item 新字段
  public float Weight;                       // 单件 kg
  // Inventory 新字段
  public float MaxWeight;                    // ≤0 = 无限制
  public string OwnerEntityId;               // 可选；有则 MaxWeight 随 owner.STR 动态
  public float CurrentWeight => Σ(slot.Weight*Stack);  // 派生
  ```

  AddItem 双层校验 → 新枚举 `AddItemResult { Ok, SlotsFull, OverWeight, PartialAccepted }`

  动态最大重量：
  ```
  MaxWeight = BackpackPreset.BaseMaxWeight  +  Owner.IStats.CarryCapacity (若有)
  ```

  事件：`InventoryManager.EVT_WEIGHT_CHANGED = "InventoryWeightChanged"`
  → `[inventoryId, current, max]`

  **(4) 背包切换（多容器 UX）**：

  `BackpackPreset` 数据（背包"品类"）：

  | ID | 中文 | Slots | BaseMaxWeight |
  |---|---|---|---|
  | `pouch_small` | 小布袋 | 8 | 15 kg |
  | `backpack_medium` | 旅行背包 | 20 | 30 kg |
  | `backpack_large` | 大型背包 | 32 | 50 kg |
  | `chest_storage` | 储物箱（不可携带） | 60 | ∞ |

  服务接口：
  - `EVT_SET_ACTIVE = "InventorySetActive"` — `[ownerEntityId, inventoryId]`
  - `GetActiveInventory(ownerEntityId)`
  - 切换不清空旧背包（保留物品）

  UI：`TribePlayerHud` 加 Tab/U 键 → SelectBackpackPanel → 列玩家持有背包 → 设 Active。

  **(5) 移动惩罚（Tribe 业务侧）**：

  新组件 `TribePlayerEncumbrance.cs` 订阅 `EVT_WEIGHT_CHANGED`：
  ```
  ratio = CurrentWeight / MaxWeight
  speedMultiplier =
    ratio ≤ 0.5 → 1.0    (轻载)
    ratio ≤ 0.8 → 1.0~0.88
    ratio ≤ 1.0 → 0.88~0.60
    ratio > 1.0 → 0.4    (超载 + UI 红字)
  ```
  `TribePlayerMovement` 加 `SpeedMultiplier` 属性，最终速度 = `_speed * SpeedMultiplier`。
  HUD 重量条：绿<50% / 黄<80% / 红<100% / 紫>100%。

- **影响范围**:
  - 新增：`EntityManager/Capabilities/Stats/` (IStats, AttributeSet, StatModifier,
    PrimaryStat, DerivedStat, StatFormulas, StatsComponent)
  - 新增：`InventoryManager/Dao/BackpackPreset.cs`、`AddItemResult.cs`
  - 修改：`InventoryManager/Dao/Item.cs` (+Weight)、`Inventory.cs` (+MaxWeight, +OwnerEntityId)
  - 修改：`InventoryManager/InventoryService.cs` (容量校验 + ActiveInventory 路由)
  - 修改：`InventoryManager/UI/InventoryUIBuilder.cs` (槽位重量显示候选)
  - 新增：`Demo/Tribe/Player/Component/TribePlayerEncumbrance.cs`
  - 修改：`Demo/Tribe/Player/Component/TribePlayerMovement.cs` (+SpeedMultiplier)
  - 修改：`Demo/Tribe/Player/Component/TribePlayerHud.cs` (+重量条 +背包切换按钮)
  - 文档：3 个相关 Manager 的 `Agent.md` 同步更新

- **里程碑**:
  - **M1 属性系统骨架** `[ ]` — IStats + AttributeSet + 6 Primary + 派生公式 +
    StatsComponent；玩家挂上 STR=10 默认值，事件广播打通
  - **M2 重量基础** `[ ]` — Item.Weight + Inventory.MaxWeight + AddItem 双层校验 +
    HUD 重量条；用固定 30kg 容量先跑通
  - **M3 属性 ↔ 重量联动** `[ ]` — Inventory.OwnerEntityId + 动态 MaxWeight；
    改 STR 看容量变化；现有 220 物品按类别批量补 Weight
    （Consumable=0.1 / Weapon=3 / Armor=5 / Material=0.5）
  - **M4 移动惩罚 + 背包切换** `[ ]` — TribePlayerEncumbrance + SpeedMultiplier 链；
    BackpackPreset + ActiveInventory + Tab 键切换 UI

- **风险 / 开放问题**:
  - **存档兼容**：旧 Inventory 序列化无 Weight 字段，需 `Weight=0`/`MaxWeight=0` 默认值兼容
  - **数字调参**：所有 `STR*5`/`+10kg` 等公式集中在 `StatFormulas` + `EncumbranceCurve`，
    避免散落到业务代码
  - **跨模块依赖**：Stats 是框架级 Capability，重量惩罚是 Tribe 业务级，保持单向（Tribe → Manager）
  - **多 Demo 复用**：Stats 设计为 EntityManager 通用 Capability，DayNight 等 demo 后续可复用
  - **220 物品 Weight**：M3 时需按类别批量赋值，先用脚本 / 默认表，后续个别精调
  - **物品稀有度 / 装备槽**：未来扩展（Modifier 系统已为装备穿戴预留接口）

- **后续 / 验证**:
  - M1 完成：`Entity.Get<IStats>().GetPrimary(STR)` 可读，改值触发事件
  - M2 完成：背包加重物到上限会被拒绝，HUD 重量条实时变化
  - M3 完成：吃 +5 STR 药水后背包容量立即扩大，触发 EVT_WEIGHT_CHANGED
  - M4 完成：装满后玩家变慢可感，超载时颜色红 + 移速 0.4，Tab 键能在多个背包间切换
  - 每 M 完成后 Agent.md 对应章节同步；新条目记录"实际落地差异"

---

### 1. 群系化大世界地图设计（v1 草案）  2026-05-17

- **状态**: `[ ]` 草案已确认，待逐里程碑实施
- **背景**: 当前 Tribe 是横版 2D 平地 + 视差背景 + 硬编码 4 个植物 + 1 个骷髅，
  缺乏地图深度。引入横向带状群系（biome strip）模型让玩家从左到右穿越多种主题，
  同时增添水体 / 石头堆 / 小镇等地图特征丰富视觉与玩法。

- **方案**:

  **(1) 世界结构**：横向 7 段 biome 串联（约 760 X 单位），段间 ±10 格过渡带：
  ```
  meadow → forest → town → swamp → rocky → snow → ruins
   0~80    80~200  200~280  280~400  400~520  520~640  640~760
  ```

  **(2) 群系清单**：

  | ID | 中文 | 关键特征 | 资源/敌人 |
  |---|---|---|---|
  | `meadow` | 起源草地 | 浅水洼、向日葵、胡萝卜田 | 胡萝卜/向日葵/兔子 |
  | `forest` | 森林 | 高树、蘑菇圈、浆果丛 | 红蘑菇/浆果/野猪 |
  | `town` | 小镇 | 房屋×3-5、井、营火、NPC | 商人 NPC（**安全区**） |
  | `swamp` | 沼泽湿地 | 大型湖泊、芦苇、泥潭 | 沼泽蘑菇/青蛙/史莱姆 |
  | `rocky` | 岩石荒原 | 石头堆、矿脉、断崖 | 铁矿/煤矿/骷髅/野山羊 |
  | `snow` | 雪原 | 冻结水洼、松树、冰柱 | 蓝莓/冰晶/雪狼 |
  | `ruins` | 遗迹边境 | 残柱、宝箱、骨堆 | 稀有掉落/精英骷髅/Boss |

  **(3) 地图特征对象（Features）**：
  - **水体**：`PuddleFeature` (1-2 格浅水) / `PondFeature` (3-5 格) / `LakeFeature` (8-15 格 + 可选小岛)
  - **岩石**：`StoneClusterFeature` (3-7 块可破坏) / `OreVeinFeature` (Tier 矿脉) / `CliffFeature` (3-5 格抬升)
  - **聚落**：`TownFeature` (房+井+营火+NPC+`SafeZoneTrigger`) / `CampFeature` (营火+帐篷)
  - **植被装饰**：`Tree` / `Bush` / `Reed` / `Bones` / `Ruin`（残柱）
  - **互动点**：`ChestFeature` / `SignFeature` / `PortalFeature`（后期）

  **(4) 数据结构**：
  ```csharp
  TribeBiomeConfig { Id, StartX, EndX, GroundTint, BackgroundOverrideFolder,
                     Landmarks[], SpawnZones[], AmbientSoundId }
  TribeFeatureSpec  (abstract Build) → 各 *Feature 子类
  TribeBiomeRegistry  集中注册 / 顺序构建
  ```

  **(5) 文件结构**：
  ```
  Demo/Tribe/
  ├── World/
  │   ├── TribeBiomeRegistry.cs
  │   ├── TribeBiomeConfig.cs
  │   ├── TribeBiomeContext.cs
  │   ├── Features/                # Puddle/Pond/Lake/StoneCluster/OreVein/Cliff/
  │   │                            # Town/Camp/Chest/Sign/...
  │   └── Presets/                 # 7 个 *BiomePreset.cs
  └── TribeWorldSpawner.cs         (保留，被 Registry 调用做散布)
  ```

  **(6) 与现有系统对接**：
  - 资源：复用 `Resources/Tribe/Objects/stone.png` 等已有图 + 颜色块占位（`SpawnDecoration` 已支持）
  - 敌人：通过 `TribeCreatureConfig` 预设扩展（青蛙/野猪/雪狼 = Skeleton 改参）
  - NPC：复用 `DialogueManager` + 新建 `TribeNpc`
  - 音效：`AudioManager` ambient → biome；走 `Resources/Sound/`
  - 背景：每 biome 自己一套图，进入时切层（不做平滑过渡，硬切 + 黑屏 0.2s 候选）

- **影响范围**:
  - 新增：`Demo/Tribe/World/`（1 个 Registry + 1 个 Config + 1 个 Context + N 个 Features + 7 个 Presets）
  - 修改：`TribeGameManager.cs`（启动流程接入 BiomeRegistry，移除硬编码 spawn）
  - 修改：`Demo/Tribe/Agent.md`（增 World 模块说明）
  - 资源：M2-M5 期间补素材到 `Resources/Tribe/{Buildings,Water,Rocks,Decor}/`（按需新建）

- **里程碑**:
  - **M1 框架骨架**`[ ]` — Registry + Config + FeatureSpec 基类 + 1 个 `MeadowBiomePreset` 跑通端到端
  - **M2 水体**`[ ]` — Puddle/Pond/Lake 三种 Feature；meadow + swamp 落地
  - **M3 岩石**`[ ]` — StoneCluster/OreVein/Cliff；rocky 落地
  - **M4 小镇**`[ ]` — TownFeature + SafeZoneTrigger + NPC 对话；town 落地
  - **M5 完整 7 群系**`[ ]` — snow/ruins + 装饰（tree/bush/reed/bones）+ chest/sign

- **风险 / 开放问题**:
  - **平地限制**：`_generateTerrain=false` 时水洼/湖只能装饰/触发体，无法真凹陷；
    M1 按装饰处理，需"真地形"时再启用 SideScroller 生成器扩展凹陷逻辑
  - **背景切换**：硬切 vs 渐变 vs 锁单 biome 一套——倾向硬切 + 短暂淡黑过渡
  - **NPC 对白**：M4 实施时需起草对白文本，DialogueManager 当前内容空
  - **过渡带**：相邻 biome 边界 ±10 格的 GroundTint / 生物表混合算法待 M1 时定

- **后续 / 验证**:
  - M1 完成后：场景中能看到 meadow biome 范围内 spawn 出原有 4 类植物，
    硬编码 spawn 已彻底移除，玩家行进体感与之前一致
  - 每个 M 完成后增加一条新 ToDo 子条目记录"实际落地差异"
  - Agent.md World 模块章节随 M1 一并新增

---

### 0. 基线状态  2026-05-17

- **状态**: `[x]`
- **背景**: 在开始新一轮 Tribe 设计前，记录当前已完成的整理工作作为基线。
- **方案**: 上一轮重构已落盘的核心结构：
  - `Demo/Tribe/` 物理布局：`Player/Component/`、`Enemy/`、`Resource/`、`Background/`
  - 骷髅已并入 `TribeCreature` + `TribeCreaturePresets.Skeleton()`，无独立类
  - `Resources/Tribe/Items/` 220 张图按 9 个功能子目录分类（Weapons / Armor / Accessories / Consumables / Materials / Currency / Tools / Magic / UI）
  - `Resources/Sound/` 统一根目录（feuer.wav 已并入），`ResourceManager` hint 加 `Sound`
  - 玩家编排器 `TribePlayer` + 子组件（Movement / Combat / Hud / Interaction / CameraFollow）
  - 通用动画器 `TribeSpriteAnimator`（取代专用 SkeletonAnimator）
- **影响范围**: 此前 9 个 commit（`81b8abe` ~ `a30b126`）
- **后续**: 接下来的所有设计变更基于此基线展开，每个改动新建一条目记录。
