using System.Collections.Generic;
using UnityEngine;
using Demo.Tribe.World.Features;

namespace Demo.Tribe.World
{
    /// <summary>
    /// Biome 注册表 + 全局构建入口。
    /// <para>
    /// <c>TribeGameManager.Start</c> 调用 <see cref="Build"/> 一次，按顺序构建所有 biome 内容。
    /// 每个 biome 自动加一条地表色带 (<see cref="GroundTintFeature"/>) 用作边界可视化。
    /// </para>
    /// </summary>
    public static class TribeBiomeRegistry
    {
        /// <summary>构建全部 biome 到场景。</summary>
        /// <param name="biomes">已配置好的 biome 列表（按 X 顺序）。</param>
        /// <param name="ctx">共享上下文（GroundY / sortingOrder / 父节点等）。</param>
        public static void Build(IList<TribeBiomeConfig> biomes, TribeBiomeContext ctx)
        {
            if (biomes == null || ctx == null) return;
            var totalFeatures = 0;
            foreach (var biome in biomes)
            {
                if (biome == null) continue;
                ctx.CurrentBiome = biome;

                // 1) 地表色带（Biome 边界可视化）
                var tint = new GroundTintFeature
                {
                    WorldX = biome.StartX,
                    Width = Mathf.Max(0f, biome.EndX - biome.StartX),
                    Tint = biome.GroundTint,
                };
                tint.Build(ctx);

                // 2) Biome 名称标识牌（在 biome 起点上方 5 格）
                BuildBiomeLabel(biome, ctx);

                // 3) 业务 Features
                var built = 0;
                foreach (var feature in biome.Features)
                {
                    if (feature == null) continue;
                    feature.Build(ctx);
                    built++;
                }
                totalFeatures += built;
                Debug.Log($"[BiomeRegistry] '{biome.Id}' ({biome.StartX:0}~{biome.EndX:0}) 构建 {built} 个 Feature");
            }
            ctx.CurrentBiome = null;
            Debug.Log($"[BiomeRegistry] 全部 biome 构建完成（{biomes.Count} 个 biome / {totalFeatures} 个 feature）");
        }

        /// <summary>在 biome 起点放一个空中文字牌，标记 biome 名（调试 / 占位用）。</summary>
        private static void BuildBiomeLabel(TribeBiomeConfig biome, TribeBiomeContext ctx)
        {
            var go = new GameObject($"BiomeLabel_{biome.Id}");
            if (ctx.WorldRoot != null) go.transform.SetParent(ctx.WorldRoot, true);
            go.transform.position = new Vector3(biome.StartX + 1f, ctx.GroundY + 5f, 0f);
            var tm = go.AddComponent<TextMesh>();
            tm.text = $"<{biome.DisplayName}>\n{biome.Id}";
            tm.fontSize = 64;
            tm.characterSize = 0.2f;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = biome.GroundTint;
            var tmr = go.GetComponent<MeshRenderer>();
            if (tmr != null) tmr.sortingOrder = ctx.BaseSortingOrder + 5;
        }
    }
}
