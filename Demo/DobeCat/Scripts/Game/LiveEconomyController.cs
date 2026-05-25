using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.ShopManager;
using BiliBiliDanmu;

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
        private bool _dailyBonusDone;

        private static string TodayKey(string playerId) => $"DobeCat_DailyBonus_{playerId}_{System.DateTime.Now:yyyyMMdd}";

        // ─── Unity 生命周期 ──────────────────────────────────────────────────

        private void OnEnable()
        {
            _danmuHandler = HandleDanmaku;
            _giftHandler  = HandleGift;
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.AddListener(DanmuService.EVT_DANMAKU, _danmuHandler);
            EventProcessor.Instance.AddListener(DanmuService.EVT_GIFT,    _giftHandler);
        }

        private void OnDisable()
        {
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.RemoveListener(DanmuService.EVT_DANMAKU, _danmuHandler);
            EventProcessor.Instance.RemoveListener(DanmuService.EVT_GIFT,    _giftHandler);
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

        /// <summary>data = [userName, commentText, userId_long]</summary>
        private List<object> HandleDanmaku(string evtName, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            var uid = data[2] is long l ? l : 0L;
            if (uid <= 0 || !_danmuUids.Add(uid)) return null; // 同 UID 本次连接内只给一次
            AddCurrency(ShopService.CURRENCY_SILVER, 1, $"弹幕 uid={uid}");
            return null;
        }

        /// <summary>data = [userName, giftName, giftCount, userId_long]</summary>
        private List<object> HandleGift(string evtName, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            var giftName  = data.Count > 1 ? data[1] as string : "";
            var giftCount = data.Count > 2 ? System.Convert.ToInt32(data[2]) : 1;
            var gold = giftCount * _goldPerGift;
            AddCurrency(ShopService.CURRENCY_GOLD, gold, $"礼物 {giftName}×{giftCount}");
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
