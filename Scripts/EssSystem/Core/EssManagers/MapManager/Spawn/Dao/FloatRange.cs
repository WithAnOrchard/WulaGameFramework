using System;

namespace EssSystem.EssManager.MapManager.Spawn.Dao
{
    /// <summary>
    /// 可选浮点闭区间过滤器。<see cref="HasValue"/> 为 false 时视为"不限"，即任何输入都通过。
    /// <para>专为 <c>EntitySpawnRule</c> 的 Elevation/Temperature/Moisture 范围设计：
    /// JsonUtility 不支持 <c>Nullable&lt;T&gt;</c>，用本结构体显式表达"未设置"语义。</para>
    /// </summary>
    [Serializable]
    public struct FloatRange
    {
        /// <summary>true 时启用 Min/Max 比较；false 时不做约束。</summary>
        public bool HasValue;
        public float Min;
        public float Max;

        public bool Contains(float v) => !HasValue || (v >= Min && v <= Max);

        public static FloatRange Of(float min, float max) =>
            new() { HasValue = true, Min = min, Max = max };
    }
}
