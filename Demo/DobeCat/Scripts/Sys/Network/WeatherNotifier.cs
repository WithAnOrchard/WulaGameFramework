using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using EssSystem.Core.Base.Util;
using Demo.DobeCat.Game.Pet;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 拉取实时天气并展示到对话气泡。完全免费，无需任何 API Key。
    /// <list type="bullet">
    /// <item>ip-api.com → 根据公网 IP 自动获取城市 + 经纬度（免费，无需 key）</item>
    /// <item>Open-Meteo → 根据经纬度拉取实时气温 / 体感 / 天气码（开源，完全免费）</item>
    /// </list>
    /// DESIGN.md §7 天气播报.
    /// </summary>
    public class WeatherNotifier : MonoBehaviour
    {
        [Tooltip("刷新间隔（小时）。")]
        [SerializeField] private float _pollHours = 6f;

        // ip-api.com：免费，无需 key；lang=zh-CN 返回中文城市名；同时拉经纬度
        private const string IP_GEO_URL =
            "http://ip-api.com/json/?lang=zh-CN&fields=status,city,lat,lon";
        // Open-Meteo：开源免费天气 API，无需任何 key
        private const string WEATHER_URL =
            "https://api.open-meteo.com/v1/forecast?latitude={0}&longitude={1}" +
            "&current=temperature_2m,apparent_temperature,weather_code&timezone=auto&forecast_days=1";

        private float  _timer;
        private float  _lat, _lon;
        private string _city = "";

        public static WeatherNotifier Instance { get; private set; }
        /// <summary>最近一次成功拉取的天气描述；未就绪时为空。</summary>
        public static string LastWeatherInfo { get; private set; } = "";
        /// <summary>IP 定位到的城市名。</summary>
        public static string DetectedCity    { get; private set; } = "";

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            _timer = _pollHours * 3600f;
            StartCoroutine(DetectLocationThenFetch());
        }

        private void Update()
        {
            if (_lat == 0f && _lon == 0f) return;
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                _timer = _pollHours * 3600f;
                StartCoroutine(FetchWeather());
            }
        }

        private IEnumerator DetectLocationThenFetch()
        {
            using var geoReq = UnityWebRequest.Get(IP_GEO_URL);
            geoReq.timeout = 6;
            yield return geoReq.SendWebRequest();

            if (geoReq.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    var node = MiniJson.Parse(geoReq.downloadHandler.text);
                    if (node["status"].ToString() == "success")
                    {
                        _city = node["city"].ToString();
                        float.TryParse(node["lat"].ToString(),  out _lat);
                        float.TryParse(node["lon"].ToString(),  out _lon);
                        DetectedCity = _city;
                        Debug.Log($"[WeatherNotifier] IP 定位：{_city} ({_lat:F2},{_lon:F2})");
                    }
                }
                catch { Debug.LogWarning("[WeatherNotifier] IP 定位解析失败"); }
            }
            else
            {
                Debug.LogWarning($"[WeatherNotifier] IP 定位请求失败：{geoReq.error}");
            }

            if (_lat != 0f || _lon != 0f)
                yield return StartCoroutine(FetchWeather());
        }

        private IEnumerator FetchWeather()
        {
            var url = string.Format(WEATHER_URL,
                _lat.ToString("F4", System.Globalization.CultureInfo.InvariantCulture),
                _lon.ToString("F4", System.Globalization.CultureInfo.InvariantCulture));

            using var req = UnityWebRequest.Get(url);
            req.timeout = 8;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[WeatherNotifier] 天气请求失败：{req.error}");
                yield break;
            }

            JsonNode root;
            try   { root = MiniJson.Parse(req.downloadHandler.text); }
            catch { yield break; }

            // Open-Meteo 返回: { "current": { "temperature_2m": 22.5, "apparent_temperature": 21.0, "weather_code": 2 } }
            var cur = root["current"];
            if (cur.IsMissing) yield break;

            float.TryParse(cur["temperature_2m"].ToString(),     System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var temp);
            float.TryParse(cur["apparent_temperature"].ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var feels);
            int.TryParse(cur["weather_code"].ToString(), out var code);

            var desc  = WmoCodeToText(code);
            var emoji = WmoCodeToEmoji(code);
            var msg   = $"{emoji} {_city} {Mathf.RoundToInt(temp)}°C，{desc}（体感 {Mathf.RoundToInt(feels)}°C）";
            LastWeatherInfo = msg;

            PetSpeechBubble.Instance?.Show(msg, 8f);
        }

        // ── WMO 天气码 → 中文描述 / emoji ────────────────────────────────────
        private static string WmoCodeToText(int code) => code switch
        {
            0          => "晴",
            1          => "晴间多云",
            2          => "多云",
            3          => "阴",
            45 or 48   => "雾",
            51 or 53   => "毛毛雨",
            55         => "浓毛毛雨",
            56 or 57   => "冻毛毛雨",
            61         => "小雨",
            63         => "中雨",
            65         => "大雨",
            66 or 67   => "冻雨",
            71         => "小雪",
            73         => "中雪",
            75         => "大雪",
            77         => "冰粒",
            80         => "阵雨",
            81         => "中阵雨",
            82         => "强阵雨",
            85 or 86   => "阵雪",
            95         => "雷暴",
            96 or 99   => "雷暴伴冰雹",
            _          => "未知"
        };

        private static string WmoCodeToEmoji(int code) => code switch
        {
            0          => "☀️",
            1 or 2     => "⛅",
            3          => "☁️",
            45 or 48   => "🌫️",
            >= 51 and <= 67 => "🌧️",
            >= 71 and <= 77 => "❄️",
            >= 80 and <= 82 => "u{1F327}u{FE0F}",
            85 or 86   => "❄️",
            >= 95      => "⛈️",
            _          => "🌤️"
        };
    }
}
