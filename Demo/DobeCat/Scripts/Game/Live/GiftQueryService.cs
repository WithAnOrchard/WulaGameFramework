using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using EssSystem.Core.Base.Util;
using UnityEngine;
using Demo.DobeCat.Sys.Auth;

namespace Demo.DobeCat.Game.Live
{
    /// <summary>礼物记录数据模型。</summary>
    public struct GiftRecord
    {
        public long   Uid;
        public string Uname;
        public string GiftName;
        public int    Count;
        public int    TotalCoin;
        public string CoinType;   // "gold" | "silver"
    }

    /// <summary>
    /// 通过 Bilibili API 查询当前账号收到的礼物记录，支持按礼物 / 按人两种聚合视图。
    /// 将此组件挂载到 DobeCatGameManager 所在 GameObject 上。
    /// </summary>
    public class GiftQueryService : MonoBehaviour
    {
        /// <summary>
        /// Bilibili 礼物收益记录 API。
        /// 参考：https://github.com/SocialSisterYi/bilibili-API-collect
        /// 若接口有变化请在此处修改 URL 和下方 ParseGiftResponse 的字段名。
        /// </summary>
        private const string ApiUrl =
            "https://api.live.bilibili.com/xlive/revenue/v2/gift/record_by_giver";

        public static GiftQueryService Instance { get; private set; }

        private static readonly HttpClient Http = new HttpClient();

        private void Awake()  { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        // ─── 公共 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 查询礼物记录并回调（主线程）。
        /// </summary>
        /// <param name="pageSize">每页条数（建议 50–200）。</param>
        /// <param name="onSuccess">成功时传入记录列表。</param>
        /// <param name="onError">失败时传入错误描述。</param>
        public void FetchGifts(int pageSize,
                               Action<List<GiftRecord>> onSuccess,
                               Action<string> onError = null)
        {
            StartCoroutine(FetchCoroutine(pageSize, onSuccess, onError));
        }

        // ─── 内部 ─────────────────────────────────────────────────────────────

        private IEnumerator FetchCoroutine(int pageSize,
                                           Action<List<GiftRecord>> onSuccess,
                                           Action<string> onError)
        {
            if (!AuthSession.IsAuthenticated)
            {
                onError?.Invoke("未登录，请先完成哔哩哔哩认证");
                yield break;
            }

            var url  = $"{ApiUrl}?page=1&page_size={pageSize}&coin_type=all";
            var task = FetchAsync(url, AuthSession.Token);
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted)
            {
                onError?.Invoke($"请求失败: {task.Exception?.GetBaseException().Message}");
                yield break;
            }
            onSuccess?.Invoke(task.Result);
        }

        private static async Task<List<GiftRecord>> FetchAsync(string url, string sessdata)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("Cookie",     $"SESSDATA={Uri.EscapeDataString(sessdata)}");
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            req.Headers.TryAddWithoutValidation("Referer",    "https://live.bilibili.com");

            using var resp = await Http.SendAsync(req).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
            return ParseGiftResponse(body);
        }

        private static List<GiftRecord> ParseGiftResponse(string json)
        {
            var list = new List<GiftRecord>();
            try
            {
                var root = MiniJson.Parse(json);
                if (root == null || root.Value<int>("code") != 0) return list;

                var data  = root["data"];
                if (data == null) return list;

                // 尝试常见字段名
                var items = data["list"] ?? data["data"] ?? data["gift_list"];
                if (items == null) return list;

                foreach (var item in items.ToList())
                {
                    list.Add(new GiftRecord
                    {
                        Uid       = item.Value<long>("uid"),
                        Uname     = (item["uname"] ?? item["username"])?.ToString() ?? "未知用户",
                        GiftName  = (item["gift_name"] ?? item["giftName"])?.ToString() ?? "礼物",
                        Count     = item.Value<int>("gift_num"),
                        TotalCoin = item.Value<int>("total_coin"),
                        CoinType  = item["coin_type"]?.ToString() ?? "silver",
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[GiftQueryService] JSON 解析失败: {ex.Message}\n{json.Substring(0, Mathf.Min(200, json.Length))}");
            }
            return list;
        }

        // ─── 聚合 / 格式化 ────────────────────────────────────────────────────

        /// <summary>按礼物聚合：giftName → List(uname, count)，各组按数量降序。</summary>
        public static Dictionary<string, List<(string uname, int count)>>
            GroupByGift(List<GiftRecord> records)
        {
            var dict = new Dictionary<string, List<(string, int)>>();
            foreach (var r in records)
            {
                if (!dict.TryGetValue(r.GiftName, out var givers))
                    dict[r.GiftName] = givers = new List<(string, int)>();
                var idx = givers.FindIndex(g => g.Item1 == r.Uname);
                if (idx >= 0) givers[idx] = (givers[idx].Item1, givers[idx].Item2 + r.Count);
                else          givers.Add((r.Uname, r.Count));
            }
            foreach (var kv in dict) kv.Value.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return dict;
        }

        /// <summary>按人聚合：uname → List(giftName, count)，各组按数量降序。</summary>
        public static Dictionary<string, List<(string gift, int count)>>
            GroupByPerson(List<GiftRecord> records)
        {
            var dict = new Dictionary<string, List<(string, int)>>();
            foreach (var r in records)
            {
                if (!dict.TryGetValue(r.Uname, out var gifts))
                    dict[r.Uname] = gifts = new List<(string, int)>();
                var idx = gifts.FindIndex(g => g.Item1 == r.GiftName);
                if (idx >= 0) gifts[idx] = (gifts[idx].Item1, gifts[idx].Item2 + r.Count);
                else          gifts.Add((r.GiftName, r.Count));
            }
            foreach (var kv in dict) kv.Value.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            return dict;
        }

        /// <summary>按礼物视图的纯文本，适合放入 ScrollView。</summary>
        public static string FormatByGift(List<GiftRecord> records)
        {
            if (records == null || records.Count == 0) return "（暂无数据，请先点击查询）";
            var grouped = GroupByGift(records);
            var sorted  = new List<(string gift, int total, List<(string, int)> givers)>();
            foreach (var kv in grouped)
            {
                int tot = 0; foreach (var g in kv.Value) tot += g.Item2;
                sorted.Add((kv.Key, tot, kv.Value));
            }
            sorted.Sort((a, b) => b.total.CompareTo(a.total));

            var sb = new StringBuilder();
            foreach (var (gift, total, givers) in sorted)
            {
                sb.AppendLine($"■ {gift}  共 {total} 个");
                foreach (var g in givers)
                    sb.AppendLine($"   └ {g.Item1}  ×{g.Item2}");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>按人视图的纯文本，适合放入 ScrollView。</summary>
        public static string FormatByPerson(List<GiftRecord> records)
        {
            if (records == null || records.Count == 0) return "（暂无数据，请先点击查询）";
            var grouped = GroupByPerson(records);
            var sorted  = new List<(string uname, int total, List<(string, int)> gifts)>();
            foreach (var kv in grouped)
            {
                int tot = 0; foreach (var g in kv.Value) tot += g.Item2;
                sorted.Add((kv.Key, tot, kv.Value));
            }
            sorted.Sort((a, b) => b.total.CompareTo(a.total));

            var sb = new StringBuilder();
            foreach (var (uname, total, gifts) in sorted)
            {
                sb.AppendLine($"■ {uname}  共 {total} 件");
                foreach (var g in gifts)
                    sb.AppendLine($"   └ {g.Item1}  ×{g.Item2}");
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }
}
