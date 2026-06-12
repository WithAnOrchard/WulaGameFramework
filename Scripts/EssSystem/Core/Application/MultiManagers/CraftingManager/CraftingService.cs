using System;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.MultiManagers.CraftingManager.Dao;
using EssSystem.Core.Application.SingleManagers.InventoryManager;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager
{
    /// <summary>
    /// 制作业务服务 —— 持久化配方表 / 工作台表 / 玩家已学配方；运行时维护 CraftingSession 列表。
    /// <para>
    /// <b>骨架阶段</b>：仅承载 Service 数据存储约定与日志通道；
    /// LearnBlueprint / StartCraft / CompleteCraft / 品质判定 / CraftSkill 经验
    /// 在 <c>Demo/Tribe/ToDo.md #5</c> 各里程碑（M1-M5）实施。
    /// </para>
    /// </summary>
    public class CraftingService : Service<CraftingService>
    {
        #region 数据分类

        /// <summary>已注册的 CraftingRecipe（按 Id）。</summary>
        public const string CAT_RECIPES        = "Recipes";

        /// <summary>已注册的 WorkstationDefinition（按 Id）。</summary>
        public const string CAT_WORKSTATIONS   = "Workstations";

        /// <summary>玩家已学会的配方（按 playerId → Set&lt;recipeId&gt;）。</summary>
        public const string CAT_KNOWN_RECIPES  = "KnownRecipes";

        /// <summary>正在进行的 CraftingSession（按 sessionId，运行时不写盘）。</summary>
        public const string CAT_SESSIONS       = "Sessions";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("CraftingService 初始化完成（骨架）", Color.green);
        }
        public void RegisterRecipe(CraftingRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.Id)) return;
            SetData(CAT_RECIPES, recipe.Id, recipe);
        }

        public void RegisterRecipes(params CraftingRecipe[] recipes)
        {
            if (recipes == null) return;
            foreach (var recipe in recipes)
                RegisterRecipe(recipe);
        }

        public CraftingRecipe GetRecipe(string recipeId) =>
            string.IsNullOrEmpty(recipeId) ? null : GetData<CraftingRecipe>(CAT_RECIPES, recipeId);

        public bool TryCraft(string inventoryId, CraftingRecipe recipe, out CraftingResult result)
        {
            result = CraftingResult.Fail("invalid_recipe", recipe);
            if (string.IsNullOrEmpty(inventoryId) || recipe == null) return false;
            if (!InventoryService.HasInstance)
            {
                result = CraftingResult.Fail("inventory_service_missing", recipe);
                return false;
            }

            var inventory = InventoryService.Instance.GetInventory(inventoryId);
            if (inventory == null)
            {
                result = CraftingResult.Fail("inventory_missing", recipe);
                return false;
            }

            var output = GetPrimaryOutput(recipe);
            if (output == null || string.IsNullOrEmpty(output.ItemId))
            {
                result = CraftingResult.Fail("output_missing", recipe);
                return false;
            }

            var outputItem = InventoryService.Instance.InstantiateTemplate(output.ItemId, Mathf.Max(1, output.Count));
            if (outputItem == null)
            {
                result = CraftingResult.Fail("output_template_missing", recipe);
                return false;
            }

            var ingredients = recipe.Ingredients ?? Array.Empty<CraftIngredient>();
            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId)) continue;
                var required = Mathf.Max(1, ingredient.Count);
                var available = inventory.CountOf(ingredient.ItemId);
                if (available < required)
                {
                    result = CraftingResult.Missing(recipe, ingredient, available);
                    return false;
                }
            }

            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId)) continue;
                var remove = InventoryService.Instance.RemoveItem(inventoryId, ingredient.ItemId, Mathf.Max(1, ingredient.Count));
                if (!remove.Success || remove.Remaining > 0)
                {
                    result = CraftingResult.Fail("consume_failed", recipe);
                    return false;
                }
            }

            var add = InventoryService.Instance.AddItem(inventoryId, outputItem, Mathf.Max(1, output.Count));
            if (!add.Success || add.Remaining > 0)
            {
                if (add.Amount > 0)
                    InventoryService.Instance.RemoveItem(inventoryId, output.ItemId, add.Amount);
                RestoreIngredients(inventoryId, ingredients);
                result = CraftingResult.Fail("output_no_space", recipe);
                return false;
            }

            result = CraftingResult.Ok(recipe, output);
            return true;
        }

        private static CraftOutput GetPrimaryOutput(CraftingRecipe recipe) =>
            recipe?.Outputs != null && recipe.Outputs.Length > 0 ? recipe.Outputs[0] : null;

        private static void RestoreIngredients(string inventoryId, CraftIngredient[] ingredients)
        {
            foreach (var ingredient in ingredients)
            {
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId)) continue;
                var item = InventoryService.Instance.InstantiateTemplate(ingredient.ItemId, Mathf.Max(1, ingredient.Count));
                if (item != null)
                    InventoryService.Instance.AddItem(inventoryId, item, Mathf.Max(1, ingredient.Count));
            }
        }
    }
}
