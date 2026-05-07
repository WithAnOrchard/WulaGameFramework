using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
// §4.1 跨模块 EVT 走 bare-string；仅 using EntityManager.Dao 以获取 DefaultEntityConfigs
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;

/// <summary>
/// 测试用玩家：WASD / 方向键移动；可选鼠标滚轮缩放跟随相机。
/// <para>
/// <b>使用方式</b>：
/// <list type="number">
/// <item>新建空 GameObject "Player"，挂本脚本。</item>
/// <item>拖一个 SpriteRenderer + Sprite 上去（任意可见图，方便观察位置）。如未挂会自动生成一个白色 16×16 占位精灵。</item>
/// <item>把 GameManager 的 <c>Map Follow Target</c> 字段拖到本 GameObject。</item>
/// <item>勾选 <c>autoFollowMainCamera</c> 让 Main Camera 跟随玩家移动。</item>
/// </list>
/// </para>
/// </summary>
[DisallowMultipleComponent]
public class TestPlayer : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("移动速度（unit / 秒）。Tilemap 每格 1 unit，所以 5 表示每秒走 5 格。")]
    [SerializeField, Min(0.1f)] private float _speed = 8f;

    [Tooltip("按住 Shift 时的速度倍率（冲刺测试快速跨区块）。")]
    [SerializeField, Min(1f)] private float _sprintMultiplier = 3f;

    [Header("Camera Follow (可选)")]
    [Tooltip("让 Main Camera 跟随玩家。仅平移，不改 Z 与 orthographicSize。")]
    [SerializeField] private bool _autoFollowMainCamera = true;

    [Tooltip("相机跟随的 Lerp 平滑系数（0=瞬移，1=无延迟）。")]
    [SerializeField, Range(0f, 1f)] private float _cameraFollowSmoothing = 0.15f;

    [Header("Visual (可选自动生成)")]
    [Tooltip("无 SpriteRenderer 时自动生成一个白色方块占位。")]
    [SerializeField] private bool _autoCreateSprite = true;

    [Tooltip("占位精灵颜色。")]
    [SerializeField] private Color _placeholderColor = new(1f, 0.85f, 0.2f, 1f);

    [Header("Spawn Tree (Space)")]
    [Tooltip("按空格在当前位置生成一颗小树（随机 4 种贴图之一）。")]
    [SerializeField] private bool _enableSpawnTree = true;

    private Camera _cam;
    private int _treeSpawnCounter;

    private void Start()
    {
        if (_autoCreateSprite && GetComponent<SpriteRenderer>() == null)
        {
            EnsurePlaceholderSprite();
        }
        if (_autoFollowMainCamera)
        {
            _cam = Camera.main;
        }
    }

    private void Update()
    {
        // 输入
        var h = Input.GetAxisRaw("Horizontal"); // A/D + ←/→
        var v = Input.GetAxisRaw("Vertical");   // W/S + ↑/↓
        var dir = new Vector2(h, v);
        if (dir.sqrMagnitude > 1f) dir.Normalize();

        var speed = _speed * (Input.GetKey(KeyCode.LeftShift) ? _sprintMultiplier : 1f);
        if (dir.sqrMagnitude > 0f)
        {
            transform.position += (Vector3)(dir * (speed * Time.deltaTime));
        }

        if (_enableSpawnTree && Input.GetKeyDown(KeyCode.Space))
        {
            SpawnSmallTreeAtSelf();
        }
    }

    /// <summary>
    /// 在玩家当前位置生成一颗小树 Entity —— 通过事件中心调用
    /// EntityManager.EVT_CREATE_ENTITY（§4.1 跨模块 bare-string 协议），
    /// 不直接依赖 <c>EntityService</c>。贴图由 EntityService 内部从 4 种里随机。
    /// </summary>
    private void SpawnSmallTreeAtSelf()
    {
        if (!EventProcessor.HasInstance)
        {
            Debug.LogWarning("[TestPlayer] EventProcessor 未就绪，无法生成小树");
            return;
        }

        // instanceId 带上本 GameObject 的 instanceID + 自增 + 短 GUID，
        // 避免同场景多个 TestPlayer / Hot Reload 下与已有 Entity 冲突。
        var id = $"player_tree_{GetInstanceID()}_{++_treeSpawnCounter}_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";
        // §4.1 跨模块 bare-string：EntityManager.EVT_CREATE_ENTITY
        var result = EventProcessor.Instance.TriggerEventMethod(
            "CreateEntity",
            new List<object>
            {
                DefaultEntityConfigs.SmallTreeEntityId,
                id,
                null,                    // parent
                transform.position,      // worldPosition (Vector3 → boxed)
            });

        if (!ResultCode.IsOk(result))
        {
            Debug.LogWarning($"[TestPlayer] 生成小树失败: {(result != null && result.Count >= 2 ? result[1] : "unknown")}");
            return;
        }

        // E2 后：EVT_CREATE_ENTITY 返 Transform 不再是 Entity（协议解耦）
        var charRoot = result.Count >= 2 ? result[1] as Transform : null;
        var viewPos = charRoot != null ? charRoot.position.ToString() : "<no view>";
        Debug.Log($"[TestPlayer] 小树已生成: id={id}, character.rootPos={viewPos}");
    }

    private void LateUpdate()
    {
        if (!_autoFollowMainCamera || _cam == null) return;

        var p = transform.position;
        var c = _cam.transform.position;
        var target = new Vector3(p.x, p.y, c.z);
        _cam.transform.position = _cameraFollowSmoothing >= 1f
            ? target
            : Vector3.Lerp(c, target, 1f - Mathf.Pow(1f - _cameraFollowSmoothing, Time.deltaTime * 60f));
    }

    /// <summary>无 SpriteRenderer 时挂上一个 16×16 单色方块作为可见占位。</summary>
    private void EnsurePlaceholderSprite()
    {
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color32[16 * 16];
        var col = (Color32)_placeholderColor;
        for (var i = 0; i < pixels.Length; i++) pixels[i] = col;
        tex.SetPixels32(pixels);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16f);
        sprite.name = "TestPlayer_Placeholder";

        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 100;  // 保证在 Tilemap 之上
    }
}
