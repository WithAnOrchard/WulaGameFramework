using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using EssSystem.Core.Base.Util;
using Demo.DobeCat.Game.Pet;

namespace Demo.DobeCat.Game
{
    /// <summary>
    /// 拉取每日天气并展示到对话气泡。
    /// 天气服务：心知天气 Seniverse API（国内，免费 key 在 console.seniverse.com 申请）。
    /// 城市自动识别：若未配置城市，通过 ip-api.com 根据 IP 获取当前城市（无需 key）。
    /// DESIGN.md §7 天气播报.
    ///
    /// Setup:
    ///   1. 在 https://console.seniverse.com/ 注册并获取免费 API Key。
    ///   2. 在 Inspector 或 PlayerPrefs "WeatherApiKey" 填入 key；城市留空则自动按 IP 检测。
    ///   3. WeatherNotifier 启动时拉取，此后每 _pollHours 小时刷新一次。
    /// </summary>
    public class WeatherNotifier : MonoBehaviour
    {
        [Tooltip("心知天气 API key（console.seniverse.com 申请）。留空则禁用天气功能。")]
        [SerializeField] private string _apiKey = "";

        [Tooltip("城市名或拼音（如 beijing）。留空则自动通过 IP 检测。")]
        [SerializeField] private string _city = "";

        [Tooltip("Re-poll interval in hours.")]
        [SerializeField] private float _pollHours = 6f;

        private const string PREFS_KEY  = "WeatherApiKey";
        private const string PREFS_CITY = "WeatherCity";
        // 心知天气 Seniverse API v3
        private const string API_URL =
            "https://api.seniverse.com/v3/weather/now.json?key={1}&location={0}&language=zh-Hans&unit=c";
        // ip-api.com：免费，无需 key，中国可访问
        private const string IP_GEO_URL =
            "http://ip-api.com/json/?lang=zh-CN&fields=status,city";

        private float _timer;

        public static WeatherNotifier Instance { get; private set; }

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            var key = PlayerPrefs.GetString(PREFS_KEY, _apiKey);
            if (string.IsNullOrWhiteSpace(key)) return;
            _apiKey = key;
            _city   = PlayerPrefs.GetString(PREFS_CITY, _city); // 有缓存则作备用，IP 失败时回退
            _timer  = _pollHours * 3600f;
            StartCoroutine(DetectCityThenFetch()); // 每次启动都重新 IP 定位
        }

        /// <summary>设置面板粘贴 API Key 后立即触发。</summary>
        public void Restart(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return;
            _apiKey = apiKey;
            StopAllCoroutines();
            _timer = _pollHours * 3600f;
            StartCoroutine(DetectCityThenFetch());
        }

        private void Update()
        {
            if (string.IsNullOrWhiteSpace(_apiKey)) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = _pollHours * 3600f;
                StartCoroutine(FetchWeather());
            }
        }

        private IEnumerator DetectCityThenFetch()
        {
            using var geoReq = UnityWebRequest.Get(IP_GEO_URL);
            geoReq.timeout = 5;
            yield return geoReq.SendWebRequest();

            if (geoReq.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var node = MiniJson.Parse(geoReq.downloadHandler.text);
                    if (node["status"].ToString() == "success")
                    {
                        var detectedCity = node["city"].ToString();
                        if (!string.IsNullOrEmpty(detectedCity))
                        {
                            _city = detectedCity;
                            PlayerPrefs.SetString(PREFS_CITY, _city);
                            PlayerPrefs.Save();
                            Debug.Log($"[WeatherNotifier] IP 定位城市：{_city}");
                        }
                    }
                }
                catch { /* 解析失败，_city 保持空，FetchWeather 会跳过 */ }
            }
            else
            {
                Debug.LogWarning($"[WeatherNotifier] IP 定位失败：{geoReq.error}，跳过天气播报");
            }

            if (!string.IsNullOrWhiteSpace(_city))
                yield return StartCoroutine(FetchWeather());
        }

        private IEnumerator FetchWeather()
        {
            var url = string.Format(API_URL, UnityWebRequest.EscapeURL(_city), _apiKey);
            using var req = UnityWebRequest.Get(url);
            req.SetRequestHeader("User-Agent", "Mozilla/5.0");
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[WeatherNotifier] {req.error}");
                yield break;
            }

            JsonNode root;
            try   { root = MiniJson.Parse(req.downloadHandler.text); }
            catch { yield break; }

            // 心知天气返回格式: { "results": [{"location":{"name":"..."},"now":{"text":"...","temperature":"25","feels_like":"24"}}] }
            var result   = root["results"][0];
            if (result.IsMissing) yield break;

            var desc     = result["now"]["text"].ToString();
            if (string.IsNullOrEmpty(desc)) yield break;
            var tempStr  = result["now"]["temperature"].ToString();
            var feelsStr = result["now"]["feels_like"].ToString();
            var temp     = float.TryParse(tempStr, out var tf) ? tf : 0f;
            var feels    = float.TryParse(feelsStr, out var ff) ? ff : temp;
            var cityName = result["location"]["name"].ToString();
            if (string.IsNullOrEmpty(cityName)) cityName = _city;

            var emoji = PickEmoji(desc);
            var msg   = $"{emoji} {cityName} {Mathf.RoundToInt(temp)}°C，{desc}（体感 {Mathf.RoundToInt(feels)}°C）";

            PetSpeechBubble.Instance?.Show(msg, 8f);
        }

        private static string PickEmoji(string desc)
        {
            if (desc.Contains("雪"))  return "❄️";
            if (desc.Contains("雨") || desc.Contains("阵雨")) return "🌧️";
            if (desc.Contains("云") || desc.Contains("阴")) return "☁️";
            if (desc.Contains("晴")) return "☀️";
            if (desc.Contains("雾") || desc.Contains("霾")) return "🌫️";
            if (desc.Contains("雷")) return "⛈️";
            return "🌤️";
        }
    }
}
