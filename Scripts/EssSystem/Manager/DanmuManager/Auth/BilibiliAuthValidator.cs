using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace BiliBiliDanmu.Auth
{
    /// <summary>
    /// 用 SESSDATA cookie 调 B 站 <c>/x/web-interface/nav</c> 接口验证 token 有效性。
    /// <list type="bullet">
    /// <item>HTTP 200 + <c>data.code==0</c> + <c>data.uname</c> 非空 → 视作登录成功，回调 <c>onSuccess(uname, mid)</c>。</item>
    /// <item>其他情况（网络失败 / code≠0 / 没拿到 uname）→ 回调 <c>onFail(errorMessage)</c>，调用方负责拒绝登录。</item>
    /// </list>
    /// 不依赖任何 JSON 解析库，手撕字段，纯 string 取值，避免引入 Newtonsoft 依赖。
    /// </summary>
    public static class BilibiliAuthValidator
    {
        private const string NavUrl = "https://api.bilibili.com/x/web-interface/nav";
        private const float TimeoutSec = 5f;

        /// <summary>验证 SESSDATA token，必须从 MonoBehaviour 协程启动。</summary>
        public static IEnumerator Validate(string sessdata, Action<string, long> onSuccess, Action<string> onFail)
        {
            if (string.IsNullOrWhiteSpace(sessdata))
            {
                onFail?.Invoke("Token 不能为空");
                yield break;
            }

            using (var req = UnityWebRequest.Get(NavUrl))
            {
                // SESSDATA 不做 URL 编码，B 站要求原样传；Cookie 头里同时带空 buvid3 可避免某些边路风控。
                req.SetRequestHeader("Cookie", "SESSDATA=" + sessdata.Trim());
                req.SetRequestHeader("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 BiliBiliDanmu/1.0");
                req.timeout = Mathf.CeilToInt(TimeoutSec); // 双保险：先给底层 timeout
                var op      = req.SendWebRequest();
                var elapsed = 0f;
                while (!op.isDone)
                {
                    elapsed += Time.unscaledDeltaTime;
                    if (elapsed >= TimeoutSec)
                    {
                        req.Abort();
                        onFail?.Invoke($"验证超时（>{TimeoutSec:0}s），请检查网络后重试");
                        yield break;
                    }
                    yield return null;
                }

#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                {
                    onFail?.Invoke($"网络错误：{req.error}");
                    yield break;
                }

                var json = req.downloadHandler != null ? req.downloadHandler.text : null;
                if (string.IsNullOrEmpty(json))
                {
                    onFail?.Invoke("B 站接口返回空响应");
                    yield break;
                }

                // 期望结构：{"code":0, "data":{"isLogin":true, "uname":"xxx", "mid":1234, ...}}
                var code = ExtractIntField(json, "code");
                if (code != 0)
                {
                    var msg = ExtractStringField(json, "message");
                    onFail?.Invoke($"Token 无效：{(string.IsNullOrEmpty(msg) ? "code=" + code : msg)}");
                    yield break;
                }

                // isLogin==false 时 B 站 code 仍是 0，但 uname 为空，必须靠 uname 判断
                var uname = ExtractStringField(json, "uname");
                if (string.IsNullOrEmpty(uname))
                {
                    onFail?.Invoke("Token 已失效或未登录（拿不到用户昵称）");
                    yield break;
                }

                var mid = ExtractLongField(json, "mid");
                onSuccess?.Invoke(uname, mid);
            }
        }

        // ── 极简 JSON 字段提取（不解析嵌套结构，按顺序匹配第一个出现的 key）──

        private static string ExtractStringField(string json, string key)
        {
            var pat = "\"" + key + "\":";
            var idx = json.IndexOf(pat, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += pat.Length;
            // 跳过空白
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            if (idx >= json.Length || json[idx] != '"') return null;
            idx++;
            var end = idx;
            while (end < json.Length && json[end] != '"')
            {
                if (json[end] == '\\' && end + 1 < json.Length) end += 2; // 跳过转义
                else end++;
            }
            return end <= json.Length ? json.Substring(idx, end - idx) : null;
        }

        private static int ExtractIntField(string json, string key)
        {
            var raw = ExtractRawNumber(json, key);
            return int.TryParse(raw, out var v) ? v : int.MinValue;
        }

        private static long ExtractLongField(string json, string key)
        {
            var raw = ExtractRawNumber(json, key);
            return long.TryParse(raw, out var v) ? v : 0L;
        }

        private static string ExtractRawNumber(string json, string key)
        {
            var pat = "\"" + key + "\":";
            var idx = json.IndexOf(pat, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += pat.Length;
            while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
            var end = idx;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-'))
                end++;
            return end > idx ? json.Substring(idx, end - idx) : null;
        }
    }
}
