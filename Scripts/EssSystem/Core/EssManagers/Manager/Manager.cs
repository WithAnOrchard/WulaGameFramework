using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Manager
{
    /// <summary>
    ///     Manager 优先级特性 — 等效于 Unity 的 <see cref="DefaultExecutionOrder" />。
    ///     <para>
    ///         数值越小越先 Awake/Start。推荐 EventManager 用 -10，DataManager 用 -5，业务 Manager &gt;= 0。
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
    ///     Manager抽象类，继承自SingletonMono，用于Unity GameObject管理器
    /// </summary>
    /// <typeparam name="T">Manager类型</typeparam>
    public abstract class Manager<T> : SingletonMono<T> where T : MonoBehaviour
    {
        /// <summary>
        ///     Unity Awake方法
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        /// <summary>
        ///     Unity Start方法
        /// </summary>
        protected virtual void Start()
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity Update方法
        /// </summary>
        protected virtual void Update()
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity FixedUpdate方法
        /// </summary>
        protected virtual void FixedUpdate()
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity LateUpdate方法
        /// </summary>
        protected virtual void LateUpdate()
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity OnEnable方法
        /// </summary>
        protected virtual void OnEnable()
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity OnDisable方法
        /// </summary>
        protected virtual void OnDisable()
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity OnDestroy方法 — 基类负责清理静态单例引用，子类可重写追加业务清理
        /// </summary>
        protected override void OnDestroy()
        {
            // 子类可重写此方法进行清理操作，但需要记得调用 base.OnDestroy()
            base.OnDestroy();
        }

        /// <summary>
        ///     Unity OnApplicationFocus方法
        /// </summary>
        /// <param name="hasFocus">是否有焦点</param>
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Unity OnApplicationPause方法
        /// </summary>
        /// <param name="pauseStatus">暂停状态</param>
        protected virtual void OnApplicationPause(bool pauseStatus)
        {
            // 子类可重写此方法
        }

        /// <summary>
        ///     Manager初始化方法，在Awake后调用
        /// </summary>
        protected override void Initialize()
        {
            // 子类可重写此方法进行初始化操作
            base.Initialize();
        }

        /// <summary>
        ///     Manager销毁时的清理方法
        /// </summary>
        protected virtual void Cleanup()
        {
            // 子类可重写此方法进行清理操作
        }
    }
}