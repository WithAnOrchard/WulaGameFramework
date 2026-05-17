namespace EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions
{
    /// <summary>
    /// 复合动作 —— 按顺序执行子 Action。
    /// <para>
    /// 每个子 Action Success 后推进到下一个；全部完成 → Success。
    /// 任一子 Action Failure → 整体 Failure。
    /// </para>
    /// </summary>
    public class SequenceAction : IBrainAction
    {
        private readonly IBrainAction[] _actions;
        private int _currentIndex;

        public SequenceAction(params IBrainAction[] actions)
        {
            _actions = actions ?? new IBrainAction[0];
        }

        public void OnEnter(BrainContext ctx)
        {
            _currentIndex = 0;
            if (_actions.Length > 0)
                _actions[0].OnEnter(ctx);
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            if (_actions.Length == 0) return BrainStatus.Success;

            while (_currentIndex < _actions.Length)
            {
                var status = _actions[_currentIndex].Tick(ctx, deltaTime);
                switch (status)
                {
                    case BrainStatus.Running:
                        return BrainStatus.Running;

                    case BrainStatus.Success:
                        _actions[_currentIndex].OnExit(ctx);
                        _currentIndex++;
                        if (_currentIndex < _actions.Length)
                            _actions[_currentIndex].OnEnter(ctx);
                        break;

                    case BrainStatus.Failure:
                        _actions[_currentIndex].OnExit(ctx);
                        return BrainStatus.Failure;
                }
            }

            return BrainStatus.Success;
        }

        public void OnExit(BrainContext ctx)
        {
            // 被抢占时确保当前子 Action 也 OnExit
            if (_currentIndex < _actions.Length)
                _actions[_currentIndex].OnExit(ctx);
        }
    }
}
