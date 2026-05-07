using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao
{
    /// <summary>
    /// 单个动作（动画）的配置 —— 一个动作 = 一组按顺序播放的 Sprite。
    /// <para>用于动态部件 <c>CharacterPartConfig.Animations</c>。</para>
    /// </summary>
    [Serializable]
    public class CharacterActionConfig
    {
        /// <summary>动作名称，部件内唯一（例如 "Idle" / "Walk" / "Attack"）。</summary>
        public string ActionName = "Idle";

        /// <summary>构成该动作的帧序列（按 <see cref="EssSystem.EssManager.ResourceManager"/> 注册的 Sprite Id）。</summary>
        public List<string> SpriteIds = new List<string>();

        /// <summary>每秒播放帧数（FPS）。</summary>
        public float FrameRate = 12f;

        /// <summary>是否循环播放。<c>false</c> 时停在最后一帧。</summary>
        public bool Loop = true;

        /// <summary>
        /// 帧事件 —— 当动作播到指定 <c>frameIndex</c> 时，通过 <c>EventProcessor</c> 广播
        /// <see cref="CharacterService.EVT_FRAME_EVENT"/>（<c>"CharacterFrameEvent"</c>），
        /// 参数 <c>[GameObject owner, string eventName, string actionName, int frameIndex]</c>。
        /// 业务层（战斗伤害判定 / 音效 / 特效）监听该事件按 eventName 分发。
        /// <para><b>仅 2D Sprite 模式生效</b>；Prefab3D 请使用 <see cref="NormalizedTimeEvents"/>。</para>
        /// </summary>
        public Dictionary<int, string> FrameEvents;

        // ─── 3D Animator 模式专用 ───

        /// <summary>
        /// 3D 模式：Animator 状态名（与 Animator Controller 中的 State 名一致）。
        /// 为空时退化使用 <see cref="ActionName"/>。
        /// </summary>
        public string AnimatorStateName = string.Empty;

        /// <summary>3D 模式：Animator Layer（默认 0）。</summary>
        public int AnimatorLayer = 0;

        /// <summary>3D 模式：CrossFade 混合时长（秒，归一化到源 state）；≤ 0 表示硬切。</summary>
        public float CrossFadeDuration = 0.1f;

        /// <summary>
        /// 3D 模式的帧事件：键 = normalizedTime（0..1+）阈值，值 = eventName。
        /// <c>CharacterPartView3D</c> 在本帧 normalizedTime 首次跨越阈值时通过
        /// <see cref="CharacterService.EVT_FRAME_EVENT"/> 广播（frameIndex 以 -1 代表）。
        /// </summary>
        public Dictionary<float, string> NormalizedTimeEvents;

        public CharacterActionConfig() { }

        public CharacterActionConfig(string actionName) { ActionName = actionName; }

        public CharacterActionConfig WithSprites(params string[] spriteIds)
        {
            SpriteIds = new List<string>(spriteIds ?? Array.Empty<string>());
            return this;
        }

        public CharacterActionConfig WithFrameRate(float fps)
        {
            FrameRate = Mathf.Max(0.01f, fps);
            return this;
        }

        public CharacterActionConfig WithLoop(bool loop)
        {
            Loop = loop;
            return this;
        }

        /// <summary>在指定帧注册一个帧事件（链式调用，2D）。</summary>
        public CharacterActionConfig WithFrameEvent(int frameIndex, string eventName)
        {
            if (FrameEvents == null) FrameEvents = new Dictionary<int, string>();
            FrameEvents[frameIndex] = eventName;
            return this;
        }

        /// <summary>3D 专用：指定 Animator state（可选）+ Layer + CrossFade。</summary>
        public CharacterActionConfig WithAnimatorState(string stateName, int layer = 0, float crossFadeDuration = 0.1f)
        {
            AnimatorStateName = stateName ?? string.Empty;
            AnimatorLayer = layer;
            CrossFadeDuration = Mathf.Max(0f, crossFadeDuration);
            return this;
        }

        /// <summary>3D 专用：在动画归一化时间 normalizedTime 跨越 <paramref name="normalizedTime"/> 时广播帧事件。</summary>
        public CharacterActionConfig WithNormalizedTimeEvent(float normalizedTime, string eventName)
        {
            if (NormalizedTimeEvents == null) NormalizedTimeEvents = new Dictionary<float, string>();
            NormalizedTimeEvents[Mathf.Clamp(normalizedTime, 0f, 0.9999f)] = eventName;
            return this;
        }

        /// <summary>3D 动作上下文使用：如果 <see cref="AnimatorStateName"/> 为空则退化到 <see cref="ActionName"/>。</summary>
        public string ResolveAnimatorState() =>
            string.IsNullOrEmpty(AnimatorStateName) ? ActionName : AnimatorStateName;
    }
}
