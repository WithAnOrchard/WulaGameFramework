using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Util;
using EssSystem.EssManager.MapManager.Dao;
using EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Config;
using EssSystem.EssManager.MapManager.Runtime;

namespace EssSystem.EssManager.MapManager
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

        [Header("Default Templates (auto-registered)")]
        [InspectorHelp("启动时是否自动用下方配置注册一个默认 PerlinMapConfig。\n" +
                       "关掉后下方配置仅作为「重新生成」按钮的输入，不会自动注册。")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [InspectorHelp("默认地图配置。展开此对象可直接编辑所有 Perlin / Continent / Elevation / Temperature / Moisture / River 参数。\n" +
                       "ConfigId 必须与 GameManager._startupMapConfigId 保持一致（默认 'PerlinIsland'）。\n" +
                       "ChunkSize 修改后必须「重新生成」才能生效。\n" +
                       "新增参数直接去 PerlinMapConfig.cs 加字段 + [InspectorHelp]，无需再动本文件。")]
        [SerializeField] private PerlinMapConfig _defaultConfig =
            new PerlinMapConfig("PerlinIsland", "Perlin 海陆地图");

        [Header("View Streaming")]
        [InspectorHelp("渲染半径：以焦点（默认跟随玩家）为中心，渲染 (2r+1)² 个 Chunk。\n" +
                       "• 0 = 仅中心 1 块；2 = 5x5=25 块；4 = 9x9=81 块（默认）；8 = 17x17=289 块\n" +
                       "• 单块 = ChunkSize² 个 Tile；总渲染 Tile ≈ (2r+1)² × ChunkSize²\n" +
                       "性能：半径每 +1，CPU/内存呈平方级增长。视觉无残影时尽量调小。\n" +
                       "Play 模式下拖滑条会即时同步到所有 MapView，无需重新生成。")]
        [SerializeField, Range(0, 32)] private int _renderRadius = 4;

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

        /// <summary>底层 Service（同等于 MapService.Instance，但 Inspector 里可见）</summary>
        public MapService Service => MapService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            RegisterDefaultTileTypes();
            if (_registerDebugTemplates) RegisterDefaultConfigs();

            Log("MapManager 初始化完成", Color.green);
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
        /// 注册默认 TileType 元数据：海洋类共用 Water RuleTile，陆地类共用 Ground RuleTile。
        /// 后续接入美术资源时，逐个把 RuleTileResourceId 替换为对应贴图即可，
        /// 不需要改生成器或 BiomeClassifier。
        /// </summary>
        private void RegisterDefaultTileTypes()
        {
            var ocean = TileTypes.DefaultOceanRuleTile;
            var land = TileTypes.DefaultLandRuleTile;
            var desert = TileTypes.DefaultDesertRuleTile;

            // 兼容旧 ID
            Service.RegisterTileType(new TileTypeDef(TileTypes.Ocean, "海洋", ocean));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Land, "陆地", land));

            // 海洋分层
            Service.RegisterTileType(new TileTypeDef(TileTypes.DeepOcean, "深海", ocean));
            Service.RegisterTileType(new TileTypeDef(TileTypes.ShallowOcean, "浅海", ocean));

            // 河流 / 湖泊：用水 RuleTile 先顶，后续替换为专用贴图
            Service.RegisterTileType(new TileTypeDef(TileTypes.River, "河流", ocean));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Lake, "湖泊", ocean));

            // 陆地：地形高度类
            // 海滩复用沙地贴图（与沙漠同源），视觉上和草地/森林区分开。
            Service.RegisterTileType(new TileTypeDef(TileTypes.Beach, "海滩", desert));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Hill, "丘陵", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Mountain, "山地", land));
            // 雪峰：用沙地(浅色)作基底，Biome debug 色会 tint 成冷白；
            // 若用 land(嫩绿)基底，乘法 tint 出来永远偏绿，看不出"苍白"。
            Service.RegisterTileType(new TileTypeDef(TileTypes.SnowPeak, "雪峰", desert));

            // 陆地：生物群系
            Service.RegisterTileType(new TileTypeDef(TileTypes.Tundra, "苔原", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Taiga, "针叶林", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Grassland, "草原", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Forest, "温带森林", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Swamp, "沼泽", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Desert, "沙漠", desert));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Savanna, "稀树草原", land));
            Service.RegisterTileType(new TileTypeDef(TileTypes.Rainforest, "热带雨林", land));
        }

        /// <summary>
        /// 注册默认 Perlin 地图配置。若同 ConfigId 已存在则不覆盖（业务层可先注册自己的）。
        /// Inspector 的 <see cref="_defaultConfig"/> 会被 JsonUtility 克隆一份写入 Service，
        /// 避免运行时修改 Inspector 字段误触已注册实例。
        /// </summary>
        private void RegisterDefaultConfigs()
        {
            if (_defaultConfig == null || string.IsNullOrEmpty(_defaultConfig.ConfigId))
            {
                LogWarning("RegisterDefaultConfigs: _defaultConfig 为空或缺 ConfigId");
                return;
            }
            if (Service.GetConfig(_defaultConfig.ConfigId) != null) return;
            Service.RegisterConfig(CloneConfig(_defaultConfig));
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

        #region Regenerate (Editor 调试 / 运行时调用)

        /// <summary>
        /// 用当前 Inspector 参数覆盖默认配置，并重建该配置下所有地图实例 + MapView。
        /// <para>仅在 Play 模式下有效（依赖 <see cref="MapService"/> 单例）。</para>
        /// </summary>
        [ContextMenu("Regenerate Map (using current params)")]
        public void RegenerateDefaultMap()
        {
            if (!Application.isPlaying)
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
                    view.ChunksPerFrame = t.budget;
                    view.FollowTarget = t.follow;
                }
            }
            Log($"已用当前参数重建 {targets.Count} 个地图实例 (ConfigId='{configId}', radius={_renderRadius})", Color.cyan);
        }

        /// <summary>把当前 _renderRadius 同步给所有该 ConfigId 下的 MapView（不重建数据）。</summary>
        private void ApplyRenderRadiusToViews()
        {
            if (!Application.isPlaying || Service == null) return;
            if (_defaultConfig == null) return;
            var configId = _defaultConfig.ConfigId;
            foreach (var map in Service.GetAllMaps())
            {
                if (map == null || map.ConfigId != configId) continue;
                var view = Service.GetMapView(map.MapId);
                if (view != null) view.RenderRadius = _renderRadius;
            }
        }

#if UNITY_EDITOR
        /// <summary>Inspector 中拖动 RenderRadius 滑条时实时同步到 MapView。</summary>
        private void OnValidate()
        {
            if (Application.isPlaying) ApplyRenderRadiusToViews();
        }
#endif

        #endregion
    }
}
