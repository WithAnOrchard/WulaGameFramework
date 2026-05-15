using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BiliBiliDanmu.Dao;
using BiliBiliDanmu.Net;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace BiliBiliDanmu
{
    /// <summary>
    /// B 站直播弹幕业务 Service
    /// <list type="bullet">
    /// <item>持有底层 <see cref="OpenDanmakuLoader"/> 长连接 + 共享 <see cref="HttpClient"/></item>
    /// <item>所有 EVT_* 都是<b>广播</b>（订阅向）：网络回调先走 <see cref="MainThreadDispatcher"/> 切主线程，再 <c>TriggerEvent</c></item>
    /// <item>JSON 解析走框架自带的 <see cref="MiniJson"/> + <see cref="JsonNode"/>，无外部 Newtonsoft 依赖</item>
    /// </list>
    /// </summary>
    public class DanmuService : Service<DanmuService>
    {
        #region 事件名称（全部为广播 / 订阅向）

        /// <summary>连接成功（广播）。参数：<c>[long roomId]</c></summary>
        public const string EVT_CONNECTED    = "OnDanmuConnected";
        /// <summary>连接断开（广播）。参数：<c>[Exception errorOrNull]</c></summary>
        public const string EVT_DISCONNECTED = "OnDanmuDisconnected";
        /// <summary>普通弹幕（广播）。参数：<c>[string userName, string commentText, long userId]</c></summary>
        public const string EVT_DANMAKU      = "OnDanmuComment";
        /// <summary>礼物（广播）。参数：<c>[string userName, string giftName, int giftCount, long userId]</c></summary>
        public const string EVT_GIFT         = "OnDanmuGift";
        /// <summary>原始消息（广播，所有类型）。参数：<c>[DanmakuModel model]</c></summary>
        public const string EVT_RAW          = "OnDanmuRaw";

        #endregion

        // ─── 共享 HTTP（避免端口耗尽） ─────────────────────────────
        private static readonly HttpClient _httpClient = new HttpClient();

        // ─── 运行时状态（不参与持久化） ────────────────────────────
        private OpenDanmakuLoader _loader;
        private RoomInfo _roomInfo;
        private bool _connecting;

        /// <summary>当前是否已连接（loader 真实在线状态）。</summary>
        public bool IsConnected => _loader != null && _loader.Connected;

        /// <summary>已成功握手得到的房间号；未连接 = 0。</summary>
        public long RoomId => _roomInfo?.RoomId ?? 0;

        protected override void Initialize()
        {
            base.Initialize();
            Log("DanmuService 初始化完成", Color.green);
        }

        // ─── Public API ────────────────────────────────────────────
        #region Public API

        /// <summary>
        /// 用身份码 + AppId 发起连接。已连接时直接返回 true。失败仅日志，不抛异常。
        /// </summary>
        public async Task<bool> ConnectAsync(string identityCode, long appId,
            string signEndpoint, string startEndpoint, float httpTimeoutSeconds)
        {
            if (IsConnected) return true;
            if (_connecting) return false;
            _connecting = true;
            try
            {
                if (string.IsNullOrEmpty(identityCode))
                {
                    LogWarning("ConnectAsync: 身份码为空");
                    return false;
                }

                // D3: 不再改 _httpClient.Timeout 全局（thread-unsafe 且一旦发过请求后修改会抛异常）。
                // 改为给本次调用独立的 CancellationTokenSource，每个 HTTP 请求退出 timeout 独立。
                var timeoutMs = (int)(Mathf.Max(1f, httpTimeoutSeconds) * 1000f);
                using var cts = new CancellationTokenSource(timeoutMs);

                var info = await GetRoomInfoByCode(identityCode, appId, signEndpoint, startEndpoint, cts.Token);
                if (info == null)
                {
                    LogWarning("ConnectAsync: 获取房间信息失败");
                    return false;
                }
                _roomInfo = info;

                _loader = new OpenDanmakuLoader(info.Auth, info.Servers, info.GameId);
                _loader.ReceivedDanmaku += OnLoaderDanmaku;
                _loader.Disconnected    += OnLoaderDisconnected;

                var ok = await _loader.ConnectAsync();
                if (!ok)
                {
                    LogWarning("ConnectAsync: 长连接握手失败");
                    DisconnectInternal();
                    return false;
                }

                Log($"已连接到房间 {info.RoomId}", Color.cyan);
                MainThreadDispatcher.Enqueue(() =>
                    EventProcessor.Instance?.TriggerEvent(EVT_CONNECTED,
                        new List<object> { (long)info.RoomId }));
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"ConnectAsync 异常: {ex.Message}");
                DisconnectInternal();
                return false;
            }
            finally
            {
                _connecting = false;
            }
        }

        /// <summary>主动断开（幂等）。</summary>
        public void Disconnect() => DisconnectInternal();

        #endregion

        // ─── Loader 回调（后台线程） ───────────────────────────────
        #region Loader Callbacks

        private void OnLoaderDanmaku(object sender, ReceivedDanmakuArgs e)
        {
            var dm = e?.Danmaku;
            if (dm == null) return;

            // 切主线程后再 TriggerEvent，避免事件订阅者触碰 Unity API 时崩
            MainThreadDispatcher.Enqueue(() =>
            {
                var ep = EventProcessor.Instance;
                if (ep == null) return;

                ep.TriggerEvent(EVT_RAW, new List<object> { dm });

                switch (dm.MsgType)
                {
                    case MsgTypeEnum.Comment:
                        ep.TriggerEvent(EVT_DANMAKU, new List<object>
                            { dm.UserName ?? string.Empty, dm.CommentText ?? string.Empty, dm.UserID_long });
                        break;
                    case MsgTypeEnum.GiftSend:
                        ep.TriggerEvent(EVT_GIFT, new List<object>
                            { dm.UserName ?? string.Empty, dm.GiftName ?? string.Empty, dm.GiftCount, dm.UserID_long });
                        break;
                }
            });
        }

        private void OnLoaderDisconnected(object sender, DisconnectEvtArgs e)
        {
            var err = e?.Error;
            MainThreadDispatcher.Enqueue(() =>
            {
                Log("长连接断开" + (err == null ? string.Empty : $": {err.Message}"), Color.yellow);
                EventProcessor.Instance?.TriggerEvent(EVT_DISCONNECTED, new List<object> { err });
            });
        }

        #endregion

        // ─── 内部 ──────────────────────────────────────────────────
        #region Internal

        private void DisconnectInternal()
        {
            if (_loader != null)
            {
                try
                {
                    _loader.ReceivedDanmaku -= OnLoaderDanmaku;
                    _loader.Disconnected    -= OnLoaderDisconnected;
                    _loader.Disconnect();
                    _loader.Dispose();
                }
                catch (Exception ex)
                {
                    LogWarning($"DisconnectInternal 异常: {ex.Message}");
                }
                _loader = null;
            }
            _roomInfo = null;
        }

        /// <summary>
        /// 用身份码换取房间号 + websocket 鉴权信息。
        /// 两步：签名代理 → biliapi <c>/v2/app/start</c>。JSON 全部走 <see cref="MiniJson"/>。
        /// </summary>
        private async Task<RoomInfo> GetRoomInfoByCode(string code, long appId,
            string signEndpoint, string startEndpoint, CancellationToken ct)
        {
            try
            {
                if (string.IsNullOrEmpty(code)) return null;

                // 请求体：{"code":"xxx","app_id":1651388990835}
                var paramJson = MiniJson.Serialize(new Dictionary<string, object>
                {
                    { "code", code },
                    { "app_id", appId },
                });

                // ① 拿签名头 · D3：传入 ct 在 timeout 时取消
                var signResp = await _httpClient.PostAsync(signEndpoint,
                    new StringContent(paramJson, Encoding.UTF8, "application/json"), ct);
                if (!signResp.IsSuccessStatusCode)
                {
                    LogWarning("签名服务器离线");
                    return null;
                }
                var signNode = MiniJson.Parse(await signResp.Content.ReadAsStringAsync());
                if (!(signNode.Raw is Dictionary<string, object> signDict))
                {
                    LogWarning("签名响应不是 JSON 对象");
                    return null;
                }

                // ② 用签名调 biliapi /v2/app/start
                var req = new HttpRequestMessage(HttpMethod.Post, startEndpoint);
                req.Content = new StringContent(paramJson, Encoding.UTF8, "application/json");
                req.Content.Headers.Remove("Content-Type");
                req.Content.Headers.Add("Content-Type", "application/json");
                foreach (var kv in signDict)
                    req.Headers.Add(kv.Key, kv.Value?.ToString() ?? string.Empty);
                req.Headers.Add("Accept", "application/json");

                // D3: 传入 ct
                var resp = await _httpClient.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    LogWarning("B 站直播中心离线");
                    return null;
                }

                // 响应示例：{code:0, data:{anchor_info:{room_id}, websocket_info:{auth_body, wss_link:[]}, game_info:{game_id}}}
                var body = MiniJson.Parse(await resp.Content.ReadAsStringAsync());
                var apiCode = body.Value<int>("code");
                if (apiCode != 0)
                {
                    LogWarning($"B 站返回错误 code={apiCode}, msg={body["message"]}");
                    return null;
                }

                var data = body["data"];
                var roomId = data["anchor_info"].Value<int>("room_id");
                var auth   = data["websocket_info"]["auth_body"].ToString();
                if (roomId <= 0 || string.IsNullOrEmpty(auth)) return null;

                var wssNodes = data["websocket_info"]["wss_link"].ToList();
                var servers = new string[wssNodes.Count];
                for (var i = 0; i < wssNodes.Count; i++) servers[i] = wssNodes[i].ToString();

                var gameId = data["game_info"]["game_id"].ToString();

                return new RoomInfo { Auth = auth, Servers = servers, RoomId = roomId, GameId = gameId };
            }
            catch (Exception ex)
            {
                LogWarning($"GetRoomInfoByCode 异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>仅内部用的握手结果；不持久化。</summary>
        private sealed class RoomInfo
        {
            public string Auth;
            public string[] Servers;
            public int RoomId;
            public string GameId;
        }

        #endregion
    }
}
