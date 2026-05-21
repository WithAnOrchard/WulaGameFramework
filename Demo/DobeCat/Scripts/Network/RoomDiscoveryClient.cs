using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using EssSystem.Core.Base.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace Demo.DobeCat.Network
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

        [Tooltip("数据收发器的 Base URL，例如 http://192.168.1.10:8765")]
        public string ServerBaseUrl = "http://localhost:8765";

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

        [Tooltip("心跳间隔（秒）。建议 < TTL/2。")]
        [Min(1f)] public float HeartbeatIntervalSeconds = 10f;

        [Tooltip("条目 TTL（秒）。")]
        [Min(5f)] public float ItemTtlSeconds = 30f;

        [Tooltip("拉取列表间隔（秒）。")]
        [Min(1f)] public float ListIntervalSeconds = 5f;

        [Tooltip("HTTP 请求超时（秒）。")]
        [Min(1f)] public float HttpTimeoutSeconds = 5f;

        [Tooltip("是否详细打印 HTTP 日志。")]
        public bool VerboseLog = false;

        // ── 运行时状态 ─────────────────────────────────────────

        public sealed class RoomInfo
        {
            public string Id;
            public string Name;
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

        // ── 生命周期 ───────────────────────────────────────────

        private void OnEnable()
        {
            if (string.IsNullOrEmpty(RoomId)) RoomId = GenerateDefaultRoomId();
            if (string.IsNullOrEmpty(RoomDisplayName)) RoomDisplayName = SystemInfo.deviceName;
            if (string.IsNullOrEmpty(AdvertisedHost)) AdvertisedHost = DetectLocalIPv4() ?? "127.0.0.1";

            _heartbeatCo = StartCoroutine(HeartbeatLoop());
            _listCo = StartCoroutine(ListLoop());
            Debug.Log($"[RoomDiscovery] 启动: id={RoomId} host={AdvertisedHost}:{AdvertisedPort} server={ServerBaseUrl}");
        }

        private void OnDisable()
        {
            if (_heartbeatCo != null) { StopCoroutine(_heartbeatCo); _heartbeatCo = null; }
            if (_listCo != null) { StopCoroutine(_listCo); _listCo = null; }
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
                yield return new WaitForSeconds(ListIntervalSeconds);
            }
        }

        private IEnumerator PullList()
        {
            if (string.IsNullOrEmpty(ServerBaseUrl)) yield break;
            var url = $"{ServerBaseUrl.TrimEnd('/')}/collections/{CollectionName}";

            using var req = UnityWebRequest.Get(url);
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(HttpTimeoutSeconds));
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                if (VerboseLog) Debug.LogWarning($"[RoomDiscovery] 拉列表失败: {req.error}");
                yield break;
            }

            var parsed = MiniJson.Parse(req.downloadHandler.text);
            if (!parsed["ok"].ToObject<bool>())
            {
                Debug.LogWarning($"[RoomDiscovery] 服务返回 ok=false: {req.downloadHandler.text}");
                yield break;
            }

            _latestRooms.Clear();
            foreach (var entry in parsed["data"].ToList())
            {
                var id = entry["id"].ToString();
                _latestRooms.Add(new RoomInfo
                {
                    Id = id,
                    Name = entry["data"]["name"].ToString(),
                    Host = entry["data"]["host"].ToString(),
                    Port = (int)entry["data"]["port"].ToObject<long>(),
                    PlayerCount = (int)entry["data"]["player_count"].ToObject<long>(),
                    TtlRemaining = entry["ttl_remaining"].ToObject<double>(),
                    IsSelf = id == RoomId,
                });
            }
            try { OnRoomsChanged?.Invoke(_latestRooms); }
            catch (Exception ex) { Debug.LogException(ex); }

            if (VerboseLog) Debug.Log($"[RoomDiscovery] 拉到 {_latestRooms.Count} 个房间");
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
