using System.Collections.Generic;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Persistence.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao
{
    /// <summary>
    /// 单个 3D Chunk 数据（heightmap-only）。
    /// <para>
    /// 不存体素体；仅 (X, Z) 二维数组的 height + topBlock + sideBlock。
    /// 顶面在 y = height 处一格，侧面从 y = height 自上而下渲染（仅在邻居比自己矮的方向，由 Mesher 决定）。
    /// </para>
    /// </summary>
    public class VoxelChunk
    {
        public readonly int ChunkX, ChunkZ;
        public readonly int Size;

        /// <summary>每格地表最高 y（含；该格顶面在此 y 处）。</summary>
        public readonly byte[] Heights;

        /// <summary>每格顶面方块 ID（参 <see cref="VoxelBlockTypes"/>）。</summary>
        public readonly byte[] TopBlocks;

        /// <summary>每格侧面方块 ID（cliff 露出的）。</summary>
        public readonly byte[] SideBlocks;

        /// <summary>世界坐标系下 chunk 起点（minX, minZ；y 永远从 0 起）。</summary>
        public int WorldMinX => ChunkX * Size;
        public int WorldMinZ => ChunkZ * Size;

        public VoxelChunk(int chunkX, int chunkZ, int size)
        {
            ChunkX = chunkX; ChunkZ = chunkZ; Size = size;
            Heights    = new byte[size * size];
            TopBlocks  = new byte[size * size];
            SideBlocks = new byte[size * size];
        }

        /// <summary>(lx, lz) 行主序索引：lz * Size + lx。</summary>
        public int Index(int lx, int lz) => lz * Size + lx;

        public byte GetHeight(int lx, int lz)    => Heights[Index(lx, lz)];
        public byte GetTopBlock(int lx, int lz)  => TopBlocks[Index(lx, lz)];
        public byte GetSideBlock(int lx, int lz) => SideBlocks[Index(lx, lz)];

        // ──────────────────────────────────────────────────────────────
        // 持久化差量 API（Phase 4a）
        //   生成器输出不入 _overrides；只有业务侧 SetVoxelColumnOverride 才记录。
        //   重新加载时：先跑 IVoxelMapGenerator 重建默认地形 → 再 ApplyOverrides。
        // ──────────────────────────────────────────────────────────────

        /// <summary>(lx,lz) → VoxelColumnOverride 的稀疏字典，Key = Index(lx, lz)。</summary>
        private Dictionary<int, VoxelColumnOverride> _overrides;

        /// <summary>是否含未持久化的修改（业务侧 override / clear / spawn 销毁触发）。</summary>
        public bool IsDirty { get; private set; }

        /// <summary>由 Service 在 SaveChunkInternal 写盘后回调。</summary>
        public void ClearDirty() { IsDirty = false; }

        /// <summary>由 Service / 装饰器在跨链路修改时手动置脏（如 spawn 销毁列表变化）。</summary>
        public void MarkDirty() { IsDirty = true; }

        /// <summary>**业务层入口** —— 覆盖单列三值（顶 / 侧 / 高度），同步入 dirty + override 字典。</summary>
        public void OverrideColumn(int lx, int lz, byte topBlock, byte sideBlock, byte height)
        {
            var idx  = Index(lx, lz);
            Heights[idx]    = height;
            TopBlocks[idx]  = topBlock;
            SideBlocks[idx] = sideBlock;
            _overrides ??= new Dictionary<int, VoxelColumnOverride>();
            _overrides[idx] = new VoxelColumnOverride
            {
                LocalX = lx, LocalZ = lz,
                TopBlock = topBlock, SideBlock = sideBlock, Height = height,
            };
            IsDirty = true;
        }

        /// <summary>
        /// 移除某列的 override 记录。返回是否真删除了（无记录返回 false）。
        /// 注意：本地 Heights/TopBlocks/SideBlocks 不会回退到生成器初始值 ——
        /// 生成器只在区块**重新加载**时跑一次；调用方若需立刻看到默认地形，应 UnloadChunk + GetOrGenerateChunk。
        /// </summary>
        public bool ClearOverride(int lx, int lz)
        {
            if (_overrides == null) return false;
            var removed = _overrides.Remove(Index(lx, lz));
            if (removed) IsDirty = true;
            return removed;
        }

        /// <summary>从存档应用差量列表 —— 只改三个数组 + 重建 _overrides 字典，不置脏。</summary>
        public void ApplyOverrides(List<VoxelColumnOverride> overrides)
        {
            if (overrides == null || overrides.Count == 0) return;
            _overrides ??= new Dictionary<int, VoxelColumnOverride>(overrides.Count);
            for (var i = 0; i < overrides.Count; i++)
            {
                var ov  = overrides[i];
                var idx = Index(ov.LocalX, ov.LocalZ);
                Heights[idx]    = ov.Height;
                TopBlocks[idx]  = ov.TopBlock;
                SideBlocks[idx] = ov.SideBlock;
                _overrides[idx] = ov;
            }
        }

        /// <summary>导出当前所有 override 记录（写盘时由 Service 调用）。</summary>
        public List<VoxelColumnOverride> EnumerateOverrides()
        {
            if (_overrides == null || _overrides.Count == 0)
                return new List<VoxelColumnOverride>(0);
            var list = new List<VoxelColumnOverride>(_overrides.Count);
            foreach (var ov in _overrides.Values) list.Add(ov);
            return list;
        }
    }
}
