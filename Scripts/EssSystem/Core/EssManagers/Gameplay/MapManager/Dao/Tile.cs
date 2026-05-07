using System;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Dao
{
    /// <summary>
    /// 单个 Tile —— 通用层最小单元。
    /// <para>
    /// 仅持有类型 ID（<see cref="TypeId"/>），具体外观 / 物理 / 行为属性由对应 <see cref="TileTypes"/>
    /// 注册项决定，便于序列化和按类型批量替换。
    /// </para>
    /// <para>
    /// 后续若需要 per-tile 状态（如农作物生长阶段、损毁度等）可扩展为 <c>Dictionary&lt;string,object&gt; State</c>
    /// 或派生子类，但请保证默认构造函数 + 公开字段以兼容 <see cref="UnityEngine.JsonUtility"/>。
    /// </para>
    /// </summary>
    [Serializable]
    public class Tile
    {
        /// <summary>Tile 类型 ID（参见 <see cref="TileTypes"/>）。</summary>
        public string TypeId;

        /// <summary>
        /// 海拔 / 地幔线值（0~255，等价 [0,1] × 255）。
        /// <para>由地图生成器写入，供山脉 / 河流 / 温度 / 资源分布等下游层读取。</para>
        /// </summary>
        public byte Elevation;

        /// <summary>温度（0~255 ↔ [0,1]）。0 = 极寒，255 = 酷热。</summary>
        public byte Temperature;

        /// <summary>湿度 / 降水（0~255 ↔ [0,1]）。0 = 极干（沙漠），255 = 极湿（雨林/沼泽）。</summary>
        public byte Moisture;

        /// <summary>河流流量（0 = 非河流，&gt;0 = 河流且累积流量）。由 RiverTracer 写入。</summary>
        public byte RiverFlow;

        public Tile() { }

        public Tile(string typeId) { TypeId = typeId; }

        public Tile(string typeId, byte elevation)
        {
            TypeId = typeId;
            Elevation = elevation;
        }

        public Tile(string typeId, byte elevation, byte temperature, byte moisture)
        {
            TypeId = typeId;
            Elevation = elevation;
            Temperature = temperature;
            Moisture = moisture;
        }

        /// <summary>把 byte 海拔转换回归一化的 [0,1]。</summary>
        public float ElevationNormalized => Elevation / 255f;

        /// <summary>归一化温度 [0,1]。</summary>
        public float TemperatureNormalized => Temperature / 255f;

        /// <summary>归一化湿度 [0,1]。</summary>
        public float MoistureNormalized => Moisture / 255f;

        public override string ToString() =>
            $"Tile({TypeId}, h={Elevation}, T={Temperature}, M={Moisture})";
    }
}
