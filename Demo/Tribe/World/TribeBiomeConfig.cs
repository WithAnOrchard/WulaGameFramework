using System.Collections.Generic;
using UnityEngine;

namespace Demo.Tribe.World
{
    /// <summary>
    /// 单个群系（Biome）配置 —— 横向带状世界的一段。
    /// <para>
    /// 设计参考 <c>Demo/Tribe/ToDo.md</c> 条目 #1。骨架阶段不做过渡带 / 背景 override /
    /// AmbientSound，仅承载基础字段 + Features 列表。
    /// </para>
    /// </summary>
    public class TribeBiomeConfig
    {
        /// <summary>唯一 Id（"meadow" / "forest" / "town" / "swamp" / "rocky" / "snow" / "ruins"）。</summary>
        public string Id;

        /// <summary>显示名（HUD / 提示用）。</summary>
        public string DisplayName;

        /// <summary>X 起点（世界绝对坐标）。</summary>
        public float StartX;

        /// <summary>X 终点（世界绝对坐标）。</summary>
        public float EndX;

        /// <summary>地表着色（占位条 + 边界标识用，未来替换为 tilemap tint）。</summary>
        public Color GroundTint = Color.gray;

        /// <summary>该 biome 内的所有特征。</summary>
        public List<TribeFeatureSpec> Features = new List<TribeFeatureSpec>();

        public TribeBiomeConfig() { }

        public TribeBiomeConfig(string id, string displayName, float startX, float endX, Color tint)
        {
            Id = id; DisplayName = displayName;
            StartX = startX; EndX = endX;
            GroundTint = tint;
        }

        public TribeBiomeConfig Add(TribeFeatureSpec feature)
        {
            if (feature != null) Features.Add(feature);
            return this;
        }
    }
}
