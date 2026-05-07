using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Util;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao.Templates;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao.Templates.DefaultVoxel;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D
{
    /// <summary>
    /// 3D 体素地图门面 —— 与 2D <c>MapManager</c> 平行的单例 MonoBehaviour。
    /// <para>负责生命周期、模板/默认配置/默认 BlockType 注册。业务逻辑在 <see cref="Voxel3DMapService"/>。</para>
    /// <para>Inspector 暴露：模板选择 + 默认 <see cref="VoxelMapConfig"/>（可直接编辑 Seed/ChunkSize/SeaLevel/...）。</para>
    /// <para>**Phase 4 完成**：chunk 持久化（<c>VoxelMapPersistenceService</c>）+ Spawn 子系统
    /// （<c>VoxelEntitySpawnService</c> + <c>VoxelEntitySpawnDecorator</c>）均已接入，
    /// 与 2D <c>MapManager</c>+<c>EntitySpawnService</c> 架构完全对齐。</para>
    /// </summary>
    [Manager(13)]
    public class Voxel3DMapManager : Manager<Voxel3DMapManager>
    {
        #region Inspector

        [Header("Voxel Map Template")]
        [InspectorHelp("体素地图生成模板 ID。决定 Voxel3DMapManager 启动时调用哪个 IVoxelMapTemplate 注册 BlockType / 默认 Config / 默认装饰器。\n" +
                       "内置：\n" +
                       "  • \"default_voxel_3d\" —— 默认 Perlin heightmap + 7 块基本方块\n" +
                       "自定义：在业务 Manager.Initialize 中调用 VoxelMapTemplateRegistry.Register(...) 后填 ID 即可。")]
        [SerializeField] private string _templateId = DefaultVoxelTemplate.Id;

        [Header("Default Templates (auto-registered)")]
        [InspectorHelp("启动时是否自动调用当前 Template 的默认注册流程（BlockType + 默认 Config + 默认装饰器）。\n" +
                       "关掉后下方 _defaultConfig 仅作为「重新生成」按钮的输入，不会自动注册。")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [InspectorHelp("默认体素地图配置。展开此对象可直接编辑 Seed / ChunkSize / SeaLevel / 地形参数 / 雪线 等。\n" +
                       "ConfigId 默认 'default_voxel_3d'，与 DefaultVoxelTemplate.DefaultConfigId 一致；改 Seed 立刻得到新世界。\n" +
                       "切换 TemplateId 后此字段会被忽略，模板的 CreateDefaultConfig 接管默认配置生成。")]
        [SerializeField] private VoxelMapConfig _defaultConfig =
            new VoxelMapConfig(DefaultVoxelTemplate.Id, "Default Voxel 3D");

        [Header("Persistence")]
        [InspectorHelp("自动写盘间隔（秒）。<= 0 关闭。默认 30s。每次最多 flush AutoSaveMaxChunksPerTick 个 dirty chunk。")]
        [SerializeField, Min(0f)] private float _autoSaveIntervalSec = 30f;

        [InspectorHelp("每次自动 flush 写盘的 chunk 上限（防 IO 雪崩）。默认 4。")]
        [SerializeField, Min(1)] private int _autoSaveMaxChunksPerTick = 4;

        [Header("Spawn")]
        [InspectorHelp("Spawn 队列每帧消费数量（每帧最多创建多少个实体）。默认 8。")]
        [SerializeField, Min(1)] private int _spawnEntitiesPerFrame = 8;

        #endregion

        public VoxelMapConfig DefaultConfig => _defaultConfig;
        public IVoxelMapTemplate ActiveTemplate { get; private set; }
        public string TemplateId => _templateId;
        public Voxel3DMapService Service => Voxel3DMapService.Instance;

        public void SetTemplateId(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return;
            if (_templateId == templateId) return;
            _templateId = templateId;
            if (ActiveTemplate != null)
                LogWarning($"SetTemplateId('{templateId}') 在 Initialize 之后调用，仅更新字段；如需切换模板请重启场景。");
        }

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            // ① 注册内置模板
            RegisterBuiltInTemplates();

            // ② 解析当前 Template
            ActiveTemplate = VoxelMapTemplateRegistry.Get(_templateId);
            if (ActiveTemplate == null)
            {
                LogWarning($"未找到 IVoxelMapTemplate '{_templateId}'，回退到 '{DefaultVoxelTemplate.Id}'");
                ActiveTemplate = VoxelMapTemplateRegistry.Get(DefaultVoxelTemplate.Id);
            }

            // ③ 模板默认注册：BlockType 必需；Config / 装饰器可选
            ActiveTemplate?.RegisterDefaultBlockTypes(Service);
            if (_registerDebugTemplates)
            {
                RegisterDefaultConfig();
                ActiveTemplate?.RegisterDefaultDecorators(Service);
            }

            // ④ 持久化参数同步到 Service
            if (Service != null)
            {
                Service.AutoSaveIntervalSec = _autoSaveIntervalSec;
                Service.AutoSaveMaxChunksPerTick = _autoSaveMaxChunksPerTick;
            }

            // ⑤ Spawn 参数同步 + 自动注册 VoxelEntitySpawnDecorator（priority=300，与 2D EntitySpawnDecorator 对齐）
            VoxelEntitySpawnService.Instance.EntitiesPerFrame = _spawnEntitiesPerFrame;
            Service?.RegisterDecorator(new VoxelEntitySpawnDecorator());

            Log($"Voxel3DMapManager 初始化完成 (template={ActiveTemplate?.TemplateId ?? "<null>"})", Color.green);
        }

        protected override void Update()
        {
            base.Update();   // 保留 Inspector 同步节流逻辑
            // 自动写盘 tick（与 2D MapManager 同构）
            Service?.AutoSaveTick(Time.deltaTime);
            // Spawn 队列分帧消费
            VoxelEntitySpawnService.Instance.TickSpawnQueue(IsChunkLoadedFor);
        }

        /// <summary>提供给 VoxelEntitySpawnService 的校验：spawn 请求的目标 chunk 是否仍加载。</summary>
        private bool IsChunkLoadedFor(string mapId, int cx, int cz) =>
            Service?.GetMap(mapId)?.PeekChunk(cx, cz) != null;

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

        private static void RegisterBuiltInTemplates()
        {
            if (!VoxelMapTemplateRegistry.Contains(DefaultVoxelTemplate.Id))
                VoxelMapTemplateRegistry.Register(new DefaultVoxelTemplate());
        }

        /// <summary>
        /// 注册当前模板的默认 Config。
        /// <para>default_voxel_3d 优先使用 Inspector 的 <see cref="_defaultConfig"/>（需 ConfigId 匹配模板 DefaultConfigId）；
        /// 其它模板走 <see cref="IVoxelMapTemplate.CreateDefaultConfig"/>。</para>
        /// </summary>
        private void RegisterDefaultConfig()
        {
            if (Service == null || ActiveTemplate == null) return;

            // 默认模板分支：Inspector 接管参数（保留 PlayMode 调试体验）
            if (ActiveTemplate.TemplateId == DefaultVoxelTemplate.Id
                && _defaultConfig != null && !string.IsNullOrEmpty(_defaultConfig.ConfigId))
            {
                if (Service.GetConfig(_defaultConfig.ConfigId) != null) return;
                Service.RegisterConfig(CloneConfig(_defaultConfig));
                return;
            }

            var cfg = ActiveTemplate.CreateDefaultConfig();
            if (cfg == null || string.IsNullOrEmpty(cfg.ConfigId))
            {
                LogWarning($"VoxelTemplate '{ActiveTemplate.TemplateId}' 未返回有效默认 Config");
                return;
            }
            if (Service.GetConfig(cfg.ConfigId) != null) return;
            Service.RegisterConfig(cfg);
        }

        /// <summary>JsonUtility 浅拷贝 —— 把 Inspector 实例与 Service 内存副本解耦。</summary>
        private static VoxelMapConfig CloneConfig(VoxelMapConfig src)
        {
            var json = JsonUtility.ToJson(src);
            return JsonUtility.FromJson<VoxelMapConfig>(json);
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        #endregion
    }
}
