using System.Collections.Generic;
using EssSystem.Core.Base;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager;
using EssSystem.Core.EssManagers.Foundation.ResourceManager;
using Demo.DayNight.Map;
using Demo.DayNight.Map.Config;
using Demo.DayNight.Player;

namespace Demo.DayNight
{
    /// <summary>
    /// 昼夜求生模式总控（Demo）。
    /// <para>
    /// 玩法骨架：场景固定一个核心据点，玩家需要在多个 <b>昼-夜</b> 循环中存活。
    /// </para>
    /// <list type="bullet">
    /// <item><b>昼</b>：低强度刷怪、可搜资源、修工事、补给；时长 <see cref="_dayDuration"/></item>
    /// <item><b>夜</b>：波次刷怪 / 据点防御战；时长 <see cref="_nightDuration"/></item>
    /// <item>过若干轮后进入 <b>BOSS 夜</b> 收尾</item>
    /// </list>
    /// <para>
    /// 继承 <see cref="AbstractGameManager"/> 复用框架的 Manager 自动发现 / 优先级初始化机制；
    /// 后续昼夜调度、波次、据点血量等子系统挂同一 GameObject 或子节点上即可被自动接管。
    /// </para>
    /// </summary>
    public class DayNightGameManager : AbstractGameManager
    {
        // ─────────────────────────────────────────────────────────────
        #region Event 名常量

        /// <summary>
        /// 昼夜阶段切换 **广播**（用 <c>[EventListener]</c> 订阅）。
        /// <para>参数顺序：<c>[bool isNight, int round, bool isBossNight]</c></para>
        /// <para>无返回（广播事件）。</para>
        /// </summary>
        public const string EVT_PHASE_CHANGED = "DayNightPhaseChanged";

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Inspector

        [Header("Cycle 昼夜循环")]
        [Tooltip("白天阶段时长（秒）")]
        [SerializeField, Min(1f)] private float _dayDuration = 60f;

        [Tooltip("夜晚阶段时长（秒）")]
        [SerializeField, Min(1f)] private float _nightDuration = 120f;

        [Tooltip("第几轮夜晚触发 BOSS 夜（从 1 开始计数）；<= 0 表示无尽模式")]
        [SerializeField] private int _bossNightRound = 5;

        [Tooltip("启动时立刻进入夜晚（调试用）")]
        [SerializeField] private bool _startAtNight = false;

        [Header("Map")]
        [Tooltip("使用的地图实例 ID")]
        [SerializeField] private string _mapId = "world1";

        [Tooltip("地图模式（决定 MapConfig ID 与对应 Template）。默认 Island = 有界海岛")]
        [SerializeField] private DayNightMapMode _mapMode = DayNightMapMode.Island;

        [Tooltip("当 _mapMode = Custom 时使用的自定义 MapConfig ID；其它模式忽略")]
        [SerializeField] private string _customMapConfigId = "";

        [Header("Player")]
        [Tooltip("启动时自动在海岛中心创建 DayNightPlayer（战士 Character + 相机跟随 + ChunkInfoOverlay）。")]
        [SerializeField] private bool _autoSpawnPlayer = true;

        /// <summary>解析后的实际 MapConfig ID（按 <see cref="_mapMode"/> 派发）。</summary>
        public string MapConfigId => _mapMode.ToConfigId(_customMapConfigId);

        /// <summary>解析后的 MapTemplate ID；Custom 模式返回 null，表示不主动改 MapManager 的 TemplateId。</summary>
        public string MapTemplateId => _mapMode.ToTemplateId();

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Runtime State

        /// <summary>当前是否夜晚。</summary>
        public bool IsNight { get; private set; }

        /// <summary>当前所处的昼夜回合（从 1 开始；昼+夜算同一 Round）。</summary>
        public int CurrentRound { get; private set; } = 1;

        /// <summary>当前阶段已经过的秒数。</summary>
        public float PhaseElapsed { get; private set; }

        /// <summary>当前阶段的总时长（秒）。</summary>
        public float PhaseDuration => IsNight ? _nightDuration : _dayDuration;

        /// <summary>当前阶段剩余秒数（永远非负）。</summary>
        public float PhaseRemaining => Mathf.Max(0f, PhaseDuration - PhaseElapsed);

        /// <summary>当前是否处于 BOSS 夜。</summary>
        public bool IsBossNight => IsNight && _bossNightRound > 0 && CurrentRound >= _bossNightRound;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        protected override void Awake()
        {
            // 注意：必须在 base.Awake() 之前同步 TemplateId 并自建 Demo 依赖的 Manager。
            // AbstractGameManager.Awake 内部会按优先级触发各 Manager.Initialize；
            // MapManager.Initialize 一旦跑完，再改 TemplateId 也没用了。
            SyncMapTemplateBeforeInit();
            EnsureDemoManagers();
            base.Awake();
            Debug.Log($"[DayNightGameManager] 模式管理器初始化完成（mapMode={_mapMode}）");
        }

        /// <summary>
        /// 确保 Demo 用到的 <see cref="CharacterManager"/> / <see cref="EntityManager"/>
        /// 等业务级 Manager 在场景中存在（不在 AbstractGameManager.EnsureBaseManagers 默认列表）。
        /// 任何未挂的 Manager 都会自动建一个子节点挂上，业务零配置。
        /// </summary>
        private void EnsureDemoManagers()
        {
            EnsureSubManager<CharacterManager>();
            EnsureSubManager<EntityManager>();
            EnsureSubManager<InventoryManager>();
        }

        /// <summary>找/建一个 <typeparamref name="T"/> Manager（在自身或子节点）。</summary>
        private void EnsureSubManager<T>() where T : MonoBehaviour
        {
            if (GetComponentInChildren<T>(true) != null) return;
            var holder = new GameObject(typeof(T).Name);
            holder.transform.SetParent(transform);
            holder.AddComponent<T>();
            Debug.Log($"[DayNightGameManager] 场景未挂 {typeof(T).Name} —— 自动创建子节点挂载。");
        }

        /// <summary>
        /// 在 MapManager.Initialize 之前把 <see cref="MapTemplateId"/> 写到它的 Inspector 字段，
        /// 让 dropdown 选择直接驱动模板切换，无需手动改 MapManager.Inspector。
        /// </summary>
        private void SyncMapTemplateBeforeInit()
        {
            var templateId = MapTemplateId;
            if (string.IsNullOrEmpty(templateId)) return; // Custom 模式：交给玩家手动配 MapManager
            // 不能用 GetManager<>() —— AbstractGameManager 还没扫描；这里直接 GetComponentInChildren 找
            var mapManager = GetComponentInChildren<MapManager>(true);
            if (mapManager == null)
            {
                // MapManager 不在 AbstractGameManager.EnsureBaseManagers 自动列表里。
                // 时序关键：Inactive 创建 → AddComponent（此时不触发 Awake）→ SetTemplateId → SetActive(true) → 才 Awake。
                // 否则 AddComponent 会立刻同步触发 Awake/Initialize，SetTemplateId 就来不及生效。
                var holder = new GameObject(nameof(MapManager));
                holder.SetActive(false);
                holder.transform.SetParent(transform);
                mapManager = holder.AddComponent<MapManager>();
                mapManager.SetTemplateId(templateId);
                holder.SetActive(true);
                Debug.Log($"[DayNightGameManager] 场景未挂 MapManager —— 自动创建子节点 '{holder.name}' 并挂载（templateId={templateId}）。");
                return;
            }
            mapManager.SetTemplateId(templateId);
        }

        protected virtual void Start()
        {
            SpawnStartupMap();
            EnterPhase(_startAtNight);
        }

        /// <summary>
        /// 用 Inspector 中的 <see cref="_mapId"/> + <see cref="_mapMode"/> 解析出的 ConfigId 创建并挂载地图视图。
        /// 默认 <see cref="DayNightMapMode.Island"/> = <see cref="Demo.DayNight.Map.IslandSurvivalTemplate"/>
        /// 注册的 <c>"DayNightIsland"</c>（MapManager 的 TemplateId 也需要切到 <c>"day_night_island"</c>）。
        /// </summary>
        private void SpawnStartupMap()
        {
            var configId = MapConfigId;
            if (string.IsNullOrEmpty(_mapId) || string.IsNullOrEmpty(configId))
            {
                Debug.LogWarning($"[DayNightGameManager] _mapId / MapConfigId 为空（mode={_mapMode}），跳过地图启动");
                return;
            }

            var mapManager = GetManager<MapManager>();
            if (mapManager == null || mapManager.Service == null)
            {
                Debug.LogWarning("[DayNightGameManager] 未找到 MapManager，跳过地图启动");
                return;
            }

            // 触发 ResourceService 同步预加载（与 GameManager 一致；幂等）
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    ResourceService.EVT_DATA_LOADED, new List<object>());

            var map = mapManager.Service.CreateMap(_mapId, configId);
            if (map == null)
            {
                Debug.LogWarning($"[DayNightGameManager] CreateMap 失败: {_mapId}/{configId}");
                return;
            }

            var view = mapManager.Service.CreateMapView(_mapId);
            if (view == null) return;

            // Island 模式：在 MapView 根节点挂个 IslandBoundary，给玩家加海岸碰撞墙
            Vector3? islandCenter = null;
            if (_mapMode == DayNightMapMode.Island)
            {
                var islandCfg = mapManager.Service.GetConfig(configId) as IslandSurvivalMapConfig;
                if (islandCfg != null)
                {
                    var grid = view.GetComponent<Grid>();
                    if (grid != null)
                    {
                        var boundaryGo = new GameObject("IslandBoundary");
                        boundaryGo.transform.SetParent(grid.transform, false);
                        var boundary = boundaryGo.AddComponent<IslandBoundary>();
                        boundary.Build(islandCfg, grid);

                        // 海岛中心 → tile 坐标 → 世界坐标，用作玩家初始位置
                        var c = Demo.DayNight.Map.Generator.IslandSurvivalGenerator.GetWorldCenter(islandCfg);
                        var cw = grid.CellToWorld(new Vector3Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y), 0));
                        islandCenter = new Vector3(cw.x, cw.y, 0f);
                    }
                    else
                    {
                        Debug.LogWarning("[DayNightGameManager] MapView 没有 Grid 组件，跳过 IslandBoundary");
                    }
                }
            }

            // 创建玩家 + 让 MapView 流式渲染跟随玩家
            if (_autoSpawnPlayer)
            {
                var player = SpawnPlayer(islandCenter ?? Vector3.zero);
                if (player != null) view.FollowTarget = player.CharacterRoot;
            }

            Debug.Log($"[DayNightGameManager] 启动地图 {_mapId}（mode={_mapMode}, config={configId}）");
        }

        /// <summary>
        /// 在 <paramref name="worldPosition"/> 创建一个 DayNightPlayer GO（已存在则返回已有那个）。
        /// 返回的 <see cref="DayNightPlayer"/> 自带战士 Character + ChunkInfoOverlay + 相机跟随。
        /// </summary>
        private DayNightPlayer SpawnPlayer(Vector3 worldPosition)
        {
            var existing = FindObjectOfType<DayNightPlayer>();
            if (existing != null)
            {
                existing.transform.position = worldPosition;
                return existing;
            }
            var go = new GameObject("DayNightPlayer");
            go.transform.position = worldPosition;
            return go.AddComponent<DayNightPlayer>();
        }

        protected virtual void Update()
        {
            TickPhase(Time.deltaTime);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Phase Driver

        /// <summary>切换到指定阶段并重置计时。</summary>
        private void EnterPhase(bool night)
        {
            IsNight = night;
            PhaseElapsed = 0f;
            Debug.Log($"[DayNightGameManager] 进入 {(night ? "夜晚" : "白天")} | 第 {CurrentRound} 轮 | 时长 {PhaseDuration:F0}s" +
                      (IsBossNight ? "（BOSS 夜）" : string.Empty));

            // 广播给 WaveSpawn / BaseDefense / Hud 等订阅方
            if (EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEvent(EVT_PHASE_CHANGED,
                    new List<object> { IsNight, CurrentRound, IsBossNight });
            }
        }

        /// <summary>主循环驱动：累计 PhaseElapsed，到点切换昼夜。</summary>
        private void TickPhase(float dt)
        {
            PhaseElapsed += dt;
            if (PhaseElapsed < PhaseDuration) return;

            if (IsNight)
            {
                // 夜 → 昼，回合 +1
                CurrentRound++;
                EnterPhase(night: false);
            }
            else
            {
                // 昼 → 夜，回合不变
                EnterPhase(night: true);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>立即跳到下一阶段（调试 / cheat 用）。</summary>
        public void SkipPhase()
        {
            PhaseElapsed = PhaseDuration;
        }

        /// <summary>重置回合数到 1，回到白天起始状态。</summary>
        public void ResetCycle()
        {
            CurrentRound = 1;
            EnterPhase(night: false);
        }

        #endregion
    }
}
