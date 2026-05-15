using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Base.Event;
using UnityEngine;

namespace BiliBiliDanmu
{
    /// <summary>
    /// B 站直播弹幕接入门面 — 挂到场景里的单例 MonoBehaviour
    /// <para>
    /// 职责严格按框架约定切分：
    /// <list type="bullet">
    /// <item><b>本类（Manager）</b>：Inspector 暴露 + 生命周期 + 调用 <see cref="DanmuService"/></item>
    /// <item><b><see cref="DanmuService"/></b>：HTTP 鉴权、长连接、回调切主线程、广播 EVT_*</item>
    /// </list>
    /// </para>
    /// <para>
    /// 业务方订阅：
    /// <code>[EventListener(DanmuService.EVT_DANMAKU)]</code>
    /// 详见 <c>Agent.md → Event API</c>。
    /// </para>
    /// <para>
    /// 优先级 50：晚于 EventProcessor / DataManager / UIManager 等基础设施初始化，
    /// 保证首条弹幕到达时事件中心已就绪。
    /// </para>
    /// </summary>
    [Manager(50)]
    public class DanmuManager : Manager<DanmuManager>
    {
        #region Inspector

        [Header("BiliBili Open Live")]
        [Tooltip("主播身份码（B 站直播姬「开放平台」面板获取）。空字符串 = 不自动连接。\n" +
                 "⚠️ 安全：身份码等同密码，请只在本地 Inspector 填写，**不要 commit 到公开仓库**。\n" +
                 "本字段默认空，由你本地填值后 Asset 即可保留；推荐把 .meta 加入 .gitignore 或用 ScriptableObject 外挂。")]
        [SerializeField] private string _identityCode = string.Empty;

        [Tooltip("第三方应用 AppId（B 站开发者后台获取）。")]
        [SerializeField] private long _appId = 1651388990835L;

        [Tooltip("Awake 完成后是否立即连接。关掉后用 Reconnect/ConnectAsync 手动触发。")]
        [SerializeField] private bool _autoConnect = true;

        [Tooltip("签名代理服务器（POST /sign）。默认走社区代理，可替换为自部署。")]
        [SerializeField] private string _signEndpoint = "https://bopen.ceve-market.org/sign";

        [Tooltip("B 站开放平台 v2/app/start 入口。")]
        [SerializeField] private string _startEndpoint = "https://live-open.biliapi.com/v2/app/start";

        [Tooltip("HTTP 超时（秒）。")]
        [SerializeField, Min(1f)] private float _httpTimeoutSeconds = 5f;

        #endregion

        /// <summary>底层 Service（同等于 DanmuService.Instance，但 Inspector 里可见）。</summary>
        public DanmuService Service => DanmuService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_autoConnect && !string.IsNullOrEmpty(_identityCode))
            {
                _ = Service.ConnectAsync(_identityCode, _appId, _signEndpoint, _startEndpoint, _httpTimeoutSeconds);
            }
            Log("DanmuManager 初始化完成", Color.green);
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

        protected override void OnDestroy()
        {
            if (Service != null) Service.Disconnect();
            base.OnDestroy();
        }

        protected override void OnApplicationQuit()
        {
            Service?.Disconnect();
            base.OnApplicationQuit();
        }

        #endregion

        #region Public API（Inspector 友好封装；业务方一般直接订阅 DanmuService.EVT_*）

        /// <summary>用 Inspector 当前配置发起连接。已连接幂等返回。</summary>
        public System.Threading.Tasks.Task<bool> ConnectAsync()
            => Service.ConnectAsync(_identityCode, _appId, _signEndpoint, _startEndpoint, _httpTimeoutSeconds);

        /// <summary>主动断开（幂等）。</summary>
        public void Disconnect() => Service?.Disconnect();

        /// <summary>断开后立即重连（用 Inspector 当前配置）。</summary>
        [ContextMenu("Reconnect")]
        public void Reconnect()
        {
            if (Service == null) return;
            Service.Disconnect();
            _ = ConnectAsync();
        }

        #endregion
    }
}
