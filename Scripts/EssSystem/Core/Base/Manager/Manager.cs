using System;
using EssSystem.Core.Base.Singleton;
using EssSystem.Core.Base.Event;
using UnityEngine;

namespace EssSystem.Core.Base.Manager
{
    /// <summary>
    ///     Manager 优先级特性 — 等效于 Unity 的 <see cref="DefaultExecutionOrder" />。
    ///     <para>
    ///         数值越小越先 Awake/Start。推荐 EventProcessor 用 -30，DataManager 用 -20，业务 Manager &gt;= 0。
    ///     </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ManagerAttribute : DefaultExecutionOrder
    {
        public ManagerAttribute(int priority = 0) : base(priority)
        {
        }
    }

    /// <summary>
    ///     Manager 抽象基类（MonoBehaviour 单例）。
    ///     <para>子类如需 Start/FixedUpdate/LateUpdate/OnEnable/OnDisable 等生命周期，直接在子类声明即可（Unity 反射调用）。</para>
    ///     <para>必须 override 的钩子：<see cref="Initialize" />, <see cref="SyncServiceLoggingSettings" />, <see cref="UpdateServiceInspectorInfo" />。</para>
    /// </summary>
    public abstract class Manager<T> : SingletonMono<T> where T : MonoBehaviour
    {
        [Header("Service Data Inspector")]
        [Tooltip("是否在 Inspector 中显示关联 Service 的数据（每帧更新）")]
        [SerializeField] protected bool _showServiceDataInInspector = true;

        [Tooltip("Service 数据摘要（只读显示，由 Service 自动更新）")]
        [SerializeField] protected ServiceDataInspectorInfo _serviceInspectorInfo;

        [Header("Service Settings")]
        [Tooltip("是否启用 Service 日志打印")]
        [SerializeField] protected bool _serviceEnableLogging = true;

        [Tooltip("Inspector 数据刷新间隔（秒）— Inspector 只供调试，过高频率会产生大量 GC。默认 0.25s。")]
        [SerializeField] protected float _inspectorRefreshInterval = 0.25f;

        private float _nextInspectorRefreshTime;

        protected override void Awake()
        {
            base.Awake();
            if (!ReferenceEquals(TryGetInstance(), this)) return;

            Initialize();
            // 初始化时同步日志设置一次（仅在启动时，不在运行时实时同步）
            SyncServiceLoggingSettings();
        }

        protected virtual void Update()
        {
            // Inspector 同步节流 — 仅在 Editor 模式下运行，避免 Build 模式下的无谓开销
            // Inspector 信息仅用于 Editor 调试，Build 模式下无意义
#if UNITY_EDITOR
            if (_showServiceDataInInspector)
            {
                var now = Time.unscaledTime;
                if (now >= _nextInspectorRefreshTime)
                {
                    _nextInspectorRefreshTime = now + Mathf.Max(0f, _inspectorRefreshInterval);
                    UpdateServiceInspectorInfo();
                }
            }
#endif
        }

        protected override void OnDestroy()
        {
            OnManagerDestroy();
            base.OnDestroy();
        }

        /// <summary>
        /// 子类重写：将 <c>_serviceEnableLogging</c> 同步到关联 Service。
        /// <para>仅在 Awake 中调用一次，日志打印设置仅在重启后生效。</para>
        /// </summary>
        protected virtual void SyncServiceLoggingSettings() { }

        /// <summary>子类重写：调用 Service.UpdateInspectorInfo() 并赋值 <c>_serviceInspectorInfo</c>。</summary>
        protected virtual void UpdateServiceInspectorInfo() { }

        /// <summary>Manager 销毁时的清理钩子（Phase 1.2 优化）。子类可重写以实现自定义清理逻辑。</summary>
        protected virtual void OnManagerDestroy() { }

        /// <summary>初始化（Awake 内自动调用）。子类重写记得调用 <c>base.Initialize()</c>。</summary>
        protected override void Initialize() => base.Initialize();
    }
}
