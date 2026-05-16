namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Brain.Actions
{
    /// <summary>
    /// 空闲动作 —— 原地等待指定时长后 Success。
    /// </summary>
    public class IdleAction : IBrainAction
    {
        private readonly float _duration;
        private float _elapsed;

        /// <param name="duration">空闲持续时间（秒）。0 = 单帧即完成。</param>
        public IdleAction(float duration = 2f)
        {
            _duration = duration;
        }

        public void OnEnter(BrainContext ctx)
        {
            _elapsed = 0f;
            ctx.IsMoving = false;
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            ctx.IsMoving = false;
            _elapsed += deltaTime;
            return _elapsed >= _duration ? BrainStatus.Success : BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx) { }
    }
}
