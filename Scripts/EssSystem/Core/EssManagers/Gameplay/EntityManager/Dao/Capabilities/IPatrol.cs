using System;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 巡逻 AI 能力 —— Entity 在指定模式下自主移动。
    /// <para>典型实现：横向往返（<see cref="Default.HorizontalPatrolComponent"/>）；高级实现可叠加路径点 / 警戒等。</para>
    /// <para>由业务侧每帧调 <see cref="Tick"/> 推进；本能力不自动订阅 Update。</para>
    /// </summary>
    public interface IPatrol : IEntityCapability
    {
        /// <summary>当前朝向：+1 = 正向，-1 = 反向；其它实现可自定义语义。</summary>
        int Direction { get; }

        /// <summary>是否暂停巡逻（例如受击 / 死亡）。</summary>
        bool Paused { get; set; }

        /// <summary>是否正在移动（非暂停且速度 > 0）。</summary>
        bool IsMoving { get; }

        /// <summary>推进一帧。</summary>
        void Tick(float deltaTime);

        /// <summary>方向变化时触发；参数为新方向。</summary>
        event Action<int> DirectionChanged;
    }
}
