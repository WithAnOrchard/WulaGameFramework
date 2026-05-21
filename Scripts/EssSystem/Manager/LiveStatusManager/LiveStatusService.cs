using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace BiliBiliLive
{
    /// <summary>
    /// B 站直播间开播状态轮询 Service —— 仅依赖公开接口 <c>api.live.bilibili.com</c>，
    /// <b>不需要主播身份码 / 鉴权</b>。
    /// <list type="bullet">
    /// <item>定时拉取 <c>get_info?room_id=xxx</c> 查 <c>live_status</c>（0=未播 / 1=直播 / 2=轮播）</item>
    /// <item>状态变更时广播 <see cref="EVT_LIVE_STARTED"/> / <see cref="EVT_LIVE_ENDED"/></item>
    /// <item>每次轮询都广播 <see cref="EVT_STATUS_POLLED"/>，方便业务方做 UI 持续刷新</item>
    /// <item>JSON 走框架 <see cref="MiniJson"/>，无外部依赖</item>
    /// </list>
    /// </summary>
    public class LiveStatusService : Service<LiveStatusService>
    {
        #region 事件常量（全部为广播）

        /// <summary>开播（0/2 → 1，状态边沿触发）。参数：<c>[long roomId, string title, LiveRoomInfo info]</c></summary>
        public const string EVT_LIVE_STARTED = "OnLiveStarted";

        /// <summary>下播（1 → 0/2，状态边沿触发）。参数：<c>[long roomId, string title, LiveRoomInfo info]</c></summary>
        public const string EVT_LIVE_ENDED = "OnLiveEnded";

        /// <summary>每次轮询都触发（无论状态是否变更）。参数：<c>[long roomId, int liveStatus, string title, LiveRoomInfo info]</c>。</summary>
        public const string EVT_STATUS_POLLED = "OnLiveStatusPolled";

        #endregion

        // 共享 HttpClient（避免端口耗尽）
        private static readonly HttpClient _httpClient = new HttpClient();

        // ─── 运行时状态（不持久化） ────────────────────────────────
        private CancellationTokenSource _cts;
        private long _roomId;
        private int _lastStatus = -1; // -1 = 未拉取过
        private LiveRoomInfo _info = new LiveRoomInfo();

        public bool IsPolling => _cts != null && !_cts.IsCancellationRequested;
        public bool IsLive => _lastStatus == 1;
        /// <summary>最近一次拉取得到的 <c>live_status</c>：-1=未拉过 / 0=未播 / 1=直播 / 2=轮播。</summary>
        public int LiveStatus => _lastStatus;
        public long RoomId => _roomId;
        public string Title => _info.Title;
        /// <summary>最近一次拉取到的房间全部公开信息。</summary>
        public LiveRoomInfo Info => _info;

        protected override void Initialize()
        {
            base.Initialize();
            Log("LiveStatusService 初始化完成", Color.green);

            // 高频心跳事件加入静默集，避免每次轮询都刷一条 "[EventProcessor] 触发事件" 日志
            if (EssSystem.Core.Base.Event.EventProcessor.HasInstance)
                EssSystem.Core.Base.Event.EventProcessor.Instance.SilenceEvent(EVT_STATUS_POLLED);
        }

        // ─── Public API ────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// 启动周期轮询。已在轮询时会先停掉再启动新房间。
        /// </summary>
        /// <param name="roomId">B 站直播间号（不是用户 UID）</param>
        /// <param name="intervalSeconds">两次拉取间隔，建议 ≥ 15s 避免被风控</param>
        /// <param name="apiEndpoint">默认 <c>https://api.live.bilibili.com/room/v1/Room/get_info</c></param>
        public void StartPolling(long roomId, float intervalSeconds = 30f,
            string apiEndpoint = "https://api.live.bilibili.com/room/v1/Room/get_info")
        {
            if (roomId <= 0)
            {
                LogWarning("StartPolling: roomId 必须 > 0");
                return;
            }
            // 同房间已在轮询 → 幂等
            if (IsPolling && _roomId == roomId) return;

            StopPolling();

            _roomId = roomId;
            _lastStatus = -1;
            _info = new LiveRoomInfo { RoomId = roomId };
            _cts = new CancellationTokenSource();

            var interval = Mathf.Max(5f, intervalSeconds);
            Log($"开始轮询直播间 {roomId} 每 {interval}s", Color.cyan);

            // fire-and-forget；内部循环会处理异常
            _ = PollLoopAsync(roomId, interval, apiEndpoint, _cts.Token);
        }

        /// <summary>停止轮询（幂等）。</summary>
        public void StopPolling()
        {
            if (_cts != null)
            {
                try { _cts.Cancel(); } catch { /* ignore */ }
                _cts.Dispose();
                _cts = null;
                Log("已停止直播状态轮询", Color.yellow);
            }
        }

        /// <summary>立即拉取一次（不影响轮询计时）。返回是否拉取成功。</summary>
        public async Task<bool> CheckOnceAsync(long roomId,
            string apiEndpoint = "https://api.live.bilibili.com/room/v1/Room/get_info")
        {
            if (roomId <= 0) return false;
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await PollOnceAsync(roomId, apiEndpoint, cts.Token);
        }

        #endregion

        // ─── 轮询主循环 ───────────────────────────────────────────
        private async Task PollLoopAsync(long roomId, float intervalSec, string apiEndpoint, CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    await PollOnceAsync(roomId, apiEndpoint, ct);
                    await Task.Delay(TimeSpan.FromSeconds(intervalSec), ct);
                }
            }
            catch (OperationCanceledException) { /* 正常停止 */ }
            catch (Exception ex)
            {
                LogWarning($"PollLoopAsync 异常: {ex.Message}");
            }
        }

        private async Task<bool> PollOnceAsync(long roomId, string apiEndpoint, CancellationToken ct)
        {
            try
            {
                var url = $"{apiEndpoint}?room_id={roomId}";
                var resp = await _httpClient.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    LogWarning($"get_info 失败 HTTP {(int)resp.StatusCode}");
                    return false;
                }

                var body = MiniJson.Parse(await resp.Content.ReadAsStringAsync());
                var apiCode = body.Value<int>("code");
                if (apiCode != 0)
                {
                    LogWarning($"get_info 业务错误 code={apiCode}, msg={body["message"]}");
                    return false;
                }

                var data = body["data"];
                var realRoomId = data.Value<long>("room_id");
                if (realRoomId <= 0) realRoomId = roomId;

                var info = new LiveRoomInfo
                {
                    RoomId = realRoomId,
                    Uid = data.Value<long>("uid"),
                    LiveStatus = data.Value<int>("live_status"),
                    Title = data["title"].ToString(),
                    AreaName = data["area_name"].ToString(),
                    ParentAreaName = data["parent_area_name"].ToString(),
                    Online = data.Value<int>("online"),
                    Attention = data.Value<int>("attention"),
                    LiveTime = data["live_time"].ToString(),
                    Tags = data["tags"].ToString(),
                    Description = data["description"].ToString(),
                    UserCover = data["user_cover"].ToString(),
                    Keyframe = data["keyframe"].ToString(),
                    Background = data["background"].ToString(),
                };

                // 切回主线程触发事件（订阅方可安全调 Unity API）
                MainThreadDispatcher.Enqueue(() => HandlePollResult(info));
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex)
            {
                LogWarning($"PollOnceAsync 异常: {ex.Message}");
                return false;
            }
        }

        private void HandlePollResult(LiveRoomInfo info)
        {
            _info = info;
            var prev = _lastStatus;
            _lastStatus = info.LiveStatus;

            var ep = EventProcessor.Instance;
            if (ep == null) return;

            // 总是广播 polled（附带完整 LiveRoomInfo）
            ep.TriggerEvent(EVT_STATUS_POLLED, new List<object> { info.RoomId, info.LiveStatus, info.Title, info });

            // 边沿检测：从 非1 → 1 = 开播；从 1 → 非1 = 下播
            if (prev == -1) return; // 第一次拉取不算转换
            if (prev != 1 && info.LiveStatus == 1)
            {
                Log($"直播开始: {info.RoomId} {info.Title}", Color.green);
                ep.TriggerEvent(EVT_LIVE_STARTED, new List<object> { info.RoomId, info.Title, info });
            }
            else if (prev == 1 && info.LiveStatus != 1)
            {
                Log($"直播结束: {info.RoomId}", Color.yellow);
                ep.TriggerEvent(EVT_LIVE_ENDED, new List<object> { info.RoomId, info.Title, info });
            }
        }
    }
}
