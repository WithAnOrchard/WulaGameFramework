using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Manager
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

        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        protected virtual void Update()
        {
            if (_showServiceDataInInspector) UpdateServiceInspectorInfo();
            SyncServiceLoggingSettings();
        }

        /// <summary>子类重写：将 <c>_serviceEnableLogging</c> 同步到关联 Service。</summary>
        protected virtual void SyncServiceLoggingSettings() { }

        /// <summary>子类重写：调用 Service.UpdateInspectorInfo() 并赋值 <c>_serviceInspectorInfo</c>。</summary>
        protected virtual void UpdateServiceInspectorInfo() { }

        /// <summary>初始化（Awake 内自动调用）。子类重写记得调用 <c>base.Initialize()</c>。</summary>
        protected override void Initialize() => base.Initialize();
    }
}