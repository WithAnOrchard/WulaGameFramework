using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using EssSystem.Core.Base.Util;
using UnityEngine;
using UnityEngine.Networking;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 与 <c>data_exchange_server</c> 的 <c>/actions</c> 接口配合的薄封装。所有"会改权威状态"的写入都应走这里：
    /// <list type="bullet">
    /// <item>统一注入 <c>Authorization: Bearer &lt;token&gt;</c>。token 来自 <see cref="DataExchangeSession"/>。</item>
    /// <item>自动单调递增 <c>client_seq</c>，由服务端用来拒绝重放 / 乱序。</item>
    /// <item>结果通过回调返回（成功 -&gt; data，失败 -&gt; 错误字符串），调用方按需处理。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class ActionsClient : MonoBehaviour
    {
        [Tooltip("数据收发器 Base URL。建议与 RoomDiscoveryClient / DataExchangeSession 保持一致。")]
        public string ServerBaseUrl = "";

        [Tooltip("HTTP 请求超时（秒）。")]
        [Min(1f)] public float HttpTimeoutSeconds = 5f;

        [Tooltip("详细日志。")]
        public bool VerboseLog = false;

        // 单调递增 seq；启动时随机化避免冷启动碰撞旧 seq。
        private long _clientSeq;
        private const string SeqPrefKey = "DobeCat.ActionsClientSeq";

        public static ActionsClient Instance { get; private set; }

        private void OnEnable()
        {
            Instance = this;
            // 持久化 seq：避免重启后用比上次小的 seq 触发服务端 stale 拒绝。
            _clientSeq = Convert.ToInt64(PlayerPrefs.GetString(SeqPrefKey, "0"));
            if (_clientSeq <= 0) _clientSeq = (long)(Time.realtimeSinceStartupAsDouble * 1000);
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            PlayerPrefs.SetString(SeqPrefKey, _clientSeq.ToString());
            PlayerPrefs.Save();
        }

        /// <summary>分配下一个 client_seq。线程不安全，仅主线程调用。</summary>
        public long NextSeq() => ++_clientSeq;

        /// <summary>
        /// 发起一次 action。<paramref name="extraFields"/> 会与 type/client_seq 合并到顶层 body。
        /// </summary>
        public Coroutine Send(string type,
                              IDictionary<string, object> extraFields = null,
                              Action<JsonNode> onSuccess = null,
                              Action<string, long> onError = null)
        {
            return StartCoroutine(SendRoutine(type, extraFields, onSuccess, onError));
        }

        private IEnumerator SendRoutine(string type,
                                        IDictionary<string, object> extraFields,
                                        Action<JsonNode> onSuccess,
                                        Action<string, long> onError)
        {
            if (string.IsNullOrEmpty(ServerBaseUrl))
            {
                onError?.Invoke("ServerBaseUrl 为空", 0);
                yield break;
            }
            if (!DataExchangeSession.IsAuthenticated)
            {
                onError?.Invoke("未鉴权（DataExchangeSession 没有 token）", 401);
                yield break;
            }

            var body = new Dictionary<string, object>
            {
                {"type", type},
                {"client_seq", NextSeq()},
            };
            if (extraFields != null)
                foreach (var kv in extraFields)
                    if (kv.Key != "type" && kv.Key != "client_seq") body[kv.Key] = kv.Value;

            var json = MiniJson.Serialize(body);
            var url = $"{ServerBaseUrl.TrimEnd('/')}/actions";

            using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", DataExchangeSession.BuildAuthorizationHeader());
            req.timeout = Mathf.Max(1, Mathf.CeilToInt(HttpTimeoutSeconds));

            yield return req.SendWebRequest();

            var status = (long)req.responseCode;
            if (req.result != UnityWebRequest.Result.Success && status == 0)
            {
                var msg = $"网络错误: {req.error}";
                if (VerboseLog) Debug.LogWarning($"[ActionsClient] {type} → {msg}");
                onError?.Invoke(msg, status);
                yield break;
            }

            JsonNode parsed;
            try { parsed = MiniJson.Parse(req.downloadHandler.text ?? string.Empty); }
            catch (Exception ex)
            {
                onError?.Invoke($"解析响应失败: {ex.Message}", status);
                yield break;
            }

            if (parsed["ok"].ToObject<bool>())
            {
                if (VerboseLog) Debug.Log($"[ActionsClient] {type} ok ← {req.downloadHandler.text}");
                onSuccess?.Invoke(parsed["data"]);
            }
            else
            {
                var err = parsed["error"].ToString();
                if (VerboseLog) Debug.LogWarning($"[ActionsClient] {type} 服务端拒绝({status}): {err}");
                onError?.Invoke(err, status);
            }
        }
    }
}
