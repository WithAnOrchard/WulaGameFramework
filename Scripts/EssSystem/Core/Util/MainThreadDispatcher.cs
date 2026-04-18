using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace EssSystem.Core.Util
{
    /// <summary>
    /// 主线程调度器 — 把后台线程的回调转发到 Unity 主线程执行
    /// 使用方式：
    /// <code>
    /// Task.Run(() => {
    ///     // 后台线程处理
    ///     MainThreadDispatcher.Enqueue(() => {
    ///         // 主线程回调（可安全使用 Unity API）
    ///     });
    /// });
    /// </code>
    /// 自动在第一次 <see cref="Enqueue"/> 时创建隐藏的 GameObject 驱动 Update 泵循环。
    /// </summary>
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private static readonly object _spawnLock = new object();

        /// <summary>
        /// 把 action 加入主线程执行队列
        /// </summary>
        public static void Enqueue(Action action)
        {
            if (action == null) return;
            _queue.Enqueue(action);
            EnsureInstance();
        }

        private static void EnsureInstance()
        {
            if (_instance != null) return;

            lock (_spawnLock)
            {
                if (_instance != null) return;

                // 仅在运行时创建，编辑器模式下也要求有 Application 存在
                if (!Application.isPlaying) return;

                var go = new GameObject("[MainThreadDispatcher]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        private void Update()
        {
            while (_queue.TryDequeue(out var action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] 执行主线程回调时发生异常: {ex}");
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
