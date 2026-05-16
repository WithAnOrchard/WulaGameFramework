using System.Collections.Generic;
using EssSystem.Core.Base;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.SideScrollerRandom;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Runtime;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D
{
    /// <summary>
    /// 地图门面 — 挂到场景里的单例 MonoBehaviour
    /// <para>
    /// 负责生命周期、默认地图配置注册、地图根 GameObject 管理。业务逻辑在 <see cref="MapService"/>。
    /// </para>
    /// <para>
    /// Inspector 只暴露：注册开关 + 一份 <see cref="PerlinMapConfig"/> 实例（展开后可直接编辑所有
    /// Perlin / Continent / Elevation / Temperature / Moisture / River 参数）+ 视图渲染半径。
    /// </para>
    /// <para>
    /// Play 模式下可点 Editor 按钮「用当前参数重新生成」（见 <c>MapManagerEditor</c>），
    /// 无需 domain reload 即可迭代地形效果。
    /// </para>
    /// </summary>
    [Manager(12)]
    public class MapManager : Manager<MapManager>
    {
        #region Inspector

        [Header("Map Template")]
        [InspectorHelp("地图生成模板 ID。决定 MapManager 启动时调用哪个 IMapTemplate 注册 TileType / 默认 Config / Spawn 规则。\n" +
                       "内置：\n" +
                       "  • \"top_down_random\" —— 俯视 2D 随机大世界（Perlin + 群系 + 河流 + 树）\n" +
                       "  • \"side_scroller_random\" —— 横版 2D 随机地图（骨架，待实现）\n" +
                       "自定义：在业务 Manager.Initialize 中调用 MapTemplateRegistry.Register(...) 后填 ID 即可。")]
        [SerializeField] private string _templateId = TopDownRandomTemplate.Id;

        [Header("Default Templates (auto-registered)")]
        [InspectorHelp("启动时是否自动调用当前 Template 的默认注册流程（TileType + 默认 Config + Spawn 规则）。\n" +
                       "关掉后下方 _defaultConfig 仅作为「重新生成」按钮的输入，不会自动注册。")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [InspectorHelp("默认地图配置（仅 top_down_random 模板使用）。展开此对象可直接编辑所有 Perlin / Continent / Elevation / Temperature / Moisture / River 参数。\n" +
                       "ConfigId 必须与 GameManager._startupMapConfigId 保持一致（默认 'PerlinIsland'）。\n" +
                       "ChunkSize 修改后必须「重新生成」才能生效。\n" +
                       "切换其它模板时此字段会被忽略，模板自带 CreateDefaultConfig 接管默认配置生成。")]
        [SerializeField] private PerlinMapConfig _defaultConfig =
            new PerlinMapConfig("PerlinIsland", "Perlin 海陆地图");

        [Header("View Streaming")]
        [InspectorHelp("渲染半径：以焦点（默认跟随玩家）为中心，渲染 (2r+1)² 个 Chunk。\n" +
                       "• 0 = 仅中心 1 块；2 = 5x5=25 块；4 = 9x9=81 块（默认）；8 = 17x17=289 块\n" +
                       "• 单块 = ChunkSize² 个 Tile；总渲染 Tile ≈ (2r+1)² × ChunkSize²\n" +
                       "性能：半径每 +1，CPU/内存呈平方级增长。视觉无残影时尽量调小。\n" +
                       "Play 模式下拖滑条会即时同步到所有 MapView，无需重新生成。")]
        [SerializeField, Range(0, 32)] private int _renderRadius = 4;

        [InspectorHelp("预加载额外圈数：MapView.PreloadRadius = RenderRadius + 此值。\n" +
                       "在 [renderRadius+1, preloadRadius] 圈层内的 chunk 会被提前 GetOrGenerateChunk\n" +
                       "（触发持久化读盘 + 装饰器 + spawn 入队），但不画 Tilemap。\n" +
                       "玩家走入 renderRadius 时只剩画行条，避免 spawn 卡顿。\n" +
                       "默认 +2；调大可让 spawn 更早就绪，每多一圈 ~ (2r+1)² 次生成。")]
        [SerializeField, Range(0, 16)] private int _preloadExtraRadius = 2;

        [Header("Persistence / Spawn")]
        [InspectorHelp("自动写盘间隔（秒）。<= 0 关闭。默认 30s。每次最多 flush AutoSaveMaxChunksPerTick 个 dirty chunk。")]
        [SerializeField, Min(0f)] private float _autoSaveIntervalSec = 30f;

        [InspectorHelp("每次自动 flush 写盘的 chunk 上限（防 IO 雪崩）。默认 4。")]
        [SerializeField, Min(1)] private int _autoSaveMaxChunksPerTick = 4;

        [InspectorHelp("Spawn 队列每帧消费数量（每帧最多创建多少个实体）。默认 8。")]
        [SerializeField, Min(1)] private int _spawnEntitiesPerFrame = 8;


        #endregion

        /// <summary>渲染半径，运行时修改会同步到所有该 ConfigId 下的 MapView。</summary>
        public int RenderRadius
        {
            get => _renderRadius;
            set
            {
                var v = Mathf.Clamp(value, 0, 32);
                if (v == _renderRadius) return;
                _renderRadius = v;
                ApplyRenderRadiusToViews();
            }
        }

        /// <summary>
        /// 默认地图配置（Inspector 可编辑）。外部可读不可替换；修改字段后需「重新生成」或重启场景。
        /// </summary>
        public PerlinMapConfig DefaultConfig => _defaultConfig;

        /// <summary>当前选中的模板（由 <see cref="_templateId"/> 在 <see cref="Initialize"/> 时解析）。</summary>
        public IMapTemplate ActiveTemplate { get; private set; }

        /// <summary>当前 TemplateId（Inspector 字段的运行时只读快照）。</summary>
        public string TemplateId => _templateId;

        /// <summary>
        /// 在 <see cref="Initialize"/> 之前由更高优先级的业务 Manager（如 <c>AbstractGameManager</c>）
        /// 用代码覆写本字段，省去手动改 Inspector。<see cref="Initialize"/> 已经跑过之后再调用本方法不会
        /// 重新初始化模板，仅更新字段方便诊断。
        /// </summary>
        public void SetTemplateId(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return;
            if (_templateId == templateId) return;
            _templateId = templateId;
            if (ActiveTemplate != null)
                LogWarning($"SetTemplateId('{templateId}') 在 Initialize 之后调用，仅更新字段；如需切换模板请重启场景。");
        }

        /// <summary>底层 Service（同等于 MapService.Instance，但 Inspector 里可见）</summary>
        public MapService Service => MapService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            // ① 启动时注册内置模板（业务侧可在自身 Manager.Initialize 里调 MapTemplateRegistry.Register 补充自定义）
            RegisterBuiltInTemplates();

            // ② 解析当前 Template
            ActiveTemplate = MapTemplateRegistry.Get(_templateId);
            if (ActiveTemplate == null)
            {
                LogWarning($"未找到 MapTemplate '{_templateId}'，回退到 '{TopDownRandomTemplate.Id}'");
                ActiveTemplate = MapTemplateRegistry.Get(TopDownRandomTemplate.Id);
            }

            // ③ 调用模板默认注册：TileType 必需；Config / Spawn 规则可选
            ActiveTemplate?.RegisterDefaultTileTypes(Service);
            if (_registerDebugTemplates) RegisterDefaultConfigs();

            // ④ 持久化 / spawn 参数同步到 Service
            if (Service != null)
            {
                Service.AutoSaveIntervalSec = _autoSaveIntervalSec;
                Service.AutoSaveMaxChunksPerTick = _autoSaveMaxChunksPerTick;
            }
            EntitySpawnService.Instance.EntitiesPerFrame = _spawnEntitiesPerFrame;

            // ⑤ 注册 spawn 装饰器（priority=300，介于地形装饰 100~200 与结构 400+ 之间）
            Service?.RegisterDecorator(new EntitySpawnDecorator());

            // ⑥ 交由模板注册默认 spawn 规则
            if (_registerDebugTemplates)
                ActiveTemplate?.RegisterDefaultSpawnRules(EntitySpawnService.Instance);

            Log($"MapManager 初始化完成 (template={ActiveTemplate?.TemplateId ?? "<null>"})", Color.green);
        }

        /// <summary>在 Registry 中登记框架内置模板。重复调用不会产生事件冑余。</summary>
        private static void RegisterBuiltInTemplates()
        {
            if (!MapTemplateRegistry.Contains(TopDownRandomTemplate.Id))
                MapTemplateRegistry.Register(new TopDownRandomTemplate());
            if (!MapTemplateRegistry.Contains(SideScrollerRandomTemplate.Id))
                MapTemplateRegistry.Register(new SideScrollerRandomTemplate());
        }

        protected override void Update()
        {
            base.Update();   // 保留 Inspector 同步节流逻辑
            // spawn 队列分帧消费 + 自动写盘
            EntitySpawnService.Instance.TickSpawnQueue(IsChunkLoadedFor);
            Service?.AutoSaveTick(Time.deltaTime);
        }

        /// <summary>提供给 EntitySpawnService 校验：spawn 请求的目标 chunk 是否仍加载（避免给已卸载的区块创建实体）。</summary>
        private bool IsChunkLoadedFor(string mapId, int cx, int cy) =>
            Service?.GetMap(mapId)?.PeekChunk(cx, cy) != null;

        protected override void OnApplicationQuit()
        {
            // 同步 flush 全部 dirty chunk —— 后台 Task.Run 此刻可能被进程杀死，必须同步
            try { Service?.SaveAllDirtyChunksAllMaps(sync: true); }
            catch { /* swallow */ }
            base.OnApplicationQuit();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // 移动端切后台 / 编辑器丢焦：异步 flush 一次防数据丢失
            if (!hasFocus) Service?.SaveAllDirtyChunksAllMaps(sync: false);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        #endregion

        #region Defaults Registration

        /// <summary>
        /// 注册当前模板的默认 Config。
        /// <para>top_down_random 优先使用 Inspector 的 <see cref="_defaultConfig"/>（需 ConfigId 与模板 DefaultConfigId 一致）；
        /// 其它模板走 <see cref="IMapTemplate.CreateDefaultConfig"/>。</para>
        /// </summary>
        private void RegisterDefaultConfigs()
        {
            if (Service == null || ActiveTemplate == null) return;

            // top-down 分支：Inspector 接管参数（保留原有可调体验）
            if (ActiveTemplate.TemplateId == TopDownRandomTemplate.Id
                && _defaultConfig != null && !string.IsNullOrEmpty(_defaultConfig.ConfigId))
            {
                if (Service.GetConfig(_defaultConfig.ConfigId) != null) return;
                Service.RegisterConfig(CloneConfig(_defaultConfig));
                return;
            }

            // 其它模板交由 Template.CreateDefaultConfig
            var cfg = ActiveTemplate.CreateDefaultConfig();
            if (cfg == null || string.IsNullOrEmpty(cfg.ConfigId))
            {
                LogWarning($"Template '{ActiveTemplate.TemplateId}' 未返回有效默认 Config");
                return;
            }
            if (Service.GetConfig(cfg.ConfigId) != null) return;
            Service.RegisterConfig(cfg);
        }

        /// <summary>
        /// 通过 JsonUtility 做一次浅拷贝，把 Inspector 里的可变实例解耦为 Service 内的独立副本。
        /// 仅对 <see cref="PerlinMapConfig"/> 的 [Serializable] public 字段生效，无反射开销。
        /// </summary>
        private static PerlinMapConfig CloneConfig(PerlinMapConfig src)
        {
            var json = JsonUtility.ToJson(src);
            return JsonUtility.FromJson<PerlinMapConfig>(json);
        }

        #endregion

        #region Spawn Rules (legacy demo, unused)

        // 默认按群系分布的小/中型树 Spawn 规则已迁移至
        // Dao/Templates/TopDownRandom/TopDownRandomTemplate.cs::RegisterDefaultSpawnRules。
        // 以下代码作为历史示例块保留（#if false 隔离），业务侧不会调用。
#if false
        private void _RegisterDefaultSpawnRules_LEGACY()
        {
            const string mapConfigId = "PerlinIsland";
            // 已存在同名规则集则跳过（业务侧或前序流程可能已注册）
            var existing = EntitySpawnService.Instance.GetRuleSets(mapConfigId);
            for (int i = 0; i < existing.Count; i++)
            {
                if (existing[i] != null && existing[i].Id == "default") return;
            }

            const string SmallTreeId  = "SmallTreeEntity";
            const string MediumTreeId = "MediumTreeEntity";

            var set = new EntitySpawnRuleSet("default")
                // ─── 热带雨林：最密，大小树叠加 ────────────────────────
                .WithRule(new EntitySpawnRule("rainforest_medium", MediumTreeId)
                    .WithTileTypes(TileTypes.Rainforest)
                    .WithMoistureRange(0.55f, 1.0f)
                    .WithDensity(0.10f)
                    .WithMaxPerChunk(28)
                    .WithMinSpacing(1)
                    .WithPriority(290)
                    .WithRngTag("def_rainforest_med"))
                .WithRule(new EntitySpawnRule("rainforest_small", SmallTreeId)
                    .WithTileTypes(TileTypes.Rainforest)
                    .WithMoistureRange(0.5f, 1.0f)
                    .WithDensity(0.06f)
                    .WithMaxPerChunk(20)
                    .WithPriority(295)
                    .WithRngTag("def_rainforest_small"))

                // ─── 温带森林：密度高，主干为中型树 ───────────────────
                .WithRule(new EntitySpawnRule("forest_medium", MediumTreeId)
                    .WithTileTypes(TileTypes.Forest)
                    .WithMoistureRange(0.4f, 1.0f)
                    .WithDensity(0.06f)
                    .WithMaxPerChunk(18)
                    .WithMinSpacing(1)
                    .WithPriority(300)
                    .WithRngTag("def_forest_med"))
                .WithRule(new EntitySpawnRule("forest_small", SmallTreeId)
                    .WithTileTypes(TileTypes.Forest)
                    .WithMoistureRange(0.35f, 1.0f)
                    .WithDensity(0.05f)
                    .WithMaxPerChunk(16)
                    .WithPriority(305)
                    .WithRngTag("def_forest_small"))

                // ─── 针叶林：冷温带，聚簇 ─────────────────────────────
                .WithRule(new EntitySpawnRule("taiga_medium", MediumTreeId)
                    .WithTileTypes(TileTypes.Taiga)
                    .WithTemperatureRange(0.0f, 0.45f)
                    .WithMoistureRange(0.3f, 1.0f)
                    .WithDensity(0.05f)
                    .WithCluster(2, 3, 2)
                    .WithMaxPerChunk(14)
                    .WithMinSpacing(1)
                    .WithPriority(310)
                    .WithRngTag("def_taiga_med"))
                .WithRule(new EntitySpawnRule("taiga_small", SmallTreeId)
                    .WithTileTypes(TileTypes.Taiga)
                    .WithTemperatureRange(0.0f, 0.5f)
                    .WithMoistureRange(0.25f, 1.0f)
                    .WithDensity(0.04f)
                    .WithMaxPerChunk(12)
                    .WithPriority(315)
                    .WithRngTag("def_taiga_small"))

                // ─── 沼泽：仅中型，散落 ────────────────────────────────
                .WithRule(new EntitySpawnRule("swamp_medium", MediumTreeId)
                    .WithTileTypes(TileTypes.Swamp)
                    .WithMoistureRange(0.6f, 1.0f)
                    .WithDensity(0.025f)
                    .WithMaxPerChunk(8)
                    .WithMinSpacing(2)
                    .WithPriority(320)
                    .WithRngTag("def_swamp_med"))

                // ─── 草原：仅小树，稀疏点缀 ────────────────────────────
                .WithRule(new EntitySpawnRule("grassland_small", SmallTreeId)
                    .WithTileTypes(TileTypes.Grassland)
                    .WithMoistureRange(0.3f, 0.85f)
                    .WithDensity(0.012f)
                    .WithMaxPerChunk(5)
                    .WithMinSpacing(2)
                    .WithPriority(330)
                    .WithRngTag("def_grass_small"))

                // ─── 稀树草原：极稀疏，单棵孤立 ─────────────────────
                .WithRule(new EntitySpawnRule("savanna_small", SmallTreeId)
                    .WithTileTypes(TileTypes.Savanna)
                    .WithMoistureRange(0.2f, 0.6f)
                    .WithDensity(0.006f)
                    .WithMaxPerChunk(3)
                    .WithMinSpacing(3)
                    .WithPriority(340)
                    .WithRngTag("def_savanna_small"));

            EntitySpawnService.Instance.RegisterRuleSet(mapConfigId, set);
        }
#endif

        #endregion

        #region Regenerate (Editor 调试 / 运行时调用)

        /// <summary>
        /// 用当前 Inspector 参数覆盖默认配置，并重建该配置下所有地图实例 + MapView。
        /// <para>仅在 Play 模式下有效（依赖 <see cref="MapService"/> 单例）。</para>
        /// </summary>
        [ContextMenu("Regenerate Map (using current params)")]
        public void RegenerateDefaultMap()
        {
            if (!UnityEngine.Application.isPlaying)
            {
                Log("RegenerateDefaultMap 仅在 Play 模式下有效", Color.yellow);
                return;
            }
            if (Service == null)
            {
                LogWarning("RegenerateDefaultMap: Service 不可用");
                return;
            }
            if (_defaultConfig == null || string.IsNullOrEmpty(_defaultConfig.ConfigId))
            {
                LogWarning("RegenerateDefaultMap: _defaultConfig 为空或缺 ConfigId");
                return;
            }

            var configId = _defaultConfig.ConfigId;

            // ① 用当前 Inspector 参数覆盖默认配置
            Service.RegisterConfig(CloneConfig(_defaultConfig));

            // ② 找到所有引用此 ConfigId 的运行时地图，记录视图参数后逐个重建
            var targets = new List<(string mapId, int budget, Transform follow, Transform parent)>();
            foreach (var map in Service.GetAllMaps())
            {
                if (map == null || map.ConfigId != configId) continue;
                var view = Service.GetMapView(map.MapId);
                Transform follow = null, parent = null;
                var budget = 1;
                if (view != null)
                {
                    follow = view.FollowTarget;
                    parent = view.transform.parent;
                    budget = view.ChunksPerFrame;
                }
                targets.Add((map.MapId, budget, follow, parent));
            }

            if (targets.Count == 0)
            {
                Log($"未找到使用 ConfigId='{configId}' 的地图实例，仅更新了配置。" +
                    $"下次 CreateMap 将使用新参数。", Color.yellow);
                return;
            }

            foreach (var t in targets)
            {
                Service.DestroyMap(t.mapId);
                Service.CreateMap(t.mapId, configId);
                var view = Service.CreateMapView(t.mapId, t.parent);
                if (view != null)
                {
                    view.RenderRadius = _renderRadius;
                    view.PreloadRadius = _renderRadius + _preloadExtraRadius;
                    view.ChunksPerFrame = t.budget;
                    view.FollowTarget = t.follow;
                }
            }
            Log($"已用当前参数重建 {targets.Count} 个地图实例 (ConfigId='{configId}', radius={_renderRadius})", Color.cyan);
        }

        /// <summary>把当前 _renderRadius / _preloadExtraRadius 同步给所有该 ConfigId 下的 MapView（不重建数据）。
        /// ConfigId 优先取 <see cref="_defaultConfig"/>（top-down），其次取 <see cref="ActiveTemplate"/>.DefaultConfigId。</summary>
        private void ApplyRenderRadiusToViews()
        {
            if (!UnityEngine.Application.isPlaying || Service == null) return;
            var configId = ResolveActiveConfigId();
            if (string.IsNullOrEmpty(configId)) return;
            foreach (var map in Service.GetAllMaps())
            {
                if (map == null || map.ConfigId != configId) continue;
                var view = Service.GetMapView(map.MapId);
                if (view == null) continue;
                view.RenderRadius = _renderRadius;
                view.PreloadRadius = _renderRadius + _preloadExtraRadius;
            }
        }

        /// <summary>解析当前 Inspector / Template 所指的默认 ConfigId。</summary>
        private string ResolveActiveConfigId()
        {
            if (_defaultConfig != null && !string.IsNullOrEmpty(_defaultConfig.ConfigId)
                && ActiveTemplate?.TemplateId == TopDownRandomTemplate.Id)
                return _defaultConfig.ConfigId;
            return ActiveTemplate?.DefaultConfigId;
        }

#if UNITY_EDITOR
        /// <summary>Inspector 中拖动 RenderRadius 滑条时实时同步到 MapView。</summary>
        private void OnValidate()
        {
            if (UnityEngine.Application.isPlaying) ApplyRenderRadiusToViews();
        }
#endif

        #endregion
    }
}
