using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using UnityEngine;

namespace Demo.DobeCat.Sys
{
    /// <summary>
    /// 桌宠音效控制器。通过 AudioManager.EVT_PLAY_SFX 触发对应音效。
    /// 静态方法供全局调用，挂载后自动激活。
    ///
    /// 音效资源放在 Resources/Sound/DobeCat/ 下（不存在时安静地跳过）：
    ///   meow_01.wav / meow_02.wav / meow_03.wav — 喵叫（点击 / 互动）
    ///   purr.wav     — 呼噜（撸猫 / 投喂）
    ///   notify.wav   — 提醒（陪伴提醒 / 闹钟）
    ///   pop.wav      — 气泡弹出音
    /// DESIGN.md §12.1 AudioManager
    /// </summary>
    public class PetSoundController : MonoBehaviour
    {
        private static readonly string[] MeowSounds =
        {
            "Sound/DobeCat/meow_01",
            "Sound/DobeCat/meow_02",
            "Sound/DobeCat/meow_03",
        };

        private const string SFX_PURR   = "Sound/DobeCat/purr";
        private const string SFX_NOTIFY = "Sound/DobeCat/notify";
        private const string SFX_POP    = "Sound/DobeCat/pop";

        private static PetSoundController _instance;
        private float _meowCooldown;

        private void Awake()  => _instance = this;
        private void OnDestroy() { if (_instance == this) _instance = null; }

        private void Update()
        {
            if (_meowCooldown > 0f) _meowCooldown -= Time.unscaledDeltaTime;
        }

        // ─── Static convenience API ──────────────────────────────────────────

        /// <summary>随机喵叫（有冷却，防止重叠）。</summary>
        public static void PlayMeow(float volumeScale = 1f)
        {
            if (_instance == null) return;
            if (_instance._meowCooldown > 0f) return;
            _instance._meowCooldown = 1.5f;
            Play(MeowSounds[Random.Range(0, MeowSounds.Length)], volumeScale);
        }

        /// <summary>呼噜声（撸猫 / 投喂）。</summary>
        public static void PlayPurr(float volumeScale = 0.8f) => Play(SFX_PURR, volumeScale);

        /// <summary>提醒音（陪伴提醒 / 闹钟触发）。</summary>
        public static void PlayNotify(float volumeScale = 0.7f) => Play(SFX_NOTIFY, volumeScale);

        /// <summary>气泡弹出音（对话气泡 Show 时调用）。</summary>
        public static void PlayPop(float volumeScale = 0.4f) => Play(SFX_POP, volumeScale);

        // ─── Internal ────────────────────────────────────────────────────────

        private static void Play(string path, float vol)
        {
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                "PlaySFX", new List<object> { path, vol });
        }
    }
}
