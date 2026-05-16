using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// MC 1.18+ 风格 noise router 的共享算子工具类。
    /// <para>核心公式（block-int 高度）：</para>
    /// <code>
    /// h = SeaLevel
    ///   + ContinentalnessSpline(c) × ampRef
    ///   + ErosionSpline(e) × ampRef × mcAmpScale × Pv(w) × extraMask
    /// </code>
    /// 其中 <c>c, e, w ∈ [-1,1]</c> 由三层独立 Perlin 噪声出。
    /// <para>分离作公共静态类是为了让 <c>VoxelHeightmapGenerator</c> 与 island / 其它派生生成器
    /// （需要把"岛屿径向 mask"嵌进 continentalness）共享同一份 spline，输出口径一致。</para>
    /// </summary>
    public static class MCNoiseRouter
    {
        /// <summary>把 Unity Mathf.PerlinNoise 实际偏窄的 [~0.15, ~0.85] 输出拉回到全幅 [0,1]，
        /// 让 spline 阈值真正用到全部范围（避免极端段永远走不到）。</summary>
        public static float Stretch(float p) => Mathf.Clamp01((p - 0.15f) / 0.70f);

        /// <summary>Weirdness → 振幅权重（单调），<c>pv = (w + 1) / 2 ∈ [0, 1]</c>。
        /// <para>原 MC 公式 <c>1 − |3|w| − 2|</c> 是**非单调三段函数**：|w|=0 出谷、|w|=2/3 出峰、|w|=1 又回 0。
        /// 在 2D 高度图里这会让任何 Perlin 圆斑形成抬升环 —— 中心 pv=0 不抬、外围 |w|≈2/3 处 pv=+1 高抬，
        /// baseline≈SeaLevel 时就长出"边缘陆地、中心水"的甜甜圈岛屿（用户实际观察到）。</para>
        /// <para>替换为 <c>w</c> 的单调线性映射 → amplitude 随 weirdness 平滑变化，
        /// 不再有圆环 / 甜甜圈图案，地表呈自然渐变（高频起伏由 erosion 自身提供）。</para>
        /// <para>想恢复 MC 真三段 pv 用<see cref="PvTriphasic"/>（仅适合做 3D 体积密度，不适合 2D heightmap）。</para></summary>
        public static float Pv(float w) => (w + 1f) * 0.5f;

        /// <summary>原 MC 1.18+ 三段 pv 公式（保留作参考；2D heightmap 慎用，会出甜甜圈）。</summary>
        public static float PvTriphasic(float w)
            => Mathf.Clamp(1f - Mathf.Abs(3f * Mathf.Abs(w) - 2f), -1f, 1f);

        /// <summary>Continentalness 分段线性 spline → 在海平面上下的基线偏移（block，单位幅）。
        /// 锚点：深海 -20 / 海 -10 / 海岸 0 / 沉积低地 +4 / 中地 +10 / 高地 +20。
        /// （v2 调整：极端段 -25/+30 → -20/+20，平缓 ~33%，仍保留 MC 风海陆过渡）</summary>
        public static float ContinentalnessSpline(float c)
        {
            if (c <= -0.7f) return Mathf.Lerp(-20f, -12f, (c + 1.0f) / 0.30f);
            if (c <= -0.3f) return Mathf.Lerp(-12f,  -4f, (c + 0.7f) / 0.40f);
            if (c <=  0.0f) return Mathf.Lerp( -4f,   0f, (c + 0.3f) / 0.30f); // 海岸过渡
            if (c <=  0.2f) return Mathf.Lerp(  0f,  +4f,  c          / 0.20f); // 沙滩带
            if (c <=  0.6f) return Mathf.Lerp( +4f, +10f, (c - 0.2f) / 0.40f);
            return Mathf.Lerp(+10f, +20f, (c - 0.6f) / 0.40f);
        }

        /// <summary>Erosion 分段线性 spline → 山势振幅。-1 崎岖崇山 18 / +1 侵蚀平原 3。
        /// （v2 调整：30/22/14/8/4 → 18/14/10/6/3，振幅 ~40% 降幅）</summary>
        public static float ErosionSpline(float e)
        {
            if (e <= -0.5f) return Mathf.Lerp(18f, 14f, (e + 1.0f) / 0.50f);
            if (e <=  0.0f) return Mathf.Lerp(14f, 10f, (e + 0.5f) / 0.50f);
            if (e <=  0.5f) return Mathf.Lerp(10f,  6f,  e          / 0.50f);
            return Mathf.Lerp(6f, 3f, (e - 0.5f) / 0.50f);
        }

        /// <summary>采样三通道噪声并出 <c>(c, e, w, pv)</c>。生成器自管偏移，避免相互锁相。</summary>
        public static void SampleChannels(
            int wx, int wz,
            float contOffX, float contOffZ, float contScale,
            float eroOffX,  float eroOffZ,  float eroScale,
            float wOffX,    float wOffZ,    float wScale,
            out float c, out float e, out float w, out float pv)
        {
            c = Stretch(Mathf.PerlinNoise((wx + contOffX) * contScale, (wz + contOffZ) * contScale)) * 2f - 1f;
            e = Stretch(Mathf.PerlinNoise((wx + eroOffX)  * eroScale,  (wz + eroOffZ)  * eroScale))  * 2f - 1f;
            w = Stretch(Mathf.PerlinNoise((wx + wOffX)    * wScale,    (wz + wOffZ)    * wScale))    * 2f - 1f;
            pv = Pv(w);
        }
    }
}
