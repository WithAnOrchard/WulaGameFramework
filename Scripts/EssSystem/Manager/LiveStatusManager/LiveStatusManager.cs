using EssSystem.Core.Base.Manager;
using UnityEngine;

namespace BiliBiliLive
{
    /// <summary>
    /// B 站直播间开播状态轮询门面（Manager）。
    /// <list type="bullet">
    /// <item>不需要主播身份码：仅访问公开接口 <c>api.live.bilibili.com/room/v1/Room/get_info</c></item>
    /// <item>启动时自动 <see cref="LiveStatusService.StartPolling"/>（取决于 Inspector）</item>
    /// <item>业务方用 <c>[EventListener(LiveStatusService.EVT_LIVE_STARTED)]</c> 等接钩子，详见 Service 上的事件常量</item>
    /// </list>
    /// 优先级 50 ：与 DanmuManager 同期初始化（晚于 EventProcessor / DataManager / UIManager）。
    /// </summary>
    [Manager(50)]
    public class LiveStatusManager : Manager<LiveStatusManager>
    {
        #region Inspector

        [Header("Live Status Polling")]
        [Tooltip("B 站直播间号（不是用户 UID）。≤ 0 不启动轮询。")]
        [SerializeField] private long _roomId = 0;

        [Tooltip("Awake 后是否立即开始轮询。关掉后用 StartPolling/StopPolling 手动控制。")]
        [SerializeField] private bool _autoStart = true;

        [Tooltip("两次拉取间隔（秒）。建议 ≥ 15s 避免被风控。最低 5s。")]
        [SerializeField, Min(5f)] private float _pollIntervalSeconds = 30f;

        [Tooltip("公开 get_info 接口地址。默认官方域名。")]
        [SerializeField] private string _apiEndpoint = "https://api.live.bilibili.com/room/v1/Room/get_info";

        #endregion

        public LiveStatusService Service => LiveStatusService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_autoStart && _roomId > 0)
            {
                Service.StartPolling(_roomId, _pollIntervalSeconds, _apiEndpoint);
            }
            Log("LiveStatusManager 初始化完成", Color.green);
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
            Service?.StopPolling();
            base.OnDestroy();
        }

        protected override void OnApplicationQuit()
        {
            Service?.StopPolling();
            base.OnApplicationQuit();
        }

        #endregion

        #region Public API（Inspector 友好封装）

        /// <summary>用 Inspector 当前配置启动轮询。</summary>
        [ContextMenu("Start Polling")]
        public void StartPolling()
        {
            if (_roomId <= 0)
            {
                LogWarning("StartPolling: roomId 必须在 Inspector 中填正确的直播间号");
                return;
            }
            Service.StartPolling(_roomId, _pollIntervalSeconds, _apiEndpoint);
        }

        /// <summary>停止轮询。</summary>
        [ContextMenu("Stop Polling")]
        public void StopPolling() => Service?.StopPolling();

        /// <summary>立即拉一次（不影响轮询计时；用 Inspector 当前 roomId）。</summary>
        [ContextMenu("Check Once")]
        public async void CheckOnce()
        {
            if (_roomId <= 0)
            {
                LogWarning("CheckOnce: roomId 必须 > 0");
                return;
            }
            await Service.CheckOnceAsync(_roomId, _apiEndpoint);
        }

        #endregion
    }
}
