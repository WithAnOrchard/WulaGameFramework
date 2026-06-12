using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.UIManager.Dao;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Foundation.DataManager.RuntimeConfig;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using EssSystem.Core.Application.MultiManagers.CraftingManager;
using EssSystem.Core.Application.MultiManagers.CraftingManager.Dao;

namespace Demo.Tribe.Interaction
{
    /// <summary>
    /// 部落 Demo 的极简营火制作面板 —— 通过 UIManager DAO 树构建，参 §5。
    /// <para>
    /// 配方硬编码，材料校验 + 消耗 + 产出走 <see cref="InventoryService"/> 直 API。
    /// 框架 <c>CraftingManager</c> 仍是骨架；本面板是 demo 占位，骨架成型后可平迁到 CraftingManager.EVT_OPEN_UI。
    /// </para>
    /// </summary>
    public static class TribeCraftingPanel
    {
        private const string PanelDaoId = "__tribe_craft_panel";
        private const string PlayerInvId = "player";
        private const string CraftingBackgroundSpriteId = "Common/UI/Crafting/crafting_panel_bg";
        private const string RecipeConfigPath = "Demo/Tribe/campfire_recipes.json";

        private static UIPanelComponent _panel;
        private static UITextComponent _statusText;
        private static CraftingRecipe[] _recipesCache;

        private static readonly Color PanelTint = Color.white;
        private static readonly Color HeaderBg = new Color(0.10f, 0.055f, 0.035f, 0.64f);
        private static readonly Color ListBg = new Color(0.025f, 0.020f, 0.018f, 0.34f);
        private static readonly Color RowBgA = new Color(0.17f, 0.105f, 0.070f, 0.82f);
        private static readonly Color RowBgB = new Color(0.11f, 0.080f, 0.065f, 0.82f);
        private static readonly Color EmberLine = new Color(1.0f, 0.46f, 0.18f, 0.72f);
        private static readonly Color EmberLineSoft = new Color(1.0f, 0.52f, 0.22f, 0.26f);
        private static readonly Color StoneLine = new Color(0.62f, 0.58f, 0.50f, 0.34f);

        /// <summary>uGUI Text 模糊主因：默认像素分辨率渲染。×N 超采样 → N× 字号 + N× 尺寸 + 1/N 缩放，
        /// 视觉体积不变但字体在 N× 分辨率 raster。与 InventoryUIBuilder 保持一致。</summary>
        private const float TextSupersample = 6f;

        /// <summary>1920×1080 参考分辨率下画布中心（与 InventoryManager 面板同一参考系）。</summary>
        private static readonly Vector2 CanvasCenter = new Vector2(960f, 540f);

        [System.Serializable]
        private class RecipeConfigFile
        {
            public CraftingRecipe[] Recipes;
        }

        private static CraftingRecipe[] GetRecipes()
        {
            if (_recipesCache != null) return _recipesCache;
            if (RuntimeConfigLoader.TryLoadJson<RecipeConfigFile>(RecipeConfigPath, out var file)
                && file?.Recipes != null)
            {
                _recipesCache = file.Recipes;
                return _recipesCache;
            }

            _recipesCache = new CraftingRecipe[0];
            return _recipesCache;
        }

        // ─── 显隐控制 ────────────────────────────────────────────
        public static void Toggle()
        {
            if (IsOpen()) Close();
            else Open();
        }

        public static void Open()
        {
            if (!EventProcessor.HasInstance) return;
            if (IsOpen()) return;

            _recipesCache = null;
            _panel = BuildPanel();
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity", new List<object> { PanelDaoId, _panel });
        }

        public static void Close()
        {
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "UnregisterUIEntity", new List<object> { PanelDaoId });
            _panel = null;
            _statusText = null;
        }

        private static bool IsOpen()
        {
            if (!EventProcessor.HasInstance) return false;
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { PanelDaoId });
            return ResultCode.IsOk(r) && r.Count >= 2 && r[1] is GameObject go && go != null && go.activeInHierarchy;
        }

        // ─── UI 构建 ─────────────────────────────────────────────
        // 坐标约定（与 UIEntityFactory 对齐）：
        //   • UIPanel / UIButton / UIText 全部使用父节点左下角 → 自身中心的偏移（BL 约定）。
        private static UIPanelComponent BuildPanel()
        {
            const float W = 720f, H = 480f;
            const float innerW = W - 100f;
            const float listH = 300f;

            // 根面板：BL 约定 → 面板中心 = canvas-BL + (CanvasCenter)
            var panel = new UIPanelComponent(PanelDaoId, "TribeCraftingPanel")
                .SetPosition(CanvasCenter.x, CanvasCenter.y)
                .SetSize(W, H)
                .SetBackgroundColor(PanelTint)
                .SetBackgroundSpriteId(CraftingBackgroundSpriteId);

            panel.AddChild(new UIPanelComponent($"{PanelDaoId}_HeaderBg", "HeaderBg")
                .SetPosition(W * 0.5f, H - 102f)
                .SetSize(innerW, 42f)
                .SetBackgroundColor(HeaderBg));

            AddBorder(panel, $"{PanelDaoId}_ListBorder", W * 0.5f, H * 0.5f - 32f, innerW + 6f, listH + 8f, 1f, StoneLine);

            // 标题
            panel.AddChild(BuildText(
                $"{PanelDaoId}_Title", "Title", "篝火 · 制作",
                centerX: W * 0.5f, centerY: H - 102f,
                width: innerW, height: 42f,
                fontSize: 25, color: new Color(1f, 0.78f, 0.38f),
                align: TextAnchor.MiddleCenter));

            // 关闭按钮（UIButton 用 BL 约定：从面板左下角出发）—— 右上角 32px 内缩
            var closeBtn = new UIButtonComponent($"{PanelDaoId}_Close", "Close", "×")
                .SetPosition(W - 54f, H - 58f)
                .SetSize(34f, 34f)
                .SetButtonColor(new Color(0.16f, 0.050f, 0.030f, 0.92f));
            closeBtn.SetText(string.Empty);
            AddBorder(closeBtn, $"{PanelDaoId}_CloseBorder", 17f, 17f, 34f, 34f, 1f, EmberLine);
            closeBtn.AddChild(BuildText(
                $"{PanelDaoId}_CloseText", "CloseText", "×",
                centerX: 17f, centerY: 17f,
                width: 34f, height: 34f,
                fontSize: 18, color: new Color(1f, 0.72f, 0.72f, 1f),
                align: TextAnchor.MiddleCenter));
            closeBtn.OnClick += _ => Close();
            panel.AddChild(closeBtn);

            var scroll = new UIScrollViewComponent($"{PanelDaoId}_RecipeScroll", "RecipeScroll")
                .SetPosition(W * 0.5f, H * 0.5f - 32f)
                .SetSize(innerW, listH)
                .SetBackgroundColor(ListBg)
                .SetContentPadding(10)
                .SetItemSpacing(8f)
                .SetScrollSensitivity(42f);
            const float rowH = 72f;
            var recipes = GetRecipes();
            for (var i = 0; i < recipes.Length; i++)
            {
                BuildRecipeRow(scroll, recipes[i], i, 0f, innerW - 24f, rowH);
            }
            if (recipes.Length == 0)
            {
                scroll.AddContentChild(BuildText(
                    $"{PanelDaoId}_RecipeMissing", "RecipeMissing", "未读取到营火配方配置。",
                    centerX: innerW * 0.5f - 12f, centerY: rowH * 0.5f,
                    width: innerW - 24f, height: rowH,
                    fontSize: 16, color: new Color(1f, 0.58f, 0.45f),
                    align: TextAnchor.MiddleCenter));
            }
            panel.AddChild(scroll);

            // 底部状态栏
            _statusText = BuildText(
                $"{PanelDaoId}_Status", "Status", "材料不足时会提示在这里。",
                centerX: W * 0.5f, centerY: 34f,
                width: innerW, height: 32f,
                fontSize: 15, color: new Color(0.78f, 0.76f, 0.68f),
                align: TextAnchor.MiddleCenter);
            panel.AddChild(_statusText);

            return panel;
        }

        // 行布局（width=600, h=80）：
        // ┌─────────┬─────────────────────────┬───────────┬────────┐
        // │ 配方名  │ [icon×N] [icon×N] ...   │ → [icon×N]│ [制作] │
        // │  120    │      ~280               │   ~80     │   80   │
        // └─────────┴─────────────────────────┴───────────┴────────┘
        // 子 UIPanel/UIButton/UIText 都用 BL 约定（从 row-BL 到自身中心）。
        private const float IconSize = 44f;       // 图标方块边长
        private const float IconLabelW = 32f;     // 图标右侧 "x N" 文本宽度
        private const float IconGroupGap = 6f;    // 图标 + 数量 与下一组之间的间距

        private static void BuildRecipeRow(UIComponent parent, CraftingRecipe recipe, int idx, float rowCenterY, float width, float h)
        {
            const float namePaneW = 112f;
            var row = new UIPanelComponent($"{PanelDaoId}_Row{idx}", $"Row_{idx}")
                .SetPosition(width * 0.5f, rowCenterY + h * 0.5f)
                .SetSize(width, h)
                .SetBackgroundColor(idx % 2 == 0 ? RowBgA : RowBgB);
            if (parent is UIScrollViewComponent scroll)
                scroll.AddContentChild(row);
            else
                parent.AddChild(row);
            AddBorder(row, $"{PanelDaoId}_Row{idx}_Border", width * 0.5f, h * 0.5f, width, h, 1f, EmberLineSoft);

            row.AddChild(new UIPanelComponent($"{PanelDaoId}_Row{idx}_NamePane", "NamePane")
                .SetPosition(namePaneW * 0.5f + 8f, h * 0.5f)
                .SetSize(namePaneW + 16f, h - 12f)
                .SetBackgroundColor(new Color(0.055f, 0.045f, 0.040f, 0.40f)));

            // 1) 配方名 —— 左侧 120 区（UIText 中心约定）
            var nameCenterX = -width * 0.5f + namePaneW * 0.5f + 8f;
            row.AddChild(BuildText(
                $"{PanelDaoId}_Row{idx}_Name", "Name", GetRecipeName(recipe),
                centerX: width * 0.5f + nameCenterX, centerY: h * 0.64f,
                width: namePaneW, height: 26f,
                fontSize: 17, color: new Color(1f, 0.95f, 0.7f),
                align: TextAnchor.MiddleCenter));
            row.AddChild(BuildText(
                $"{PanelDaoId}_Row{idx}_Desc", "Desc", GetRecipeDescription(recipe),
                centerX: width * 0.5f + nameCenterX, centerY: h * 0.30f,
                width: namePaneW, height: 24f,
                fontSize: 9, color: new Color(0.78f, 0.72f, 0.62f),
                align: TextAnchor.MiddleCenter));

            // 2) 输入图标组 —— 从配方名右边开始；制作按钮 + 输出 (~180) 留在右侧
            const float btnW = 86f;
            const float outputW = 84f;       // [→] + 输出图标 + 数量
            var inputsStartX = -width * 0.5f + namePaneW + 16f;
            var inputsEndX   =  width * 0.5f - btnW - 8f - outputW - 8f;

            var ingredients = recipe?.Ingredients ?? new CraftIngredient[0];
            var groupW = IconSize + IconLabelW + IconGroupGap;
            var inputsAllW = ingredients.Length * groupW - IconGroupGap;
            var inputsCenterX = (inputsStartX + inputsEndX) * 0.5f;
            var firstGroupCenterX = inputsCenterX - inputsAllW * 0.5f + groupW * 0.5f;

            for (var i = 0; i < ingredients.Length; i++)
            {
                var ingredient = ingredients[i];
                if (ingredient == null || string.IsNullOrEmpty(ingredient.ItemId)) continue;
                var groupCenterX = firstGroupCenterX + i * groupW;
                BuildItemIcon(row, $"{PanelDaoId}_Row{idx}_In{i}",
                    iconCenterX: groupCenterX - IconLabelW * 0.5f,
                    labelCenterX: groupCenterX + IconSize * 0.5f,
                    rowSize: new Vector2(width, h),
                    spritePath: ResolveItemIcon(ingredient.ItemId),
                    amount: Mathf.Max(1, ingredient.Count),
                    labelColor: Color.white);
            }

            // 3) 输出区：→ + icon + 数量
            var output = GetPrimaryOutput(recipe);
            var outputCenterX = inputsEndX + outputW * 0.5f;
            row.AddChild(BuildText(
                $"{PanelDaoId}_Row{idx}_Arrow", "Arrow", "→",
                centerX: width * 0.5f + outputCenterX - outputW * 0.5f + 12f, centerY: h * 0.5f,
                width: 24f, height: h - 8f,
                fontSize: 22, color: new Color(0.7f, 1f, 0.7f),
                align: TextAnchor.MiddleCenter));

            BuildItemIcon(row, $"{PanelDaoId}_Row{idx}_Out",
                iconCenterX: outputCenterX,
                labelCenterX: outputCenterX + IconSize * 0.5f + IconLabelW * 0.5f,
                rowSize: new Vector2(width, h),
                spritePath: ResolveItemIcon(output?.ItemId),
                amount: Mathf.Max(1, output?.Count ?? 1),
                labelColor: new Color(0.6f, 1f, 0.6f));

            // 4) 制作按钮（UIButton BL 约定）
            var btnH = h * 0.7f;
            var btn = new UIButtonComponent($"{PanelDaoId}_Row{idx}_Craft", "Craft", "制作")
                .SetPosition(width - 8f - btnW * 0.5f, h * 0.5f)
                .SetSize(btnW, btnH)
                .SetButtonColor(new Color(0.12f, 0.36f, 0.10f, 0.96f));
            btn.SetText(string.Empty);
            AddBorder(btn, $"{PanelDaoId}_Row{idx}_CraftBorder", btnW * 0.5f, btnH * 0.5f, btnW, btnH, 1f,
                new Color(0.72f, 0.95f, 0.56f, 0.48f));
            btn.AddChild(BuildText(
                $"{PanelDaoId}_Row{idx}_CraftText", "CraftText", "制作",
                centerX: btnW * 0.5f, centerY: btnH * 0.5f,
                width: btnW, height: btnH,
                fontSize: 18, color: Color.white,
                align: TextAnchor.MiddleCenter));
            var capturedRecipe = recipe;
            btn.OnClick += _ => CraftRecipe(capturedRecipe);
            row.AddChild(btn);
        }

        /// <summary>在 row 内画一个 [icon][xN] 二件套。<br/>
        /// iconCenterX/labelCenterX 都是行中心约定（−rowW/2 ~ +rowW/2）。
        /// 内部转 BL 约定再交给 UI 组件。</summary>
        private static void BuildItemIcon(UIPanelComponent row, string idPrefix,
            float iconCenterX, float labelCenterX, Vector2 rowSize,
            string spritePath, int amount, Color labelColor)
        {
            // icon UIPanel 用 BL 约定：从 row-BL 到 icon-center
            var iconPanel = new UIPanelComponent($"{idPrefix}_Icon", "Icon")
                .SetPosition(rowSize.x * 0.5f + iconCenterX, rowSize.y * 0.5f)
                .SetSize(IconSize, IconSize)
                .SetBackgroundColor(string.IsNullOrEmpty(spritePath) ? new Color(0.3f, 0.3f, 0.3f, 0.6f) : Color.white)
                .SetBackgroundSpriteId(spritePath ?? string.Empty);
            row.AddChild(iconPanel);

            // 数量文本 用中心约定
            row.AddChild(BuildText(
                $"{idPrefix}_Num", "Num", $"x{amount}",
                centerX: rowSize.x * 0.5f + labelCenterX, centerY: rowSize.y * 0.5f,
                width: IconLabelW, height: rowSize.y - 8f,
                fontSize: 16, color: labelColor,
                align: TextAnchor.MiddleLeft));
        }

        /// <summary>查 InventoryItem 模板 IconSpriteId（同 InventoryUIBuilder 模式）。fallback null。</summary>
        private static string ResolveItemIcon(string itemId)
        {
            if (string.IsNullOrEmpty(itemId) || !InventoryService.HasInstance) return null;
            var template = InventoryService.Instance.InstantiateTemplate(itemId, 1);
            return template?.IconSpriteId;
        }

        private static string GetRecipeName(CraftingRecipe recipe)
        {
            if (recipe == null) return "未知配方";
            return !string.IsNullOrEmpty(recipe.DisplayName) ? recipe.DisplayName : recipe.Id;
        }

        private static string GetRecipeDescription(CraftingRecipe recipe)
        {
            if (recipe == null || string.IsNullOrEmpty(recipe.Description)) return string.Empty;
            return recipe.Description.Length <= 18 ? recipe.Description : recipe.Description.Substring(0, 18) + "...";
        }

        private static CraftOutput GetPrimaryOutput(CraftingRecipe recipe)
        {
            return recipe?.Outputs != null && recipe.Outputs.Length > 0 ? recipe.Outputs[0] : null;
        }

        private static string GetOutputDisplay(CraftOutput output)
        {
            if (output == null || string.IsNullOrEmpty(output.ItemId)) return "无产物";
            return $"{ResolveItemDisplayName(output.ItemId)} x{Mathf.Max(1, output.Count)}";
        }

        /// <summary>创建 UIText（中心约定），自动应用 ×N 超采样 —— 重构后可复用。</summary>
        private static UITextComponent BuildText(
            string id, string name, string text,
            float centerX, float centerY, float width, float height,
            int fontSize, Color color, TextAnchor align)
        {
            var t = new UITextComponent(id, name, text)
                .SetPosition(centerX, centerY)
                .SetColor(color)
                .SetAlignment(align);
            ApplySupersample(t, width, height, fontSize);
            return t;
        }

        /// <summary>uGUI Text 超采样：fontSize×N + size×N + scale×1/N，可见体积不变但 raster 在 N× 分辨率。</summary>
        private static void ApplySupersample(UITextComponent text, float width, float height, int fontSize)
        {
            var n = TextSupersample;
            var inv = 1f / n;
            text.SetSize(width * n, height * n)
                .SetFontSize(Mathf.Max(1, Mathf.RoundToInt(fontSize * n)))
                .SetScale(inv, inv);
        }

        private static void AddBorder(UIComponent parent, string id, float centerX, float centerY,
            float width, float height, float thickness, Color color)
        {
            parent.AddChild(new UIPanelComponent($"{id}_Top", "BorderTop")
                .SetPosition(centerX, centerY + height * 0.5f - thickness * 0.5f)
                .SetSize(width, thickness)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{id}_Bottom", "BorderBottom")
                .SetPosition(centerX, centerY - height * 0.5f + thickness * 0.5f)
                .SetSize(width, thickness)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{id}_Left", "BorderLeft")
                .SetPosition(centerX - width * 0.5f + thickness * 0.5f, centerY)
                .SetSize(thickness, height)
                .SetBackgroundColor(color));
            parent.AddChild(new UIPanelComponent($"{id}_Right", "BorderRight")
                .SetPosition(centerX + width * 0.5f - thickness * 0.5f, centerY)
                .SetSize(thickness, height)
                .SetBackgroundColor(color));
        }

        private static string ResolveItemDisplayName(string itemId)
        {
            // 反查 InventoryService 已注册模板的 DisplayName。fallback 用 id。
            if (!InventoryService.HasInstance) return itemId;
            var template = InventoryService.Instance.InstantiateTemplate(itemId, 1);
            return template != null && !string.IsNullOrEmpty(template.Name) ? template.Name : itemId;
        }

        private static void CraftRecipe(CraftingRecipe recipe)
        {
            if (!CraftingService.HasInstance)
            {
                ShowStatus("Crafting system is not ready", isError: true);
                return;
            }

            if (!CraftingService.Instance.TryCraft(PlayerInvId, recipe, out var result))
            {
                ShowCraftFail(recipe, result);
                return;
            }

            ShowStatus($"Crafted: {GetRecipeName(recipe)} -> {GetOutputDisplay(GetPrimaryOutput(recipe))}");
            EventProcessor.Instance.TriggerEventMethod(
                "PlaySFX", new List<object> { "Tribe/Common/Sound/harvest" });
        }

        private static void ShowCraftFail(CraftingRecipe recipe, CraftingResult result)
        {
            if (result.Code == "missing_ingredient")
            {
                ShowStatus($"Missing material: {ResolveItemDisplayName(result.MissingItemId)} need {result.RequiredCount}, have {result.AvailableCount}", isError: true);
                return;
            }

            var recipeName = GetRecipeName(recipe);
            var msg = result.Code switch
            {
                "inventory_service_missing" => "Inventory system is not ready",
                "inventory_missing" => "Player inventory does not exist",
                "output_missing" => $"Recipe has no output: {recipeName}",
                "output_template_missing" => $"Output item template is missing: {recipeName}",
                "output_no_space" => "Inventory has no space",
                "consume_failed" => "Failed to consume materials",
                _ => $"Craft failed: {recipeName}",
            };
            ShowStatus(msg, isError: true);
        }
        private static void ShowStatus(string msg, bool isError = false)
        {
            if (_statusText == null) return;
            _statusText.SetText(msg).SetColor(isError ? new Color(1f, 0.5f, 0.5f) : new Color(0.8f, 1f, 0.8f));
        }
    }
}
