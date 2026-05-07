using System;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Dao
{
    /// <summary>
    /// 区块 —— <see cref="Size"/> × <see cref="Size"/> 个 <see cref="Tile"/> 的扁平数组容器。
    /// <para>
    /// 区块坐标 <see cref="ChunkX"/> / <see cref="ChunkY"/> 表示该区块在世界中的整数偏移
    /// （世界 Tile 坐标 = ChunkCoord * Size + LocalCoord）。数组按行主序展平：<c>Tiles[ly * Size + lx]</c>。
    /// </para>
    /// <para>
    /// **两条 Tile 写入路径**（用于决定哪些数据需要写盘）：
    /// <list type="bullet">
    /// <item><see cref="SetTile"/> / 直接 <see cref="Tiles"/>[i] = 由生成器（<c>IMapGenerator.FillChunk</c>）使用，
    /// **不**触发 <see cref="IsDirty"/>。生成器输出确定性，不入存档。</item>
    /// <item><see cref="OverrideTile"/> 由业务层（玩家挖矿/铺路）使用，
    /// 同时写入主数组 + <see cref="_tileOverrides"/> 字典 + 标 <see cref="IsDirty"/>。这条路径才入存档。</item>
    /// </list>
    /// </para>
    /// </summary>
    [Serializable]
    public class Chunk
    {
        public int ChunkX;
        public int ChunkY;
        public int Size;
        public Tile[] Tiles;

        /// <summary>
        /// 玩家覆盖记录 idx → TypeId（仅记录被 <see cref="OverrideTile"/> 写过的 Tile）。
        /// 区块卸载时由 <c>MapService</c> 转换为 <c>List&lt;TileOverride&gt;</c> 写盘；按需懒分配。
        /// </summary>
        [NonSerialized] private Dictionary<int, string> _tileOverrides;

        /// <summary>
        /// 该 chunk 自加载（或新建）以来是否产生了需要写盘的差量。
        /// <para>
        /// 来源：<see cref="OverrideTile"/> / <see cref="ClearOverride"/> / 业务层调用 <see cref="MarkDirty"/>。
        /// 写盘后由 <see cref="ClearDirty"/> 复位。
        /// </para>
        /// </summary>
        [NonSerialized] public bool IsDirty;

        public Chunk() { }

        public Chunk(int chunkX, int chunkY, int size)
        {
            ChunkX = chunkX;
            ChunkY = chunkY;
            Size = size;
            Tiles = new Tile[size * size];
        }

        /// <summary>按本地坐标取 Tile（不做边界检查，调用方保证 0 ≤ lx,ly &lt; Size）。</summary>
        public Tile GetTile(int lx, int ly) => Tiles[ly * Size + lx];

        /// <summary>
        /// **生成器入口** —— 按本地坐标设 Tile（替换整对象）。
        /// <para>不会标记 IsDirty。仅生成器 / 加载流程使用。</para>
        /// </summary>
        public void SetTile(int lx, int ly, Tile tile) => Tiles[ly * Size + lx] = tile;

        /// <summary>
        /// **业务层入口** —— 仅修改 <see cref="Tile.TypeId"/>，保留 Elevation/Temperature/Moisture/RiverFlow。
        /// <para>同时写入 <see cref="_tileOverrides"/> 字典并标 <see cref="IsDirty"/>，写盘后会出现在 <c>ChunkSaveData.TileOverrides</c>。</para>
        /// </summary>
        public void OverrideTile(int lx, int ly, string typeId)
        {
            var idx = ly * Size + lx;
            if (Tiles[idx] == null) Tiles[idx] = new Tile(typeId);
            else Tiles[idx].TypeId = typeId;
            _tileOverrides ??= new Dictionary<int, string>(8);
            _tileOverrides[idx] = typeId;
            IsDirty = true;
        }

        /// <summary>
        /// 移除某格的 override 记录。
        /// <para>**视觉上不立刻还原** —— 只把记录从字典中删除，标 IsDirty。
        /// 下次区块卸载 + 重新加载（生成器重跑）时才恢复为生成器默认输出。</para>
        /// </summary>
        public bool ClearOverride(int lx, int ly)
        {
            if (_tileOverrides == null) return false;
            var idx = ly * Size + lx;
            if (!_tileOverrides.Remove(idx)) return false;
            IsDirty = true;
            return true;
        }

        /// <summary>由 <c>MapService</c> 在加载时调用：把存档里的 override 列表写回主数组 + 字典。</summary>
        public void ApplyOverrides(IList<EssSystem.Core.EssManagers.Gameplay.MapManager.Persistence.Dao.TileOverride> overrides)
        {
            if (overrides == null || overrides.Count == 0) return;
            _tileOverrides ??= new Dictionary<int, string>(overrides.Count);
            for (var i = 0; i < overrides.Count; i++)
            {
                var ov = overrides[i];
                if (ov.LocalX < 0 || ov.LocalY < 0 || ov.LocalX >= Size || ov.LocalY >= Size) continue;
                var idx = ov.LocalY * Size + ov.LocalX;
                if (Tiles[idx] == null) Tiles[idx] = new Tile(ov.TypeId);
                else Tiles[idx].TypeId = ov.TypeId;
                _tileOverrides[idx] = ov.TypeId;
            }
            // ApplyOverrides 用于"加载已有存档"，应用完仍是 clean 状态（与磁盘一致）。
            IsDirty = false;
        }

        /// <summary>
        /// 由 <c>MapService</c> 在保存时调用：把字典里的 override 平铺为可序列化列表。
        /// 没有 override 时返回空列表，调用方据此判断是否值得写盘。
        /// </summary>
        public List<EssSystem.Core.EssManagers.Gameplay.MapManager.Persistence.Dao.TileOverride> EnumerateOverrides()
        {
            var list = new List<EssSystem.Core.EssManagers.Gameplay.MapManager.Persistence.Dao.TileOverride>(
                _tileOverrides?.Count ?? 0);
            if (_tileOverrides == null) return list;
            foreach (var kv in _tileOverrides)
            {
                var ly = kv.Key / Size;
                var lx = kv.Key - ly * Size;
                list.Add(new Persistence.Dao.TileOverride { LocalX = lx, LocalY = ly, TypeId = kv.Value });
            }
            return list;
        }

        /// <summary>是否含有任何 override（避免没必要的写盘判断时构造列表）。</summary>
        public bool HasOverrides => _tileOverrides != null && _tileOverrides.Count > 0;

        /// <summary>业务层显式标 dirty（如修改了 chunk 上挂的其他差量）。</summary>
        public void MarkDirty() => IsDirty = true;

        /// <summary>写盘成功后由 <c>MapService</c> 调用，复位 IsDirty。</summary>
        public void ClearDirty() => IsDirty = false;

        /// <summary>世界 Tile 坐标 → 该区块的左下角世界 X。</summary>
        public int WorldOriginX => ChunkX * Size;

        /// <summary>世界 Tile 坐标 → 该区块的左下角世界 Y。</summary>
        public int WorldOriginY => ChunkY * Size;

        public override string ToString() => $"Chunk({ChunkX},{ChunkY}) {Size}x{Size}{(IsDirty ? " *" : "")}";
    }
}
