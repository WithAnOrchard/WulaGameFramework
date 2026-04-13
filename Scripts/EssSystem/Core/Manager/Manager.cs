using UnityEngine;

namespace EssSystem.Core.Manager
{
    /// <summary>
    /// Manager抽象类，继承自SingletonMono，用于Unity GameObject管理器
    /// </summary>
    /// <typeparam name="T">Manager类型</typeparam>
    public abstract class Manager<T> : SingletonMono<T> where T : MonoBehaviour
    {
        /// <summary>
        /// Manager初始化方法，在Awake后调用
        /// </summary>
        protected override void Initialize()
        {
            // 子类可重写此方法进行初始化操作
            base.Initialize();
        }

        /// <summary>
        /// Unity Awake方法
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        /// <summary>
        /// Unity Start方法
        /// </summary>
        protected virtual void Start()
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity Update方法
        /// </summary>
        protected virtual void Update()
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity FixedUpdate方法
        /// </summary>
        protected virtual void FixedUpdate()
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity LateUpdate方法
        /// </summary>
        protected virtual void LateUpdate()
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity OnDestroy方法
        /// </summary>
        protected virtual void OnDestroy()
        {
            // 子类可重写此方法进行清理操作
        }

        /// <summary>
        /// Unity OnEnable方法
        /// </summary>
        protected virtual void OnEnable()
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity OnDisable方法
        /// </summary>
        protected virtual void OnDisable()
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity OnApplicationPause方法
        /// </summary>
        /// <param name="pauseStatus">暂停状态</param>
        protected virtual void OnApplicationPause(bool pauseStatus)
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Unity OnApplicationFocus方法
        /// </summary>
        /// <param name="hasFocus">是否有焦点</param>
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            // 子类可重写此方法
        }

        /// <summary>
        /// Manager销毁时的清理方法
        /// </summary>
        protected virtual void Cleanup()
        {
            // 子类可重写此方法进行清理操作
        }
    }
}
