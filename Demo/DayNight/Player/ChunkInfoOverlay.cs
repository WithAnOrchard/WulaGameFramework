using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao;

/// <summary>
/// 挂在玩家（或任意跟随物体）上，用屏幕左上角 OnGUI 面板实时显示当前脚下 Tile 的 Biome 参数。
/// <para>
/// 读取顺序：World → FloorDiv(ChunkSize) → Chunk(cx,cy) → LocalIndex → Tile(TypeId/Elevation/Temperature/Moisture/RiverFlow)。
/// </para>
/// <para>
/// <b>使用</b>：挂到 TestPlayer 同一 GameObject；默认启用；按 <see cref="_toggleKey"/> (F3) 可开关面板。
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class ChunkInfoOverlay : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("留空则自动用 MapService 里第一个 Map（适合单地图场景）。")]
    [SerializeField] private string _mapId;

    [Header("Display")]
    [Tooltip("可见时按此键切换开关。")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.F3;

    [SerializeField] private bool _visible = true;

    [Tooltip("面板左上角屏幕坐标 (px)。")]
    [SerializeField] private Vector2 _anchor = new(12f, 12f);

    [Tooltip("字号。")]
    [SerializeField, Min(8)] private int _fontSize = 13;

    [Tooltip("Tile 坐标是否用 Floor（适合负坐标）；关闭会用 Unity 默认整除。")]
    [SerializeField] private bool _floorDivide = true;

    // —— 运行态 ——
    private GUIStyle _labelStyle;
    private GUIStyle _boxStyle;
    private Texture2D _bgTex;

    // —— FPS 统计 ——（指数滑动平均，避免单帧抖动）
    private const float FPS_SMOOTH = 0.1f; // 新样本权重
    private float _fpsSmoothed;

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey)) _visible = !_visible;

        // unscaledDeltaTime：不受 Time.timeScale 影响，真实帧耗时
        var dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            var instant = 1f / dt;
            _fpsSmoothed = _fpsSmoothed <= 0f
                ? instant
                : Mathf.Lerp(_fpsSmoothed, instant, FPS_SMOOTH);
        }
    }

    private void OnGUI()
    {
        if (!_visible) return;
        EnsureStyles();

        var map = ResolveMap();
        string body;
        if (map == null)
        {
            body = "<b>ChunkInfoOverlay</b>   " + FormatFps(_fpsSmoothed) + "\nNo Map (MapService 尚未创建地图实例)";
        }
        else
        {
            var wp = transform.position;
            // 用 Grid 世界坐标 → Tile 坐标：Tilemap 单元 = 1 unit，所以取整即可。
            var tx = _floorDivide ? FloorInt(wp.x) : (int)wp.x;
            var ty = _floorDivide ? FloorInt(wp.y) : (int)wp.y;
            var size = map.ChunkSize;
            var cx = FloorDiv(tx, size);
            var cy = FloorDiv(ty, size);
            var lx = tx - cx * size;
            var ly = ty - cy * size;

            var tile = map.GetTile(tx, ty); // 必要时触发 Chunk 生成
            var typeId = tile?.TypeId ?? "<null>";
            var typeName = MapService.Instance?.GetTileType(typeId)?.DisplayName ?? "?";

            body = string.Format(
                "<b>ChunkInfoOverlay</b>   [{0}] 切换   {17}\n" +
                "Map:   {1}\n" +
                "World: ({2}, {3})\n" +
                "Chunk: ({4}, {5})   Local: ({6}, {7})\n" +
                "Biome: <color=#FFD36E>{8}</color>  ({9})\n" +
                "Elev:  {10:000}  ({11:0.000})\n" +
                "Temp:  {12:000}  ({13:0.000})\n" +
                "Moist: {14:000}  ({15:0.000})\n" +
                "River: {16}",
                _toggleKey, map.MapId, tx, ty, cx, cy, lx, ly,
                typeName, typeId,
                tile?.Elevation ?? 0,   tile?.ElevationNormalized   ?? 0f,
                tile?.Temperature ?? 0, tile?.TemperatureNormalized ?? 0f,
                tile?.Moisture ?? 0,    tile?.MoistureNormalized    ?? 0f,
                tile?.RiverFlow ?? 0,
                FormatFps(_fpsSmoothed));
        }

        var content = new GUIContent(body);
        var size2 = _labelStyle.CalcSize(content);
        var rect = new Rect(_anchor.x, _anchor.y, size2.x + 16f, size2.y + 12f);
        GUI.Box(rect, GUIContent.none, _boxStyle);
        GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), content, _labelStyle);
    }

    private Map ResolveMap()
    {
        var svc = MapService.Instance;
        if (svc == null) return null;
        if (!string.IsNullOrEmpty(_mapId)) return svc.GetMap(_mapId);
        foreach (var m in svc.GetAllMaps()) return m; // first
        return null;
    }

    private void EnsureStyles()
    {
        if (_labelStyle != null) return;
        _bgTex = new Texture2D(1, 1);
        _bgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
        _bgTex.Apply();

        _boxStyle = new GUIStyle(GUI.skin.box) { normal = { background = _bgTex } };
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = _fontSize,
            richText = true,
            normal = { textColor = Color.white },
            alignment = TextAnchor.UpperLeft,
            wordWrap = false,
        };
    }

    /// <summary>把平滑后的 FPS 格式化为带颜色等级的富文本（≥55 绿、≥30 黄、否则红）。</summary>
    private static string FormatFps(float fps)
    {
        if (fps <= 0f) return "FPS: --";
        string color = fps >= 55f ? "#7CFC00" : fps >= 30f ? "#FFD36E" : "#FF6E6E";
        return string.Format("FPS: <color={0}>{1:000}</color> ({2:0.0}ms)", color, fps, 1000f / fps);
    }

    private static int FloorInt(float v) => Mathf.FloorToInt(v);
    private static int FloorDiv(int a, int b)
    {
        var q = a / b;
        if ((a % b) != 0 && ((a < 0) ^ (b < 0))) q--;
        return q;
    }
}
