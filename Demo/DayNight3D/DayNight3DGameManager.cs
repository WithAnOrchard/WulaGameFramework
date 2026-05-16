using System.Collections.Generic;
using EssSystem.Core.Base;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Core.Foundation.ResourceManager;
using Demo.DayNight3D.Player;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Runtime;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Persistence;
using Demo.DayNight3D.Map;

namespace Demo.DayNight3D
{
    /// <summary>
    /// 3D 昼夜 Demo 总控 —— 极简版：游戏开始时创建一个 <see cref="DayNight3DPlayer"/>。
    /// <para>
    /// 职责：
    /// <list type="bullet">
    /// <item><b>Awake</b>：<c>base.Awake()</c> 自动发现 / 初始化基础 Manager（含 ResourceManager）；
    /// 同时确保 <see cref="CharacterManager"/> 在场景中存在。</item>
    /// <item><b>Start</b>：同步触发 <c>EVT_DATA_LOADED</c>（幂等）→ 资源加载 → FBX Config 批量注册。
    /// 控制流返回后实例化一个挂 <see cref="DayNight3DPlayer"/> 的 GameObject。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class DayNight3DGameManager : AbstractGameManager
    {
        // ─────────────────────────────────────────────────────────────
        #region Inspector

        [Header("Player")]
        [Tooltip("启动时自动 spawn 一个 Player（场景里若已存在则跳过）。")]
        [SerializeField] private bool _autoSpawnPlayer = true;

        [Tooltip("Player 出生位置（世界坐标）。建议 y > 0 避免穿地。")]
        [SerializeField] private Vector3 _playerSpawnPosition = new Vector3(0f, 1f, 0f);

        [Tooltip("Player 使用的 CharacterConfig ID（= FBX 文件名）。留空则使用 DayNight3DPlayer Inspector 默认值。")]
        [SerializeField] private string _playerConfigId = "herobrine";

        [Header("Voxel World")]
        [Tooltip("启动时自动创建 <see cref=\"Voxel3DMapView\"/> 体素世界。")]
        [SerializeField] private bool _autoSpawnVoxelWorld = true;

        [Tooltip("DayNight3D 专属体素世界配置（基于默认 VoxelMapConfig 微调：SnowLine=32, BeachBand=3, Seed=20240509，ConfigId=daynight3d_voxel。\n" +
                 "Inspector 调 Seed / TerrainAmplitude / MCAmplitudeScale / ContinentalnessScale / ErosionScale / WeirdnessScale / MCHeightSmoothRadius 看生成变化。")]
        [SerializeField] private DayNight3DVoxelMapConfig _voxelConfig = new DayNight3DVoxelMapConfig();

        [Tooltip("体素渲染半径（chunk 数）。调高会明显增加首帧烘 mesh 代价（~R² chunk），距离 30+ 需中高端机器。")]
        [SerializeField, Range(2, 32)] private int _voxelRenderRadius = 12;

        [Tooltip("每次进 PlayMode 都随机化 Seed，得到全新世界（默认 true）。\n" +
                 "关掉则使用 Inspector 里 _voxelConfig.Seed 的值（同 seed 同世界，便于反复测试 / 截图复现）。")]
        [SerializeField] private bool _randomizeSeedEachPlay = true;

        [Tooltip("每次进 PlayMode 都把上一轮持久化的 chunk 存档清掉（默认 true）。\n" +
                 "保证不会读到旧 seed 留下的 chunk 数据；关掉则保留存档（生产环境别关）。")]
        [SerializeField] private bool _clearPersistedMapEachPlay = true;

        [Header("Lighting (Voxel)")]
        [Tooltip("是否在场景中自动挂 VoxelLightManager 子节点（提供 DayCycle 滑块 + 光源 API）。")]
        [SerializeField] private bool _autoSpawnLightManager = true;

        [Tooltip("启动时在小镇和小平地中心撒一组演示光源（中心萤石 + 边缘火把环 + 各小平地灯笼）。")]
        [SerializeField] private bool _spawnDemoLights = true;

        [Tooltip("启动 DayCycle 值 [0..1]：0 = 午夜全黑、0.5 = 正午全亮（默认）、0.85 = 黄昏接近黑夜，能看到火把暖光最明显。")]
        [SerializeField, Range(0f, 1f)] private float _initialDayCycle = 0.5f;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        protected override void Awake()
        {
            // 必须在 base.Awake / 任何 Manager 初始化之前 ——
            //   * 随机 Seed → 影响地形 / planner RNG / 光照位置等所有用 Seed 派生的东西
            //   * 清持久化 → 防止读到上轮旧 chunk
            ResetWorldForFreshPlayMode();

            EnsureSubManager<CharacterManager>();
            if (_autoSpawnLightManager) EnsureSubManager<VoxelLightManager>();
            base.Awake();
            Debug.Log("[DayNight3DGameManager] 基础 Manager 初始化完成");
        }

        /// <summary>每次进 PlayMode 时调用：根据 Inspector 开关重置 cfg.Seed + 清掉持久化的 chunk 存档。
        /// 让"按 Play 按钮 = 全新世界"成立。</summary>
        private void ResetWorldForFreshPlayMode()
        {
            if (_voxelConfig == null) return;

            // 1) 随机 Seed —— 用 System.Random 不污染 Unity Random.state（Unity 全局给业务用）
            if (_randomizeSeedEachPlay)
            {
                var newSeed = new System.Random().Next(int.MinValue, int.MaxValue);
                Debug.Log($"[DayNight3DGameManager] 重新生成地图 Seed：{_voxelConfig.Seed} → {newSeed}");
                _voxelConfig.Seed = newSeed;
            }

            // 2) 清持久化 chunk 存档（路径 {persistentDataPath}/VoxelMapData/{ConfigId}/）
            //    防止上轮 seed 留下的 region 文件被读回来覆盖新地形
            if (_clearPersistedMapEachPlay && !string.IsNullOrEmpty(_voxelConfig.ConfigId))
            {
                try
                {
                    var ok = VoxelMapPersistenceService.Instance.DeleteMapData(_voxelConfig.ConfigId);
                    Debug.Log($"[DayNight3DGameManager] 清持久化地图存档 '{_voxelConfig.ConfigId}'：{(ok ? "已清" : "无需清/无存档")}");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[DayNight3DGameManager] 清持久化失败（可忽略，未首次绑定持久化）：{ex.Message}");
                }
            }

        }

        private void EnsureSubManager<T>() where T : MonoBehaviour
        {
            if (GetComponentInChildren<T>(true) != null) return;
            var holder = new GameObject(typeof(T).Name);
            holder.transform.SetParent(transform);
            holder.AddComponent<T>();
            Debug.Log($"[DayNight3DGameManager] 自动创建 {typeof(T).Name} 子节点");
        }

        /// <summary>
        /// 与 <c>DayNightGameManager</c> 一致的范式：在 <c>Start()</c> 主动同步触发
        /// <c>ResourceService.EVT_DATA_LOADED</c>（幂等 —— 内部 <c>_dataLoaded</c> 标志去重），
        /// 同步完成 <c>AutoLoadAllResources</c> 并广播 <c>EVT_RESOURCES_LOADED</c>，
        /// 进而触发 <c>CharacterManager.OnResourcesLoaded</c> 把 FBX 注册成 Config。
        /// 控制流返回后立即 spawn Player。
        /// </summary>
        protected virtual void Start()
        {
            if (EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEventMethod(
                    ResourceService.EVT_DATA_LOADED, new List<object>());
            }

            // 先建 World，再 Spawn Player 并把出生点提到地表高 + 2
            Voxel3DMapView world = null;
            if (_autoSpawnVoxelWorld) world = SpawnVoxelWorld();

            // 光源系统：先设 DayCycle，再撒 demo 光源 —— 全部在 view 第一次 Update 烘 mesh 前完成，
            // 这样首帧的 mesh 顶点色就能正确反映黄昏 + 火把
            if (VoxelLightManager.HasInstance)
            {
                VoxelLightManager.Instance.DayCycle01 = _initialDayCycle;
                if (_spawnDemoLights) SpawnDemoLights();
            }

            if (_autoSpawnPlayer) SpawnPlayer(world);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Player

        private void SpawnPlayer(Voxel3DMapView world)
        {
            // 场景里已有 Player 就不重建（但仍要帮它跟世界对齐）
            var player = FindObjectOfType<DayNight3DPlayer>();
            if (player == null)
            {
                var go = new GameObject("Player");
                go.transform.position = _playerSpawnPosition;
                player = go.AddComponent<DayNight3DPlayer>();
                if (!string.IsNullOrEmpty(_playerConfigId)) player.SetConfigId(_playerConfigId);
                Debug.Log($"[DayNight3DGameManager] Spawn Player @ {_playerSpawnPosition}（configId='{_playerConfigId}'）");
            }
            else
            {
                Debug.Log($"[DayNight3DGameManager] 场景已存在 DayNight3DPlayer ({player.gameObject.name})，跳过创建");
            }

            // ── 出生点定位 ────────────────────────────────────────────
            if (world != null)
            {
                // 1) 找陆地（避免水底） + Warmup 该块（同步生成 Mesh + Collider）
                var p = player.transform.position;
                var land = world.FindNearestLandSpawn(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.z));
                var spawnXZ = new Vector3(land.x + 0.5f, 0f, land.z + 0.5f);
                world.Warmup(spawnXZ, radius: 2);

                // 2) 必须 SyncTransforms，否则 Rigidbody 在 FixedUpdate 用旧坐标
                var rb = player.GetComponent<Rigidbody>();

                // 3) 用 raycast 找真实 collider 顶面（信任 collider 而不是 SampleHeight）
                var probeOrigin = new Vector3(spawnXZ.x, land.y + 4f, spawnXZ.z);
                var groundY = (float)land.y;
                if (Physics.Raycast(probeOrigin, Vector3.down, out var hit, 16f))
                {
                    groundY = hit.point.y;
                    Debug.Log($"[DayNight3DGameManager] Spawn raycast 命中 y={groundY:F2}（chunk='{hit.collider.name}'）");
                }
                else
                {
                    Debug.LogWarning($"[DayNight3DGameManager] Spawn 区无 collider 命中，使用 SampleHeight={groundY}（小心穿地）");
                }

                // 4) 同时设 transform 与 Rigidbody.position，再 Sync 一次，防止物理用旧坐标
                var finalPos = new Vector3(spawnXZ.x, groundY + 0.05f, spawnXZ.z);
                player.transform.position = finalPos;
                if (rb != null)
                {
                    rb.position = finalPos;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                Physics.SyncTransforms();

                Debug.Log($"[DayNight3DGameManager] Player spawn → {finalPos}");
            }

            // 让体素世界跟随玩家
            if (world != null)
            {
                world.FollowTarget = player.transform;
                player.World       = world; // Player.LateUpdate 据此钳位（若 world.HasWorldBounds）
            }
        }

        private Voxel3DMapView SpawnVoxelWorld()
        {
            var existing = FindObjectOfType<Voxel3DMapView>();
            if (existing != null)
            {
                Debug.Log($"[DayNight3DGameManager] 场景已存在 Voxel3DMapView ({existing.gameObject.name})，跳过创建");
                return existing;
            }
            var go = new GameObject("VoxelWorld");
            var view = go.AddComponent<Voxel3DMapView>();
            view.Config = _voxelConfig;
            view.RenderRadius = _voxelRenderRadius;
            view.KeepAliveRadius = Mathf.Max(view.KeepAliveRadius, _voxelRenderRadius + 2);

            // DayNight3D 专属 AABB —— 仅作 Player 钳位 safety net；岛屿形状由 DayNight3DIslandGenerator 径向 mask 自包含
            if (_voxelConfig.BoundedWorld)
            {
                view.SetWorldBoundsXZ(_voxelConfig.WorldRect, enabled: true);
                Debug.Log($"[DayNight3DGameManager] Spawn VoxelWorld（seed={_voxelConfig.Seed}, R={_voxelRenderRadius}, MC noise router, AABB={_voxelConfig.WorldHalfChunksXZ * 2}×{_voxelConfig.WorldHalfChunksXZ * 2} chunks）");
            }
            else
            {
                Debug.Log($"[DayNight3DGameManager] Spawn VoxelWorld（seed={_voxelConfig.Seed}, R={_voxelRenderRadius}, MC noise router, 无边界）");
            }
            return view;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lighting Demo

        /// <summary>
        /// 在世界中心周围撒一组演示光源：1 盏萤石 + 一圈火把环。
        /// <para>高度采用生成器实际地表 + 2，避免黄昏时灯被埋在山体里。</para>
        /// </summary>
        private void SpawnDemoLights()
        {
            if (_voxelConfig == null || !VoxelLightManager.HasInstance) return;
            var lm  = VoxelLightManager.Instance;
            var gen = _voxelConfig.CreateGenerator();

            var center = _voxelConfig.WorldCenterWorld;
            var cx     = Mathf.RoundToInt(center.x);
            var cz     = Mathf.RoundToInt(center.y);

            // 1) 世界中心：1 盏萤石
            var centerY = gen.SampleHeight(cx, cz) + 2f;
            lm.AddGlowstone(new Vector3(center.x, centerY, center.y));

            // 2) 周围火把环：半径 24 block（8 盏均布）
            const float ringR = 24f;
            const int   ringN = 8;
            for (var i = 0; i < ringN; i++)
            {
                var a  = i * (Mathf.PI * 2f / ringN);
                var px = center.x + Mathf.Cos(a) * ringR;
                var pz = center.y + Mathf.Sin(a) * ringR;
                var py = gen.SampleHeight(Mathf.RoundToInt(px), Mathf.RoundToInt(pz)) + 1.5f;
                lm.AddTorch(new Vector3(px, py, pz));
            }
            Debug.Log($"[DayNight3DGameManager] 世界中心 ({center.x:F0}, {center.y:F0}) 演示光源：1 萤石 + {ringN} 火把，ringR={ringR}");
        }

        #endregion

    }
}
