namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting.Dao
{
    /// <summary>
    /// 光照系统常量 —— 模仿 MC 0..15 整数光级。
    /// <para>组合公式：<c>final = max(skyLight × DayCycle, blockLight)</c>。</para>
    /// <para>顶点色映射：<c>brightness01 = AmbientFloor + (1 - AmbientFloor) × (light/15)</c>，
    /// 让最暗也不至于全黑（<see cref="AmbientFloor01"/>，默认 0.18 = MC night 视觉手感）。</para>
    /// </summary>
    public static class VoxelLightConstants
    {
        /// <summary>整数光级上限（与 MC 一致 0..15）。</summary>
        public const byte MaxLight = 15;

        /// <summary>正午全亮 sky light。</summary>
        public const byte SkyLightFullDay = 15;

        /// <summary>顶点色最暗时的下限亮度（避免完全漆黑看不清）。</summary>
        public const float AmbientFloor01 = 0.18f;

        /// <summary>把 0..15 整数光级转 0..1 顶点色亮度系数（已含 AmbientFloor）。</summary>
        public static float ToBrightness01(byte light)
        {
            var t = light / (float)MaxLight;
            return AmbientFloor01 + (1f - AmbientFloor01) * t;
        }
    }
}
