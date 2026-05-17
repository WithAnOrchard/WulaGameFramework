using System.Collections.Generic;
using EssSystem.Core.Base;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager;
// §4.1 跨模块 InventoryManager / ResourceService 事件常量走 bare-string。
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.InventoryManager;
using EssSystem.Core.Application.SingleManagers.InventoryManager.Dao;
using EssSystem.Core.Application.SingleManagers.DialogueManager;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Runtime;
using UnityEngine.Tilemaps;
using Demo.Tribe.Player;
using Demo.Tribe.Background;
using Demo.Tribe.Enemy;
using Demo.Tribe.Resource;

namespace Demo.Tribe
{
    /// <summary>
    /// 部落 Demo 总控。
    /// <para>
    /// 继承 <see cref="AbstractGameManager"/> 复用框架的 Manager 自动发现 / 优先级初始化机制：
    /// 把任何 <c>Manager&lt;T&gt;</c> 子类挂到本 GameObject 或其子节点上，启动时即被自动接管并按
    /// <c>[Manager(N)]</c> 优先级排序初始化。
    /// </para>
    /// <para>骨架阶段：暂未定义任何业务字段 / 事件常量；后续按需求逐步往里填。</para>
    /// </summary>
    public class TribeGameManager : AbstractGameManager
    {
        // ─────────────── Inspector ───────────────
        [Header("Map (横版 2D)")]
        [Tooltip("是否生成随机地形（SideScroller Tilemap）。关闭后场景仅依靠背景图 + Fallback Ground。")]
        [SerializeField] private bool _generateTerrain = false;

        [Tooltip("MapView 在场景里的唯一 mapId（实例 Id）。仅 _generateTerrain=true 时使用。")]
        [SerializeField] private string _mapId = "TribeWorld";

        [Tooltip("地图模板的默认 ConfigId。留空 = 用 SideScrollerRandomTemplate.DefaultConfigId（=\"SideScrollerWorld\"）。")]
        [SerializeField] private string _mapConfigId = "SideScrollerWorld";

        [Header("Camera")]
        [Tooltip("启动时强制设定主相机 orthographicSize；≤0 = 不修改保留当前值。")]
        [SerializeField] private float _cameraOrthographicSize = 7f;

        [Tooltip("是否锁定镜头 Y（X 仍跟玩家）。开启后背景中心不会随玩家/地板上下。")]
        [SerializeField] private bool _lockCameraY = true;

        [Tooltip("镜头锁定 Y 值（_lockCameraY=true 时生效）。")]
        [SerializeField] private float _cameraLockY = 0f;

        [Header("Background (视差 + 循环)")]
        [Tooltip("背景图资源根目录（Resources/ 下的相对路径）。里面的所有 Sprite 会按名称顺序作为层加载。")]
        [SerializeField] private string _backgroundResourceFolder = "Tribe/Background";

        [Tooltip("前景起始索引：i 〈 _foregroundStartIndex 的层 → 玩家后面；≥的层 → 玩家前面。\n" +
                 "例：5 张图 + index=4 → 前 4 尠作背景，最后一张（5.png）作前景。")]
        [SerializeField, Min(0)] private int _foregroundStartIndex = 4;

        [Tooltip("后面背景层的 sortingOrder 基础值；后面层从小到大（越远越小）。Player 需在此上方。")]
        [SerializeField] private int _backSortingOrderBase = -100;

        [Tooltip("前面背景层的 sortingOrder 基础值；前面层从小到大（越近越大）。Player 需在此下方。")]
        [SerializeField] private int _frontSortingOrderBase = 200;

        [Tooltip("最远层的视差倍率（0 最静止）。")]
        [SerializeField, Range(0f, 1f)] private float _minParallax = 0.05f;

        [Tooltip("最近层的视差倍率（1 跟世界同步，>1 超越镜头动作越多，出现“迎面扫”感）")]
        [SerializeField, Range(0f, 2f)] private float _maxParallax = 1.5f;

        [Tooltip("“踩脚层”索引（默认 3 = 4.png）：仅用于该层的 parallax 强制=1（与世界严格同步，避免与玩家脚下世界出现仅某些层不同步的诡异）。-1 = 不启用。")]
        [SerializeField] private int _groundLayerIndex = 3;

        [Header("Floor (玩家撞撞地板)")]
        [Tooltip("关闭 _generateTerrain 后是否生成一块隐藏面板作为玩家撞撞点。")]
        [SerializeField] private bool _spawnFallbackGround = true;

        [Tooltip("地板顶面世界 Y（玩家脚下撞撞面高度）。背景图不随本值变化，仅决定玩家踩在哪个世界 Y。")]
        [SerializeField] private float _fallbackGroundY = -1f;

        [Tooltip("Fallback ground 宽度（世界单位）；需足够宽避免玩家跳出。")]
        [SerializeField, Min(1f)] private float _fallbackGroundWidth = 10000f;

        [SerializeField] private float _groundColliderYOffset = -0.1f;

        [Header("Player Spawn")]
        [Tooltip("启动时若场景内未挂 TribePlayer，自动创建一个。")]
        [SerializeField] private bool _autoSpawnPlayer = true;

        [Tooltip("Player 初始世界 X 坐标（Y 自动设在该列地表上方几格）。")]
        [SerializeField] private float _playerSpawnX = 0f;

        [Tooltip("Player 生成点相对地板的 Y 偏移（格）；正值=上方，避免初生压进地里。")]
        [SerializeField, Min(0)] private int _playerSpawnHeightAboveSurface = 3;

        protected override void Awake()
        {
            // 顺序关键：必须在 base.Awake() 之前挂载 Demo 依赖的业务 Manager，
            // AbstractGameManager.Awake 才能在发现-排序-初始化阶段把它们接管。
            EnsureDemoManagers();
            // 仅在需要生成地形时才挂 MapManager
            if (_generateTerrain) EnsureMapManagerWithTemplate(SideScrollerRandomTemplate.Id);
            base.Awake();
            Debug.Log(_generateTerrain
                ? "[TribeGameManager] Manager 初始化完成（template=side_scroller_random）"
                : "[TribeGameManager] Manager 初始化完成（未生成地形）");
        }

        protected virtual void Start()
        {
            // 不能在 Awake 里调 CreateMap：此时 ResourceService.EVT_DATA_LOADED 可能还未发出，
            // 且其它 Manager.Initialize 均需在 base.Awake 完成 → Start 阶段才安全使用 Service。
            RegisterTribeInventoryContent();
            SpawnStartupMap();
            SpawnTribeAttackableEntities();
            SpawnTribeSkeletonEnemies();
        }

        private void RegisterTribeInventoryContent()
        {
            if (!EventProcessor.HasInstance) return;

            RegisterTribeItem(
                new InventoryItem("tribe_carrot", "胡萝卜")
                    .WithDescription("新鲜的胡萝卜。")
                    .WithType(InventoryItemType.Consumable)
                    .WithIcon("Tribe/Items/Consumables/carrot")
                    .WithMaxStack(99)
                    .WithValue(3),
                new PickableItemDefinition("tribe_carrot_pickable", "tribe_carrot", "胡萝卜", "Tribe/Items/Consumables/carrot", 1));

            RegisterTribeItem(
                new InventoryItem("tribe_sunflower", "向日葵")
                    .WithDescription("面向太阳盛开的花。")
                    .WithType(InventoryItemType.Material)
                    .WithIcon("Tribe/Items/Consumables/flower_sunflower")
                    .WithMaxStack(99)
                    .WithValue(5),
                new PickableItemDefinition("tribe_sunflower_pickable", "tribe_sunflower", "向日葵", "Tribe/Items/Consumables/flower_sunflower", 1));

            RegisterTribeItem(
                new InventoryItem("tribe_red_mushroom", "红蘑菇")
                    .WithDescription("鲜红色的蘑菇。")
                    .WithType(InventoryItemType.Consumable)
                    .WithIcon("Tribe/Items/Consumables/mushroom_red")
                    .WithMaxStack(99)
                    .WithValue(4),
                new PickableItemDefinition("tribe_red_mushroom_pickable", "tribe_red_mushroom", "红蘑菇", "Tribe/Items/Consumables/mushroom_red", 1));

            RegisterTribeItem(
                new InventoryItem("tribe_berries", "浆果")
                    .WithDescription("从灌木上采下来的浆果。")
                    .WithType(InventoryItemType.Consumable)
                    .WithIcon("Tribe/Items/Consumables/berries_bush")
                    .WithMaxStack(99)
                    .WithValue(2),
                new PickableItemDefinition("tribe_berries_pickable", "tribe_berries", "浆果", "Tribe/Items/Consumables/berries_bush", 1));
        }

        private static void RegisterTribeItem(InventoryItem item, PickableItemDefinition pickableDefinition)
        {
            EventProcessor.Instance.TriggerEventMethod(
                "InventoryRegisterItem",
                new List<object> { item });
            EventProcessor.Instance.TriggerEventMethod(
                "InventoryRegisterPickableItem",
                new List<object> { pickableDefinition });
        }

        private void SpawnTribeAttackableEntities()
        {
            var y = _fallbackGroundY + 0.55f;
            var layer = GameObject.Find("Layer_3_4_BACK")?.transform;
            var root = EnsureGatherablesRoot();
            var sortingOrder = GetLayerSortingOrder(layer);
            SpawnTribeAttackableEntity("向日葵", "Tribe/Objects/Crops (sunflower)", "tribe_sunflower_pickable", new Vector3(_playerSpawnX + 3f, y - 0.2f, 0f), 1f, 1, root, sortingOrder);
            SpawnTribeAttackableEntity("红蘑菇", "Tribe/Objects/Mushroom_2", "tribe_red_mushroom_pickable", new Vector3(_playerSpawnX + 5f, y - 0.35f, 0f), 1f, 1, root, sortingOrder);
            SpawnTribeAttackableEntity("浆果丛", "Tribe/Objects/Crops (berries)", "tribe_berries_pickable", new Vector3(_playerSpawnX + 7f, y + 0.25f, 0f), 2f, 3, root, sortingOrder);
        }

        private Transform EnsureGatherablesRoot()
        {
            const string rootName = "TribeGatherables";
            var existing = GameObject.Find(rootName);
            if (existing != null) return existing.transform;
            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private static void SpawnTribeAttackableEntity(string displayName, string spriteResourcePath, string pickableId, Vector3 position, float hp, int dropAmount, Transform root, int sortingOrder)
        {
            var go = new GameObject(displayName);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 6f;
            if (root != null) go.transform.SetParent(root, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadObjectSprite(spriteResourcePath);
            sr.sortingOrder = sortingOrder;

            var collider2D = go.AddComponent<BoxCollider2D>();
            collider2D.size = Vector2.one;
            collider2D.isTrigger = true;

            var entity = go.AddComponent<PickableDropEntity>();
            entity.Configure(pickableId, hp, dropAmount, "player");
        }

        private void SpawnTribeSkeletonEnemies()
        {
            var y = _fallbackGroundY + 0.75f;
            var root = EnsureEnemiesRoot();
            var layer = GameObject.Find("Layer_3_4_BACK")?.transform;
            var sortingOrder = GetLayerSortingOrder(layer) + 2;
            SpawnTribeSkeletonEnemy(new Vector3(_playerSpawnX + 10f, y, 0f), root, sortingOrder);
        }

        private Transform EnsureEnemiesRoot()
        {
            const string rootName = "TribeEnemies";
            var existing = GameObject.Find(rootName);
            if (existing != null) return existing.transform;
            var root = new GameObject(rootName);
            root.transform.SetParent(transform, false);
            return root.transform;
        }

        private static void SpawnTribeSkeletonEnemy(Vector3 position, Transform root, int sortingOrder)
        {
            // 骷髅已统一走通用 <see cref="TribeCreature"/> + <see cref="TribeCreaturePresets.Skeleton"/> 配置。
            // RequireComponent 链会自动加 Rigidbody2D + CircleCollider2D；视觉缩放由 Visual 子节点承担。
            var go = new GameObject("TribeSkeletonEnemy");
            go.transform.position = position;
            if (root != null) go.transform.SetParent(root, true);
            var enemy = go.AddComponent<TribeCreature>();
            enemy.Configure(TribeCreaturePresets.Skeleton());
            enemy.SortingOrder = sortingOrder;
        }

        private static int GetLayerSortingOrder(Transform layer)
        {
            var renderer = layer != null ? layer.GetComponentInChildren<SpriteRenderer>() : null;
            return renderer != null ? renderer.sortingOrder : 20;
        }

        private static Sprite LoadObjectSprite(string spriteResourcePath)
        {
            var sprites = Resources.LoadAll<Sprite>(spriteResourcePath);
            if (sprites != null && sprites.Length >= 3) return sprites[2];
            if (sprites != null && sprites.Length > 0) return sprites[0];
            return Resources.Load<Sprite>(spriteResourcePath);
        }

        /// <summary>
        /// 查/建一个 <see cref="TribePlayer"/>：场景中已存在则仅调位置，
        /// 否则新建 GameObject 并挂载 <see cref="TribePlayer"/>。
        /// </summary>
        private TribePlayer EnsurePlayer(Vector3 worldPosition)
        {
            var existing = FindObjectOfType<TribePlayer>(true);
            if (existing != null)
            {
                existing.transform.position = worldPosition;
                return existing;
            }
            var go = new GameObject("TribePlayer");
            go.transform.position = worldPosition;
            return go.AddComponent<TribePlayer>();
        }

        /// <summary>
        /// 启动时创建横版地图 + MapView，同时生成 Player 并让 MapView 跟随。
        /// </summary>
        private void SpawnStartupMap()
        {
            // 先校准相机 size，再生成背景（背景 viewHeight/viewWidth 依赖此值）。
            ApplyCameraSize(Camera.main);
            // 背景：与地形无关，总是生成
            SpawnBackground(Camera.main);

            if (_generateTerrain)
            {
                SpawnTerrainAndPlayer();
            }
            else
            {
                // 无地形分支：可选 Fallback Ground + 直接生成玩家
                if (_spawnFallbackGround) EnsureFallbackGround();
                TribePlayer player = null;
                if (_autoSpawnPlayer)
                {
                    var spawnY = _fallbackGroundY + _playerSpawnHeightAboveSurface;
                    player = EnsurePlayer(new Vector3(_playerSpawnX, spawnY, 0f));
                }
                ApplyCameraLock(player);
                Debug.Log("[TribeGameManager] 启动完成（背景模式，无地形）");
            }
        }

        /// <summary>把 <see cref="_cameraLockY"/> 同步到 <see cref="TribePlayer"/>，锁定镜头 Y。</summary>
        private void ApplyCameraLock(TribePlayer player)
        {
            if (!_lockCameraY) return;
            if (player == null) player = FindObjectOfType<TribePlayer>(true);
            if (player == null) return;
            player.SetLockedCameraY(_cameraLockY, enable: true);
            Debug.Log($"[TribeGameManager] 镜头 Y 锁定={_cameraLockY:0.00}");
        }

        /// <summary>启动时强制设定主相机 orthographicSize（仅 2D 正交相机生效；&lt;=0 不修改）。</summary>
        private void ApplyCameraSize(Camera cam)
        {
            if (cam == null || _cameraOrthographicSize <= 0f) return;
            if (!cam.orthographic)
            {
                Debug.LogWarning("[TribeGameManager] 主相机不是正交模式，跳过 orthographicSize 设定");
                return;
            }
            cam.orthographicSize = _cameraOrthographicSize;
        }

        /// <summary>带地形分支。</summary>
        private void SpawnTerrainAndPlayer()
        {
            var mapManager = GetManager<MapManager>();
            if (mapManager == null || mapManager.Service == null)
            {
                Debug.LogWarning("[TribeGameManager] 未找到 MapManager，跳过地图启动");
                return;
            }

            // 触发 ResourceService 同步预加载（幂等）
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "OnResourceDataLoaded", new List<object>());

            var configId = !string.IsNullOrEmpty(_mapConfigId)
                ? _mapConfigId
                : new SideScrollerRandomTemplate().DefaultConfigId;

            var map = mapManager.Service.CreateMap(_mapId, configId);
            if (map == null)
            {
                Debug.LogWarning($"[TribeGameManager] CreateMap 失败: {_mapId}/{configId}");
                return;
            }

            var view = mapManager.Service.CreateMapView(_mapId);
            if (view == null)
            {
                Debug.LogWarning($"[TribeGameManager] CreateMapView 失败: {_mapId}");
                return;
            }

            EnsureTilemapColliders(view);

            var spawnPos = ComputePlayerSpawnPosition(mapManager, configId, view.GetComponent<Grid>());
            TribePlayer player = null;
            if (_autoSpawnPlayer)
            {
                player = EnsurePlayer(spawnPos);
                if (player != null) view.FollowTarget = player.CharacterRoot;
            }
            ApplyCameraLock(player);
            Debug.Log($"[TribeGameManager] 启动横版地图 {_mapId}（config={configId}）");
        }

        /// <summary>
        /// 加载 <see cref="_backgroundResourceFolder"/> 下的所有图片，按名称排序为层背景。
        /// <para>
        /// 背景根为顺层世界节点，ParallaxLayer 按相机位置驱动。所有层：<br/>
        /// - 缩放：纵向适配（scaledHeight = viewHeight）。<br/>
        /// - Y：中心 随镜头 Y（始终填满屏幕高度）。<br/>
        /// - X：视差 + 多副本循环（由宽高比决定副本数量）。
        /// </para>
        /// </summary>
        private void SpawnBackground(Camera cam)
        {
            if (cam == null)
            {
                Debug.LogWarning("[TribeGameManager] Camera.main 为空，跳过背景生成");
                return;
            }
            if (string.IsNullOrEmpty(_backgroundResourceFolder)) return;

            // 避免重复生成（bgRoot 现为顶层世界节点 → 用 GameObject.Find）
            const string rootName = "TribeBackground";
            var existing = GameObject.Find(rootName);
            if (existing != null) return;

            var sprites = Resources.LoadAll<Sprite>(_backgroundResourceFolder);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning($"[TribeGameManager] 背景资源 Resources/{_backgroundResourceFolder} 下未找到图片");
                return;
            }
            System.Array.Sort(sprites, (a, b) => string.Compare(a.name, b.name, System.StringComparison.OrdinalIgnoreCase));

            // 背景根 —— 顶级世界节点（独立于相机）。每个 Layer 自己根据相机位置驱动 worldX/worldY。
            var bgRoot = new GameObject(rootName);
            bgRoot.transform.position = Vector3.zero;

            var viewHeight = cam.orthographicSize * 2f;
            var viewWidth  = viewHeight * cam.aspect;

            var n = sprites.Length;
            for (var i = 0; i < n; i++)
            {
                var sp = sprites[i];
                var bw = sp.bounds.size.x;
                var bh = sp.bounds.size.y;
                if (bw <= 0f || bh <= 0f) continue;

                // 统一纵向适配：scaledHeight = viewHeight。
                var scale = viewHeight / bh;
                var scaledWidth = bw * scale;

                // 视差：远→近 minP~maxP 线性插值；踩脚层强制 = 1。
                var t = n > 1 ? (float)i / (n - 1) : 1f;
                var parallax = (i == _groundLayerIndex) ? 1f : Mathf.Lerp(_minParallax, _maxParallax, t);

                // 前/后分组：i < fgIdx 为背；≥ 为前。
                var isForeground = i >= _foregroundStartIndex;
                var orderInGroup = isForeground ? (i - _foregroundStartIndex) : i;
                var sortingOrder = (isForeground ? _frontSortingOrderBase : _backSortingOrderBase) + orderInGroup;

                // Layer 节点（受 ParallaxLayer 控制）。
                var layerGo = new GameObject($"Layer_{i}_{sp.name}_{(isForeground ? "FRONT" : "BACK")}");
                layerGo.transform.SetParent(bgRoot.transform, false);
                var pl = layerGo.AddComponent<ParallaxLayer>();
                pl.Configure(cam, parallax, scaledWidth,
                    localY: 0f, localZ: isForeground ? -1f : 10f);

                // 副本沿 X 平铺。ParallaxLayer 在 [-W, 0] 内循环。
                // 副本范围 [-K, +K] 需保证最差情况也覆盖视口：
                //   最右边沿 = camera.x + (K - 0.5)*W ≥ camera.x + viewWidth/2
                //   → K ≥ 0.5 + viewWidth/(2W)。多 +1 防边界帧抖。
                var extraCopies = Mathf.Max(1, Mathf.CeilToInt(viewWidth / (2f * scaledWidth) + 0.5f) + 1);
                for (var k = -extraCopies; k <= extraCopies; k++)
                {
                    var copy = new GameObject($"copy_{k}");
                    copy.transform.SetParent(layerGo.transform, false);
                    copy.transform.localPosition = new Vector3(k * scaledWidth, 0f, 0f);
                    copy.transform.localScale = new Vector3(scale, scale, 1f);
                    var sr = copy.AddComponent<SpriteRenderer>();
                    sr.sprite = sp;
                    sr.sortingOrder = sortingOrder;
                    sr.drawMode = SpriteDrawMode.Simple;
                }
            }
            Debug.Log($"[TribeGameManager] 背景加载完成（{n} 层，前景从 i={_foregroundStartIndex} 起，视差 {_minParallax:0.00}~{_maxParallax:0.00}，踩脚层 i={_groundLayerIndex}）");
        }

        /// <summary>无地形模式下生成一块隐藏的面板；顶面 Y = <see cref="_fallbackGroundY"/>。</summary>
        private void EnsureFallbackGround()
        {
            const string name = "FallbackGround";
            var existing = transform.Find(name);
            if (existing != null) return;

            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, _fallbackGroundY - 0.5f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(_fallbackGroundWidth, 1f);
            box.offset = new Vector2(0f, _groundColliderYOffset);
            Debug.Log($"[TribeGameManager] 生成 Fallback Ground（顶面 y={_fallbackGroundY:0.00}, width={_fallbackGroundWidth}）");
        }

        /// <summary>
        /// 用 SideScroller 生成器抽样 X 列的地表高度转为世界坐标，
        /// 让 Player 初生在地表上方 <see cref="_playerSpawnHeightAboveSurface"/> 格。
        /// </summary>
        private Vector3 ComputePlayerSpawnPosition(MapManager mapManager, string configId, Grid grid)
        {
            var ssCfg = mapManager.Service.GetConfig(configId) as SideScrollerMapConfig;
            if (ssCfg == null)
            {
                // 不是横版配置 → 仅用 X 轴，Y 默认 0
                return new Vector3(_playerSpawnX, 0f, 0f);
            }
            var gen = new SideScrollerMapGenerator(ssCfg);
            var worldX = Mathf.RoundToInt(_playerSpawnX);
            var surfaceY = gen.SampleSurfaceY(worldX);
            var tileY = surfaceY + _playerSpawnHeightAboveSurface;
            if (grid != null)
            {
                var w = grid.CellToWorld(new Vector3Int(worldX, tileY, 0));
                return new Vector3(w.x, w.y, 0f);
            }
            return new Vector3(worldX, tileY, 0f);
        }

        /// <summary>
        /// 给 <see cref="MapView"/> 下的 Tilemap GO 补 <see cref="TilemapCollider2D"/> +
        /// <see cref="CompositeCollider2D"/> + Static <see cref="Rigidbody2D"/>，
        /// 并把 TilemapCollider 接入 Composite，合并相邻方块减少 collider 数量。
        /// <para>Sky 方块未注册 RuleTile（null）不会入入 Tilemap，自然不参与撞撞。</para>
        /// </summary>
        private static void EnsureTilemapColliders(MapView view)
        {
            var tilemap = view.GetComponentInChildren<Tilemap>(true);
            if (tilemap == null)
            {
                Debug.LogWarning("[TribeGameManager] MapView 下没有 Tilemap，跳过碰撞体注入");
                return;
            }
            var go = tilemap.gameObject;

            // 顺序非常重要：
            // 1) Rigidbody2D 必须先于 CompositeCollider2D（后者 [RequireComponent(Rigidbody2D)]，
            //    如果反过来 AddComponent，Unity 会自动塞一个 Dynamic Rigidbody2D，且某些版本下
            //    GetComponent 立即查询可能拿不到 → MissingComponentException）。
            // 2) 用显式 == null 检查而非 ?? 操作符，避开 Unity fake-null 与 C# null 的差异。
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            if (rb == null)
            {
                Debug.LogError("[TribeGameManager] AddComponent<Rigidbody2D> 返回 null，无法接入碰撞体");
                return;
            }
            rb.bodyType = RigidbodyType2D.Static;

            var tc = go.GetComponent<TilemapCollider2D>();
            if (tc == null) tc = go.AddComponent<TilemapCollider2D>();
#pragma warning disable CS0618 // 2022.3 LTS 仍使用 usedByComposite；未来升级 2023+ 可改 compositeOperation
            if (tc != null) tc.usedByComposite = true;
#pragma warning restore CS0618

            var composite = go.GetComponent<CompositeCollider2D>();
            if (composite == null) composite = go.AddComponent<CompositeCollider2D>();
            if (composite != null)
            {
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
                composite.offset = new Vector2(composite.offset.x, -0.1f);
            }
        }

        /// <summary>
        /// 创建带 <paramref name="templateId"/> 的 <see cref="MapManager"/> 子节点；Inactive 先加 → SetTemplateId → SetActive。
        /// 避免 AddComponent 同步触发 Awake/Initialize 在 SetTemplateId 之前。
        /// </summary>
        private void EnsureMapManagerWithTemplate(string templateId)
        {
            var existing = GetComponentInChildren<MapManager>(true);
            if (existing != null)
            {
                existing.SetTemplateId(templateId);
                return;
            }
            var holder = new GameObject(nameof(MapManager));
            holder.SetActive(false);
            holder.transform.SetParent(transform);
            var mm = holder.AddComponent<MapManager>();
            mm.SetTemplateId(templateId);
            holder.SetActive(true);
            Debug.Log($"[TribeGameManager] 场景未挂 MapManager —— 自动创建子节点（templateId={templateId}）。");
        }

        /// <summary>
        /// 确保 Demo 需要的业务级 Manager 存在（不在
        /// <see cref="AbstractGameManager"/> 默认基础 Manager 列表里的都在这里添加）。
        /// </summary>
        private void EnsureDemoManagers()
        {
            EnsureSubManager<CharacterManager>();
            EnsureSubManager<EntityManager>();
            EnsureSubManager<InventoryManager>();
            EnsureSubManager<DialogueManager>();
        }

        /// <summary>找/建一个 <typeparamref name="T"/> Manager（在自身或子节点）。</summary>
        private void EnsureSubManager<T>() where T : MonoBehaviour
        {
            if (GetComponentInChildren<T>(true) != null) return;
            var holder = new GameObject(typeof(T).Name);
            holder.transform.SetParent(transform);
            holder.AddComponent<T>();
            Debug.Log($"[TribeGameManager] 场景未挂 {typeof(T).Name} —— 自动创建子节点挂载。");
        }
    }
}
