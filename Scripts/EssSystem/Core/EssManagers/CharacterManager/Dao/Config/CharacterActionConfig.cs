using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.EssManager.CharacterManager.Dao
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
        /// </summary>
        public Dictionary<int, string> FrameEvents;

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

        /// <summary>在指定帧注册一个帧事件（链式调用）。</summary>
        public CharacterActionConfig WithFrameEvent(int frameIndex, string eventName)
        {
            if (FrameEvents == null) FrameEvents = new Dictionary<int, string>();
            FrameEvents[frameIndex] = eventName;
            return this;
        }
    }
}
