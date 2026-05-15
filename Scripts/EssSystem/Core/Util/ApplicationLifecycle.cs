using UnityEngine;

namespace EssSystem.Core.Util
{
    /// <summary>
    /// 全局应用生命周期信号 —— 解决 Unity 退出 Play 时单例销毁顺序不可控的问题。
    /// <para><see cref="IsQuitting"/> 在 <c>Application.quitting</c> 时设为 true（Editor 按 Stop / Build 退出均触发）。
    /// 所有事件分发、UI 清理等 teardown 路径可检测此标志并 silent-return，避免访问已销毁的 Unity Object。</para>
    /// <para>场景内切换（<c>SceneManager.LoadScene</c>）**不会**设此标志——DontDestroyOnLoad 的单例仍然存活，事件照常运作。</para>
    /// </summary>
    public static class ApplicationLifecycle
    {
        /// <summary>应用正在退出（Editor Stop Play / Build 退出）。</summary>
        public static bool IsQuitting { get; private set; }

        /// <summary>
        /// 早于任何 Awake/OnDestroy 被调用（SubsystemRegistration），每次进入 Play 重置标志。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            IsQuitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
        }

        private static void OnQuitting()
        {
            IsQuitting = true;
        }
    }
}
