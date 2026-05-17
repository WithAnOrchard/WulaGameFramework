using UnityEngine;

namespace Demo.Tribe.World
{
    /// <summary>
    /// 地图特征基类 —— 所有可放置在 biome 上的"东西"（采集物 / 装饰 / 工作台 / 传送门 / NPC ...）
    /// 都派生自此。
    /// <para>
    /// 由 <see cref="TribeBiomeRegistry"/> 在 Build 时按 biome 顺序调用 <see cref="Build"/>，
    /// 子类负责实例化 GameObject + 挂渲染器 / 碰撞 / 业务组件。
    /// </para>
    /// </summary>
    public abstract class TribeFeatureSpec
    {
        /// <summary>Feature 在所属 biome 内的世界 X 坐标（绝对值，不是相对偏移）。</summary>
        public float WorldX;

        /// <summary>地面 Y 偏移（正值 = 地面之上）。</summary>
        public float YOffset;

        /// <summary>实例化此 Feature 到场景。子类实现具体表现 / 行为。</summary>
        public abstract void Build(TribeBiomeContext ctx);

        /// <summary>计算最终世界坐标（基于 ctx.GroundY + YOffset）。</summary>
        protected Vector3 ComputeWorldPosition(TribeBiomeContext ctx) =>
            new Vector3(WorldX, ctx.GroundY + YOffset, 0f);
    }
}
