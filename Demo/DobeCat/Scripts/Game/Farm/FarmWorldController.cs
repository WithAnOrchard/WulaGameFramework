using System;
using System.Collections.Generic;
using System.Text;
using EssSystem.Core.Application.MultiManagers.FarmManager;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.Presentation.UIManager.Entity;
using EssSystem.Core.Presentation.UIManager.Theme;
using UnityEngine;
using UnityEngine.UI;
using Demo.DobeCat.Game.Pet;
using Demo.DobeCat.Game;
using Demo.DobeCat.Sys.Network;
using Demo.DobeCat.Sys.UI;

namespace Demo.DobeCat.Game.Farm
{
    /// <summary>
    /// 农场世界实体控制器 —— 在游戏世界中生成 3×3 农田格子。
    /// 种子须通过商店（ShopWindow）购买，不再自动发放。
    /// </summary>
    public class FarmWorldController : MonoBehaviour
    {
        // ── 格子尺寸（世界单位）────────────────────────────────────
        public const float TileW = 0.65f, TileH = 0.44f;
        private const float Gap  = 0.05f;

        [Tooltip("3×3 网格中心的世界坐标（可在 Inspector 调整）")]
        public Vector3 FarmOrigin = new Vector3(3.8f, -3.0f, 0f);

        [Tooltip("玩家与某块农田格子中心的最大交互距离（世界单位）。进入此范围该格子变为活跃格，可用空格操作。")]
        [Min(0.1f)] public float TileInteractRange = 0.8f;

        [Header("Debug")]
        [Tooltip("开启后每帧绘制触发圆圈（Game view 开 Gizmos 即可展示，Scene view 始终可见）。")]
        public bool DebugShowRanges = true;

        // ── 农场常量 ──────────────────────────────────────────────
        internal const string FarmInstId = "farm_dobecat_001";
        private  const string FarmCfgId  = "farm_basic";

        // ── 单例 ──────────────────────────────────────────────────
        public static FarmWorldController Instance { get; private set; }

        // ── 运行时（世界对象）────────────────────────────────────
        private FarmTileObject[,] _tiles;
        private GameObject[,]     _plantGos; // 植物精灵 GO（与格子同步显隐）
        private GameObject[,]     _farmBg;   // 每格农田背景图 GO（3×3）
        private float _refreshTimer;
        private bool  _setupDone;
        private bool  _visible;
        private FarmKeyPrompt _keyPrompt;     // 已废弃：保留 GO 但始终隐藏
        private int           _hoveredRow  = -1, _hoveredCol  = -1; // 鼠标悬停格（tooltip 用）
        private int           _activeTileRow = -1, _activeTileCol = -1; // 玩家最近格（调试可视化用）

        // ── UI（全部走 UIManager / DobeCatCanvas）────────────────
        // 完整面板：背景框 + 标题栏（拖拽柄）+ 关闭按钮 + 容纳 3×3 tile 的 body 区
        private const string UI_PANEL_BG_ID  = "farm-panel-bg";
        private const string UI_TITLEBAR_ID  = "farm-titlebar";
        private const string UI_SEED_MENU_ID = "farm-seed-menu";
        private const string UI_TOOLTIP_ID   = "farm-tooltip";
        private const float  TitleBarH       = 32f;
        private const float  PanelBgPaddingPx = 14f; // 背景框相对 tile 网格的内边距（像素）

        private bool           _uiBuilt;
        private RectTransform  _panelRootRt; // 背景框根（拖拽目标、位置驱动）
        private RectTransform  _titleBarRt;  // 标题栏（拖拽柄、点击拦截区）
        private RectTransform  _seedMenuRt;
        private RectTransform  _tooltipRt;
        private UITextComponent _tooltipTextDao;
        private bool           _seedMenuOpen;
        private bool           _tooltipShown;
        private bool           _suppressNextSync; // 程序设置标题栏位置时跳过一次反算

        // 本帧左键是否被 UIManager 元素消费（避免 tile 点击与 UI 点击叠加）
        private bool _leftClickConsumed;

        // ── 生命周期 ──────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            DefaultUITheme.OnThemeChanged += RebuildPanelUI;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            DefaultUITheme.OnThemeChanged -= RebuildPanelUI;
            if (UIService.HasInstance)
            {
                UIService.Instance.DestroyUIEntity(UI_PANEL_BG_ID);
                UIService.Instance.DestroyUIEntity(UI_SEED_MENU_ID);
                UIService.Instance.DestroyUIEntity(UI_TOOLTIP_ID);
            }
        }

        /// <summary>主题切换时重建面板和 Tooltip，种子菜单下次打开时会自动用新主题色重建。</summary>
        private void RebuildPanelUI()
        {
            if (!UIService.HasInstance) return;
            UIService.Instance.DestroyUIEntity(UI_PANEL_BG_ID);
            UIService.Instance.DestroyUIEntity(UI_TOOLTIP_ID);
            _panelRootRt    = null;
            _titleBarRt     = null;
            _tooltipRt      = null;
            _tooltipTextDao = null;
            _uiBuilt        = false;
            BuildPanelUI();
            BuildTooltipUI();
            // 重建后同步位置 + 尺寸
            SetPanelScreenPosFromWorld(transform.position);
            ResizePanelToFitGrid();
            if (!_visible) SetUIActive(_panelRootRt, false);
        }

        /// <summary>切换农场格子的显小状态（供托盘菜单调用）。</summary>
        public static void ToggleVisibility()
        {
            if (Instance == null) return;
            Instance._visible = !Instance._visible;
            Instance.ApplyVisibility();
            if (Instance._visible) DobeCatGameContext.Enter();
            else                   DobeCatGameContext.Exit();
        }

        private void ApplyVisibility()
        {
            if (_tiles == null) return;
            // 世界格子 / 植物 / 背景图 / 标签：跟随显隐
            if (_farmBg != null)
                for (var r = 0; r < 3; r++)
                    for (var c = 0; c < 3; c++)
                        if (_farmBg[r, c] != null) _farmBg[r, c].SetActive(_visible);
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                {
                    if (_tiles[r, c] != null) _tiles[r, c].gameObject.SetActive(_visible);
                    if (_plantGos?[r, c] != null) _plantGos[r, c].SetActive(_visible);
                }
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("FarmLabel_")) child.gameObject.SetActive(_visible);
            }

            // UI 背景面板跟随（含标题栏与关闭按钮）；菜单 / tooltip 关闭时隐藏
            SetUIActive(_panelRootRt, _visible);
            if (!_visible)
            {
                CloseSeedMenu();
                HideTooltip();
            }

            _activeTileRow = -1; _activeTileCol = -1;
            _hoveredRow = -1; _hoveredCol = -1;
        }

        private static void SetUIActive(RectTransform rt, bool active)
        {
            if (rt != null) rt.gameObject.SetActive(active);
        }

        private void Start()
        {
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transparencySortMode = UnityEngine.TransparencySortMode.CustomAxis;
                cam.transparencySortAxis = Vector3.up;
            }
            EnsureSetup();
            // 将本身 transform 移到农场初始位置，后续拖拽只动 transform，子物体自动跟随
            transform.position = FarmOrigin;
            SpawnTiles();
            BuildPanelUI();         // 屏幕空间标题栏 + 关闭按钮（UIManager）
            BuildTooltipUI();       // 屏幕空间 tooltip（UIManager）
            // 初始隐藏（等待托盘菜单激活）
            ApplyVisibility();
            PetClickThroughDriver.AdditionalHitTests.Add(HitTestAny);
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 1f) { _refreshTimer = 0f; RefreshAll(); }
            UpdateActiveTile();

            _leftClickConsumed = false;
            // UIManager 元素本帧若被点中（拖拽/关闭/菜单按钮）通过 OnClick 回调把 _leftClickConsumed 置 true
            SyncFarmFromPanel();        // 反算背景面板屏幕位置 → 世界 transform.position
            ResizePanelToFitGrid();     // 根据镜头投影动态调整背景面板尺寸包住 3×3 网格
            HandleLeftClickTile();      // 未被 UI 消费的左键：格子上下文动作
            HandleRightClick();
            UpdateHover();
            UpdateTooltip();
            // 农场全部操作已改为鼠标，不再需要空格提示
            if (_keyPrompt != null) _keyPrompt.Refresh(FarmOrigin, false);
            if (DebugShowRanges)
            {
                DrawDebugRanges();
                if (Input.GetKeyDown(KeyCode.F5))
                {
                    PlayerDataSync.Instance?.ClearMyServerData();
                    Debug.Log("[FarmWorldController] F5: 触发服务器数据清除");
                }
            }
        }

        // ── 交互处理 ──────────────────────────────────────────────

        /// <summary>
        /// 左键点击格子：空格 → 弹种子菜单；非空 → 按优先级执行上下文动作（除虫/收获/浇水/清枯萎）。
        /// </summary>
        private void HandleLeftClickTile()
        {
            if (_leftClickConsumed) return;
            if (!_visible || _tiles == null) return;
            if (!Input.GetMouseButtonDown(0)) return;

            var screenMp = (Vector2)Input.mousePosition;
            // 点击落在标题栏 / 种子菜单 上 → 交由 UIManager 处理；body 区域不拦截，让点击透到 tile
            if (HitTestUiRect(_titleBarRt, screenMp)) return;
            if (_seedMenuOpen && HitTestUiRect(_seedMenuRt, screenMp)) return;

            var cam = Camera.main;
            if (cam == null) return;
            var mp = screenMp;
            var z  = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, z));
            var w2 = new Vector2(world.x, world.y);

            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    if (_tiles[r, c]?.HitTest(w2) == true)
                    {
                        var slot = QuerySlot(r, c);
                        if (slot != null && slot.Stage == CropGrowthStage.Empty)
                            OpenSeedMenu(r, c, mp);
                        else
                            OnTileInteract(r, c);
                        return;
                    }
            // 左键空白处 → 顺便关掉种子菜单
            _seedMenuOpen = false;
        }

        private void OnTileInteract(int row, int col)
        {
            if (!EventProcessor.HasInstance) return;
            var slot = QuerySlot(row, col);
            if (slot == null) return;
            var ep   = EventProcessor.Instance;

            // 注：种植统一改为右键弹菜单（HandleRightClick / OnGUI 处理），空格键不再触发拼种
            // 优先1：有害虫 → 除虫
            if (slot.HasPest)
            {
                ep.TriggerEventMethod(FarmManager.EVT_REMOVE_PEST,
                    new List<object> { FarmInstId, row, col });
                RefreshAll();
                return;
            }
            // 优先2：成熟 → 收获
            if (slot.Stage == CropGrowthStage.Mature)
            {
                ep.TriggerEventMethod(FarmManager.EVT_HARVEST_CROP,
                    new List<object> { FarmInstId, row, col, "player" });
                RefreshAll();
                return;
            }
            // 优先3：未浇水 → 浇水
            if (slot.Stage != CropGrowthStage.Empty && slot.Stage != CropGrowthStage.Wilted && !slot.Watered)
            {
                ep.TriggerEventMethod(FarmManager.EVT_WATER_CROP,
                    new List<object> { FarmInstId, row, col });
                RefreshAll();
                return;
            }
            // 优先4：枯萎 → 清除
            if (slot.Stage == CropGrowthStage.Wilted)
            {
                ep.TriggerEventMethod(FarmManager.EVT_CLEAR_SLOT,
                    new List<object> { FarmInstId, row, col });
                RefreshAll();
            }
        }

        // ── 命中测试（注册到 PetClickThroughDriver）────────────────

        public bool HitTestAny(Vector2 screenPos)
        {
            if (!_visible) return false;

            // 1) UI 矩形（整张背景面板 / 种子菜单 / tooltip）—屏幕空间，避免 click-through 到桌面
            if (HitTestUiRect(_panelRootRt, screenPos)) return true;
            if (_seedMenuOpen && HitTestUiRect(_seedMenuRt, screenPos)) return true;
            if (_tooltipShown && HitTestUiRect(_tooltipRt, screenPos)) return true;

            // 2) 世界格子
            var cam = Camera.main;
            if (cam == null || _tiles == null) return false;
            var z     = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
            var w2 = new Vector2(world.x, world.y);
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    if (_tiles[r, c]?.HitTest(w2) == true) return true;
            return false;
        }

        /// <summary>给定 RectTransform（ScreenSpaceOverlay）是否包含屏幕坐标点。</summary>
        private static bool HitTestUiRect(RectTransform rt, Vector2 screenPos)
        {
            if (rt == null || !rt.gameObject.activeInHierarchy) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPos, null);
        }

        // ── 初始化 ────────────────────────────────────────────────

        internal void EnsureSetup()
        {
            if (_setupDone) return;
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;

            // 注册全部作物配置 + 物品模板（幂等）
            DobeCatCropSetup.RegisterAll(ep);

            // 注册商店、货币并初始化 100 金币钱包（幂等）
            Demo.DobeCat.Game.Shop.DobeCatShopSetup.RegisterAll(ep);

            // FarmConfig（幂等）
            ep.TriggerEventMethod(FarmManager.EVT_REGISTER_FARM_CONFIG,
                new List<object> { new FarmConfig
                    { Id = FarmCfgId, DisplayName = "基础农场", InitialRows = 3, InitialCols = 3 } });

            // 生成农场（重复调用直接返回已有实例）
            ep.TriggerEventMethod(FarmManager.EVT_SPAWN_FARM,
                new List<object> { FarmCfgId, FarmOrigin, FarmInstId });

            _setupDone = true;
        }

        // ── 格子 GameObject 创建 ──────────────────────────────────

        private void SpawnTiles()
        {
            _tiles    = new FarmTileObject[3, 3];
            _plantGos = new GameObject[3, 3];

            // ── 每格农田背景图 ──────────────────────────────────────
            var bgSprite = Resources.Load<Sprite>("Sprites/farmland");
            _farmBg = new GameObject[3, 3];

            for (var r = 0; r < 3; r++)
            {
                for (var c = 0; c < 3; c++)
                {
                    var pos = TileWorldPos(r, c);

                    // 农田背景图 GO（位于格子正后方）
                    if (bgSprite != null)
                    {
                        var bgGo = new GameObject($"FarmBg_{r}_{c}");
                        bgGo.transform.SetParent(transform);
                        bgGo.transform.position = pos + new Vector3(0f, 0f, 0.05f);
                        var bgSr = bgGo.AddComponent<SpriteRenderer>();
                        bgSr.sprite       = bgSprite;
                        bgSr.sortingOrder = 0;
                        var sprSize = bgSprite.bounds.size;
                        if (sprSize.x > 0.001f && sprSize.y > 0.001f)
                            bgGo.transform.localScale = new Vector3(TileW / sprSize.x, TileH / sprSize.y, 1f);
                        _farmBg[r, c] = bgGo;
                    }

                    // 格子 GO（含 SpriteRenderer + Collider）
                    var go = new GameObject($"FarmTile_{r}_{c}");
                    go.transform.SetParent(transform);
                    go.transform.position   = pos;
                    go.transform.localScale = new Vector3(TileW, TileH, 1f);

                    var tile = go.AddComponent<FarmTileObject>();
                    tile.Init(r, c);
                    _tiles[r, c] = tile;

                    // 植物精灵 GO（独立世界坐标，位于格子中上方）
                    var plantGo = new GameObject($"FarmPlant_{r}_{c}");
                    plantGo.transform.SetParent(transform);
                    plantGo.transform.position = pos + new Vector3(0f, TileH * 0.15f, -0.01f);
                    var psr = plantGo.AddComponent<SpriteRenderer>();
                    psr.sortingOrder = 1; // 与玩家同层，交由相机 Y 轴排序处理
                    plantGo.SetActive(false);
                    tile.PlantRenderer = psr;
                    tile.PlantTransform = plantGo.transform;
                    _plantGos[r, c] = plantGo;

                    // 标签 GO（独立缩放，不继承 Tile 的 scale）
                    var labelGo = new GameObject($"FarmLabel_{r}_{c}");
                    labelGo.transform.SetParent(transform);
                    labelGo.transform.position = pos + new Vector3(0f, -TileH * 0.32f, -0.01f);
                    var tm = labelGo.AddComponent<TextMesh>();
                    tm.alignment     = TextAlignment.Center;
                    tm.anchor        = TextAnchor.MiddleCenter;
                    tm.characterSize = 0.07f;
                    tm.fontSize      = 12;
                    tm.color         = Color.white;
                    tm.text          = "";
                    tile.Label       = tm;
                }
            }

            // 按键提示组件
            _keyPrompt = gameObject.AddComponent<FarmKeyPrompt>();
        }

        private Vector3 TileWorldPos(int row, int col)
        {
            var p = transform.position;
            return new Vector3(
                p.x + (col - 1) * (TileW + Gap),
                p.y - (row - 1) * (TileH + Gap),
                p.z);
        }

        // ── 视觉刷新 ──────────────────────────────────────────────

        /// <summary>供外部（如 PlayerDataSync 还原数据后）立即触发一次视觉刷新。</summary>
        public void RefreshAllPublic() => RefreshAll();

        private void RefreshAll()
        {
            if (_tiles == null) return;
            // 农场隐藏时跳过刷新，避免 UpdateVisual 重新 SetActive(true) 植物精灵覆盖 ApplyVisibility 的隐藏
            if (!_visible) return;
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                {
                    var slot = QuerySlot(r, c);
                    CropConfig cfg = null;
                    if (slot?.CropConfigId != null)
                        DobeCatCropSetup.Configs.TryGetValue(slot.CropConfigId, out cfg);
                    _tiles[r, c]?.UpdateVisual(slot, cfg);
                }
        }

        // ── 调试可视化 ────────────────────────────────────────────────

        private void DrawDebugRanges()
        {
            if (_tiles == null) return;
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                {
                    var tp    = TileWorldPos(r, c);
                    var isActive = _activeTileRow == r && _activeTileCol == c;
                    // 触发圆圈：活跃格=绿色，其余=黄色
                    DrawCircleDebug(new Vector2(tp.x, tp.y), TileInteractRange,
                        isActive ? Color.green : Color.yellow);
                    // 格子轮廓：青色
                    DrawRectDebug(new Vector2(tp.x, tp.y), TileW, TileH, Color.cyan);
                    // 格子中心小十字
                    DrawCrossDebug(new Vector2(tp.x, tp.y), 0.05f, Color.cyan);
                }

            // 宠物位置：红色十字标
            var pet = PetAiController.Current;
            if (pet != null)
            {
                var pp = new Vector2(pet.transform.position.x, pet.transform.position.y);
                DrawCrossDebug(pp, 0.15f, Color.red);
                DrawCircleDebug(pp, 0.08f, Color.red);
            }
        }

        private static void DrawCircleDebug(Vector2 center, float radius, Color color, int seg = 24)
        {
            for (var i = 0; i < seg; i++)
            {
                var a0 = (float)i       / seg * Mathf.PI * 2f;
                var a1 = (float)(i + 1) / seg * Mathf.PI * 2f;
                var p0 = center + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius;
                var p1 = center + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius;
                Debug.DrawLine(new Vector3(p0.x, p0.y, 0f), new Vector3(p1.x, p1.y, 0f), color);
            }
        }

        private static void DrawRectDebug(Vector2 center, float w, float h, Color color)
        {
            var hw = w * 0.5f; var hh = h * 0.5f;
            var tl = new Vector3(center.x - hw, center.y + hh, 0f);
            var tr = new Vector3(center.x + hw, center.y + hh, 0f);
            var br = new Vector3(center.x + hw, center.y - hh, 0f);
            var bl = new Vector3(center.x - hw, center.y - hh, 0f);
            Debug.DrawLine(tl, tr, color);
            Debug.DrawLine(tr, br, color);
            Debug.DrawLine(br, bl, color);
            Debug.DrawLine(bl, tl, color);
        }

        private static void DrawCrossDebug(Vector2 center, float size, Color color)
        {
            Debug.DrawLine(new Vector3(center.x - size, center.y, 0f),
                           new Vector3(center.x + size, center.y, 0f), color);
            Debug.DrawLine(new Vector3(center.x, center.y - size, 0f),
                           new Vector3(center.x, center.y + size, 0f), color);
        }

        // ── 玩家活跃格检测（空格交互用）─────────────────────────────

        private void UpdateActiveTile()
        {
            if (!_visible || _tiles == null) { _activeTileRow = -1; _activeTileCol = -1; return; }
            var pet = PetAiController.Current;
            if (pet == null)               { _activeTileRow = -1; _activeTileCol = -1; return; }
            var petPos  = new Vector2(pet.transform.position.x, pet.transform.position.y);
            float best  = TileInteractRange;
            _activeTileRow = -1; _activeTileCol = -1;
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                {
                    var tp = TileWorldPos(r, c);
                    var d  = Vector2.Distance(petPos, new Vector2(tp.x, tp.y));
                    if (d < best) { best = d; _activeTileRow = r; _activeTileCol = c; }
                }
        }

        // ── 鼠标悬停检测（仅用于 tooltip 信息展示）──────────────────

        private void UpdateHover()
        {
            if (!_visible || _tiles == null) { _hoveredRow = -1; _hoveredCol = -1; return; }
            var cam = Camera.main;
            if (cam == null) { _hoveredRow = -1; _hoveredCol = -1; return; }
            var screen = (Vector2)Input.mousePosition;
            var z     = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
            _hoveredRow = -1; _hoveredCol = -1;
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    if (_tiles[r, c]?.HitTest(new Vector2(world.x, world.y)) == true)
                    { _hoveredRow = r; _hoveredCol = c; return; }
        }

        // ── Tooltip（UIManager 实现，每帧由 UpdateTooltip 驱动）────────

        private static string BuildTooltipText(FarmSlot slot, CropConfig cfg)
        {
            if (slot.Stage == CropGrowthStage.Empty)
                return "<b>空格子</b>\n左键 / 右键点击 → 弹出种子菜单选择种植";

            var sb   = new StringBuilder();
            var name = cfg?.DisplayName ?? slot.CropConfigId;
            sb.Append("<b>").Append(name).AppendLine("</b>");
            sb.Append("阶段: ").AppendLine(StageName(slot.Stage));

            if (slot.Stage == CropGrowthStage.Mature)
            {
                sb.AppendLine("<color=#7FFF00>✓ 可以收获！</color>");
            }
            else if (slot.Stage == CropGrowthStage.Wilted)
            {
                sb.AppendLine("<color=#FF6060>✗ 作物已枯萎</color>");
            }
            else
            {
                var stageIndex = (int)slot.Stage - 1;
                if (cfg?.StageDurations != null && stageIndex >= 0 && stageIndex < cfg.StageDurations.Count)
                {
                    var svc          = FarmService.Instance;
                    var duration     = cfg.StageDurations[stageIndex];
                    var nowUnix      = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var realElapsed  = (float)(nowUnix - slot.StageStartUnixSeconds);
                    var speedMult    = 1f;
                    if (slot.Watered && svc != null)                         speedMult *= svc.WateredSpeedMultiplier;
                    if (svc != null && slot.FertilizeBoostUntilUnix > nowUnix) speedMult *= svc.FertilizedSpeedMultiplier;
                    var effElapsed   = realElapsed * speedMult;
                    var progress     = duration > 0f ? Mathf.Clamp01(effElapsed / duration) : 1f;
                    var remaining    = duration > 0f ? Mathf.Max(0f, (duration - effElapsed) / speedMult) : 0f;

                    sb.Append("进度: ").Append((progress * 100f).ToString("F0")).AppendLine("%");
                    sb.Append("距下阶段: ").AppendLine(remaining > 0f ? FormatTime(remaining) : "即将推进");
                }
            }

            if (slot.Watered)                                               sb.AppendLine("💧 已浇水 (生长加速)");
            if (slot.HasPest)                                               sb.AppendLine("<color=#FF9900>🐛 有害虫 (生长停滞)</color>");
            var nowU = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (slot.FertilizeBoostUntilUnix > nowU)
            {
                var fertLeft = slot.FertilizeBoostUntilUnix - nowU;
                sb.Append("✨ 施肥中 (剩余 ").Append(FormatTime(fertLeft)).AppendLine(")");
            }

            return sb.ToString().TrimEnd();
        }

        private static string StageName(CropGrowthStage stage)
        {
            switch (stage)
            {
                case CropGrowthStage.Seed:    return "种子期";
                case CropGrowthStage.Sprout:  return "幼苗期";
                case CropGrowthStage.Growing: return "生长期";
                case CropGrowthStage.Mature:  return "成熟";
                case CropGrowthStage.Wilted:  return "枯萎";
                default:                      return "空";
            }
        }

        private static string FormatTime(float seconds)
        {
            var s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            if (s < 60) return $"{s}秒";
            var m = s / 60; var sr = s % 60;
            if (m < 60) return sr > 0 ? $"{m}分{sr}秒" : $"{m}分";
            var h = m / 60; m = m % 60;
            return m > 0 ? $"{h}时{m}分" : $"{h}时";
        }

        // ── 面板（UIManager 背景框 + 标题栏 + 关闭按钮）──────────────

        /// <summary>构建完整面板：背景框（包住 3×3 tile）+ 顶部标题栏 + ✕ 关闭按钮。</summary>
        private void BuildPanelUI()
        {
            if (_uiBuilt) return;
            if (!UIService.HasInstance) { Debug.LogWarning("[FarmWorldController] UIService 未就绪，跳过面板 UI 构建"); return; }
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) return;

            var t = DefaultUITheme.Instance.Current;
            const float BorderPx = 2f;
            var borderColor = new Color(t.Header.r, t.Header.g, t.Header.b, 0.75f);

            // 背景框 body 完全透明 —— tile 在世界空间，Canvas ScreenSpaceOverlay 盖在上方，
            // 有填充色就会遮挡 tile，改为只画 3 条细边框（左/右/底）
            var bg = new UIPanelComponent(UI_PANEL_BG_ID)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetSize(320f, 280f).SetPosition(960f, 540f);

            // 三条边框：临时尺寸，注册后用 anchor 设置自动拉伸
            bg.AddChild(new UIPanelComponent("farm-border-left")
                .SetBackgroundColor(borderColor).SetSize(BorderPx, 100f).SetPosition(0f, 100f));
            bg.AddChild(new UIPanelComponent("farm-border-right")
                .SetBackgroundColor(borderColor).SetSize(BorderPx, 100f).SetPosition(320f, 100f));
            bg.AddChild(new UIPanelComponent("farm-border-bottom")
                .SetBackgroundColor(borderColor).SetSize(320f, BorderPx).SetPosition(160f, 0f));

            // 标题栏（背景框子项；锚到顶端横向拉伸）
            var bar = new UIPanelComponent(UI_TITLEBAR_ID)
                .SetBackgroundColor(t.Header).SetSize(320f, TitleBarH)
                .SetPosition(160f, 280f - TitleBarH / 2f);

            bar.AddChild(new UITextComponent("farm-title-text", text: "🌾 农场")
                .SetSize(280f, TitleBarH).SetPosition(140f, TitleBarH / 2f)
                .SetColor(t.TextOnHeader).SetFontSize(13).SetAlignment(TextAnchor.MiddleLeft));

            var closeBtn = new UIButtonComponent("farm-close-btn", text: "✕")
                .SetSize(22f, 22f).SetPosition(320f - 16f, TitleBarH / 2f)
                .SetButtonColor(t.Close).SetFontSize(12);
            closeBtn.OnClick += _ => { _leftClickConsumed = true; ToggleVisibility(); };
            bar.AddChild(closeBtn);

            bg.AddChild(bar);

            var entity = UIService.Instance.RegisterUIEntity(UI_PANEL_BG_ID, bg, canvasT);
            if (entity == null) return;
            _panelRootRt = entity.GetComponent<RectTransform>();

            // 背景框本身不拦截左键；边框条也不拦截
            var bgImg = entity.GetComponent<Image>();
            if (bgImg != null) { bgImg.raycastTarget = false; bgImg.color = new Color(0f,0f,0f,0f); }

            // 三条边框：锚定自动拉伸（随父 sizeDelta 变化）
            void SetBorderAnchors(string id, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 offMin, Vector2 offMax)
            {
                var ent = UIService.Instance.GetUIEntity(id);
                if (ent == null) return;
                var rt = ent.GetComponent<RectTransform>();
                rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
                rt.offsetMin = offMin; rt.offsetMax = offMax;
                var img2 = ent.GetComponent<Image>(); if (img2) img2.raycastTarget = false;
            }
            SetBorderAnchors("farm-border-left",
                new Vector2(0,0), new Vector2(0,1), new Vector2(0,0.5f),
                new Vector2(0, 0), new Vector2(BorderPx, -TitleBarH));
            SetBorderAnchors("farm-border-right",
                new Vector2(1,0), new Vector2(1,1), new Vector2(1,0.5f),
                new Vector2(-BorderPx, 0), new Vector2(0, -TitleBarH));
            SetBorderAnchors("farm-border-bottom",
                new Vector2(0,0), new Vector2(1,0), new Vector2(0.5f,0),
                new Vector2(0, 0), new Vector2(0, BorderPx));

            // 标题栏：锚到顶端横向拉伸（纯视觉，不再承担拖拽逻辑）
            var titleEntity = UIService.Instance.GetUIEntity(UI_TITLEBAR_ID);
            if (titleEntity != null)
            {
                _titleBarRt = titleEntity.GetComponent<RectTransform>();
                _titleBarRt.anchorMin        = new Vector2(0f, 1f);
                _titleBarRt.anchorMax        = new Vector2(1f, 1f);
                _titleBarRt.pivot            = new Vector2(0.5f, 1f);
                _titleBarRt.sizeDelta        = new Vector2(0f, TitleBarH);
                _titleBarRt.anchoredPosition = Vector2.zero;
            }
            // 根面板挂 UIWindowBehavior：顶部拖移、四边/角缩放
            _panelRootRt.gameObject.AddComponent<UIWindowBehavior>();

            // 关闭按钮：锚到标题栏右侧
            var closeEnt = UIService.Instance.GetUIEntity("farm-close-btn");
            if (closeEnt != null)
            {
                var crt = closeEnt.GetComponent<RectTransform>();
                crt.anchorMin        = new Vector2(1f, 0.5f);
                crt.anchorMax        = new Vector2(1f, 0.5f);
                crt.pivot            = new Vector2(1f, 0.5f);
                crt.sizeDelta        = new Vector2(22f, 22f);
                crt.anchoredPosition = new Vector2(-6f, 0f);
            }

            // 标题文字：横向拉伸（留右侧给关闭按钮）
            var titleTextEnt = UIService.Instance.GetUIEntity("farm-title-text");
            if (titleTextEnt != null)
            {
                var trt = titleTextEnt.GetComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.offsetMin = new Vector2(8f, 0f);
                trt.offsetMax = new Vector2(-30f, 0f);
                var txtImg = titleTextEnt.GetComponent<Graphic>();
                if (txtImg != null) txtImg.raycastTarget = false;
            }

            // 初始定位 + 尺寸
            SetPanelScreenPosFromWorld(transform.position);
            ResizePanelToFitGrid();

            _uiBuilt = true;
        }

        /// <summary>把背景面板放到对应屏幕位置（农场中心投影 + 顶部留 TitleBarH/2 给标题栏）。</summary>
        private void SetPanelScreenPosFromWorld(Vector3 farmCenterWorld)
        {
            if (_panelRootRt == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var sp = cam.WorldToScreenPoint(farmCenterWorld);
            _panelRootRt.position = new Vector3(sp.x, sp.y + TitleBarH * 0.5f, 0f);
            _suppressNextSync = true;
        }

        /// <summary>从背景面板屏幕位置反算农场中心世界坐标并应用到 transform.position。</summary>
        private void SyncFarmFromPanel()
        {
            if (_panelRootRt == null || !_visible) return;
            if (_suppressNextSync) { _suppressNextSync = false; return; }
            var cam = Camera.main;
            if (cam == null) return;

            var sp         = (Vector2)_panelRootRt.position;
            var farmScreen = new Vector2(sp.x, sp.y - TitleBarH * 0.5f);
            var z          = Mathf.Abs(cam.transform.position.z);
            var world      = cam.ScreenToWorldPoint(new Vector3(farmScreen.x, farmScreen.y, z));
            var newPos     = new Vector3(world.x, world.y, transform.position.z);
            if ((newPos - transform.position).sqrMagnitude > 1e-8f)
            {
                transform.position = newPos;
                FarmOrigin         = newPos;
                if (_seedMenuOpen) CloseSeedMenu();
                if (_tooltipShown) HideTooltip();
            }
        }

        /// <summary>根据当前镜头投影动态调整背景面板尺寸，刚好包住 3×3 tile 网格 + 内边距 + 标题栏。</summary>
        private void ResizePanelToFitGrid()
        {
            if (_panelRootRt == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            const float gridWorldHalfW = (TileW + Gap) + TileW * 0.5f; // 3 列总半宽
            const float gridWorldHalfH = (TileH + Gap) + TileH * 0.5f; // 3 行总半高
            var c  = cam.WorldToScreenPoint(transform.position);
            var cR = cam.WorldToScreenPoint(transform.position + new Vector3(gridWorldHalfW, 0f, 0f));
            var cT = cam.WorldToScreenPoint(transform.position + new Vector3(0f, gridWorldHalfH, 0f));
            var halfPxW = Mathf.Abs(cR.x - c.x);
            var halfPxH = Mathf.Abs(cT.y - c.y);
            var w = halfPxW * 2f + PanelBgPaddingPx * 2f;
            var h = halfPxH * 2f + PanelBgPaddingPx * 2f + TitleBarH;
            _panelRootRt.sizeDelta = new Vector2(w, h);
        }

        // ── 右键种子菜单（UIManager 动态构建）────────────────────────

        /// <summary>右键检测：空格子 → 弹种子菜单；菜单已开 + 点击它处 → 关菜单。</summary>
        private void HandleRightClick()
        {
            if (!_visible) return;

            if (Input.GetMouseButtonDown(1))
            {
                var cam = Camera.main;
                if (cam == null) return;
                var mp = (Vector2)Input.mousePosition;
                var z  = Mathf.Abs(cam.transform.position.z);
                var world = cam.ScreenToWorldPoint(new Vector3(mp.x, mp.y, z));
                var w2 = new Vector2(world.x, world.y);

                int hitR = -1, hitC = -1;
                for (var r = 0; r < 3 && hitR < 0; r++)
                    for (var c = 0; c < 3; c++)
                        if (_tiles[r, c]?.HitTest(w2) == true) { hitR = r; hitC = c; break; }

                if (hitR >= 0)
                {
                    var slot = QuerySlot(hitR, hitC);
                    if (slot != null && slot.Stage == CropGrowthStage.Empty)
                    {
                        OpenSeedMenu(hitR, hitC, mp);
                        return;
                    }
                }
                CloseSeedMenu();
            }

            if (_seedMenuOpen && Input.GetKeyDown(KeyCode.Escape)) CloseSeedMenu();
        }

        // 当前打开菜单的目标格子（OnClick 回调里用）
        private int _seedMenuRow, _seedMenuCol;

        /// <summary>动态构建一个种子菜单 UIManager 面板。每次打开重建（销毁旧的）。</summary>
        private void OpenSeedMenu(int row, int col, Vector2 screenPos)
        {
            CloseSeedMenu();
            if (!UIService.HasInstance) return;
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) return;

            _seedMenuRow = row; _seedMenuCol = col;

            // 收集背包种子
            var items = new List<(string seedId, string cropId, string name, int count, string invId)>();
            var svc = InventoryService.Instance;
            if (svc != null)
            {
                foreach (var kv in DobeCatCropSetup.SeedToCropId)
                {
                    var seedId = kv.Key; var cropId = kv.Value;
                    int playerCount = svc.GetInventory("player")?.CountOf(seedId) ?? 0;
                    int hotbarCount = svc.GetInventory("hotbar")?.CountOf(seedId) ?? 0;
                    int total = playerCount + hotbarCount;
                    if (total <= 0) continue;
                    var invId = playerCount > 0 ? "player" : "hotbar";
                    DobeCatCropSetup.Configs.TryGetValue(cropId, out var cfg);
                    items.Add((seedId, cropId, cfg?.DisplayName ?? cropId, total, invId));
                }
            }

            var t        = DefaultUITheme.Instance.Current;
            const float menuW   = 200f;
            const float titleH  = 24f;
            const float rowH    = 26f;
            const float padding = 4f;
            int   rows  = Mathf.Max(1, items.Count);
            float menuH = titleH + rows * rowH + padding * 2f;

            // 钳位到屏幕内
            float x = screenPos.x;
            float y = screenPos.y - menuH; // 鼠标点是菜单左上角，向下展开
            if (x + menuW > Screen.width)  x = Screen.width - menuW - 4f;
            if (x < 4f)                    x = 4f;
            if (y < 4f)                    y = 4f;
            if (y + menuH > Screen.height) y = Screen.height - menuH - 4f;

            var root = new UIPanelComponent(UI_SEED_MENU_ID)
                .SetBackgroundColor(t.Background).SetSize(menuW, menuH)
                .SetPosition(x + menuW / 2f, y + menuH / 2f);

            // 标题
            root.AddChild(new UIPanelComponent("farm-seed-title-bg")
                .SetBackgroundColor(t.Header).SetSize(menuW, titleH)
                .SetPosition(menuW / 2f, menuH - titleH / 2f));
            root.AddChild(new UITextComponent("farm-seed-title", text: $"种植 ({row + 1},{col + 1})")
                .SetSize(menuW - 16f, titleH).SetPosition(menuW / 2f, menuH - titleH / 2f)
                .SetColor(t.TextOnHeader).SetFontSize(12).SetAlignment(TextAnchor.MiddleCenter));

            if (items.Count == 0)
            {
                root.AddChild(new UITextComponent("farm-seed-empty", text: "(背包里没有种子)")
                    .SetSize(menuW - 16f, rowH).SetPosition(menuW / 2f, menuH - titleH - rowH / 2f - padding)
                    .SetColor(t.ButtonRed).SetFontSize(11).SetAlignment(TextAnchor.MiddleCenter));
            }
            else
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    var btn = new UIButtonComponent($"farm-seed-btn-{i}", text: $"  {it.name}  ×{it.count}")
                        .SetSize(menuW - 8f, rowH - 2f)
                        .SetPosition(menuW / 2f, menuH - titleH - padding - rowH * i - rowH / 2f)
                        .SetButtonColor(t.ButtonBg).SetFontSize(12);
                    var capturedCrop = it.cropId; var capturedInv = it.invId;
                    btn.OnClick += _ =>
                    {
                        _leftClickConsumed = true;
                        PlantSeedAt(_seedMenuRow, _seedMenuCol, capturedCrop, capturedInv);
                        CloseSeedMenu();
                    };
                    root.AddChild(btn);
                }
            }

            var entity = UIService.Instance.RegisterUIEntity(UI_SEED_MENU_ID, root, canvasT);
            if (entity == null) return;
            _seedMenuRt = entity.GetComponent<RectTransform>();
            // 菜单背景拦截鼠标事件（HandleLeftClickTile 中额外做 UI 命中检查）
            var bgImg = entity.GetComponent<Image>();
            if (bgImg != null) bgImg.raycastTarget = true;
            _seedMenuOpen = true;
        }

        private void CloseSeedMenu()
        {
            if (!_seedMenuOpen && _seedMenuRt == null) return;
            if (UIService.HasInstance) UIService.Instance.DestroyUIEntity(UI_SEED_MENU_ID);
            _seedMenuRt = null;
            _seedMenuOpen = false;
        }

        /// <summary>调用 FarmManager 完成种植，从对应背包扣种子。</summary>
        private void PlantSeedAt(int row, int col, string cropId, string invId)
        {
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.TriggerEventMethod(FarmManager.EVT_PLANT_CROP,
                new List<object> { FarmInstId, row, col, cropId, invId });
            RefreshAll();
        }

        // ── Tooltip（UIManager 实现）─────────────────────────────────

        private void BuildTooltipUI()
        {
            if (_tooltipRt != null) return;
            if (!UIService.HasInstance) return;
            var canvasT = DobeCatCanvasProvider.GetOrCreate();
            if (canvasT == null) return;

            var t = DefaultUITheme.Instance.Current;
            var root = new UIPanelComponent(UI_TOOLTIP_ID)
                .SetBackgroundColor(new Color(0.05f, 0.05f, 0.05f, 0.92f))
                .SetSize(260f, 80f).SetPosition(-9999f, -9999f); // 初始在屏外

            _tooltipTextDao = new UITextComponent("farm-tooltip-text", text: "")
                .SetSize(248f, 72f).SetPosition(130f, 40f)
                .SetColor(t.TextMain).SetFontSize(12).SetAlignment(TextAnchor.UpperLeft);
            root.AddChild(_tooltipTextDao);

            var entity = UIService.Instance.RegisterUIEntity(UI_TOOLTIP_ID, root, canvasT);
            if (entity == null) return;
            _tooltipRt = entity.GetComponent<RectTransform>();
            var img = entity.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;

            // 文字：全拉伸锚定到父 Panel，父 sizeDelta 变化时自动跟随，不再错位
            var textEnt = UIService.Instance.GetUIEntity("farm-tooltip-text");
            if (textEnt != null)
            {
                var trt = textEnt.GetComponent<RectTransform>();
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(8f, 6f);
                trt.offsetMax = new Vector2(-8f, -6f);
                var txtImg = textEnt.GetComponent<Graphic>();
                if (txtImg != null) txtImg.raycastTarget = false;
            }
            entity.gameObject.SetActive(false);
        }

        private void UpdateTooltip()
        {
            if (!_visible || _tooltipRt == null) { HideTooltip(); return; }
            if (_hoveredRow < 0) { HideTooltip(); return; }
            var slot = QuerySlot(_hoveredRow, _hoveredCol);
            if (slot == null) { HideTooltip(); return; }
            CropConfig cfg = null;
            if (slot.CropConfigId != null)
                DobeCatCropSetup.Configs.TryGetValue(slot.CropConfigId, out cfg);
            var text = BuildTooltipText(slot, cfg);
            ShowTooltip(text, Input.mousePosition);
        }

        private void ShowTooltip(string text, Vector2 mouseScreenPos)
        {
            if (_tooltipRt == null) return;
            if (string.IsNullOrEmpty(text)) { HideTooltip(); return; }
            if (_tooltipTextDao != null && _tooltipTextDao.Text != text)
                _tooltipTextDao.SetText(text);
            // 简单尺寸：按行数估算
            int   lines = 1; foreach (var ch in text) if (ch == '\n') lines++;
            float h     = Mathf.Max(40f, lines * 16f + 16f);
            const float w = 260f;
            _tooltipRt.sizeDelta = new Vector2(w, h);
            // 锚到鼠标右下角；越界则翻转
            const float off = 14f;
            float x = mouseScreenPos.x + off + w / 2f;
            float y = mouseScreenPos.y - off - h / 2f;
            if (x + w / 2f > Screen.width)  x = mouseScreenPos.x - off - w / 2f;
            if (y - h / 2f < 0f)            y = mouseScreenPos.y + off + h / 2f;
            _tooltipRt.position = new Vector3(x, y, 0f);
            if (!_tooltipRt.gameObject.activeSelf) _tooltipRt.gameObject.SetActive(true);
            _tooltipShown = true;
        }

        private void HideTooltip()
        {
            if (_tooltipRt != null && _tooltipRt.gameObject.activeSelf)
                _tooltipRt.gameObject.SetActive(false);
            _tooltipShown = false;
        }

        // ── 查询 FarmService ──────────────────────────────────────

        private static FarmSlot QuerySlot(int row, int col)
        {
            if (!EventProcessor.HasInstance) return null;
            var res = EventProcessor.Instance.TriggerEventMethod(
                FarmManager.EVT_QUERY_SLOT, new List<object> { FarmInstId, row, col });
            return res != null && res.Count >= 2 ? res[1] as FarmSlot : null;
        }
    }
}
