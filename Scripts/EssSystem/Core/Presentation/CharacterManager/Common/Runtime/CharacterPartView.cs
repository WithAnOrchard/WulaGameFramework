using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace EssSystem.Core.Presentation.CharacterManager.Runtime
{
    /// <summary>
    /// 单个部件运行时 View 的<b>抽象基类</b> ——
    /// 由 <see cref="CharacterView.Build"/> 按 <see cref="CharacterConfig.RenderMode"/>
    /// 分派到 <see cref="CharacterPartView2D"/> 或 <see cref="CharacterPartView3D"/>。
    /// <para>对外提供统一接口：<see cref="Setup"/> / <see cref="Play"/> / <see cref="Stop"/> /
    /// <see cref="SetVisible"/> + <see cref="OnActionComplete"/>。</para>
    /// <para>共同职责：应用 <see cref="CharacterPartConfig.LocalPosition"/> /
    /// <see cref="CharacterPartConfig.LocalScale"/> /
    /// <see cref="CharacterPartConfig.LocalEulerAngles"/> / <see cref="CharacterPartConfig.IsVisible"/>，
    /// 以及通过 <see cref="EventProcessor"/> 广播 <see cref="CharacterService.EVT_FRAME_EVENT"/> 的工具方法。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class CharacterPartView : MonoBehaviour
    {
        /// <summary>部件配置（绑定时存下来）。</summary>
        public CharacterPartConfig Config { get; protected set; }

        /// <summary>
        /// 非循环动作播完时触发；2D = 末帧贴上后；3D = Animator state normalizedTime ≥ 1。
        /// 参数：刚播完的 <c>actionName</c>。<see cref="CharacterView.PlayThenReturn"/> 基于此实现。
        /// </summary>
        public event System.Action<string> OnActionComplete;

        /// <summary>
        /// 是否能作为 <see cref="CharacterView"/> 完成事件聚合的 pivot 部件。
        /// 2D：仅 Dynamic 部件；3D：恒为 true（Animator 始终存在）。
        /// </summary>
        public abstract bool CanPivotComplete { get; }

        #region Public API

        /// <summary>
        /// 由 <see cref="CharacterView"/> 在挂载部件时调用 —— 应用通用 Transform/可见性，
        /// 然后委派到 <see cref="OnSetup"/> 让子类完成自身渲染初始化。
        /// </summary>
        public void Setup(CharacterPartConfig config)
        {
            Config = config ?? new CharacterPartConfig();

            transform.localPosition    = Config.LocalPosition;
            transform.localScale       = Config.LocalScale;
            transform.localEulerAngles = Config.LocalEulerAngles;
            gameObject.SetActive(Config.IsVisible);

            OnSetup();
        }

        /// <summary>开始播放指定动作；返回是否成功。</summary>
        public abstract bool Play(string actionName);

        /// <summary>停止当前动作（具体定义由子类决定）。</summary>
        public abstract void Stop();

        /// <summary>显示/隐藏部件（直接 SetActive）。</summary>
        public virtual void SetVisible(bool visible) => gameObject.SetActive(visible);

        #endregion

        #region Protected helpers (for subclasses)

        /// <summary>子类完成自身渲染初始化的钩子（Renderer / Animator 等）。</summary>
        protected abstract void OnSetup();

        /// <summary>触发完成事件 —— 仅供子类调用。</summary>
        protected void RaiseActionComplete(string actionName)
        {
            OnActionComplete?.Invoke(actionName);
        }

        /// <summary>
        /// 广播一帧/一次 Animator 帧事件 —— 仅供子类调用。
        /// 自动 <c>HasListener</c> 判空；frameIndex 在 3D 通常为 -1。
        /// </summary>
        protected void BroadcastFrameEvent(string evtName, string actionName, int frameIndex)
        {
            if (string.IsNullOrEmpty(evtName)) return;
            var ep = EventProcessor.Instance;
            if (ep == null || !ep.HasListener(CharacterService.EVT_FRAME_EVENT)) return;
            ep.TriggerEventMethod(CharacterService.EVT_FRAME_EVENT,
                new List<object> { gameObject, evtName, actionName, frameIndex });
        }

        #endregion
    }
}
