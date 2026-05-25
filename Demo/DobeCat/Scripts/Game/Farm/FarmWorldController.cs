using System;
using System.Collections.Generic;
using System.Text;
using EssSystem.Core.Application.MultiManagers.FarmManager;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;
using EssSystem.Core.Base.Event;
using UnityEngine;
using Demo.DobeCat.Game.Pet;
using Demo.DobeCat.Game;
using Demo.DobeCat.Sys.Network;

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

        // ── 运行时 ────────────────────────────────────────────────
        private FarmTileObject[,] _tiles;
        private GameObject[,]     _plantGos; // 植物精灵 GO（与格子同步显隐）
        private GameObject[,]     _farmBg;   // 每格农田背景图 GO（3×3）
        private float _refreshTimer;
        private bool  _setupDone;
        private bool  _wasSpace;
        private bool  _visible;
        private FarmKeyPrompt _keyPrompt;     // 格子默认隐藏，由托盘菜单切换
        private int           _hoveredRow  = -1, _hoveredCol  = -1; // 鼠标悬停（仅用于 tooltip）
        private int           _activeTileRow = -1, _activeTileCol = -1; // 玩家最近格（用于空格交互）
        private GUIStyle      _tooltipStyle;

        // ── 生命周期 ──────────────────────────────────────────────

        private void Awake() => Instance = this;
        private void OnDestroy() { if (Instance == this) Instance = null; }

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
            if (_farmBg != null)
                for (var r = 0; r < 3; r++)
                    for (var c = 0; c < 3; c++)
                        if (_farmBg[r, c] != null) _farmBg[r, c].SetActive(_visible);
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                {
                    if (_tiles[r, c] != null) _tiles[r, c].gameObject.SetActive(_visible);
                    // 植物精灵 GO 单独管理（UpdateVisual 内部会按作物阶段决定是否激活）
                    if (_plantGos?[r, c] != null) _plantGos[r, c].SetActive(_visible);
                }
            // 隐藏农场时同步隐藏按键提示
            if (!_visible && _keyPrompt != null) _keyPrompt.Refresh(FarmOrigin, false);
            _activeTileRow = -1; _activeTileCol = -1;
            // 标签 GO 展居在 FarmWorldController.transform 下，一起切换
            for (var i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("FarmLabel_")) child.gameObject.SetActive(_visible);
            }
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
            SpawnTiles();
            // 初始隐藏（等待托盘菜单激活）
            ApplyVisibility();
            PetClickThroughDriver.AdditionalHitTests.Add(HitTestAny);
        }

        private void Update()
        {
            _refreshTimer += Time.deltaTime;
            if (_refreshTimer >= 1f) { _refreshTimer = 0f; RefreshAll(); }
            UpdateActiveTile();
            HandleSpace();
            RefreshKeyPrompt();
            UpdateHover();
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

        /// <summary>空格 —— 通用交互键，对玩家当前活跃格执行操作。</summary>
        private void HandleSpace()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var sp = (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(
                         Demo.DobeCat.Sys.Platform.Windows.Win32Native.VK_SPACE) & 0x8000) != 0;
#else
            var sp = Input.GetKey(KeyCode.Space);
#endif
            if (!sp || _wasSpace) { _wasSpace = sp; return; }
            _wasSpace = sp;
            TryInteractActiveTile();
        }

        /// <summary>对当前活跃格（玩家最近格）执行交互，无需鼠标。</summary>
        private void TryInteractActiveTile()
        {
            if (_activeTileRow < 0) return;
            OnTileInteract(_activeTileRow, _activeTileCol);
        }

        private void OnTileInteract(int row, int col)
        {
            if (!EventProcessor.HasInstance) return;
            var slot = QuerySlot(row, col);
            if (slot == null) return;
            var ep   = EventProcessor.Instance;
            var held = HotbarSelectionDriver.HeldItem;

            // 优先1：有害虫 → 除虫
            if (slot.HasPest)
            {
                ep.TriggerEventMethod(FarmManager.EVT_REMOVE_PEST,
                    new List<object> { FarmInstId, row, col });
                RefreshAll();
                return;
            }
            // 优先2：空格 + 手持种子 → 种植
            if (slot.Stage == CropGrowthStage.Empty && held != null
                && DobeCatCropSetup.SeedToCropId.TryGetValue(held.Id, out var cropId))
            {
                ep.TriggerEventMethod(FarmManager.EVT_PLANT_CROP,
                    new List<object> { FarmInstId, row, col, cropId, "hotbar" }); // 种子在快捷栏库存
                RefreshAll();
                return;
            }
            // 优先3：成熟 → 收获
            if (slot.Stage == CropGrowthStage.Mature)
            {
                ep.TriggerEventMethod(FarmManager.EVT_HARVEST_CROP,
                    new List<object> { FarmInstId, row, col, "player" });
                RefreshAll();
                return;
            }
            // 优先4：未浇水 → 浇水
            if (slot.Stage != CropGrowthStage.Empty && slot.Stage != CropGrowthStage.Wilted && !slot.Watered)
            {
                ep.TriggerEventMethod(FarmManager.EVT_WATER_CROP,
                    new List<object> { FarmInstId, row, col });
                RefreshAll();
                return;
            }
            // 优先5：枯萎 → 清除
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
            var cam = Camera.main;
            if (cam == null || _tiles == null) return false;
            var z     = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
            for (var r = 0; r < 3; r++)
                for (var c = 0; c < 3; c++)
                    if (_tiles[r, c]?.HitTest(new Vector2(world.x, world.y)) == true) return true;
            return false;
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

        private Vector3 TileWorldPos(int row, int col) =>
            new Vector3(
                FarmOrigin.x + (col - 1) * (TileW + Gap),
                FarmOrigin.y - (row - 1) * (TileH + Gap),
                FarmOrigin.z);

        // ── 视觉刷新 ──────────────────────────────────────────────

        /// <summary>供外部（如 PlayerDataSync 还原数据后）立即触发一次视觉刷新。</summary>
        public void RefreshAllPublic() => RefreshAll();

        private void RefreshAll()
        {
            if (_tiles == null) return;
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

        // ── Tooltip 渲染 ──────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible || _hoveredRow < 0) return;
            var slot = QuerySlot(_hoveredRow, _hoveredCol);
            if (slot == null) return;
            CropConfig cfg = null;
            if (slot.CropConfigId != null)
                DobeCatCropSetup.Configs.TryGetValue(slot.CropConfigId, out cfg);

            var text = BuildTooltipText(slot, cfg);
            if (string.IsNullOrEmpty(text)) return;

            if (_tooltipStyle == null)
            {
                _tooltipStyle = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize  = 13,
                    padding   = new RectOffset(10, 10, 8, 8),
                    wordWrap  = false,
                    richText  = true
                };
                _tooltipStyle.normal.textColor      = Color.white;
                _tooltipStyle.normal.background     = MakeTex(2, 2, new Color(0.08f, 0.08f, 0.08f, 0.88f));
            }

            var content = new GUIContent(text);
            var size    = _tooltipStyle.CalcSize(content);
            var mp = Event.current.mousePosition;
            const float off = 16f;
            var x = mp.x + off;
            var y = mp.y + off;
            if (x + size.x > Screen.width)  x = mp.x - size.x - off;
            if (y + size.y > Screen.height) y = mp.y - size.y - off;
            GUI.Box(new Rect(x, y, size.x, size.y), content, _tooltipStyle);
        }

        private static string BuildTooltipText(FarmSlot slot, CropConfig cfg)
        {
            if (slot.Stage == CropGrowthStage.Empty)
                return "<b>空格子</b>\n手持种子并按空格键即可种植";

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

        private static Texture2D MakeTex(int w, int h, Color col)
        {
            var tex = new Texture2D(w, h);
            var pix = new Color[w * h];
            for (var i = 0; i < pix.Length; i++) pix[i] = col;
            tex.SetPixels(pix);
            tex.Apply();
            return tex;
        }

        // ── 按键提示刷新 ──────────────────────────────────────────

        private void RefreshKeyPrompt()
        {
            if (_keyPrompt == null || !_visible) return;

            // 没有活跃格 → 隐藏提示
            if (_activeTileRow < 0) { _keyPrompt.Refresh(FarmOrigin, false); return; }

            var slot = QuerySlot(_activeTileRow, _activeTileCol);
            if (slot == null)       { _keyPrompt.Refresh(FarmOrigin, false); return; }

            // 活跃格有可操作内容 → 显示提示
            var held = HotbarSelectionDriver.HeldItem;
            bool actionable =
                slot.HasPest
                || slot.Stage == CropGrowthStage.Mature
                || slot.Stage == CropGrowthStage.Wilted
                || (slot.Stage == CropGrowthStage.Empty && held != null
                    && DobeCatCropSetup.SeedToCropId.ContainsKey(held.Id))
                || (slot.Stage != CropGrowthStage.Empty
                    && slot.Stage != CropGrowthStage.Wilted
                    && !slot.Watered);

            // 提示图标锁定到玩家头顶
            var pet2 = PetAiController.Current;
            var promptPos = pet2 != null
                ? pet2.transform.position
                : TileWorldPos(_activeTileRow, _activeTileCol);
            _keyPrompt.Refresh(promptPos, actionable);
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
