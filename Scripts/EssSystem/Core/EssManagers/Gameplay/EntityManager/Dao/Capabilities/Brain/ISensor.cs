namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Brain
{
    /// <summary>
    /// Brain 感知器接口 —— 周期性刷新 <see cref="BrainContext"/> 中的感知数据。
    /// <para>
    /// 典型实现：<see cref="Default.RangeSensor"/>（OverlapCircle 检测附近实体）。
    /// 可叠加多个 Sensor（视觉锥、听觉范围等）。
    /// </para>
    /// </summary>
    public interface ISensor
    {
        /// <summary>感知刷新间隔（秒）。0 = 每帧。</summary>
        float Interval { get; }

        /// <summary>执行一次感知，结果写入 <paramref name="ctx"/>。</summary>
        void Sense(BrainContext ctx);
    }
}
