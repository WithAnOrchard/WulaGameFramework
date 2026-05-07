using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Spawn.Dao
{
    /// <summary>
    /// 可选整数闭区间过滤器（与 2D <c>FloatRange</c> 平行；用于 column 高度等整数 spawn 过滤）。
    /// <para><see cref="HasValue"/> = false 时视为"不限"。JsonUtility 不支持 <c>Nullable&lt;T&gt;</c>，用本结构体显式表达"未设置"。</para>
    /// </summary>
    [Serializable]
    public struct IntRange
    {
        public bool HasValue;
        public int Min;
        public int Max;

        public bool Contains(int v) => !HasValue || (v >= Min && v <= Max);

        public static IntRange Of(int min, int max) =>
            new() { HasValue = true, Min = min, Max = max };
    }
}
