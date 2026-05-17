using UnityEngine;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 地表色带 —— 沿 biome 全宽贴一条薄色块到地面下方，可视化标识 biome 边界。
    /// <para>
    /// 由 <see cref="TribeBiomeRegistry"/> 自动为每个 biome 添加一条；不需要业务侧手动声明。
    /// 后期接入 Tilemap 后可替换为 TileBase tint。
    /// </para>
    /// </summary>
    public class GroundTintFeature : TribeFeatureSpec
    {
        /// <summary>色带宽度（biome.EndX - biome.StartX）。</summary>
        public float Width;

        /// <summary>色带厚度（世界单位，向下延伸）。</summary>
        public float Thickness = 0.4f;

        /// <summary>色调（来自 biome.GroundTint）。</summary>
        public Color Tint;

        public override void Build(TribeBiomeContext ctx)
        {
            // 锚点：色带顶在 GroundY，整体向下延伸
            var centerX = WorldX + Width * 0.5f;
            var centerY = ctx.GroundY - Thickness * 0.5f;
            var go = new GameObject($"GroundTint_{ctx.CurrentBiome?.Id ?? "unknown"}");
            if (ctx.WorldRoot != null) go.transform.SetParent(ctx.WorldRoot, true);
            go.transform.position = new Vector3(centerX, centerY, 0f);
            go.transform.localScale = new Vector3(Width, Thickness, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteCache.GetWhitePixel();
            sr.color = Tint;
            // 比 Placeholder/采集物 略低，让它当作背景
            sr.sortingOrder = ctx.BaseSortingOrder - 5;
        }
    }
}
