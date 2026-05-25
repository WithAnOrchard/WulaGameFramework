using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using BiliBiliDanmu;
using BiliBiliLive;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// Connects DanmuManager live events to PetSpeechBubble reactions (DESIGN.md §10.2).
    /// - Danmaku  → speech bubble shows "[user]: [text]"  (rate-limited)
    /// - Gift     → speech bubble shows thanks (overrides danmaku, longer duration)
    /// </summary>
    public class PetReactionController : MonoBehaviour
    {
        [Tooltip("Min seconds between danmaku bubbles (rate limit).")]
        [SerializeField, Min(1f)] private float _danmuCooldown = 5f;

        [Tooltip("Probability (0-1) that any single danmaku triggers a bubble.")]
        [SerializeField, Range(0f, 1f)] private float _danmuShowChance = 0.3f;

        [Tooltip("Max chars shown from danmaku text.")]
        [SerializeField, Min(10)] private int _danmuMaxChars = 30;

        [Tooltip("Bubble duration for danmaku messages (seconds).")]
        [SerializeField] private float _danmuDuration = 3f;

        [Tooltip("Bubble duration for gift messages (seconds).")]
        [SerializeField] private float _giftDuration  = 5f;

        private EventDelegate _danmuHandler;
        private EventDelegate _giftHandler;
        private EventDelegate _scHandler;
        private EventDelegate _liveStartHandler;
        private EventDelegate _liveEndHandler;
        private float         _danmuCooldownTimer;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            _danmuHandler     = HandleDanmaku;
            _giftHandler      = HandleGift;
            _scHandler        = HandleSC;
            _liveStartHandler = HandleLiveStarted;
            _liveEndHandler   = HandleLiveEnded;
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.AddListener(DanmuService.EVT_DANMAKU,          _danmuHandler);
            ep.AddListener(DanmuService.EVT_GIFT,             _giftHandler);
            ep.AddListener(DanmuService.EVT_SC,               _scHandler);
            ep.AddListener(LiveStatusService.EVT_LIVE_STARTED, _liveStartHandler);
            ep.AddListener(LiveStatusService.EVT_LIVE_ENDED,   _liveEndHandler);
        }

        private void OnDisable()
        {
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.RemoveListener(DanmuService.EVT_DANMAKU,           _danmuHandler);
            ep.RemoveListener(DanmuService.EVT_GIFT,              _giftHandler);
            ep.RemoveListener(DanmuService.EVT_SC,                _scHandler);
            ep.RemoveListener(LiveStatusService.EVT_LIVE_STARTED,  _liveStartHandler);
            ep.RemoveListener(LiveStatusService.EVT_LIVE_ENDED,    _liveEndHandler);
        }

        private void Update()
        {
            if (_danmuCooldownTimer > 0f)
                _danmuCooldownTimer -= Time.unscaledDeltaTime;
        }

        // ─── Event handlers ───────────────────────────────────────────────────

        /// <summary>data = [userName, commentText, userId_long]</summary>
        private List<object> HandleDanmaku(string evtName, List<object> data)
        {
            if (PetSpeechBubble.Instance == null) return null;
            if (data == null || data.Count < 2)   return null;
            if (_danmuCooldownTimer > 0f)          return null;
            if (Random.value > _danmuShowChance)   return null;

            var user = data[0] as string ?? "?";
            var text = data[1] as string ?? "";
            if (text.Length > _danmuMaxChars) text = text.Substring(0, _danmuMaxChars) + "…";

            PetSpeechBubble.Instance.Show($"{user}: {text}", _danmuDuration);
            _danmuCooldownTimer = _danmuCooldown;
            return null;
        }

        /// <summary>data = [userName, giftName, giftCount, userId_long]</summary>
        private List<object> HandleGift(string evtName, List<object> data)
        {
            if (PetSpeechBubble.Instance == null) return null;
            if (data == null || data.Count < 3)   return null;

            var user  = data[0] as string ?? "?";
            var gift  = data.Count > 1 ? data[1] as string ?? "礼物" : "礼物";
            var count = data.Count > 2 ? System.Convert.ToInt32(data[2]) : 1;

            var msg = count > 1
                ? $"感谢 {user} 送来 {gift}×{count}！"
                : $"感谢 {user} 送来 {gift}！";

            PetSpeechBubble.Instance.Show(msg, _giftDuration);
            _danmuCooldownTimer = _danmuCooldown;
            return null;
        }

        /// <summary>data = [userName, text, priceYuan_int, userId_long]</summary>
        private List<object> HandleSC(string evtName, List<object> data)
        {
            if (PetSpeechBubble.Instance == null) return null;
            if (data == null || data.Count < 2)   return null;

            var user  = data[0] as string ?? "?";
            var text  = data[1] as string ?? "";
            var price = data.Count > 2 ? System.Convert.ToInt32(data[2]) : 0;

            if (text.Length > _danmuMaxChars) text = text.Substring(0, _danmuMaxChars) + "…";

            var msg = price > 0
                ? $"📫 SC ¥{price} [{user}]: {text}"
                : $"📫 SC [{user}]: {text}";

            PetSpeechBubble.Instance.Show(msg, _giftDuration + 3f);
            _danmuCooldownTimer = _danmuCooldown;
            return null;
        }

        /// <summary>data = [roomId, title, LiveRoomInfo]</summary>
        private List<object> HandleLiveStarted(string evtName, List<object> data)
        {
            if (PetSpeechBubble.Instance == null) return null;
            var title = data?.Count >= 2 ? data[1] as string ?? "直播" : "直播";
            PetSpeechBubble.Instance.Show($"📡 开播啊！{title}！快去看！", 8f);
            return null;
        }

        /// <summary>data = [roomId, title, LiveRoomInfo]</summary>
        private List<object> HandleLiveEnded(string evtName, List<object> data)
        {
            if (PetSpeechBubble.Instance == null) return null;
            PetSpeechBubble.Instance.Show("下播了，主播辛苦了～下次见哟！", 6f);
            return null;
        }
    }
}
