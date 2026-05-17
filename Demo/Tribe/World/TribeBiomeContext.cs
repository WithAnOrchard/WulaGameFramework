using UnityEngine;

namespace Demo.Tribe.World
{
    /// <summary>
    /// Biome / Feature 构建期共享上下文 —— 由 <see cref="TribeBiomeRegistry"/> 在 Build 时
    /// 构造一次，按顺序传给每个 Biome 与 Feature。
    /// </summary>
    public class TribeBiomeContext
    {
        /// <summary>所有 biome 内容会挂到的根节点（建议 = TribeWorld 子节点）。</summary>
        public Transform WorldRoot;

        /// <summary>采集物 / 怪物专用根（与 _gatherablesRoot / _enemiesRoot 对齐）。</summary>
        public Transform GatherablesRoot;
        public Transform EnemiesRoot;

        /// <summary>地表世界 Y（玩家脚下平面）。Feature 默认在此 Y 之上 spawn。</summary>
        public float GroundY;

        /// <summary>采集物 / 装饰用的 sortingOrder 基准（来自 Layer_3_4_BACK 同层）。</summary>
        public int BaseSortingOrder;

        /// <summary>当前正在构建的 biome（Feature 可读，了解所属归属）。</summary>
        public TribeBiomeConfig CurrentBiome;
    }
}
