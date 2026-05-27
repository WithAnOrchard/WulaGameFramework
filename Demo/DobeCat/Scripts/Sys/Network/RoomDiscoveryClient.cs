using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Demo.DobeCat.Sys.Auth;
using EssSystem.Core.Base.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 多人桌宠房间发现客户端 —— 与 <c>tools/data_exchange_server</c> 配合：
    /// <list type="bullet">
    /// <item>启动后每 <see cref="HeartbeatIntervalSeconds"/> 秒 POST 一次本机房间信息（带 TTL）。</item>
    /// <item>每 <see cref="ListIntervalSeconds"/> 秒 GET 一次房间列表，更新 <see cref="LatestRooms"/> 并广播 <see cref="OnRoomsChanged"/>。</item>
    /// <item>OnApplicationQuit 时尽量 DELETE 自己注册的条目，避免 30s 后才被 GC。</item>
    /// </list>
    /// </summary>
    public class RoomDiscoveryClient : MonoBehaviour
    {
        // ── Inspector / 调用方注入 ─────────────────────────────

        [Tooltip("数据收发器的 Base URL，例如 http://192.168.1.10:8765。留空则禁用收发。")]
        public string ServerBaseUrl = "";

        [Tooltip("集合名（同集合的房间互相能看到）。")]
        public string CollectionName = "rooms";

        [Tooltip("本房间在数据收发器里的稳定 ID（默认设备名+随机后缀）。")]
        public string RoomId = "";

        [Tooltip("展示给其他玩家的房间名（默认设备名）。")]
        public string RoomDisplayName = "";

        [Tooltip("公布给其他玩家的 Mirror Host 地址（默认本机首个非环回 IPv4）。")]
        public string AdvertisedHost = "";

        [Tooltip("公布给其他玩家的 Mirror Host 端口。")]
        public ushort AdvertisedPort = 7777;

        [Tooltip("心跳间隔（秒）。建议 < TTL/2。默认 3s 让新房间更快被其他客户端看到。")]
        [Min(1f)] public float HeartbeatIntervalSeconds = 3f;

        [Tooltip("条目 TTL（秒）。心跳间隔 3s 时 TTL 15s 容忍 ~4 次失败而不被踢。")]
        [Min(5f)] public float ItemTtlSeconds = 15f;

        [Tooltip("拉取列表间隔（秒）。SSE 已连上时仅作为兜底，可以放宽；断流时 fallback 用此频率。")]
        [Min(0.5f)] public float ListIntervalSeconds = 1.5f;

        [Tooltip("是否启用服务端 SSE 推送（/collections/<name>/stream）。开启后主路径走推送，HTTP 列表轮询作为兜底。")]
        public bool UseServerPush = true;

        [Tooltip("SSE 断线重连等待秒数。")]
        [Min(0.5f)] public float StreamReconnectDelaySeconds = 2f;

        [Tooltip("HTTP 请求超时（秒）。")]
        [Min(1f)] public float HttpTimeoutSeconds = 5f;

        [Tooltip("是否详细打印 HTTP 日志。")]
        public bool VerboseLog = false;

        // ── 运行时状态 ─────────────────────────────────────────

        public sealed class RoomInfo
        {
            public string Id;
            public string Name;
            public long   BiliUid;
            public string Host;
            public int Port;
            public int PlayerCount;
            public double TtlRemaining;
            public bool IsSelf;
        }

        private readonly List<RoomInfo> _latestRooms = new List<RoomInfo>();
        public IReadOnlyList<RoomInfo> LatestRooms => _latestRooms;

        /// <summary>每次拉到新列表都会触发（即使列表为空）。在 Unity 主线程派发。</summary>
        public event Action<IReadOnlyList<RoomInfo>> OnRoomsChanged;

        private Coroutine _heartbeatCo;
        private Coroutine _listCo;
        private bool _registered;
        private bool _forceListPull; // RefreshNow 设 true，ListLoop 下一次循环立刻 PullList 不等

        // SSE 推送线程相关
        private Thread _streamThread;
        private volatile bool _streamThreadRunning;
        private volatile bool _streamConnected;
        private HttpWebRequest _streamRequest;
        private readonly object _pendingSnapshotLock = new object();
        private string _pendingSnapshotJson; // 由 SSE 线程写入，主线程消费

        // ── 生命周期 ───────────────────────────────────────────

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(RoomId)) RoomId = GenerateDefaultRoomId();
            if (string.IsNullOrEmpty(RoomDisplayName))
                RoomDisplayName = !string.IsNullOrEmpty(AuthSession.Nickname) ? AuthSession.Nickname : SystemInfo.deviceName;
            if (string.IsNullOrEmpty(AdvertisedHost)) AdvertisedHost = DetectLocalIPv4() ?? "127.0.0.1";

            _heartbeatCo = StartCoroutine(HeartbeatLoop());
            _listCo = StartCoroutine(ListLoop());
            StartStreamThread();
            Debug.Log($"[RoomDiscovery] 启动: id={RoomId} host={AdvertisedHost}:{AdvertisedPort} server={ServerBaseUrl} push={UseServerPush}");
        }

        private void OnDisable()
        {
            if (_heartbeatCo != null) { StopCoroutine(_heartbeatCo); _heartbeatCo = null; }
            if (_listCo != null) { StopCoroutine(_listCo); _listCo = null; }
            StopStreamThread();
        }

        private void Update()
        {
            // 主线程消费 SSE 线程投递的最新快照（最多每帧一次，丢弃中间帧）
            string json = null;
            lock (_pendingSnapshotLock)
            {
                if (_pendingSnapshotJson != null)
                {
                    json = _pendingSnapshotJson;
                    _pendingSnapshotJson = null;
                }
            }
            if (json != null) ApplySnapshotJson(json, "stream");
        }

        private void OnApplicationQuit()
        {
            if (!_registered || string.IsNullOrEmpty(ServerBaseUrl)) return;
            // 同步快速 DELETE：用 .NET HttpWebRequest 因为 UnityWebRequest 在退出阶段不可靠
            try
            {
                var url = $"{ServerBaseUrl.TrimEnd('/')}/collections/{CollectionName}/items/{UnityWebRequest.EscapeURL(RoomId)}";
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "DELETE";
                req.Timeout = (int)(HttpTimeoutSeconds * 1000);
                var bearer = DataExchangeSession.BuildAuthorizationHeader();
                if (bearer != null) req.Headers["Authorization"] = bearer;
                using (var resp = req.GetResponse()) { /* fire & forget */ }
                Debug.Log($"[RoomDiscovery] OnQuit: 已下线 id={RoomId}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RoomDiscovery] OnQuit DELETE 失败: {ex.Message}");
            }
        }

        // ── 心跳：上行 upsert ─────────────────────────────────

        private IEnumerator HeartbeatLoop()
        {
            while (true)
            {
                yield return PostHeartbeat();
                yield return new WaitForSeconds(HeartbeatIntervalSeconds);
            }
        }

        private IEnumerator PostHeartbeat()
        {
            if (string.IsNullOrEmpty(ServerBaseUrl)) yield break;

            var body = new Dictionary<string, object>
            {
                {"id", RoomId},
                {"ttl", (double)ItemTtlSeconds},
                {"data", new Dictionary<string, object>
                    {
                        {"name", RoomDisplayName},
                        {"bili_uid", AuthSession.Mid},
                        {"host", AdvertisedHost},
                        {"port", (long)AdvertisedPort},
                        {"player_count", (long)1}, // 至少自己；后续可由 NetworkService 提供真实值
                        {"version", Application.version},
                    }
                },
            };
            var json = MiniJson.Serialize(body);
            var url = $"{ServerBaseUrl.TrimEnd('/')}/collections/{CollectionName}/items";

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            // 受保护 collection 必须带 token；非受保护 collection 也带，服务端会顺便把 owner 戳上。
            var bearer = DataExchangeSession.BuildAuthorizationHeader();
            if (bearer != null) req.SetRequestHeader("Authorization", bearer);
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(HttpTimeoutSeconds));

            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[RoomDiscovery] 心跳失败: {req.error} url={url}");
                yield break;
            }
            _registered = true;
            if (VerboseLog) Debug.Log($"[RoomDiscovery] 心跳 ok ← {req.downloadHandler.text}");
        }

        // ── 拉房间列表 ────────────────────────────────────────

        private IEnumerator ListLoop()
        {
            while (true)
            {
                yield return PullList();
                // 用累计计时而不是 WaitForSeconds，便于 RefreshNow 中途打断
                var t = 0f;
                while (t < ListIntervalSeconds && !_forceListPull)
                {
                    yield return null;
                    t += Time.unscaledDeltaTime;
                }
                _forceListPull = false;
            }
        }

        /// <summary>外部触发立即拉一次列表（无视 ListIntervalSeconds），用于托盘菜单弹出 / 用户手动刷新。</summary>
        public void RefreshNow() => _forceListPull = true;

        private IEnumerator PullList()
        {
            if (string.IsNullOrEmpty(ServerBaseUrl)) yield break;
            // SSE 已连通时 HTTP 列表只在 RefreshNow 显式触发时拉，平时省流量。
            if (UseServerPush && _streamConnected && !_forceListPull) yield break;

            var url = $"{ServerBaseUrl.TrimEnd('/')}/collections/{CollectionName}";

            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(HttpTimeoutSeconds));
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (VerboseLog) Debug.LogWarning($"[RoomDiscovery] 拉列表失败: {req.error}");
                yield break;
            }

            // 复用 SSE 路径的应用逻辑：本接口返回 {"ok":..., "data":[...]}，
            // SSE 推送的 payload 是 {"type":..., "items":[...]}，统一在 ApplySnapshotJson 里识别。
            ApplySnapshotJson(req.downloadHandler.text, "poll");
        }

        /// <summary>将一份 JSON 快照应用到 _latestRooms 并触发回调。兼容 HTTP /collections 的 "data" 与 SSE 的 "items"。</summary>
        private void ApplySnapshotJson(string json, string source)
        {
            JsonNode parsed;
            try { parsed = MiniJson.Parse(json); }
            catch (Exception ex) { Debug.LogWarning($"[RoomDiscovery] 解析快照失败({source}): {ex.Message}"); return; }

            // SSE 推送 payload 含 "items"，HTTP /collections 列表接口含 "data"。两者结构一致。
            var items = parsed["items"];
            if (!items.Exists) items = parsed["data"];
            if (!items.Exists)
            {
                if (VerboseLog) Debug.LogWarning($"[RoomDiscovery] 快照无 items/data 字段({source}): {json}");
                return;
            }

            _latestRooms.Clear();
            foreach (var entry in items.ToList())
            {
                var id = entry["id"].ToString();
                _latestRooms.Add(new RoomInfo
                {
                    Id = id,
                    Name = entry["data"]["name"].ToString(),
                    BiliUid = entry["data"]["bili_uid"].Exists ? entry["data"]["bili_uid"].ToObject<long>() : 0L,
                    Host = entry["data"]["host"].ToString(),
                    Port = (int)entry["data"]["port"].ToObject<long>(),
                    PlayerCount = (int)entry["data"]["player_count"].ToObject<long>(),
                    TtlRemaining = entry["ttl_remaining"].ToObject<double>(),
                    IsSelf = id == RoomId,
                });
            }
            try { OnRoomsChanged?.Invoke(_latestRooms); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (VerboseLog) Debug.Log($"[RoomDiscovery] 快照应用({source}): {_latestRooms.Count} 房间");
        }

        // ── SSE 推送线程 ──────────────────────────────────────

        private void StartStreamThread()
        {
            if (!UseServerPush) return;
            if (string.IsNullOrEmpty(ServerBaseUrl)) return;
            if (_streamThread != null && _streamThread.IsAlive) return;
            _streamThreadRunning = true;
            _streamThread = new Thread(StreamLoop) { IsBackground = true, Name = "RoomDiscoverySSE" };
            _streamThread.Start();
        }

        private void StopStreamThread()
        {
            _streamThreadRunning = false;
            _streamConnected = false;
            try { _streamRequest?.Abort(); } catch { }
            _streamRequest = null;
            // 不 Join：线程是 background，避免主线程卡住
            _streamThread = null;
        }

        private void StreamLoop()
        {
            var baseUrl = ServerBaseUrl?.TrimEnd('/');
            var url = $"{baseUrl}/collections/{CollectionName}/stream";
            var reconnectMs = Mathf.Max(500, Mathf.CeilToInt(StreamReconnectDelaySeconds * 1000f));

            while (_streamThreadRunning)
            {
                try
                {
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.Method = "GET";
                    req.Accept = "text/event-stream";
                    req.KeepAlive = true;
                    req.Timeout = Mathf.Max(2000, Mathf.CeilToInt(HttpTimeoutSeconds * 1000f));
                    req.ReadWriteTimeout = Timeout.Infinite; // 长连接读不超时
                    req.AllowReadStreamBuffering = false;
                    _streamRequest = req;

                    using var resp = (HttpWebResponse)req.GetResponse();
                    using var stream = resp.GetResponseStream();
                    if (stream == null) throw new IOException("response stream null");
                    using var reader = new StreamReader(stream, Encoding.UTF8);

                    _streamConnected = true;
                    if (VerboseLog) Debug.Log($"[RoomDiscovery][SSE] 已连接 {url}");

                    var dataBuf = new StringBuilder();
                    string line;
                    while (_streamThreadRunning && (line = reader.ReadLine()) != null)
                    {
                        if (line.Length == 0)
                        {
                            // 一条事件结束
                            if (dataBuf.Length > 0)
                            {
                                var payload = dataBuf.ToString();
                                dataBuf.Length = 0;
                                lock (_pendingSnapshotLock) _pendingSnapshotJson = payload;
                            }
                            continue;
                        }
                        if (line[0] == ':') continue; // SSE 注释 / 保活
                        if (line.StartsWith("data:"))
                        {
                            // 多行 data: 字段累积，规范是按行拼接，这里 server 只发单行所以直接覆盖也行
                            var part = line.Length > 5 && line[5] == ' ' ? line.Substring(6) : line.Substring(5);
                            if (dataBuf.Length > 0) dataBuf.Append('\n');
                            dataBuf.Append(part);
                        }
                        // event:/id:/retry: 字段忽略
                    }
                }
                catch (ThreadAbortException) { break; }
                catch (Exception ex)
                {
                    if (_streamThreadRunning && VerboseLog) Debug.LogWarning($"[RoomDiscovery][SSE] 断开: {ex.Message}");
                }
                finally
                {
                    _streamConnected = false;
                    _streamRequest = null;
                }

                if (!_streamThreadRunning) break;
                // 重连前等待，期间检查停止信号
                var waited = 0;
                while (_streamThreadRunning && waited < reconnectMs)
                {
                    Thread.Sleep(100);
                    waited += 100;
                }
            }
        }

        // ── Helpers ────────────────────────────────────────────

        private static string GenerateDefaultRoomId()
        {
            // 设备名 + 8 位随机：保证同设备开两份也能区分
            var dev = SystemInfo.deviceName ?? "unknown";
            var safe = new StringBuilder();
            foreach (var ch in dev)
                safe.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            return $"{safe}_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
        }

        /// <summary>挑一个第一个非环回的 IPv4 地址作为本机 LAN IP。</summary>
        public static string DetectLocalIPv4()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var props = nic.GetIPProperties();
                    foreach (var addr in props.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                        if (IPAddress.IsLoopback(addr.Address)) continue;
                        return addr.Address.ToString();
                    }
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[RoomDiscovery] DetectLocalIPv4 失败: {ex.Message}"); }
            return null;
        }
    }
}
