using System;

namespace Demo.DobeCat.Game
{
    /// <summary>
    /// 轻量级游戏上下文追踪器 —— 支持多个小游戏同时请求上下文（引用计数）。
    /// <list type="bullet">
    /// <item>任意小游戏调用 <see cref="Enter"/> 激活上下文 → 背包快捷栏显示。</item>
    /// <item>所有小游戏调用 <see cref="Exit"/> 后上下文关闭 → 快捷栏隐藏、背包关闭。</item>
    /// <item><see cref="OnContextChanged"/> 由 <c>DobeCatGameManager</c> 订阅来驱动 UI 可见性。</item>
    /// </list>
    /// </summary>
    public static class DobeCatGameContext
    {
        private static int _count;

        /// <summary>当前是否有任何小游戏上下文活跃。</summary>
        public static bool IsActive => _count > 0;

        /// <summary>上下文活跃状态改变时触发。参数为新状态（true=激活，false=关闭）。</summary>
        public static event Action<bool> OnContextChanged;

        /// <summary>进入一个游戏上下文（引用计数 +1）。</summary>
        public static void Enter()
        {
            _count++;
            if (_count == 1) OnContextChanged?.Invoke(true);
        }

        /// <summary>退出一个游戏上下文（引用计数 -1，降到 0 时广播关闭）。</summary>
        public static void Exit()
        {
            if (_count <= 0) return;
            _count--;
            if (_count == 0) OnContextChanged?.Invoke(false);
        }
    }
}
