using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Application.SingleManagers.InventoryManager;

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

        private static UIPanelComponent _panel;
        private static UITextComponent _statusText;

        /// <summary>uGUI Text 模糊主因：默认像素分辨率渲染。×N 超采样 → N× 字号 + N× 尺寸 + 1/N 缩放，
        /// 视觉体积不变但字体在 N× 分辨率 raster。与 InventoryUIBuilder 保持一致。</summary>
        private const float TextSupersample = 6f;

        /// <summary>1920×1080 参考分辨率下画布中心（与 InventoryManager 面板同一参考系）。</summary>
        private static readonly Vector2 CanvasCenter = new Vector2(960f, 540f);

        // ─── 配方表 ────────────────────────────────────────────
        private struct Recipe
        {
            public string Name;
            public (string id, int amount)[] Inputs;
            public (string id, int amount) Output;
            public string OutputDisplay;
        }

        private static readonly Recipe[] Recipes = new[]
        {
            new Recipe
            {
                Name = "杂菇汤",
                Inputs = new (string,int)[] { ("tribe_red_mushroom", 2), ("tribe_berries", 1) },
                Output = ("tribe_carrot", 3),
                OutputDisplay = "胡萝卜 x3"
            },
            new Recipe
            {
                Name = "果味浆",
                Inputs = new (string,int)[] { ("tribe_berries", 4) },
                Output = ("tribe_red_mushroom", 1),
                OutputDisplay = "红蘑菇 x1"
            },
            new Recipe
            {
                Name = "野菜烩",
                Inputs = new (string,int)[] { ("tribe_carrot", 2), ("tribe_sunflower", 1) },
                Output = ("tribe_berries", 5),
                OutputDisplay = "浆果 x5"
            },
        };

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

            _panel = BuildPanel();
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity", new List<object> { PanelDaoId, _panel });
        }

        public static void Close()
        {
            if (!EventProcessor.HasInstance) return;
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
        // 坐标约定（与 UIEntityFactory / UITextEntity 对齐）：
        //   • UIPanel / UIButton：anchor=(0,0)/(0,0), pivot=(0.5,0.5)
        //     → anchoredPosition = 从父节点左下角 到 自身中心 的偏移（BL 约定）
        //   • UIText：anchor=(0.5,0.5), pivot=(0.5,0.5)
        //     → anchoredPosition = 从父节点中心 到 自身中心 的偏移（中心约定）
        // 问题修正：原代码错把 UIPanel/UIButton 的位置也当中心约定用 → 都打到左下角。
        private static UIPanelComponent BuildPanel()
        {
            const float W = 640f, H = 420f;

            // 根面板：BL 约定 → 面板中心 = canvas-BL + (CanvasCenter)
            var panel = new UIPanelComponent(PanelDaoId, "TribeCraftingPanel")
                .SetPosition(CanvasCenter.x, CanvasCenter.y)
                .SetSize(W, H)
                .SetBackgroundColor(new Color(0.10f, 0.08f, 0.06f, 0.92f));

            // 标题（UIText 用中心约定：从面板中心出发）—— 顶部 36px。
            panel.AddChild(BuildText(
                $"{PanelDaoId}_Title", "Title", "篝火 · 制作",
                centerX: 0f, centerY: H * 0.5f - 36f,
                width: W, height: 50f,
                fontSize: 28, color: new Color(1f, 0.85f, 0.4f),
                align: TextAnchor.MiddleCenter));

            // 关闭按钮（UIButton 用 BL 约定：从面板左下角出发）—— 右上角 32px 内缩
            var closeBtn = new UIButtonComponent($"{PanelDaoId}_Close", "Close", "×")
                .SetPosition(W - 32f, H - 32f)
                .SetSize(48f, 48f)
                .SetButtonColor(new Color(0.5f, 0.2f, 0.2f, 1f));
            closeBtn.OnClick += _ => Close();
            panel.AddChild(closeBtn);

            // 配方行（在面板中间垂直布局）
            const float rowH = 80f, rowGap = 12f;
            // 顶行中心 Y（从面板 BL 起算）：顶部 - 标题区 110
            var rowCenterYTop = H - 110f - rowH * 0.5f;
            for (var i = 0; i < Recipes.Length; i++)
            {
                var rowCenterY = rowCenterYTop - i * (rowH + rowGap);
                BuildRecipeRow(panel, Recipes[i], i, rowCenterY, W - 40f, rowH);
            }

            // 底部状态栏
            _statusText = BuildText(
                $"{PanelDaoId}_Status", "Status", "材料不足时会提示在这里。",
                centerX: 0f, centerY: -H * 0.5f + 30f,
                width: W - 40f, height: 40f,
                fontSize: 16, color: new Color(0.7f, 0.7f, 0.7f),
                align: TextAnchor.MiddleCenter);
            panel.AddChild(_statusText);

            return panel;
        }

        // 行布局（width=600, h=80）：
        // ┌─────────┬─────────────────────────┬───────────┬────────┐
        // │ 配方名  │ [icon×N] [icon×N] ...   │ → [icon×N]│ [制作] │
        // │  120    │      ~280               │   ~80     │   80   │
        // └─────────┴─────────────────────────┴───────────┴────────┘
        // 子 UIPanel/UIButton 用 BL 约定（从 row-BL 到自身中心），UIText 用中心约定（从 row-center）。
        private const float IconSize = 44f;       // 图标方块边长
        private const float IconLabelW = 32f;     // 图标右侧 "x N" 文本宽度
        private const float IconGroupGap = 6f;    // 图标 + 数量 与下一组之间的间距

        private static void BuildRecipeRow(UIPanelComponent parent, Recipe recipe, int idx, float rowCenterY, float width, float h)
        {
            // 行容器（面板中心水平：parentW = W = 640，行 center.x = parentW/2 = 320）
            const float parentW = 640f;
            var row = new UIPanelComponent($"{PanelDaoId}_Row{idx}", $"Row_{idx}")
                .SetPosition(parentW * 0.5f, rowCenterY)
                .SetSize(width, h)
                .SetBackgroundColor(new Color(0.18f, 0.14f, 0.10f, 1f));
            parent.AddChild(row);

            // 1) 配方名 —— 左侧 120 区（UIText 中心约定）
            const float namePaneW = 120f;
            var nameCenterX = -width * 0.5f + namePaneW * 0.5f + 8f;
            row.AddChild(BuildText(
                $"{PanelDaoId}_Row{idx}_Name", "Name", recipe.Name,
                centerX: nameCenterX, centerY: 0f,
                width: namePaneW, height: h - 8f,
                fontSize: 20, color: new Color(1f, 0.95f, 0.7f),
                align: TextAnchor.MiddleCenter));

            // 2) 输入图标组 —— 从配方名右边开始；制作按钮 + 输出 (~180) 留在右侧
            const float btnW = 96f;
            const float outputW = 90f;       // [→] + 输出图标 + 数量
            var inputsStartX = -width * 0.5f + namePaneW + 16f;
            var inputsEndX   =  width * 0.5f - btnW - 8f - outputW - 8f;

            var groupW = IconSize + IconLabelW + IconGroupGap;
            var inputsAllW = recipe.Inputs.Length * groupW - IconGroupGap;
            var inputsCenterX = (inputsStartX + inputsEndX) * 0.5f;
            var firstGroupCenterX = inputsCenterX - inputsAllW * 0.5f + groupW * 0.5f;

            for (var i = 0; i < recipe.Inputs.Length; i++)
            {
                var (id, amount) = recipe.Inputs[i];
                var groupCenterX = firstGroupCenterX + i * groupW;
                BuildItemIcon(row, $"{PanelDaoId}_Row{idx}_In{i}",
                    iconCenterX: groupCenterX - IconLabelW * 0.5f,
                    labelCenterX: groupCenterX + IconSize * 0.5f,
                    rowSize: new Vector2(width, h),
                    spritePath: ResolveItemIcon(id),
                    amount: amount,
                    labelColor: Color.white);
            }

            // 3) 输出区：→ + icon + 数量
            var outputCenterX = inputsEndX + outputW * 0.5f;
            row.AddChild(BuildText(
                $"{PanelDaoId}_Row{idx}_Arrow", "Arrow", "→",
                centerX: outputCenterX - outputW * 0.5f + 12f, centerY: 0f,
                width: 24f, height: h - 8f,
                fontSize: 22, color: new Color(0.7f, 1f, 0.7f),
                align: TextAnchor.MiddleCenter));

            BuildItemIcon(row, $"{PanelDaoId}_Row{idx}_Out",
                iconCenterX: outputCenterX,
                labelCenterX: outputCenterX + IconSize * 0.5f + IconLabelW * 0.5f,
                rowSize: new Vector2(width, h),
                spritePath: ResolveItemIcon(recipe.Output.id),
                amount: recipe.Output.amount,
                labelColor: new Color(0.6f, 1f, 0.6f));

            // 4) 制作按钮（UIButton BL 约定）
            var btnH = h * 0.7f;
            var btn = new UIButtonComponent($"{PanelDaoId}_Row{idx}_Craft", "Craft", "制作")
                .SetPosition(width - 8f - btnW * 0.5f, h * 0.5f)
                .SetSize(btnW, btnH)
                .SetButtonColor(new Color(0.35f, 0.55f, 0.30f));
            var capturedRecipe = recipe;
            btn.OnClick += _ => TryCraft(capturedRecipe);
            row.AddChild(btn);
        }

        /// <summary>在 row 内画一个 [icon][xN] 二件套。<br/>
        /// iconCenterX/labelCenterX 都是行中心约定（−rowW/2 ~ +rowW/2）。
        /// 内部转 BL 约定再交给 UIPanelComponent。</summary>
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
                centerX: labelCenterX, centerY: 0f,
                width: IconLabelW, height: rowSize.y - 8f,
                fontSize: 16, color: labelColor,
                align: TextAnchor.MiddleLeft));
        }

        /// <summary>查 InventoryItem 模板 IconSpriteId（同 InventoryUIBuilder 模式）。fallback null。</summary>
        private static string ResolveItemIcon(string itemId)
        {
            if (!InventoryService.HasInstance) return null;
            var template = InventoryService.Instance.InstantiateTemplate(itemId, 1);
            return template?.IconSpriteId;
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

        private static string ResolveItemDisplayName(string itemId)
        {
            // 反查 InventoryService 已注册模板的 DisplayName。fallback 用 id。
            if (!InventoryService.HasInstance) return itemId;
            var template = InventoryService.Instance.InstantiateTemplate(itemId, 1);
            return template != null && !string.IsNullOrEmpty(template.Name) ? template.Name : itemId;
        }

        // ─── 制作执行 ────────────────────────────────────────────
        private static void TryCraft(Recipe recipe)
        {
            if (!InventoryService.HasInstance) { ShowStatus("InventoryService 未就绪"); return; }
            var inv = InventoryService.Instance.GetInventory(PlayerInvId);
            if (inv == null) { ShowStatus("玩家容器不存在"); return; }

            // 1) 校验材料足够
            foreach (var (id, amount) in recipe.Inputs)
            {
                var have = inv.CountOf(id);
                if (have < amount)
                {
                    ShowStatus($"材料不足：{ResolveItemDisplayName(id)} 需 {amount}，当前 {have}", isError: true);
                    return;
                }
            }

            // 2) 消耗（走事件 → InventoryService 自动广播 EVT_CHANGED 让 UI 刷新）
            foreach (var (id, amount) in recipe.Inputs)
            {
                EventProcessor.Instance.TriggerEventMethod(
                    InventoryService.EVT_REMOVE,
                    new List<object> { PlayerInvId, id, amount });
            }

            // 3) 产出
            EventProcessor.Instance.TriggerEventMethod(
                InventoryService.EVT_ADD,
                new List<object> { PlayerInvId, recipe.Output.id, recipe.Output.amount });

            // 4) 反馈：提示 + 音效
            ShowStatus($"制作成功：{recipe.Name} → {recipe.OutputDisplay}");
            EventProcessor.Instance.TriggerEventMethod(
                "PlaySFX", new List<object> { "Tribe/Common/Sound/harvest" });
        }

        private static void ShowStatus(string msg, bool isError = false)
        {
            if (_statusText == null) return;
            _statusText.SetText(msg).SetColor(isError ? new Color(1f, 0.5f, 0.5f) : new Color(0.8f, 1f, 0.8f));
        }
    }
}
