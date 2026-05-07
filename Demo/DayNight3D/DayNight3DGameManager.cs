using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.EssManager.CharacterManager;
using EssSystem.Core.EssManagers.ResourceManager;
using Demo.DayNight3D.Player;
using EssSystem.EssManager.MapManager.Voxel3D.Dao;
using EssSystem.EssManager.MapManager.Voxel3D.Runtime;

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

        [Tooltip("体素世界生成参数。在 Inspector 里展开调 Seed / SeaLevel / SnowLine 等。")]
        [SerializeField] private VoxelMapConfig _voxelConfig = new VoxelMapConfig();

        [Tooltip("体素远智半径（chunk 数）。")]
        [SerializeField, Range(2, 16)] private int _voxelRenderRadius = 6;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        protected override void Awake()
        {
            EnsureSubManager<CharacterManager>();
            base.Awake();
            Debug.Log("[DayNight3DGameManager] 基础 Manager 初始化完成");
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
            if (world != null) world.FollowTarget = player.transform;
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
            Debug.Log($"[DayNight3DGameManager] Spawn VoxelWorld（seed={_voxelConfig.Seed}, R={_voxelRenderRadius}）");
            return view;
        }

        #endregion
    }
}
