using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.ShopManager;
using BiliBiliDanmu;
using BiliBiliLive;

namespace Demo.DobeCat.Game
{
    /// <summary>
    /// 直播经济控制器 —— 将直播行为映射为游戏内货币。
    /// <list type="bullet">
    /// <item><b>银币</b>：陪伴时长（每 10 分钟 +1）+ 发送弹幕（每条 +1，同 UID 场次去重）+ 每日首登 +5。</item>
    /// <item><b>金币</b>：礼物事件（当前版本全量礼物均视为有价值礼物，<c>giftCount × 1</c> 金币；
    ///   后续可在 DanmakuModel 加 <c>coin_type</c> 字段后细化）。</item>
    /// </list>
    /// 挂在任意 MonoBehaviour 上，在 <c>OnEnable/OnDisable</c> 自动注册 / 反注册事件监听。
    /// </summary>
    public class LiveEconomyController : MonoBehaviour
    {
        [Tooltip("受益玩家 ID（与钱包绑定）。")]
        [SerializeField] private string _playerId = "player";

        [Tooltip("陪伴计时每隔多少秒给一枚银币（默认 600 秒 = 10 分钟）。")]
        [SerializeField, Min(60f)] private float _silverPerSeconds = 600f;

        [Tooltip("礼物事件：每份礼物给多少金币（默认 1）。后续细化 coin_type 后可差异化。")]
        [SerializeField, Min(1)] private int _goldPerGift = 1;

        private float _companionTimer;
        private readonly HashSet<long> _danmuUids = new HashSet<long>();
        private EventDelegate _danmuHandler;
        private EventDelegate _giftHandler;
        private EventDelegate _liveStartHandler;
        private EventDelegate _liveEndHandler;
        private bool _dailyBonusDone;

        /// <summary>主播是否当前开播中。只有开播期间发送弹幕才能获取銀币。</summary>
        private bool _isLive;

        private static string TodayKey(string playerId) => $"DobeCat_DailyBonus_{playerId}_{System.DateTime.Now:yyyyMMdd}";

        // ─── Unity 生命周期 ──────────────────────────────────────────────────

        private void OnEnable()
        {
            _danmuHandler    = HandleDanmaku;
            _giftHandler     = HandleGift;
            _liveStartHandler = HandleLiveStarted;
            _liveEndHandler   = HandleLiveEnded;
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.AddListener(DanmuService.EVT_DANMAKU,          _danmuHandler);
            ep.AddListener(DanmuService.EVT_GIFT,             _giftHandler);
            ep.AddListener(LiveStatusService.EVT_LIVE_STARTED, _liveStartHandler);
            ep.AddListener(LiveStatusService.EVT_LIVE_ENDED,   _liveEndHandler);
        }

        private void OnDisable()
        {
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.RemoveListener(DanmuService.EVT_DANMAKU,           _danmuHandler);
            ep.RemoveListener(DanmuService.EVT_GIFT,              _giftHandler);
            ep.RemoveListener(LiveStatusService.EVT_LIVE_STARTED,  _liveStartHandler);
            ep.RemoveListener(LiveStatusService.EVT_LIVE_ENDED,    _liveEndHandler);
        }

        private void Start()
        {
            TryDailyBonus();
            InitWallets();
        }

        private void Update()
        {
            _companionTimer += Time.unscaledDeltaTime;
            if (_companionTimer >= _silverPerSeconds)
            {
                _companionTimer -= _silverPerSeconds;
                AddCurrency(ShopService.CURRENCY_SILVER, 1, "陪伴时长");
            }
        }

        // ─── 事件处理 ────────────────────────────────────────────────────────

        private List<object> HandleLiveStarted(string evtName, List<object> data)
        {
            _isLive = true;
            _danmuUids.Clear(); // 每次开播重置去重集合，新一场直播重新计算
            Debug.Log("[LiveEconomy] 开播，开始统计弹幕銀币奖励");
            return null;
        }

        private List<object> HandleLiveEnded(string evtName, List<object> data)
        {
            _isLive = false;
            Debug.Log("[LiveEconomy] 下播，彬幕銀币奖励已停止");
            return null;
        }

        /// <summary>data = [userName, commentText, userId_long] — 只在开播期间给銀币。</summary>
        private List<object> HandleDanmaku(string evtName, List<object> data)
        {
            if (!_isLive) return null;  // 未开播时弹幕不给銀币
            if (data == null || data.Count < 3) return null;
            var uid = data[2] is long l ? l : 0L;
            if (uid <= 0 || !_danmuUids.Add(uid)) return null; // 同 UID 本场直播内只给一次
            AddCurrency(ShopService.CURRENCY_SILVER, 1, $"直播弹幕 uid={uid}");
            return null;
        }

        /// <summary>data = [userName, giftName, giftCount, userId_long, pricePerGift_int, coinType_string]</summary>
        private List<object> HandleGift(string evtName, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            var giftName  = data.Count > 1 ? data[1] as string ?? "" : "";
            var giftCount = data.Count > 2 ? System.Convert.ToInt32(data[2]) : 1;
            var pricePerGift = data.Count > 4 ? System.Convert.ToInt32(data[4]) : -1;
            var coinType     = data.Count > 5 ? data[5] as string : null;

            int gold;
            string reason;
            if (coinType == "gold" && pricePerGift > 0)
            {
                // price is in 金瓜子; 100 金瓜子 = 1 battery (0.1 RMB)
                int totalBattery = pricePerGift / 100 * giftCount;
                if (totalBattery < 1) totalBattery = giftCount; // fallback: treat count as batteries
                float mult = totalBattery >= 1000 ? 2f :
                             totalBattery >= 100  ? 1.5f :
                             totalBattery >= 10   ? 1.2f : 1f;
                gold   = UnityEngine.Mathf.RoundToInt(totalBattery * mult);
                reason = $"礼物 {giftName}×{giftCount}（电池×{totalBattery}，×{mult}）";
            }
            else if (coinType != null && coinType != "gold")
            {
                return null; // silver/free gifts → no gold
            }
            else
            {
                // Fallback (old event format without coin_type)
                gold   = giftCount * _goldPerGift;
                reason = $"礼物 {giftName}×{giftCount}（旧格式）";
            }

            if (gold > 0) AddCurrency(ShopService.CURRENCY_GOLD, gold, reason);
            return null;
        }

        // ─── 内部工具 ─────────────────────────────────────────────────────────

        private void TryDailyBonus()
        {
            if (_dailyBonusDone) return;
            _dailyBonusDone = true;
            var key = TodayKey(_playerId);
            if (PlayerPrefs.GetInt(key, 0) != 0) return;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            AddCurrency(ShopService.CURRENCY_SILVER, 5, "每日首登");
        }

        private void InitWallets()
        {
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.TriggerEventMethod(ShopManager.EVT_INIT_WALLET,
                new List<object> { _playerId, ShopService.CURRENCY_SILVER, 0 });
            ep.TriggerEventMethod(ShopManager.EVT_INIT_WALLET,
                new List<object> { _playerId, ShopService.CURRENCY_GOLD, 0 });
        }

        private void AddCurrency(string currencyId, int amount, string reason)
        {
            if (!EventProcessor.HasInstance || amount <= 0) return;
            EventProcessor.Instance.TriggerEventMethod(ShopManager.EVT_ADD_WALLET,
                new List<object> { _playerId, currencyId, amount });
            Debug.Log($"[LiveEconomy] +{amount} {currencyId} ({reason})");
        }
    }
}
