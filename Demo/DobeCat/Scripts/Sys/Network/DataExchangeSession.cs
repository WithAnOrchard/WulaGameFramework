using System;
using System.Collections;
using System.Text;
using Demo.DobeCat.Sys.Auth;
using EssSystem.Core.Base.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 与 <c>data_exchange_server</c> 的会话客户端。把 B 站 SESSDATA（来自 <see cref="AuthSession"/>）换成
    /// 服务端签发的短期 token，供后续上行写操作（heartbeat upsert / actions / 受保护 collection 写入）携带。
    /// <para>
    /// 设计意图：SESSDATA 是 B 站身份凭据，绝不要直接发到我们自己的服务器去做"每次写入都验"——
    /// 一来吞吐受 nav 接口限制，二来泄漏面更大。换成短 token 后，写操作只携带 token，
    /// 服务端用本地内存表 O(1) 验签 + 取 mid，配合 <c>--auth-collections</c> 实现"调用方只能改自己的 item"。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DataExchangeSession : MonoBehaviour
    {
        [Tooltip("数据收发器 Base URL，例如 http://192.168.1.10:8765。留空则禁用。建议与 RoomDiscoveryClient 保持一致。")]
        public string ServerBaseUrl = "";

        [Tooltip("启用后在 OnEnable 自动用当前 AuthSession.Token (=SESSDATA) 换取服务端 token；登录态变化时自动重换。")]
        public bool AutoLogin = true;

        [Tooltip("Trust 模式：服务端以 --no-bilibili-check 启动时使用。会跳过 SESSDATA 校验，把本地 Mid/Nickname 直接报上去。仅用于本地联调。")]
        public bool TrustMode = false;

        [Tooltip("HTTP 请求超时（秒）。")]
        [Min(1f)] public float HttpTimeoutSeconds = 8f;

        [Tooltip("详细日志。")]
        public bool VerboseLog = false;

        // ── 全局可访问态：上行请求拼 Authorization 用 ────────────────

        /// <summary>服务端签发的 token；空表示当前未鉴权。</summary>
        public static string Token { get; private set; } = string.Empty;
        /// <summary>当前会话 mid（来自服务端响应，可作权威值）。</summary>
        public static long Mid { get; private set; }
        /// <summary>当前会话用户名（来自服务端响应）。</summary>
        public static string Uname { get; private set; } = string.Empty;
        /// <summary>token 过期时间（Unix 秒）。0 表示无 token。</summary>
        public static double ExpiresAtUnix { get; private set; }

        public static bool IsAuthenticated => !string.IsNullOrEmpty(Token);

        /// <summary>token 拿到 / 续期 / 失效时触发。</summary>
        public static event Action OnTokenChanged;

        private static DataExchangeSession _instance;
        /// <summary>提供给非 MonoBehaviour 调用方（例如手写 HttpWebRequest）做认证头注入。</summary>
        public static string BuildAuthorizationHeader()
            => string.IsNullOrEmpty(Token) ? null : $"Bearer {Token}";

        // ── 生命周期 ───────────────────────────────────────────

        private void OnEnable()
        {
            _instance = this;
            AuthSession.OnLogin += HandleAuthLogin;
            if (AutoLogin && AuthSession.IsAuthenticated) StartCoroutine(LoginRoutine());
        }

        private void OnDisable()
        {
            AuthSession.OnLogin -= HandleAuthLogin;
            if (_instance == this) _instance = null;
        }

        private void HandleAuthLogin()
        {
            if (AutoLogin && AuthSession.IsAuthenticated) StartCoroutine(LoginRoutine());
        }

        // ── 公开 API ──────────────────────────────────────────

        /// <summary>用 <see cref="AuthSession"/> 的 SESSDATA 换 token。重复调用会替换当前 token。</summary>
        public IEnumerator LoginRoutine()
        {
            if (string.IsNullOrEmpty(ServerBaseUrl))
            {
                Debug.LogWarning("[DataExchangeSession] ServerBaseUrl 为空，跳过登录。");
                yield break;
            }
            var sessdata = AuthSession.Token;
            if (string.IsNullOrEmpty(sessdata))
            {
                if (VerboseLog) Debug.Log("[DataExchangeSession] AuthSession 未登录，等待 OnLogin 再换 token。");
                yield break;
            }

            var url = $"{ServerBaseUrl.TrimEnd('/')}/sessions";
            var bodyDict = new System.Collections.Generic.Dictionary<string, object> { {"sessdata", sessdata} };
            if (TrustMode)
            {
                bodyDict["mid"] = AuthSession.Mid;
                bodyDict["uname"] = AuthSession.Nickname ?? "";
            }
            var json = MiniJson.Serialize(bodyDict);

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(HttpTimeoutSeconds));
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[DataExchangeSession] 登录失败: {req.error} resp={req.downloadHandler?.text}");
                yield break;
            }

            var parsed = MiniJson.Parse(req.downloadHandler.text);
            if (!parsed["ok"].ToObject<bool>())
            {
                Debug.LogWarning($"[DataExchangeSession] 服务端拒绝: {req.downloadHandler.text}");
                yield break;
            }

            var data = parsed["data"];
            Token = data["token"].ToString();
            Mid = data["mid"].ToObject<long>();
            Uname = data["uname"].ToString();
            ExpiresAtUnix = data["expires_at"].ToObject<double>();
            if (VerboseLog) Debug.Log($"[DataExchangeSession] token 已签发: mid={Mid} uname={Uname} expires_at={ExpiresAtUnix}");
            try { OnTokenChanged?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
        }

        /// <summary>主动注销（DELETE /sessions）。本地态无论网络是否成功都会清空。</summary>
        public IEnumerator LogoutRoutine()
        {
            if (!string.IsNullOrEmpty(ServerBaseUrl) && IsAuthenticated)
            {
                var url = $"{ServerBaseUrl.TrimEnd('/')}/sessions";
                using var req = UnityWebRequest.Delete(url);
                req.SetRequestHeader("Authorization", $"Bearer {Token}");
                req.timeout = Mathf.Max(1, Mathf.CeilToInt(HttpTimeoutSeconds));
                yield return req.SendWebRequest();
                if (VerboseLog) Debug.Log($"[DataExchangeSession] 注销结果: {req.result} {req.downloadHandler?.text}");
            }
            Token = string.Empty;
            Mid = 0;
            Uname = string.Empty;
            ExpiresAtUnix = 0;
            try { OnTokenChanged?.Invoke(); } catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
