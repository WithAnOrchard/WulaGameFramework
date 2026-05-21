using EssSystem.Core.Base.Manager;
using UnityEngine;

namespace BiliBiliDanmu
{
    /// <summary>
    /// 统一 B 站直播弹幕接入门面（单 Manager，三模式可选）。
    /// <list type="bullet">
    /// <item><b>Polling</b>：HTTP 轮询 <c>/ajax/msg</c>。零认证；仅文字弹幕；适合任何房间。</item>
    /// <item><b>Token</b>：SESSDATA cookie + WSS 实时。需用户登录态；含礼物/SC。</item>
    /// <item><b>OpenLive</b>：主播身份码 + 官方开放平台。仅自己直播间；含礼物/SC；实时。</item>
    /// </list>
    /// 业务订阅方只需 <c>[EventListener(DanmuService.EVT_DANMAKU)]</c>，无需感知模式。
    /// </summary>
    [Manager(50)]
    public class DanmuManager : Manager<DanmuManager>
    {
        #region Inspector

        [Header("Mode")]
        [Tooltip("三选一：Polling=零认证 / Token=登录态 / OpenLive=主播身份码")]
        [SerializeField] private DanmuMode _mode = DanmuMode.Polling;

        [Tooltip("Initialize 后是否立即按当前 Mode 发起连接。\n注意：DanmuManager 是自动创建的单例，Inspector 默认值通常用不上；\n业务方一般在自己的 GameManager 里用 DanmuService.Instance.ConnectAsync(config) 显式控制。")]
        [SerializeField] private bool _autoConnect = false;

        [Header("Polling / Token 共用：直播间号")]
        [Tooltip("直播间号（短号或真实号都可）。OpenLive 模式不用填，会从身份码反查。")]
        [SerializeField] private long _roomId = 0;

        [Header("Polling 模式")]
        [Tooltip("轮询间隔（秒），最低 1.5s。建议 3s。")]
        [SerializeField, Min(1.5f)] private float _pollIntervalSeconds = 3f;

        [Header("Token 模式（登录 cookie）")]
        [Tooltip("浏览器 DevTools → bilibili.com cookies 复制 SESSDATA。\n⚠️ 安全：等同登录密码，不要 commit 到公开仓库。")]
        [TextArea(2, 4)]
        [SerializeField] private string _sessdata = string.Empty;

        [Tooltip("可选 bili_jct（CSRF token），仅发弹幕/打赏需要。")]
        [SerializeField] private string _biliJct = string.Empty;

        [Header("OpenLive 模式（主播身份码）")]
        [Tooltip("主播身份码（B 站直播姬「开放平台」面板获取）。\n⚠️ 安全：身份码等同密码，请只在本地 Inspector 填写。")]
        [SerializeField] private string _identityCode = string.Empty;

        [Tooltip("第三方应用 AppId（B 站开发者后台获取）。")]
        [SerializeField] private long _appId = 1651388990835L;

        [Tooltip("签名代理服务器（POST /sign）。默认走社区代理，可替换为自部署。")]
        [SerializeField] private string _signEndpoint = "https://bopen.ceve-market.org/sign";

        [Tooltip("B 站开放平台 v2/app/start 入口。")]
        [SerializeField] private string _startEndpoint = "https://live-open.biliapi.com/v2/app/start";

        [Tooltip("HTTP 超时（秒）。")]
        [SerializeField, Min(1f)] private float _httpTimeoutSeconds = 5f;

        #endregion

        public DanmuService Service => DanmuService.Instance;
        public DanmuMode Mode => _mode;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            if (_autoConnect) _ = ConnectAsync();
            Log($"DanmuManager 初始化完成 (Mode={_mode})", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void OnDestroy() { Service?.Disconnect(); base.OnDestroy(); }
        protected override void OnApplicationQuit() { Service?.Disconnect(); base.OnApplicationQuit(); }

        #endregion

        #region Public API

        /// <summary>用 Inspector 当前模式与参数发起连接。已连接幂等返回。</summary>
        public System.Threading.Tasks.Task<bool> ConnectAsync()
            => Service.ConnectAsync(BuildConfig());

        /// <summary>显式指定 mode + room，覆盖 Inspector 配置。</summary>
        public System.Threading.Tasks.Task<bool> ConnectAsync(DanmuMode mode, long roomId)
        {
            _mode = mode;
            _roomId = roomId;
            return ConnectAsync();
        }

        /// <summary>主动断开（幂等）。</summary>
        public void Disconnect() => Service?.Disconnect();

        /// <summary>断开后用 Inspector 当前配置重连。</summary>
        [ContextMenu("Reconnect")]
        public void Reconnect()
        {
            if (Service == null) return;
            Service.Disconnect();
            _ = ConnectAsync();
        }

        #endregion

        private DanmuConnectConfig BuildConfig() => new DanmuConnectConfig
        {
            Mode = _mode,
            RoomId = _roomId,
            IdentityCode = _identityCode,
            AppId = _appId,
            SignEndpoint = _signEndpoint,
            StartEndpoint = _startEndpoint,
            HttpTimeoutSeconds = _httpTimeoutSeconds,
            Sessdata = _sessdata,
            BiliJct = _biliJct,
            PollIntervalSeconds = _pollIntervalSeconds,
        };
    }
}
