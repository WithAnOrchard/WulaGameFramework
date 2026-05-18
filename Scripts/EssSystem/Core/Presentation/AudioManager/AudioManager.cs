using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.Presentation.AudioManager
{
    /// <summary>
    /// 音频管理器 — 管理 BGM 和 SFX 播放。
    /// 音频资源统一走 ResourceManager 缓存（bare-string "GetAudioClip"，§4.1）。
    /// </summary>
    [Manager(3)]
    public class AudioManager : Manager<AudioManager>
    {
        // ─── Event 常量 ────────────────────────────────────────────────
        public const string EVT_PLAY_BGM          = "PlayBGM";
        public const string EVT_STOP_BGM          = "StopBGM";
        public const string EVT_PAUSE_BGM         = "PauseBGM";
        public const string EVT_RESUME_BGM        = "ResumeBGM";
        public const string EVT_PLAY_SFX          = "PlaySFX";
        public const string EVT_SET_MASTER_VOLUME = "SetMasterVolume";
        public const string EVT_SET_BGM_VOLUME    = "SetBGMVolume";
        public const string EVT_SET_SFX_VOLUME    = "SetSFXVolume";
        public const string EVT_PLAY_DAMAGE_SFX   = "PlayDamageSFX";
        public const string EVT_PLAY_ATTACK_SFX   = "PlayAttackSFX";
        public const string EVT_PLAY_UI_SFX       = "PlayUISFX";
        public const string EVT_PLAY_ITEM_USE_SFX = "PlayItemUseSFX";
        /// <summary>在指定 Transform 上挂一个 3D 循环 SFX 源。
        /// args: [clipPath, Transform anchor, float minDist?=1.5, float maxDist?=12, float volumeScale?=1]
        /// 返回: Ok(handleId:string) / Fail。</summary>
        public const string EVT_PLAY_POSITIONAL_LOOP_SFX = "PlayPositionalLoopSFX";
        /// <summary>停止并释放由 <see cref="EVT_PLAY_POSITIONAL_LOOP_SFX"/> 创建的循环音源。
        /// args: [handleId:string]。</summary>
        public const string EVT_STOP_POSITIONAL_SFX = "StopPositionalSFX";

        // ─── 便捷音效路径常量
        private const string SFX_DAMAGE   = "Sound/Bump";
        private const string SFX_UI       = "Sound/Bubble";
        private const string SFX_ITEM_USE = "Sound/AppleUse";

        private static readonly string[] AttackSounds =
            { "Sound/Sword1", "Sound/Sword2", "Sound/Sword3" };

        [Header("BGM")]
        [SerializeField] private AudioSource _bgmSource;
        [SerializeField] private float _bgmVolume = 1f;
        [SerializeField] private float _bgmFadeDuration = 1f;

        [Header("SFX Pool")]
        [SerializeField] private int _sfxPoolSize = 20;
        [SerializeField] private float _sfxVolume = 1f;

        private List<AudioSource> _sfxPool;
        private int _sfxPoolIndex;
        private Coroutine _bgmFadeCoroutine;

        /// <summary>位置循环音注册表 —— handle 关联 (AudioSource, volumeScale)，便于
        /// SFXVolume 变化时同步刷新，以及 Stop 时找回 source 销毁。</summary>
        private readonly Dictionary<string, (AudioSource Src, float Scale)> _positionalSources
            = new Dictionary<string, (AudioSource, float)>();
        private int _positionalIdCounter;

        // ─── 初始化 ────────────────────────────────────────────────────

        protected override void Initialize()
        {
            base.Initialize();

            if (_bgmSource == null)
            {
                _bgmSource = gameObject.AddComponent<AudioSource>();
                _bgmSource.loop = true;
                _bgmSource.playOnAwake = false;
            }
            _bgmSource.volume = _bgmVolume;

            InitializeSFXPool();

            Log("AudioManager 初始化完成", Color.green);
        }

        private void InitializeSFXPool()
        {
            _sfxPool = new List<AudioSource>(_sfxPoolSize);
            for (var i = 0; i < _sfxPoolSize; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                _sfxPool.Add(source);
            }
            _sfxPoolIndex = 0;
        }

        // ─── BGM 控制 ────────────────────────────────────────────────

        /// <summary>播放背景音乐</summary>
        public void PlayBGM(string bgmPath, bool fade = true)
        {
            var clip = LoadAudioClip(bgmPath);
            if (clip == null) return;

            if (_bgmFadeCoroutine != null)
                StopCoroutine(_bgmFadeCoroutine);

            if (fade)
            {
                _bgmFadeCoroutine = StartCoroutine(FadeBGM(clip, _bgmFadeDuration));
            }
            else
            {
                _bgmSource.clip = clip;
                _bgmSource.Play();
            }
        }

        /// <summary>停止背景音乐</summary>
        public void StopBGM(bool fade = true)
        {
            if (_bgmFadeCoroutine != null)
                StopCoroutine(_bgmFadeCoroutine);

            if (fade)
            {
                _bgmFadeCoroutine = StartCoroutine(FadeBGM(null, _bgmFadeDuration));
            }
            else
            {
                _bgmSource.Stop();
            }
        }

        /// <summary>暂停背景音乐</summary>
        public void PauseBGM() => _bgmSource.Pause();

        /// <summary>继续背景音乐</summary>
        public void ResumeBGM() => _bgmSource.UnPause();

        /// <summary>设置 BGM 音量</summary>
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            _bgmSource.volume = _bgmVolume;
        }

        // ─── SFX 控制 ────────────────────────────────────────────────

        /// <summary>播放音效</summary>
        public void PlaySFX(string sfxPath, float volumeScale = 1f)
        {
            var clip = LoadAudioClip(sfxPath);
            if (clip == null) return;

            var source = GetPooledSFXSource();
            source.clip = clip;
            source.volume = _sfxVolume * volumeScale;
            source.Play();
        }

        /// <summary>当前 SFX 音量 (0..1)</summary>
        public float SFXVolume => _sfxVolume;

        /// <summary>设置 SFX 音量 —— 同步刷新所有位置循环音的实际音量。</summary>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
            // 一次性 SFX 在 PlaySFX 时取值即可；位置循环音持久存在，需主动刷新
            foreach (var kv in _positionalSources)
                if (kv.Value.Src != null) kv.Value.Src.volume = _sfxVolume * kv.Value.Scale;
        }

        // ─── 位置循环音（环境音源：营火 / 流水 / 机械等）─────────────────

        /// <summary>
        /// 在指定 Transform 上挂一个 3D 循环音源，由 Unity 按 AudioListener 距离做线性衰减。
        /// </summary>
        /// <param name="anchor">挂载节点（GameObject 销毁时音源同时销毁）。</param>
        /// <param name="volumeScale">相对 SFXVolume 的倍率（默认 1）。</param>
        /// <returns>handle id，用于后续 <see cref="StopPositionalSFX"/>。失败返回 null。</returns>
        public string PlayPositionalLoopSFX(string clipPath, Transform anchor,
            float minDistance = 1.5f, float maxDistance = 12f, float volumeScale = 1f)
        {
            if (anchor == null) { LogWarning($"PlayPositionalLoopSFX: anchor 为空 ({clipPath})"); return null; }
            var clip = LoadAudioClip(clipPath);
            if (clip == null) return null;

            var src = anchor.gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = Mathf.Max(0.01f, minDistance);
            src.maxDistance = Mathf.Max(src.minDistance + 0.1f, maxDistance);
            src.dopplerLevel = 0f;
            src.spread = 0f;
            src.volume = _sfxVolume * volumeScale;
            src.Play();

            var id = $"pos_sfx_{++_positionalIdCounter}";
            _positionalSources[id] = (src, volumeScale);
            return id;
        }

        /// <summary>停止并销毁由 <see cref="PlayPositionalLoopSFX"/> 创建的音源。</summary>
        public bool StopPositionalSFX(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (!_positionalSources.TryGetValue(id, out var entry)) return false;
            if (entry.Src != null) { entry.Src.Stop(); Destroy(entry.Src); }
            _positionalSources.Remove(id);
            return true;
        }

        /// <summary>设置主音量（同时影响 BGM 和 SFX）</summary>
        public void SetMasterVolume(float volume) => AudioListener.volume = Mathf.Clamp01(volume);

        // ─── 便捷音效播放方法 ───────────────────────────────────────────

        /// <summary>播放受伤音效</summary>
        public void PlayDamageSFX() => PlaySFX(SFX_DAMAGE);

        /// <summary>播放攻击音效（随机选择）</summary>
        public void PlayAttackSFX() => PlaySFX(AttackSounds[Random.Range(0, AttackSounds.Length)]);

        /// <summary>播放 UI 操作音效</summary>
        public void PlayUISFX() => PlaySFX(SFX_UI);

        /// <summary>播放物品使用音效</summary>
        public void PlayItemUseSFX() => PlaySFX(SFX_ITEM_USE);

        // ─── 内部辅助 ────────────────────────────────────────────────

        /// <summary>
        /// 通过 ResourceManager 加载 AudioClip（bare-string "GetAudioClip"，§4.1 / B4）。
        /// </summary>
        private AudioClip LoadAudioClip(string path)
        {
            if (!EventProcessor.HasInstance)
            {
                LogWarning($"EventProcessor 不可用，无法加载音频: {path}");
                return null;
            }
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetAudioClip", new List<object> { path });
            if (ResultCode.IsOk(result) && result.Count >= 2 && result[1] is AudioClip clip)
                return clip;
            LogWarning($"音频资源加载失败: {path}");
            return null;
        }

        private AudioSource GetPooledSFXSource()
        {
            var source = _sfxPool[_sfxPoolIndex];
            _sfxPoolIndex = (_sfxPoolIndex + 1) % _sfxPoolSize;
            return source;
        }

        private IEnumerator FadeBGM(AudioClip newClip, float duration)
        {
            var startVolume = _bgmSource.volume;
            var half = duration * 0.5f;
            var timer = 0f;

            // 淡出当前 BGM
            while (timer < half)
            {
                timer += Time.deltaTime;
                _bgmSource.volume = Mathf.Lerp(startVolume, 0f, timer / half);
                yield return null;
            }

            _bgmSource.Stop();

            if (newClip != null)
            {
                _bgmSource.clip = newClip;
                _bgmSource.Play();

                // 淡入新 BGM
                timer = 0f;
                while (timer < half)
                {
                    timer += Time.deltaTime;
                    _bgmSource.volume = Mathf.Lerp(0f, _bgmVolume, timer / half);
                    yield return null;
                }
            }

            _bgmSource.volume = _bgmVolume;
            _bgmFadeCoroutine = null;
        }

        // ─── Service 同步 ─────────────────────────────────────────────

        protected override void SyncServiceLoggingSettings()
        {
            if (AudioService.HasInstance)
                AudioService.Instance.EnableLogging = _serviceEnableLogging;
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (!AudioService.HasInstance) return;
            AudioService.Instance.UpdateInspectorInfo();
            _serviceInspectorInfo = AudioService.Instance.InspectorInfo;
        }

        // ─── Event API ───────────────────────────────────────────────

        [Event(EVT_PLAY_BGM)]
        public List<object> OnPlayBGM(List<object> data)
        {
            if (data != null && data.Count >= 1 && data[0] is string path)
            {
                var fade = data.Count >= 2 && data[1] is bool f ? f : true;
                PlayBGM(path, fade);
            }
            return null;
        }

        [Event(EVT_STOP_BGM)]
        public List<object> OnStopBGM(List<object> data)
        {
            var fade = data != null && data.Count >= 1 && data[0] is bool f ? f : true;
            StopBGM(fade);
            return null;
        }

        [Event(EVT_PAUSE_BGM)]
        public List<object> OnPauseBGM(List<object> data) { PauseBGM(); return null; }

        [Event(EVT_RESUME_BGM)]
        public List<object> OnResumeBGM(List<object> data) { ResumeBGM(); return null; }

        [Event(EVT_PLAY_SFX)]
        public List<object> OnPlaySFX(List<object> data)
        {
            if (data != null && data.Count >= 1 && data[0] is string path)
            {
                var vol = data.Count >= 2 && data[1] is float v ? v : 1f;
                PlaySFX(path, vol);
            }
            return null;
        }

        [Event(EVT_SET_MASTER_VOLUME)]
        public List<object> OnSetMasterVolume(List<object> data)
        {
            if (data != null && data.Count >= 1 && data[0] is float volume)
                SetMasterVolume(volume);
            return null;
        }

        [Event(EVT_SET_BGM_VOLUME)]
        public List<object> OnSetBGMVolume(List<object> data)
        {
            if (data != null && data.Count >= 1 && data[0] is float volume)
                SetBGMVolume(volume);
            return null;
        }

        [Event(EVT_SET_SFX_VOLUME)]
        public List<object> OnSetSFXVolume(List<object> data)
        {
            if (data != null && data.Count >= 1 && data[0] is float volume)
                SetSFXVolume(volume);
            return null;
        }

        [Event(EVT_PLAY_DAMAGE_SFX)]
        public List<object> OnPlayDamageSFX(List<object> data) { PlayDamageSFX(); return null; }

        [Event(EVT_PLAY_ATTACK_SFX)]
        public List<object> OnPlayAttackSFX(List<object> data) { PlayAttackSFX(); return null; }

        [Event(EVT_PLAY_UI_SFX)]
        public List<object> OnPlayUISFX(List<object> data) { PlayUISFX(); return null; }

        [Event(EVT_PLAY_ITEM_USE_SFX)]
        public List<object> OnPlayItemUseSFX(List<object> data) { PlayItemUseSFX(); return null; }

        [Event(EVT_PLAY_POSITIONAL_LOOP_SFX)]
        public List<object> OnPlayPositionalLoopSFX(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数：[clipPath, Transform anchor, minDist?, maxDist?, volumeScale?]");
            var path = data[0] as string;
            var anchor = data[1] as Transform;
            var minD = data.Count >= 3 && data[2] is float a ? a : 1.5f;
            var maxD = data.Count >= 4 && data[3] is float b ? b : 12f;
            var vol  = data.Count >= 5 && data[4] is float c ? c : 1f;
            var id = PlayPositionalLoopSFX(path, anchor, minD, maxD, vol);
            return id != null ? ResultCode.Ok(id) : ResultCode.Fail("位置音源创建失败");
        }

        [Event(EVT_STOP_POSITIONAL_SFX)]
        public List<object> OnStopPositionalSFX(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string id))
                return ResultCode.Fail("参数：[handleId]");
            return StopPositionalSFX(id) ? ResultCode.Ok(id) : ResultCode.Fail($"handleId 未注册: {id}");
        }
    }
}
