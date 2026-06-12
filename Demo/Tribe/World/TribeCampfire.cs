using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace Demo.Tribe
{
    /// <summary>
    /// 营火音频锚点 —— 在所属 GameObject 上挂载一个由 <c>AudioManager</c> 拥有的位置循环音。
    /// <para>视觉（帧动画）已迁移到 <c>CharacterManager</c>，本组件只剩音频生命周期：
    /// <list type="bullet">
    ///   <item><c>Start</c> 走 §4.1 bare-string 事件 <c>PlayPositionalLoopSFX</c> 拿 handleId</item>
    ///   <item><c>OnDestroy</c> 走 <c>StopPositionalSFX</c> 释放</item>
    /// </list>
    /// 距离衰减由 Unity 3D 音引擎按 <c>AudioListener</c>（主相机，跟随玩家）自动计算。</para>
    /// </summary>
    public class TribeCampfire : MonoBehaviour
    {
        [Tooltip("音频资源路径（走 ResourceManager bare-string）。")]
        [SerializeField] private string _audioPath;
        [Tooltip("最近距离 —— 在此距离内取最大音量。")]
        [SerializeField] private float _minDistance = 1.5f;
        [Tooltip("最远可听距离 —— 超过则完全静音。")]
        [SerializeField] private float _maxDistance = 12f;
        [Tooltip("相对 SFXVolume 的倍率。")]
        [SerializeField] private float _volumeScale = 1f;

        private string _audioHandle;

        /// <summary>外部传入音频路径；为空跳过音效。</summary>
        public void Initialize(string audioPath)
        {
            _audioPath = audioPath;
        }

        private void Start()
        {
            if (string.IsNullOrEmpty(_audioPath) || !EventProcessor.HasInstance) return;
            var r = EventProcessor.Instance.TriggerEventMethod(
                "PlayPositionalLoopSFX",
                new List<object> { _audioPath, transform, _minDistance, _maxDistance, _volumeScale });
            if (ResultCode.IsOk(r) && r.Count >= 2 && r[1] is string id)
                _audioHandle = id;
        }

        private void OnDestroy()
        {
            if (string.IsNullOrEmpty(_audioHandle) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                "StopPositionalSFX", new List<object> { _audioHandle });
            _audioHandle = null;
        }
    }
}
