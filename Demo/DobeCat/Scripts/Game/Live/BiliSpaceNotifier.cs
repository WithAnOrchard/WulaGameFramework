using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Manager.NetworkManager;
using Demo.DobeCat.Game.Pet;
using NetMgr = EssSystem.Manager.NetworkManager.NetworkManager;

namespace Demo.DobeCat.Game.Live
{
    /// <summary>
    /// Polls Bilibili space dynamics and videos for watched UIDs.
    /// Shows a clickable speech bubble when new content is detected.
    /// Broadcasts new content to all Mirror-connected peers via NetworkManager.EVT_BROADCAST.
    /// DESIGN.md §10.3
    /// </summary>
    public class BiliSpaceNotifier : MonoBehaviour
    {
        [Tooltip("Comma-separated B-station UIDs to watch, e.g. \"12345,67890\".")]
        [SerializeField] private string _watchUids = "";

        [SerializeField] private float _dynamicPollSeconds = 60f;
        [SerializeField] private float _videoPollSeconds   = 120f;
        [SerializeField] private bool  _enableDynamic = true;
        [SerializeField] private bool  _enableVideo   = true;

        private readonly Dictionary<string, string> _lastDynamicId = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _lastBvid      = new Dictionary<string, string>();
        private readonly HashSet<string> _seenDynamic = new HashSet<string>();
        private readonly HashSet<string> _seenVideo   = new HashSet<string>();

        private float _dynamicTimer;
        private float _videoTimer;

        public static BiliSpaceNotifier Instance { get; private set; }

        private const string PREFS_DYN  = "BiliNotifier_dyn_";
        private const string PREFS_VID  = "BiliNotifier_vid_";
        private const string PREFS_UIDS = "BiliNotifier_uids";
        private const string NET_TOPIC  = "SpaceNotify";

        private EssSystem.Core.Base.Event.EventDelegate _netMsgDelegate;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            var saved = PlayerPrefs.GetString(PREFS_UIDS, "");
            if (!string.IsNullOrWhiteSpace(saved))
            {
                _watchUids = saved;
            }
            else if (!string.IsNullOrEmpty(_watchUids))
            {
                // Inspector 字段有默认值则持久化
                PlayerPrefs.SetString(PREFS_UIDS, _watchUids);
            }
        }

        private void OnEnable()
        {
            _netMsgDelegate ??= OnNetMessage;
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.AddListener(NetworkService.EVT_NET_MESSAGE, _netMsgDelegate);
        }

        private void OnDisable()
        {
            if (_netMsgDelegate != null && EventProcessor.HasInstance)
                EventProcessor.Instance.RemoveListener(NetworkService.EVT_NET_MESSAGE, _netMsgDelegate);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            // 若 UIDs 仍为空，尝试用直播间主播的 UID 作为默认值
            if (string.IsNullOrWhiteSpace(_watchUids))
                TrySeedUidFromLiveRoom();

            foreach (var uid in GetUids())
            {
                _lastDynamicId[uid] = PlayerPrefs.GetString(PREFS_DYN + uid, "");
                _lastBvid[uid]      = PlayerPrefs.GetString(PREFS_VID + uid, "");
            }
            // Stagger first polls so they don't fire instantly on startup
            _dynamicTimer = 15f;
            _videoTimer   = 35f;

            // 若直播信息未就绪，订阅第一次 poll 事件时再补种
            if (string.IsNullOrWhiteSpace(_watchUids) && EventProcessor.HasInstance)
                EventProcessor.Instance.AddListener(BiliBiliLive.LiveStatusService.EVT_STATUS_POLLED, OnLivePolledForSeed);
        }

        /// <summary>尝试从 LiveStatusService 已缓存的直播间信息里取主播 UID 作为默认订阅。</summary>
        private void TrySeedUidFromLiveRoom()
        {
            var svc = BiliBiliLive.LiveStatusService.HasInstance ? BiliBiliLive.LiveStatusService.Instance : null;
            var uidRaw = svc?.Info?.Uid;
            var uid = uidRaw?.ToString();
            if (!string.IsNullOrEmpty(uid) && uid != "0")
            {
                SetWatchUids(uid);
                Debug.Log($"[BiliSpaceNotifier] 自动使用直播主播 UID: {uid}");
            }
        }

        private List<object> OnLivePolledForSeed(string evt, List<object> data)
        {
            TrySeedUidFromLiveRoom();
            if (!string.IsNullOrWhiteSpace(_watchUids) && EventProcessor.HasInstance)
                EventProcessor.Instance.RemoveListener(BiliBiliLive.LiveStatusService.EVT_STATUS_POLLED, OnLivePolledForSeed);
            return null;
        }

        /// <summary>Update watched UIDs at runtime and persist to PlayerPrefs.</summary>
        public void SetWatchUids(string uids)
        {
            _watchUids = uids ?? "";
            PlayerPrefs.SetString(PREFS_UIDS, _watchUids);
            // Seed new UIDs so first poll doesn't false-fire
            foreach (var uid in GetUids())
            {
                if (!_lastDynamicId.ContainsKey(uid))
                    _lastDynamicId[uid] = PlayerPrefs.GetString(PREFS_DYN + uid, "");
                if (!_lastBvid.ContainsKey(uid))
                    _lastBvid[uid] = PlayerPrefs.GetString(PREFS_VID + uid, "");
            }
        }

        private void Update()
        {
            if (_enableDynamic)
            {
                _dynamicTimer -= Time.unscaledDeltaTime;
                if (_dynamicTimer <= 0f)
                {
                    _dynamicTimer = _dynamicPollSeconds;
                    foreach (var uid in GetUids())
                        StartCoroutine(PollDynamic(uid));
                }
            }
            if (_enableVideo)
            {
                _videoTimer -= Time.unscaledDeltaTime;
                if (_videoTimer <= 0f)
                {
                    _videoTimer = _videoPollSeconds;
                    foreach (var uid in GetUids())
                        StartCoroutine(PollVideo(uid));
                }
            }
        }

        // ─── Polls ────────────────────────────────────────────────────────────

        private IEnumerator PollDynamic(string uid)
        {
            var url = $"https://api.bilibili.com/x/polymer/web-dynamic/v1/feed/space?host_mid={uid}&type=all";
            using var req = UnityWebRequest.Get(url);
            SetHeaders(req);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            JsonNode root;
            try   { root = MiniJson.Parse(req.downloadHandler.text); }
            catch { yield break; }

            if (root.Value<int>("code") != 0) yield break;
            var items = root["data"]["items"].ToList();
            if (items.Count == 0) yield break;

            var latestId = items[0]["id_str"].ToString();
            if (string.IsNullOrEmpty(latestId)) yield break;

            if (!_lastDynamicId.TryGetValue(uid, out var lastId) || string.IsNullOrEmpty(lastId))
            {
                CacheDynamic(uid, latestId);
                yield break; // first run — baseline only
            }
            if (latestId == lastId || _seenDynamic.Contains(latestId)) yield break;

            _seenDynamic.Add(latestId);
            CacheDynamic(uid, latestId);

            var desc = items[0]["modules"]["module_dynamic"]["desc"]["text"].ToString();
            if (desc.Length > 20) desc = desc.Substring(0, 20) + "…";

            PetSpeechBubble.Instance?.ShowWithLink(
                $"主播发新动态啦！{desc}",
                $"https://t.bilibili.com/{latestId}",
                duration: 8f);

            BroadcastNotify("dynamic", desc.Length > 0 ? desc : latestId, $"https://t.bilibili.com/{latestId}");
        }

        private IEnumerator PollVideo(string uid)
        {
            var url = $"https://api.bilibili.com/x/space/arc/search?mid={uid}&ps=1&pn=1";
            using var req = UnityWebRequest.Get(url);
            SetHeaders(req);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) yield break;

            JsonNode root;
            try   { root = MiniJson.Parse(req.downloadHandler.text); }
            catch { yield break; }

            if (root.Value<int>("code") != 0) yield break;
            var vlist = root["data"]["list"]["vlist"].ToList();
            if (vlist.Count == 0) yield break;

            var bvid  = vlist[0]["bvid"].ToString();
            var title = vlist[0]["title"].ToString();
            if (string.IsNullOrEmpty(bvid)) yield break;

            if (!_lastBvid.TryGetValue(uid, out var lastBvid) || string.IsNullOrEmpty(lastBvid))
            {
                CacheVideo(uid, bvid);
                yield break;
            }
            if (bvid == lastBvid || _seenVideo.Contains(bvid)) yield break;

            _seenVideo.Add(bvid);
            CacheVideo(uid, bvid);

            if (title.Length > 24) title = title.Substring(0, 24) + "…";

            PetSpeechBubble.Instance?.ShowWithLink(
                $"主播出新视频了！🎬 {title}",
                $"https://www.bilibili.com/video/{bvid}",
                duration: 10f);

            BroadcastNotify("video", title, $"https://www.bilibili.com/video/{bvid}");
        }

        // ─── Network broadcast ────────────────────────────────────────────────

        private void BroadcastNotify(string kind, string title, string url)
        {
            if (!EventProcessor.HasInstance) return;
            var payload = new Dictionary<string, object>
            {
                { "kind",  kind  },
                { "title", title },
                { "url",   url   },
            };
            EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_BROADCAST,
                new List<object> { NET_TOPIC, payload });
        }

        private List<object> OnNetMessage(string eventName, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            if (data[1] as string != NET_TOPIC) return null;

            var payloadStr = data[2] as string;
            var decoded = NetworkService.DecodePayload(payloadStr);
            if (decoded is not Dictionary<string, object> dict) return null;

            var kind  = dict.TryGetValue("kind",  out var k) ? k?.ToString() : "";
            var title = dict.TryGetValue("title", out var t) ? t?.ToString() : "";
            var url   = dict.TryGetValue("url",   out var u) ? u?.ToString() : "";

            var msg = kind == "video"
                ? $"主播出新视频了！🎬 {title}"
                : $"主播发新动态啦！{title}";
            var dur = kind == "video" ? 10f : 8f;

            if (!string.IsNullOrEmpty(url))
                PetSpeechBubble.Instance?.ShowWithLink(msg, url, dur);
            else
                PetSpeechBubble.Instance?.Show(msg, dur);

            return null;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void CacheDynamic(string uid, string id)
        {
            _lastDynamicId[uid] = id;
            PlayerPrefs.SetString(PREFS_DYN + uid, id);
        }

        private void CacheVideo(string uid, string bvid)
        {
            _lastBvid[uid] = bvid;
            PlayerPrefs.SetString(PREFS_VID + uid, bvid);
        }

        private static void SetHeaders(UnityWebRequest req)
        {
            req.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            req.SetRequestHeader("Referer", "https://www.bilibili.com/");
        }

        private IEnumerable<string> GetUids()
        {
            if (string.IsNullOrWhiteSpace(_watchUids)) yield break;
            foreach (var part in _watchUids.Split(','))
            {
                var uid = part.Trim();
                if (!string.IsNullOrEmpty(uid)) yield return uid;
            }
        }
    }
}
