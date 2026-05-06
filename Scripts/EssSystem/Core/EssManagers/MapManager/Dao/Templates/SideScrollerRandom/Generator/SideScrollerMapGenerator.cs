using EssSystem.EssManager.MapManager.Dao;
using EssSystem.EssManager.MapManager.Dao.Generator;
using EssSystem.EssManager.MapManager.Dao.Templates.SideScrollerRandom.Config;
using EssSystem.EssManager.MapManager.Dao.Templates.SideScrollerRandom.Dao;
using UnityEngine;

namespace EssSystem.EssManager.MapManager.Dao.Templates.SideScrollerRandom.Generator
{
    /// <summary>
    /// 横版 2D 随机生成器（骨架）：fBm Perlin 决定地表线 Y(x)，
    /// y &gt; surface 填 <see cref="SideScrollerTileTypes.Sky"/>，
    /// y == surface 填 Grass，
    /// surface-DirtThickness ≤ y &lt; surface 填 Dirt，
    /// 其余至 BedrockY 填 Stone，y &lt; BedrockY 填 Bedrock。
    /// <para>
    /// 仅对 X 轴噪声，Y 不参与噪声 → 保证同一 X 列在不同 chunk 中接缝一致。
    /// </para>
    /// <para>
    /// 后续扩展点：洞穴（2D Perlin/cell）、矿脉、群系横向带（按 X 范围切沙漠/雪原/草地）、
    /// 液体填充（水/岩浆）、敌人 / 战利品装饰器。建议作为 <c>IChunkDecorator</c> 加在管线上。
    /// </para>
    /// </summary>
    public class SideScrollerMapGenerator : IMapGenerator
    {
        private readonly SideScrollerMapConfig _cfg;
        private readonly float _seedOffsetX;

        public SideScrollerMapGenerator(SideScrollerMapConfig cfg)
        {
            _cfg = cfg;
            // 用种子派生固定偏移，避免 Unity PerlinNoise 在原点附近重复
            var rng = new System.Random(cfg.Seed);
            _seedOffsetX = (float)(rng.NextDouble() * 10000.0);
        }

        public void FillChunk(Chunk chunk)
        {
            var size = chunk.Size;
            var ox = chunk.WorldOriginX;
            var oy = chunk.WorldOriginY;

            for (var lx = 0; lx < size; lx++)
            {
                var worldX = ox + lx;
                var surfaceY = SampleSurfaceY(worldX);

                for (var ly = 0; ly < size; ly++)
                {
                    var worldY = oy + ly;
                    var typeId = ClassifyColumn(worldY, surfaceY);
                    chunk.SetTile(lx, ly, new Tile(typeId));
                }
            }
        }

        /// <summary>对单列做高度采样（fBm Perlin）。</summary>
        public int SampleSurfaceY(int worldX)
        {
            float amp = 1f, freq = _cfg.SurfaceFrequency, sum = 0f, norm = 0f;
            for (var o = 0; o < _cfg.SurfaceOctaves; o++)
            {
                var sx = (worldX + _seedOffsetX) * freq;
                // PerlinNoise 期望 (x,y)，Y 固定一个非整数避开网格周期
                var n = Mathf.PerlinNoise(sx, 0.123f) * 2f - 1f; // [-1,1]
                sum += n * amp;
                norm += amp;
                amp *= _cfg.SurfacePersistence;
                freq *= 2f;
            }
            var unit = norm > 0f ? sum / norm : 0f;       // [-1,1]
            return _cfg.SeaLevelY + Mathf.RoundToInt(unit * _cfg.SurfaceAmplitude);
        }

        /// <summary>给定世界 Y 与该列地表高度，决定 TileType。</summary>
        private string ClassifyColumn(int worldY, int surfaceY)
        {
            if (worldY < _cfg.BedrockY) return SideScrollerTileTypes.Bedrock;
            if (worldY > surfaceY)      return SideScrollerTileTypes.Sky;
            if (worldY == surfaceY)     return SideScrollerTileTypes.Grass;
            if (worldY >= surfaceY - _cfg.DirtThickness) return SideScrollerTileTypes.Dirt;
            return SideScrollerTileTypes.Stone;
        }

        /// <summary>横版无 RiverRegion 这类区域级预热，留空即可。</summary>
        public void PrewarmAround(int chunkX, int chunkY, int chunkSize) { }
    }
}
