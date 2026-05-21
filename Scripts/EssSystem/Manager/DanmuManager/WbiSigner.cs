using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using EssSystem.Core.Base.Util;

namespace BiliBiliDanmu
{
    /// <summary>
    /// B 站 WBI 签名工具（Token 模式用）。
    /// <list type="bullet">
    /// <item>从 <c>/x/web-interface/nav</c> 拿 img_url + sub_url，提取 img_key / sub_key</item>
    /// <item>按 <c>mixinKeyEncTab</c> 打乱并截前 32 字节得到 mixin_key</item>
    /// <item><c>w_rid = md5(sortedQuery + mixin_key)</c></item>
    /// </list>
    /// </summary>
    internal static class WbiSigner
    {
        private static readonly int[] MixinKeyEncTab =
        {
            46, 47, 18,  2, 53,  8, 23, 32, 15, 50, 10, 31, 58,  3, 45, 35,
            27, 43,  5, 49, 33,  9, 42, 19, 29, 28, 14, 39, 12, 38, 41, 13,
            37, 48,  7, 16, 24, 55, 40, 61, 26, 17,  0,  1, 60, 51, 30,  4,
            22, 25, 54, 21, 56, 59,  6, 63, 57, 62, 11, 36, 20, 34, 44, 52,
        };

        private static string _imgKey;
        private static string _subKey;
        private static DateTime _keysFetchedAt;
        private const int KeysTtlSeconds = 600;
        private const string NavApi = "https://api.bilibili.com/x/web-interface/nav";

        public static async Task<string> SignQueryAsync(HttpClient http, IDictionary<string, string> queryParams)
        {
            await EnsureKeysAsync(http);
            var mixinKey = GetMixinKey(_imgKey + _subKey);

            var wts = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var allParams = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in queryParams) allParams[kv.Key] = kv.Value;
            allParams["wts"] = wts;

            var sb = new StringBuilder();
            foreach (var kv in allParams)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv.Value));
            }
            var query = sb.ToString();
            var wRid = Md5Hex(query + mixinKey);
            return $"{query}&w_rid={wRid}";
        }

        private static async Task EnsureKeysAsync(HttpClient http)
        {
            if (!string.IsNullOrEmpty(_imgKey) && !string.IsNullOrEmpty(_subKey)
                && (DateTime.UtcNow - _keysFetchedAt).TotalSeconds < KeysTtlSeconds)
                return;

            using var resp = await http.GetAsync(NavApi);
            var raw = await resp.Content.ReadAsStringAsync();
            var body = MiniJson.Parse(raw);
            var img = body["data"]["wbi_img"];
            var imgUrl = img["img_url"].ToString();
            var subUrl = img["sub_url"].ToString();
            if (string.IsNullOrEmpty(imgUrl) || string.IsNullOrEmpty(subUrl))
                throw new Exception($"WBI nav 缺少 img/sub url: {raw}");

            _imgKey = ExtractKey(imgUrl);
            _subKey = ExtractKey(subUrl);
            _keysFetchedAt = DateTime.UtcNow;
        }

        private static string ExtractKey(string url)
        {
            var slash = url.LastIndexOf('/');
            var dot = url.LastIndexOf('.');
            if (slash < 0 || dot <= slash) return url;
            return url.Substring(slash + 1, dot - slash - 1);
        }

        private static string GetMixinKey(string orig)
        {
            var sb = new StringBuilder(32);
            for (var i = 0; i < 32 && i < MixinKeyEncTab.Length; i++)
            {
                var idx = MixinKeyEncTab[i];
                if (idx < orig.Length) sb.Append(orig[idx]);
            }
            return sb.ToString();
        }

        private static string Md5Hex(string s)
        {
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(s));
            var sb = new StringBuilder(32);
            foreach (var b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
