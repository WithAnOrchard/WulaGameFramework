# CraftingManager 指南

## 概述

`CraftingManager`（`[Manager(18)]`）+ `CraftingService`（业务服务 + 持久化）提供
配方驱动的装备制作 + 蓝图解锁 + 工作台门槛 + 品质系统。

**职责单一**：配方表 + 制作流程 + 蓝图学习 + 品质判定。地图上的物理工作台属于业务侧
`WorkstationFeature`（在 #1 框架下）；蓝图本身是特殊 InventoryItem
（`InventoryItemType.Blueprint`，待 InventoryManager 扩展）。

## 状态

🚧 **骨架阶段**：Manager / Service 已挂入优先级链；Dao（`CraftingRecipe` / `CraftIngredient`
/ `CraftOutput` / `RecipeSkillRequirement` / `WorkstationDefinition` / `CraftQuality` /
`CraftingSession`）已定义；制作流程、蓝图、品质、CraftSkill Capability、UI 尚未实现。
详见 `Demo/Tribe/ToDo.md` 条目 #5 M1-M5。

## 文件结构

```
CraftingManager/
├── CraftingManager.cs            薄门面（Manager 单例）
├── CraftingService.cs            业务服务（CAT_RECIPES / CAT_WORKSTATIONS / CAT_KNOWN_RECIPES / CAT_SESSIONS）
├── Agent.md                      本文档
└── Dao/
    ├── CraftingRecipe.cs         Id / Ingredients[] / Outputs[] / CatalystKeep[] / WorkstationId / Tier / CraftSeconds / Skill / LearnedByDefault / BlueprintItemId / CategoryId
    ├── CraftIngredient.cs        ItemId / Count
    ├── CraftOutput.cs            ItemId / Count / Chance
    ├── RecipeSkillRequirement.cs CraftSkillMin / IntelligenceMin / StrengthMin
    ├── WorkstationDefinition.cs  Id / DisplayName / Tier / Categories[]
    ├── CraftQuality.cs           Crude / Common / Fine / Superior / Masterwork / Legendary
    └── CraftingSession.cs        运行时会话（SessionId / PlayerId / RecipeId / WorkstationId / Quantity / StartTime / EndTime / MaterialsReserved）
```

## 数据分类（持久化）

| 常量 | 用途 |
|---|---|
| `CraftingService.CAT_RECIPES`        = `"Recipes"`        | 已注册 `CraftingRecipe`（按 Id） |
| `CraftingService.CAT_WORKSTATIONS`   = `"Workstations"`   | 已注册 `WorkstationDefinition`（按 Id） |
| `CraftingService.CAT_KNOWN_RECIPES`  = `"KnownRecipes"`   | 玩家已学配方（playerId → Set&lt;recipeId&gt;） |
| `CraftingService.CAT_SESSIONS`       = `"Sessions"`       | 运行时 CraftingSession（不写盘） |

## 计划事件（M1+ 实施时新增）

> 当前骨架阶段尚未声明 `EVT_*` 常量。实施时按 ToDo #5 第 (8) 节注册：
> RegisterRecipe / RegisterWorkstation / LearnBlueprint / RecipeLearned /
> StartCraft / CancelCraft / CraftProgress / CraftCompleted / CraftFailed /
> OpenCraftingUI / CloseCraftingUI / QueryKnownRecipes。
