using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BiliBiliDanmu.Dao;
using BiliBiliDanmu.Net;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace BiliBiliDanmu
{
    public enum DanmuMode
    {
        /// <summary>/ajax/msg 周期轮询。零认证；仅文字弹幕；约 3s 延迟。任何房间可用。</summary>
        Polling,
        /// <summary>SESSDATA cookie + WSS 长连接。需用户登录态；可见所有人 + 礼物 + SC；实时。</summary>
        Token,
        /// <summary>主播身份码 + OpenLive。仅自己直播间；可见所有人 + 礼物 + SC；实时。</summary>
        OpenLive,
    }

    /// <summary>
    /// 统一弹幕连接配置。<see cref="Mode"/> 决定走哪条策略，其余字段按需填。
    /// </summary>
    public sealed class DanmuConnectConfig
    {
        public DanmuMode Mode = DanmuMode.Polling;

        // 通用（Polling/Token）
        public long RoomId;

        // OpenLive 模式
        public string IdentityCode = string.Empty;
        public long AppId = 1651388990835L;
        public string SignEndpoint = "https://bopen.ceve-market.org/sign";
        public string StartEndpoint = "https://live-open.biliapi.com/v2/app/start";
        public float HttpTimeoutSeconds = 5f;

        // Token 模式（SESSDATA cookie 登录态）
        public string Sessdata = string.Empty;
        public string BiliJct = string.Empty;

        // Polling 模式
        public float PollIntervalSeconds = 3f;
    }

    /// <summary>
    /// B 站直播弹幕业务 Service —— 单点入口，按 <see cref="DanmuMode"/> 路由到三种策略。
    /// <list type="bullet">
    /// <item>事件常量统一为 <c>EVT_*</c>，业务订阅方<b>无需感知模式</b>。</item>
    /// <item><b>能力差异</b>：Polling 仅文字；Token / OpenLive 含礼物 + SC 实时推送。</item>
    /// </list>
    /// </summary>
    public class DanmuService : Service<DanmuService>
    {
        #region 事件常量（全部为广播）

        public const string EVT_CONNECTED    = "OnDanmuConnected";
        public const string EVT_DISCONNECTED = "OnDanmuDisconnected";
        public const string EVT_DANMAKU      = "OnDanmuComment";
        public const string EVT_GIFT         = "OnDanmuGift";
        /// <summary>Super Chat 事件。data: [userName, text, priceYuan_int, userId_long]</summary>
        public const string EVT_SC           = "OnDanmuSuperChat";
        public const string EVT_RAW          = "OnDanmuRaw";

        #endregion

        #region GC 优化

        private static readonly List<object> _tempList1 = new List<object>(1);
        private static readonly List<object> _tempList3 = new List<object>(3);
        #endregion

        // ─── 共享 HTTP ────────────────────────────────────────
        private static readonly HttpClient _httpClient = new HttpClient();
        // Token 模式专用 HTTP（CookieContainer 自动管理）
        private static readonly CookieContainer _tokenCookies = new CookieContainer();
        private static HttpClient _tokenHttp;
        private static bool _tokenCookiesWarmed;

        // ─── 运行时状态 ────────────────────────────────────────
        private DanmuMode _activeMode;
        private bool _connecting;
        private long _activeRoomId;

        // OpenLive
        private OpenDanmakuLoader _openLoader;
        private RoomInfo _roomInfo;
        // Token (anon WSS)
        private AnonDanmakuLoader _anonLoader;
        // Polling
        private CancellationTokenSource _pollCts;
        private readonly HashSet<string> _pollSeenIds = new HashSet<string>();
        private readonly Queue<string> _pollSeenIdQueue = new Queue<string>();

        public bool IsConnected =>
            (_openLoader != null && _openLoader.Connected) ||
            (_anonLoader != null && _anonLoader.Connected) ||
            (_pollCts != null && !_pollCts.IsCancellationRequested);

        public long RoomId => _activeRoomId;
        public DanmuMode ActiveMode => _activeMode;

        protected override void Initialize()
        {
            base.Initialize();
            Log("DanmuService 初始化完成", Color.green);
        }

        // ─── Public API ───────────────────────────────────────
        public async Task<bool> ConnectAsync(DanmuConnectConfig cfg)
        {
            if (cfg == null) { LogWarning("ConnectAsync: cfg=null"); return false; }
            if (IsConnected) return true;
            if (_connecting) return false;
            _connecting = true;
            try
            {
                _activeMode = cfg.Mode;
                switch (cfg.Mode)
                {
                    case DanmuMode.Polling:  return await ConnectPollingAsync(cfg);
                    case DanmuMode.Token:    return await ConnectTokenAsync(cfg);
                    case DanmuMode.OpenLive: return await ConnectOpenLiveAsync(cfg);
                    default: return false;
                }
            }
            finally { _connecting = false; }
        }

        public void Disconnect() => DisconnectInternal();

        // ─── 模式：Polling ────────────────────────────────────
        #region Polling
        private const string AjaxMsgApi = "http://api.live.bilibili.com/ajax/msg";
        private const int PollMaxIdCache = 200;
        private bool _pollFirstDone;
        private float _pollInterval = 3f;

        private Task<bool> ConnectPollingAsync(DanmuConnectConfig cfg)
        {
            if (cfg.RoomId <= 0) { LogWarning("Polling: roomId 必须 > 0"); return Task.FromResult(false); }
            _activeRoomId = cfg.RoomId;
            _pollInterval = Mathf.Max(1.5f, cfg.PollIntervalSeconds);
            _pollSeenIds.Clear(); _pollSeenIdQueue.Clear(); _pollFirstDone = false;
            _pollCts = new CancellationTokenSource();

            Log($"[Polling] 开始轮询 room={cfg.RoomId} 间隔 {_pollInterval}s", Color.cyan);
            _ = PollLoopAsync(cfg.RoomId, _pollCts.Token);
            var roomId = cfg.RoomId;
            MainThreadDispatcher.Enqueue(() =>
            {
                _tempList1.Clear();
                _tempList1.Add(roomId);
                EventProcessor.Instance?.TriggerEvent(EVT_CONNECTED, _tempList1);
            });
            return Task.FromResult(true);
        }

        private async Task PollLoopAsync(long roomId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try { await PollOnceAsync(roomId, ct); }
                catch (OperationCanceledException) { break; }
                catch (Exception ex) { LogWarning($"[Polling] 异常: {ex.Message}"); }
                try { await Task.Delay(TimeSpan.FromSeconds(_pollInterval), ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task PollOnceAsync(long roomId, CancellationToken ct)
        {
            using var resp = await _httpClient.GetAsync($"{AjaxMsgApi}?roomid={roomId}", ct);
            if (!resp.IsSuccessStatusCode) return;
            var body = MiniJson.Parse(await resp.Content.ReadAsStringAsync());
            if (body.Value<int>("code") != 0) return;

            var arr = body["data"]["room"].ToList();
            if (arr.Count == 0) { _pollFirstDone = true; return; }

            if (!_pollFirstDone)
            {
                foreach (var msg in arr)
                {
                    var id = msg["id_str"].ToString();
                    if (!string.IsNullOrEmpty(id)) PollMarkSeen(id);
                }
                _pollFirstDone = true;
                return;
            }

            foreach (var msg in arr)
            {
                var id = msg["id_str"].ToString();
                if (string.IsNullOrEmpty(id) || _pollSeenIds.Contains(id)) continue;
                PollMarkSeen(id);

                var text = msg["text"].ToString();
                var uname = msg["nickname"].ToString();
                var uid = msg.Value<long>("uid");
                var t = text; var u = uname; var userId = uid;
                MainThreadDispatcher.Enqueue(() =>
                {
                    _tempList3.Clear();
                    _tempList3.Add(u); _tempList3.Add(t); _tempList3.Add(userId);
                    EventProcessor.Instance?.TriggerEvent(EVT_DANMAKU, _tempList3);
                });
            }
        }

        private void PollMarkSeen(string id)
        {
            if (!_pollSeenIds.Add(id)) return;
            _pollSeenIdQueue.Enqueue(id);
            while (_pollSeenIdQueue.Count > PollMaxIdCache)
                _pollSeenIds.Remove(_pollSeenIdQueue.Dequeue());
        }
        #endregion

        // ─── 模式：Token (SESSDATA + WSS) ──────────────────────
        #region Token
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
        private const string DanmuInfoApi = "https://api.live.bilibili.com/xlive/web-room/v1/index/getDanmuInfo";
        private const string RoomInfoApi  = "https://api.live.bilibili.com/room/v1/Room/get_info";
        private const string BuvidApi     = "https://api.bilibili.com/x/frontend/finger/spi";
        private const string NavApi       = "https://api.bilibili.com/x/web-interface/nav";
        private const string HomePage     = "https://www.bilibili.com";
        private long _tokenUid;

        private static HttpClient EnsureTokenHttp()
        {
            if (_tokenHttp != null) return _tokenHttp;
            var handler = new HttpClientHandler
            {
                CookieContainer = _tokenCookies,
                UseCookies = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };
            _tokenHttp = new HttpClient(handler);
            _tokenHttp.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            _tokenHttp.DefaultRequestHeaders.Add("Accept", "application/json, text/plain, */*");
            _tokenHttp.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9");
            return _tokenHttp;
        }

        private async Task<bool> ConnectTokenAsync(DanmuConnectConfig cfg)
        {
            if (cfg.RoomId <= 0) { LogWarning("Token: roomId 必须 > 0"); return false; }
            try
            {
                var http = EnsureTokenHttp();
                await EnsureTokenCookiesAsync(http, cfg);

                // 解析真实房间号
                var realRoomId = await ResolveRealRoomIdAsync(http, cfg.RoomId);
                if (realRoomId <= 0) realRoomId = cfg.RoomId;

                // WBI 签名拉 token
                var signedQuery = await WbiSigner.SignQueryAsync(http, new Dictionary<string, string>
                {
                    { "id", realRoomId.ToString() }, { "type", "0" }
                });
                using var req = new HttpRequestMessage(HttpMethod.Get, $"{DanmuInfoApi}?{signedQuery}");
                req.Headers.Referrer = new Uri($"https://live.bilibili.com/{realRoomId}");
                req.Headers.Add("Origin", "https://live.bilibili.com");
                using var resp = await http.SendAsync(req);
                resp.EnsureSuccessStatusCode();
                var json = await resp.Content.ReadAsStringAsync();
                var body = MiniJson.Parse(json);
                var code = body.Value<int>("code");
                if (code != 0)
                {
                    LogWarning($"[Token] getDanmuInfo code={code} msg={body["message"]}");
                    return false;
                }

                var data = body["data"];
                var wsToken = data["token"].ToString();
                var hostList = data["host_list"].ToList();
                if (string.IsNullOrEmpty(wsToken) || hostList.Count == 0)
                {
                    LogWarning("[Token] token/host_list 为空");
                    return false;
                }
                var first = hostList[0];
                var host = first["host"].ToString();
                var wssPort = first.Value<int>("wss_port");
                if (wssPort <= 0) wssPort = 443;

                _anonLoader = new AnonDanmakuLoader(realRoomId, wsToken, host, wssPort, _tokenUid);
                _anonLoader.ReceivedDanmaku += OnAnonLoaderDanmaku;
                _anonLoader.Disconnected += OnAnonLoaderDisconnected;
                var ok = await _anonLoader.ConnectAsync();
                if (!ok) { LogWarning("[Token] WSS 握手失败"); return false; }

                _activeRoomId = realRoomId;
                Log($"[Token] 已连接 room={realRoomId} via {host}:{wssPort} (uid={_tokenUid})", Color.cyan);
                var room = realRoomId;
                MainThreadDispatcher.Enqueue(() =>
                {
                    _tempList1.Clear();
                    _tempList1.Add(room);
                    EventProcessor.Instance?.TriggerEvent(EVT_CONNECTED, _tempList1);
                });
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"[Token] ConnectAsync 异常: {ex.Message}");
                return false;
            }
        }

        private async Task EnsureTokenCookiesAsync(HttpClient http, DanmuConnectConfig cfg)
        {
            if (_tokenCookiesWarmed) return;
            // 注入用户 cookie
            if (!string.IsNullOrEmpty(cfg.Sessdata))
            {
                _tokenCookies.Add(new Cookie("SESSDATA", cfg.Sessdata.Trim(), "/", ".bilibili.com"));
            }
            if (!string.IsNullOrEmpty(cfg.BiliJct))
            {
                _tokenCookies.Add(new Cookie("bili_jct", cfg.BiliJct.Trim(), "/", ".bilibili.com"));
            }
            // 预热主站 cookie
            try { using var _ = await http.GetAsync(HomePage); } catch { }

            // 已登录 → 用 nav 拿 uid
            if (!string.IsNullOrEmpty(cfg.Sessdata))
            {
                try
                {
                    using var r = await http.GetAsync(NavApi);
                    if (r.IsSuccessStatusCode)
                    {
                        var nav = MiniJson.Parse(await r.Content.ReadAsStringAsync());
                        if (nav.Value<int>("code") == 0)
                        {
                            _tokenUid = nav["data"].Value<long>("mid");
                            Log($"[Token] SESSDATA 登录成功 uid={_tokenUid} uname={nav["data"]["uname"]}", Color.green);
                        }
                    }
                }
                catch (Exception ex) { LogWarning($"[Token] nav 异常: {ex.Message}"); }
            }
            // 补 buvid3
            try
            {
                using var r = await http.GetAsync(BuvidApi);
                if (r.IsSuccessStatusCode)
                {
                    var b = MiniJson.Parse(await r.Content.ReadAsStringAsync());
                    if (b.Value<int>("code") == 0)
                    {
                        var b3 = b["data"]["b_3"].ToString();
                        if (!string.IsNullOrEmpty(b3))
                            _tokenCookies.Add(new Cookie("buvid3", b3, "/", ".bilibili.com"));
                    }
                }
            }
            catch (Exception ex) { LogWarning($"[Token] buvid3 异常: {ex.Message}"); }

            _tokenCookiesWarmed = true;
        }

        private async Task<long> ResolveRealRoomIdAsync(HttpClient http, long roomId)
        {
            try
            {
                using var resp = await http.GetAsync($"{RoomInfoApi}?room_id={roomId}");
                if (!resp.IsSuccessStatusCode) return roomId;
                var body = MiniJson.Parse(await resp.Content.ReadAsStringAsync());
                if (body.Value<int>("code") != 0) return roomId;
                var real = body["data"].Value<long>("room_id");
                return real > 0 ? real : roomId;
            }
            catch { return roomId; }
        }

        private void OnAnonLoaderDanmaku(object sender, AnonDanmuMessage msg)
        {
            if (msg == null) return;
            MainThreadDispatcher.Enqueue(() =>
            {
                var ep = EventProcessor.Instance;
                if (ep == null) return;
                switch (msg.Type)
                {
                    case AnonDanmuMsgType.Comment:
                        ep.TriggerEvent(EVT_DANMAKU, new List<object> { msg.UserName, msg.Text, msg.UserId });
                        break;
                    case AnonDanmuMsgType.Gift:
                        ep.TriggerEvent(EVT_GIFT, new List<object> { msg.UserName, msg.GiftName, msg.GiftCount, msg.UserId, msg.GiftPrice, msg.GiftCoinType });
                        break;
                    case AnonDanmuMsgType.SuperChat:
                        ep.TriggerEvent(EVT_SC, new List<object> { msg.UserName, msg.Text, msg.GiftCount, msg.UserId });
                        break;
                    // SuperChat 已映射到 EVT_SC
                }
            });
        }

        private void OnAnonLoaderDisconnected(object sender, Exception err)
        {
            MainThreadDispatcher.Enqueue(() =>
            {
                Log("[Token] WSS 断开" + (err == null ? string.Empty : $": {err.Message}"), Color.yellow);
                EventProcessor.Instance?.TriggerEvent(EVT_DISCONNECTED, new List<object> { err });
            });
        }
        #endregion

        // ─── 模式：OpenLive ────────────────────────────────────
        #region OpenLive
        private async Task<bool> ConnectOpenLiveAsync(DanmuConnectConfig cfg)
        {
            if (string.IsNullOrEmpty(cfg.IdentityCode)) { LogWarning("[OpenLive] 身份码为空"); return false; }
            try
            {
                var timeoutMs = (int)(Mathf.Max(1f, cfg.HttpTimeoutSeconds) * 1000f);
                using var cts = new CancellationTokenSource(timeoutMs);
                var info = await GetRoomInfoByCode(cfg.IdentityCode, cfg.AppId, cfg.SignEndpoint, cfg.StartEndpoint, cts.Token);
                if (info == null) { LogWarning("[OpenLive] 获取房间信息失败"); return false; }
                _roomInfo = info;

                _openLoader = new OpenDanmakuLoader(info.Auth, info.Servers, info.GameId);
                _openLoader.ReceivedDanmaku += OnOpenLoaderDanmaku;
                _openLoader.Disconnected    += OnOpenLoaderDisconnected;
                var ok = await _openLoader.ConnectAsync();
                if (!ok) { LogWarning("[OpenLive] 长连接握手失败"); DisconnectInternal(); return false; }

                _activeRoomId = info.RoomId;
                Log($"[OpenLive] 已连接 room={info.RoomId}", Color.cyan);
                MainThreadDispatcher.Enqueue(() =>
                    EventProcessor.Instance?.TriggerEvent(EVT_CONNECTED, new List<object> { (long)info.RoomId }));
                return true;
            }
            catch (Exception ex)
            {
                LogWarning($"[OpenLive] ConnectAsync 异常: {ex.Message}");
                DisconnectInternal();
                return false;
            }
        }

        private void OnOpenLoaderDanmaku(object sender, ReceivedDanmakuArgs e)
        {
            var dm = e?.Danmaku;
            if (dm == null) return;
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
                            { dm.UserName ?? string.Empty, dm.GiftName ?? string.Empty, dm.GiftCount, dm.UserID_long, dm.GiftPrice, dm.GiftCoinType });
                        break;
                }
            });
        }

        private void OnOpenLoaderDisconnected(object sender, DisconnectEvtArgs e)
        {
            var err = e?.Error;
            MainThreadDispatcher.Enqueue(() =>
            {
                Log("[OpenLive] 长连接断开" + (err == null ? string.Empty : $": {err.Message}"), Color.yellow);
                EventProcessor.Instance?.TriggerEvent(EVT_DISCONNECTED, new List<object> { err });
            });
        }

        private async Task<RoomInfo> GetRoomInfoByCode(string code, long appId,
            string signEndpoint, string startEndpoint, CancellationToken ct)
        {
            try
            {
                var paramJson = MiniJson.Serialize(new Dictionary<string, object>
                {
                    { "code", code }, { "app_id", appId },
                });
                var signResp = await _httpClient.PostAsync(signEndpoint,
                    new StringContent(paramJson, Encoding.UTF8, "application/json"), ct);
                if (!signResp.IsSuccessStatusCode) { LogWarning("签名服务器离线"); return null; }
                var signNode = MiniJson.Parse(await signResp.Content.ReadAsStringAsync());
                if (!(signNode.Raw is Dictionary<string, object> signDict))
                { LogWarning("签名响应不是 JSON 对象"); return null; }

                var req = new HttpRequestMessage(HttpMethod.Post, startEndpoint);
                req.Content = new StringContent(paramJson, Encoding.UTF8, "application/json");
                req.Content.Headers.Remove("Content-Type");
                req.Content.Headers.Add("Content-Type", "application/json");
                foreach (var kv in signDict)
                    req.Headers.Add(kv.Key, kv.Value?.ToString() ?? string.Empty);
                req.Headers.Add("Accept", "application/json");

                var resp = await _httpClient.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode) { LogWarning("B 站直播中心离线"); return null; }
                var body = MiniJson.Parse(await resp.Content.ReadAsStringAsync());
                var apiCode = body.Value<int>("code");
                if (apiCode != 0) { LogWarning($"B 站返回 code={apiCode} msg={body["message"]}"); return null; }

                var data = body["data"];
                var roomId = data["anchor_info"].Value<int>("room_id");
                var auth = data["websocket_info"]["auth_body"].ToString();
                if (roomId <= 0 || string.IsNullOrEmpty(auth)) return null;
                var wssNodes = data["websocket_info"]["wss_link"].ToList();
                var servers = new string[wssNodes.Count];
                for (var i = 0; i < wssNodes.Count; i++) servers[i] = wssNodes[i].ToString();
                var gameId = data["game_info"]["game_id"].ToString();
                return new RoomInfo { Auth = auth, Servers = servers, RoomId = roomId, GameId = gameId };
            }
            catch (Exception ex) { LogWarning($"GetRoomInfoByCode 异常: {ex.Message}"); return null; }
        }

        private sealed class RoomInfo
        {
            public string Auth;
            public string[] Servers;
            public int RoomId;
            public string GameId;
        }
        #endregion

        // ─── 公共销毁 ──────────────────────────────────────────
        private void DisconnectInternal()
        {
            // Polling
            if (_pollCts != null)
            {
                try { _pollCts.Cancel(); _pollCts.Dispose(); } catch { }
                _pollCts = null;
                MainThreadDispatcher.Enqueue(() =>
                    EventProcessor.Instance?.TriggerEvent(EVT_DISCONNECTED, new List<object> { (Exception)null }));
            }
            // Token
            if (_anonLoader != null)
            {
                try
                {
                    _anonLoader.ReceivedDanmaku -= OnAnonLoaderDanmaku;
                    _anonLoader.Disconnected -= OnAnonLoaderDisconnected;
                    _anonLoader.Disconnect();
                    _anonLoader.Dispose();
                }
                catch { }
                _anonLoader = null;
            }
            // OpenLive
            if (_openLoader != null)
            {
                try
                {
                    _openLoader.ReceivedDanmaku -= OnOpenLoaderDanmaku;
                    _openLoader.Disconnected -= OnOpenLoaderDisconnected;
                    _openLoader.Disconnect();
                    _openLoader.Dispose();
                }
                catch (Exception ex) { LogWarning($"DisconnectInternal 异常: {ex.Message}"); }
                _openLoader = null;
            }
            _roomInfo = null;
            _activeRoomId = 0;
        }
    }
}
