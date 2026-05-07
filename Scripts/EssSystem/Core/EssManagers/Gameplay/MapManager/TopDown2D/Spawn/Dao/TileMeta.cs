namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Spawn.Dao
{
    /// <summary>
    /// 地图生成器对单个 Tile 暴露的元数据（群系 / 海拔 / 温湿度 等）。
    /// <para>
    /// 仅由 <see cref="IMapMetaProvider"/> 实现者按需查询；不存档、不暴露给业务运行态。
    /// 所有数值已归一化到 <c>[0,1]</c>，过滤条件统一用 <c>FloatRange</c> 比较。
    /// </para>
    /// </summary>
    public struct TileMeta
    {
        /// <summary>群系 ID（与 <c>TileTypes.*</c> 中的群系常量一致；非群系类（如 ocean / hill / mountain）也允许填）。</summary>
        public string BiomeId;
        /// <summary>归一化海拔 [0,1]。</summary>
        public float Elevation;
        /// <summary>归一化温度 [0,1]。</summary>
        public float Temperature;
        /// <summary>归一化湿度 [0,1]。</summary>
        public float Moisture;
        /// <summary>归一化大陆度 [0,1]（生成器无此概念时填 0 即可）。</summary>
        public float Continentalness;
    }
}
