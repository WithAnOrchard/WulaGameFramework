using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Util
{
    /// <summary>
    /// 区块级确定性子种子工具。
    /// <para>
    /// 装饰器 / 业务代码想在某个区块里"随机但每次一样"地生成内容时，
    /// 用 <see cref="Rng"/> 取一个独立 <see cref="System.Random"/>：
    /// </para>
    /// <code>
    /// var rng = ChunkSeed.Rng(map.MapId, chunk.ChunkX, chunk.ChunkY, "flora");
    /// for (var ly = 0; ly &lt; chunk.Size; ly++) ...  rng.NextDouble() ...
    /// </code>
    /// <para>
    /// <paramref name="tag"/> 让同一区块可以派生多个互不相关的子种子
    /// （例如 "flora" / "fauna" / "loot" 三条管线各自独立）。
    /// </para>
    /// <para>
    /// **注意**：这不包含主生成器的 Seed —— 换种子换 MapId 即可让整张图重开，
    /// 但如果业务希望"只改 Config.Seed 也让花草重排"，可把 seed 拼进 tag。
    /// </para>
    /// </summary>
    public static class ChunkSeed
    {
        /// <summary>派生 32bit 确定性种子值。</summary>
        public static int For(string mapId, int chunkX, int chunkY, string tag = null)
        {
            unchecked
            {
                var h = 17;
                h = h * 31 + (mapId == null ? 0 : mapId.GetHashCode());
                h = h * 31 + chunkX;
                h = h * 31 + chunkY;
                h = h * 31 + (tag == null ? 0 : tag.GetHashCode());
                return h;
            }
        }

        /// <summary>派生独立 <see cref="Random"/> 实例（最常用入口）。</summary>
        public static Random Rng(string mapId, int chunkX, int chunkY, string tag = null)
            => new Random(For(mapId, chunkX, chunkY, tag));
    }
}
