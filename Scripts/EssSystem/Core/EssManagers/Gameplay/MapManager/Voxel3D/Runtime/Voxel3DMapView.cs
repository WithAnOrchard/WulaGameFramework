using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Runtime
{
    /// <summary>
    /// 3D 体素地图视图 —— 流式按玩家位置加载 / 卸载 chunk。
    /// <para>
    /// 数据 (heightmap) 与 网格 (Mesh) 分两阶段：
    /// <list type="number">
    /// <item>数据预生成圈 = <see cref="RenderRadius"/> + 1：保证 mesher 跨 chunk 边界 cull 时邻居 heightmap 已就绪</item>
    /// <item>渲染圈 = <see cref="RenderRadius"/>：把数据烘成 Mesh + 创建 GameObject</item>
    /// </list>
    /// 每帧上限由 <see cref="DataPerFrame"/> / <see cref="MeshPerFrame"/> 控制，防 spike。
    /// 离开 <see cref="KeepAliveRadius"/> 的 chunk 销毁 GameObject + 释放数据。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class Voxel3DMapView : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        #region Inspector

        [Header("World")]
        [Tooltip("生成参数。改 Seed 立刻得到新世界（需重启 PlayMode）。")]
        public VoxelMapConfig Config = new VoxelMapConfig();

        [Header("Streaming")]
        [Tooltip("跟随的目标 Transform（通常是 Player）。null 时按本节点位置算焦点。")]
        public Transform FollowTarget;

        [Tooltip("渲染半径（chunk 数）。可见区域约 (2R+1)²。")]
        [Range(1, 16)] public int RenderRadius = 6;

        [Tooltip("KeepAlive 半径（>= RenderRadius）。在此半径外的 chunk 被卸载。")]
        [Range(2, 32)] public int KeepAliveRadius = 8;

        [Tooltip("每帧最多生成几块 heightmap 数据（远比 mesh 便宜）。")]
        [Range(1, 64)] public int DataPerFrame = 16;

        [Tooltip("每帧最多 build 几个 Mesh。")]
        [Range(1, 8)] public int MeshPerFrame = 2;

        [Header("Rendering")]
        [Tooltip("自动给每个 chunk 加 MeshCollider（玩家踩在地上需要）。")]
        public bool AddMeshCollider = true;

        [Tooltip("Material —— 留空将运行时创建一个使用顶点色的简单 Material（Wula/VoxelVertexColor shader）。")]
        public Material ChunkMaterial;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Runtime

        private VoxelHeightmapGenerator _generator;
        private VoxelBlockType[] _palette;

        /// <summary>由 <see cref="Bind"/> 注入的数据源（来自 Voxel3DMapService）。null 时退化为 Inspector 直跑。</summary>
        private VoxelMap _boundMap;

        // chunk 数据缓存：(cx, cz) → VoxelChunk（unbound 模式下 View 自己持有；bound 模式下镜像 _boundMap.LoadedChunks）
        private readonly Dictionary<(int, int), VoxelChunk> _chunkData = new Dictionary<(int, int), VoxelChunk>(256);
        // 已渲染 GO：(cx, cz) → GO
        private readonly Dictionary<(int, int), GameObject> _chunkGO = new Dictionary<(int, int), GameObject>(256);

        private int _focusCX, _focusCZ;
        private bool _focusValid;
        private Transform _chunksRoot;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Service Integration

        /// <summary>**Service 入口** —— 把视图绑到一个由 Voxel3DMapService 管理的 <see cref="VoxelMap"/>。
        /// 绑定后所有 chunk 生成 / 卸载都走 map（触发 PostFillHook + ChunkGenerated + ChunkUnloading 事件链）。</summary>
        public void Bind(VoxelMap map)
        {
            if (_boundMap == map) return;
            ClearAllChunks();
            _boundMap = map;
            // ChunkSize 以 map 为准（避免 Inspector Config.ChunkSize 与 map.ChunkSize 漂移导致烘 mesh 错位）
            if (map != null) Config.ChunkSize = map.ChunkSize;
        }

        /// <summary>解绑并清空所有缓存与 GO（Service 销毁视图时调）。</summary>
        public void Unbind()
        {
            ClearAllChunks();
            _boundMap = null;
        }

        /// <summary>有效 ChunkSize（bound 时跟随 map，否则按 Inspector）。</summary>
        private int EffectiveChunkSize => _boundMap != null ? _boundMap.ChunkSize : Config.ChunkSize;

        /// <summary>统一 chunk 生成入口：bound 时走 map（自动跑 PostFillHook + 装饰器），否则用本地生成器。</summary>
        private VoxelChunk GenerateOrGet(int cx, int cz)
        {
            if (_boundMap != null) return _boundMap.GetOrGenerateChunk(cx, cz);
            return _generator.Generate(cx, cz);
        }

        /// <summary>统一卸载入口：bound 时走 map（触发 ChunkUnloading → Service 写盘）；本地缓存与 GO 由调用方处理。</summary>
        private void UnloadChunkBacking(int cx, int cz)
        {
            _boundMap?.UnloadChunk(cx, cz);
        }

        private int SampleWorldHeight(int wx, int wz)
        {
            if (_boundMap != null) return _boundMap.SampleHeight(wx, wz);
            return _generator.SampleHeight(wx, wz);
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Lifecycle

        private void EnsureInitialized()
        {
            if (_generator != null) return;
            _generator = new VoxelHeightmapGenerator(Config);
            _palette   = VoxelBlockTypes.DefaultPalette;
            if (ChunkMaterial == null) ChunkMaterial = VoxelMaterialFactory.CreateDefault();

            var rootGO = new GameObject("Voxel3DChunks");
            rootGO.transform.SetParent(transform, worldPositionStays: false);
            _chunksRoot = rootGO.transform;
        }

        private void Update()
        {
            EnsureInitialized();
            UpdateFocus();
            EnsureDataInRadius(RenderRadius + 1, DataPerFrame);
            EnsureMeshInRadius(RenderRadius, MeshPerFrame);
            UnloadOutsideKeepAlive();
        }

        private void UpdateFocus()
        {
            var p = FollowTarget != null ? FollowTarget.position : transform.position;
            var size = EffectiveChunkSize;
            var cx = Mathf.FloorToInt(p.x / size);
            var cz = Mathf.FloorToInt(p.z / size);
            if (!_focusValid || cx != _focusCX || cz != _focusCZ)
            {
                _focusCX = cx; _focusCZ = cz; _focusValid = true;
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Streaming

        /// <summary>按到焦点的曼哈顿距离从近到远，确保半径内 chunk 数据都已生成；本帧上限 budget。</summary>
        private void EnsureDataInRadius(int radius, int budget)
        {
            for (var dz = -radius; dz <= radius && budget > 0; dz++)
            for (var dx = -radius; dx <= radius && budget > 0; dx++)
            {
                if (Mathf.Abs(dx) + Mathf.Abs(dz) > radius * 2) continue; // 粗略圆形
                var key = (_focusCX + dx, _focusCZ + dz);
                if (_chunkData.ContainsKey(key)) continue;
                _chunkData[key] = GenerateOrGet(key.Item1, key.Item2);
                budget--;
            }
        }

        /// <summary>渲染半径内、4 邻居数据均已就绪的 chunk 烘 Mesh + 建 GO。</summary>
        private void EnsureMeshInRadius(int radius, int budget)
        {
            // 按到焦点的曼哈顿距离从近到远扫
            for (var d = 0; d <= radius && budget > 0; d++)
            for (var dz = -d; dz <= d && budget > 0; dz++)
            {
                var dxAbs = d - Mathf.Abs(dz);
                for (var s = -1; s <= 1 && budget > 0; s += 2)
                {
                    var dx = dxAbs * s;
                    if (Mathf.Abs(dx) + Mathf.Abs(dz) != d) continue;
                    var key = (_focusCX + dx, _focusCZ + dz);
                    if (_chunkGO.ContainsKey(key)) continue;
                    if (!_chunkData.TryGetValue(key, out var data)) continue;

                    // 邻居数据需就绪才烘（保证侧面 cull 正确）
                    _chunkData.TryGetValue((key.Item1 - 1, key.Item2), out var xm);
                    _chunkData.TryGetValue((key.Item1 + 1, key.Item2), out var xp);
                    _chunkData.TryGetValue((key.Item1, key.Item2 - 1), out var zm);
                    _chunkData.TryGetValue((key.Item1, key.Item2 + 1), out var zp);
                    if (xm == null || xp == null || zm == null || zp == null) continue;

                    BuildChunkGO(data, xm, xp, zm, zp);
                    budget--;

                    if (dx == 0) break; // 避免 dx=0 时 s=±1 重复
                }
            }
        }

        private void BuildChunkGO(VoxelChunk c, VoxelChunk xm, VoxelChunk xp, VoxelChunk zm, VoxelChunk zp)
        {
            var mesh = VoxelChunkMesher.Build(c, Config, _palette, xm, xp, zm, zp);

            var go = new GameObject($"Chunk_{c.ChunkX}_{c.ChunkZ}");
            go.transform.SetParent(_chunksRoot, worldPositionStays: false);
            go.transform.localPosition = new Vector3(c.WorldMinX, 0f, c.WorldMinZ);

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = ChunkMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = true;

            if (AddMeshCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }

            _chunkGO[(c.ChunkX, c.ChunkZ)] = go;
        }

        private void UnloadOutsideKeepAlive()
        {
            // 收集越界键（避免 modifying-during-iteration）
            List<(int, int)> toUnload = null;
            foreach (var kv in _chunkGO)
            {
                var dx = Mathf.Abs(kv.Key.Item1 - _focusCX);
                var dz = Mathf.Abs(kv.Key.Item2 - _focusCZ);
                if (dx > KeepAliveRadius || dz > KeepAliveRadius)
                {
                    (toUnload ??= new List<(int, int)>()).Add(kv.Key);
                }
            }
            if (toUnload == null) return;
            foreach (var k in toUnload)
            {
                if (_chunkGO.TryGetValue(k, out var go) && go != null)
                {
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                    Destroy(go);
                }
                _chunkGO.Remove(k);
                _chunkData.Remove(k);
                UnloadChunkBacking(k.Item1, k.Item2);
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// 查询 (wx, wz) 处的<b>有效地表 y</b>（玩家可站立的高度）。
        /// 水下柱（raw_h ≤ SeaLevel）返回 <c>SeaLevel</c> —— 因为水面 mesh 在该 y 有 collider。
        /// </summary>
        public int SampleHeight(int wx, int wz)
        {
            EnsureInitialized();
            var rawH = SampleWorldHeight(wx, wz);
            return rawH <= Config.SeaLevel ? Config.SeaLevel : rawH;
        }

        /// <summary>
        /// 在 <paramref name="searchRadius"/> 范围内（曼哈顿）找一块**陆地**（raw_h &gt; SeaLevel）的 (wx, wz, y)。
        /// 找不到则退化到 (originX, originZ, SeaLevel)（站水面）。
        /// 用于 Player spawn：避免出生在水底。
        /// </summary>
        public Vector3Int FindNearestLandSpawn(int originX, int originZ, int searchRadius = 32)
        {
            EnsureInitialized();
            var sea = Config.SeaLevel;
            for (var d = 0; d <= searchRadius; d++)
            {
                for (var dz = -d; dz <= d; dz++)
                for (var dx = -d; dx <= d; dx++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != d) continue; // 仅走当前环
                    var wx = originX + dx;
                    var wz = originZ + dz;
                    var rawH = SampleWorldHeight(wx, wz);
                    if (rawH > sea) return new Vector3Int(wx, rawH, wz);
                }
            }
            return new Vector3Int(originX, sea, originZ);
        }

        /// <summary>
        /// 同步预热：以 <paramref name="worldPos"/> 为中心、<paramref name="radius"/> 个 chunk 范围内
        /// 立即生成所有数据 + 烘 Mesh + 建 GameObject —— 跳过帧预算限制。
        /// 用于 spawn 玩家前确保脚下的地表 collider 已就绪，避免玩家穿地坠落。
        /// </summary>
        public void Warmup(Vector3 worldPos, int radius = 2)
        {
            EnsureInitialized();
            var size = EffectiveChunkSize;
            var cx = Mathf.FloorToInt(worldPos.x / size);
            var cz = Mathf.FloorToInt(worldPos.z / size);

            // 1) 数据生成（半径 +1 保证邻居 cull 正确）
            for (var dz = -radius - 1; dz <= radius + 1; dz++)
            for (var dx = -radius - 1; dx <= radius + 1; dx++)
            {
                var key = (cx + dx, cz + dz);
                if (!_chunkData.ContainsKey(key))
                    _chunkData[key] = GenerateOrGet(key.Item1, key.Item2);
            }

            // 2) Mesh 烘焙
            for (var dz = -radius; dz <= radius; dz++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var key = (cx + dx, cz + dz);
                if (_chunkGO.ContainsKey(key)) continue;
                if (!_chunkData.TryGetValue(key, out var data)) continue;
                _chunkData.TryGetValue((key.Item1 - 1, key.Item2), out var xm);
                _chunkData.TryGetValue((key.Item1 + 1, key.Item2), out var xp);
                _chunkData.TryGetValue((key.Item1, key.Item2 - 1), out var zm);
                _chunkData.TryGetValue((key.Item1, key.Item2 + 1), out var zp);
                BuildChunkGO(data, xm, xp, zm, zp);
            }

            _focusCX = cx; _focusCZ = cz; _focusValid = true;
        }

        /// <summary>清空所有缓存（换 Config 时调）。重建本地 generator，不影响绑定的 VoxelMap。</summary>
        public void Reset()
        {
            ClearAllChunks();
            _generator = new VoxelHeightmapGenerator(Config);
        }

        /// <summary>仅清空 GO 与本地数据缓存；不重建 generator，也不解绑 _boundMap。</summary>
        private void ClearAllChunks()
        {
            foreach (var kv in _chunkGO)
            {
                if (kv.Value == null) continue;
                var mf = kv.Value.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) Destroy(mf.sharedMesh);
                Destroy(kv.Value);
            }
            _chunkGO.Clear();
            _chunkData.Clear();
            _focusValid = false;
        }

        #endregion
    }
}
