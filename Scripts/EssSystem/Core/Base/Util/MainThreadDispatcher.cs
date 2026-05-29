using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace EssSystem.Core.Base.Util
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

        [Header("Performance")]
        [Tooltip("每帧最大处理回调数量，防止队列积压时卡帧")]
        [SerializeField] private int _maxCallbacksPerFrame = 64;

        [Tooltip("每帧最大处理时间（毫秒），防止长时间阻塞")]
        [SerializeField] private float _maxTimePerFrameMs = 8f;

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
                if (!UnityEngine.Application.isPlaying) return;

                var go = new GameObject("[MainThreadDispatcher]");
                go.hideFlags = HideFlags.HideAndDontSave;
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }

        private void Update()
        {
            if (_queue.IsEmpty) return;

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            int processed = 0;

            while (processed < _maxCallbacksPerFrame && _queue.TryDequeue(out var action))
            {
                if (stopwatch.ElapsedMilliseconds >= _maxTimePerFrameMs)
                {
                    Debug.LogWarning($"[MainThreadDispatcher] 达到每帧时间上限，剩余 {_queue.Count} 个回调将在下一帧处理");
                    break;
                }

                try
                {
                    action?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MainThreadDispatcher] 执行主线程回调时发生异常: {ex.Message}\n{ex.StackTrace}");
                }
                processed++;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
