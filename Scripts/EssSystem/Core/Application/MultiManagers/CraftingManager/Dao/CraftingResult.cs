using System;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager.Dao
{
    [Serializable]
    public readonly struct CraftingResult
    {
        public readonly bool Success;
        public readonly string Code;
        public readonly string RecipeId;
        public readonly string OutputItemId;
        public readonly int OutputCount;
        public readonly string MissingItemId;
        public readonly int RequiredCount;
        public readonly int AvailableCount;

        private CraftingResult(bool success, string code, string recipeId, string outputItemId,
            int outputCount, string missingItemId, int requiredCount, int availableCount)
        {
            Success = success;
            Code = code ?? string.Empty;
            RecipeId = recipeId ?? string.Empty;
            OutputItemId = outputItemId ?? string.Empty;
            OutputCount = outputCount;
            MissingItemId = missingItemId ?? string.Empty;
            RequiredCount = requiredCount;
            AvailableCount = availableCount;
        }

        public static CraftingResult Ok(CraftingRecipe recipe, CraftOutput output) =>
            new(true, "ok", recipe?.Id, output?.ItemId, Math.Max(1, output?.Count ?? 1),
                null, 0, 0);

        public static CraftingResult Fail(string code, CraftingRecipe recipe = null) =>
            new(false, code, recipe?.Id, null, 0, null, 0, 0);

        public static CraftingResult Missing(CraftingRecipe recipe, CraftIngredient ingredient, int available) =>
            new(false, "missing_ingredient", recipe?.Id, null, 0, ingredient?.ItemId,
                Math.Max(1, ingredient?.Count ?? 1), Math.Max(0, available));
    }
}
