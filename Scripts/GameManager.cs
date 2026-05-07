using System.Linq;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D;
using EssSystem.Core.EssManagers.Foundation.ResourceManager;
using EssSystem.Core.EssManagers.Presentation.UIManager;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime.Preview;

/// <summary>
/// 游戏管理器 - 用于测试 EssSystem 框架
/// <para>
/// 继承自 AbstractGameManager，自动管理同一 GameObject 上的所有 Manager
/// </para>
/// </summary>
public class GameManager : AbstractGameManager
{
    [Header("Map")]
    [Tooltip("启动时自动渲染的地图配置 ID")]
    [SerializeField] private string _startupMapConfigId = "PerlinIsland";
    [Tooltip("启动时自动渲染的地图实例 ID")]
    [SerializeField] private string _startupMapId = "world1";
    [Tooltip("地图渲染半径：以焦点为中心 (2*radius+1)² 区块流式渲染")]
    [SerializeField, Range(0, 32)] private int _mapRenderRadius = 4;
    [Tooltip("每帧最多渲染区块数（异步流式预算，1~3 通常足够平摊 spike）")]
    [SerializeField, Min(1)] private int _mapChunksPerFrame = 2;
    [Tooltip("地图焦点跟随的 Transform；null 时焦点固定在 (0,0)")]
    [SerializeField] private Transform _mapFollowTarget;

    /// <summary>
    /// 初始化游戏
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        Debug.Log("[GameManager] 游戏管理器初始化完成");
    }

    /// <summary>
    /// 游戏开始
    /// </summary>
    protected virtual void Start()
    {
        Debug.Log("[GameManager] 游戏开始");

        // 测试框架功能
        TestFramework();

        // 确保玩家背包存在
        EnsurePlayerInventory();

        // 启动时自动渲染地图
        SpawnStartupMap();
    }

    /// <summary>
    /// 测试框架功能
    /// </summary>
    private void TestFramework()
    {
        Debug.Log("[GameManager] 开始测试框架功能...");

        // 获取已管理的 Manager
        var managers = GetManagedManagers();
        Debug.Log($"[GameManager] 当前管理的 Manager 数量: {managers.Count()}");

        foreach (var manager in managers)
        {
            Debug.Log($"[GameManager] - {manager.GetType().Name}");
        }
    }

    /// <summary>
    /// 确保玩家背包存在
    /// </summary>
    private void EnsurePlayerInventory()
    {
        var inventoryManager = GetManager<InventoryManager>();
        if (inventoryManager != null && inventoryManager.Service != null)
        {
            var playerInventory = inventoryManager.Service.GetInventory("player");
            if (playerInventory == null)
            {
                inventoryManager.Service.CreateInventory("player", "玩家背包", 30);
                Debug.Log("[GameManager] 创建玩家背包");
            }
        }
    }

    /// <summary>
    /// 启动时自动创建并渲染默认地图（创建 Map 实例 → 自动构建 Grid + Tilemap → 渲染指定半径区块）。
    /// </summary>
    private void SpawnStartupMap()
    {
        var mapManager = GetManager<MapManager>();
        if (mapManager == null || mapManager.Service == null)
        {
            Debug.LogWarning("[GameManager] 未找到 MapManager，跳过启动地图渲染");
            return;
        }

        // 确保 ResourceService 已经把 Resources/ 下的 RuleTile 同步预加载入缓存。
        // ResourceManager.Start() 也会触发同一事件，但 Unity 中两者 Start 顺序未定义；
        // OnDataLoaded 内部有 _dataLoaded 幂等守卫，重复触发无副作用。
        EventProcessor.Instance.TriggerEventMethod(
            ResourceService.EVT_DATA_LOADED,
            new System.Collections.Generic.List<object>());

        var map = mapManager.Service.CreateMap(_startupMapId, _startupMapConfigId);
        if (map == null)
        {
            Debug.LogWarning($"[GameManager] CreateMap 失败: {_startupMapId}/{_startupMapConfigId}");
            return;
        }

        var view = mapManager.Service.CreateMapView(_startupMapId);
        if (view == null) return;

        // 配置流式参数；MapView.Update() 会自动按 follow target / 默认 (0,0) 焦点流式渲染
        view.RenderRadius = _mapRenderRadius;
        view.ChunksPerFrame = _mapChunksPerFrame;

        // 未在 Inspector 指定时，自动找场景中的 TestPlayer 当跟随目标
        var follow = _mapFollowTarget;
        if (follow == null)
        {
            var tp = FindObjectOfType<TestPlayer>();
            if (tp != null) follow = tp.transform;
        }
        view.FollowTarget = follow;

        var side = 2 * _mapRenderRadius + 1;
        var followInfo = follow != null ? $"跟随 {follow.name}" : "焦点固定 (0,0)";
        Debug.Log($"[GameManager] 启动流式地图 {_startupMapId}: 半径 {_mapRenderRadius}（{side}x{side} 区块），" +
                  $"每帧 {_mapChunksPerFrame} 块，{followInfo}");
    }

    /// <summary>
    /// 每帧更新 - 处理测试按键
    /// </summary>
    protected virtual void Update()
    {
        HandleTestInput();
    }

    /// <summary>
    /// 处理测试按键
    /// </summary>
    private void HandleTestInput()
    {
        // B 键 - 切换玩家背包（打开/关闭）
        if (Input.GetKeyDown(KeyCode.B))
        {
            TogglePlayerInventory();
        }

        // K 键 - 切换 Character 预览面板（打开/关闭）
        if (Input.GetKeyDown(KeyCode.K))
        {
            ToggleCharacterPreview();
        }
    }

    /// <summary>
    /// 切换玩家背包（打开/关闭）
    /// </summary>
    private void TogglePlayerInventory()
    {
        // InventoryManager 缓存了 UI（关闭=隐藏不销毁），所以「实体存在」已不能代表「正在显示」。
        // 改为检查 GameObject 是否处于激活状态。走 EVT_GET_UI_GAMEOBJECT 不暴露 UIEntity 类型。
        var result = EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_GET_UI_GAMEOBJECT,
            new System.Collections.Generic.List<object> { "player" });

        var isVisible = false;
        if (ResultCode.IsOk(result) && result.Count >= 2 && result[1] is GameObject go)
        {
            isVisible = go != null && go.activeSelf;
        }

        if (isVisible)
        {
            Debug.Log("[GameManager] 关闭玩家背包 (B键)");
            EventProcessor.Instance.TriggerEventMethod(InventoryManager.EVT_CLOSE_UI,
                new System.Collections.Generic.List<object> { "player" });
        }
        else
        {
            Debug.Log("[GameManager] 打开玩家背包 (B键)");
            EventProcessor.Instance.TriggerEventMethod(InventoryManager.EVT_OPEN_UI,
                new System.Collections.Generic.List<object> { "player", "PlayerBackPack" });
        }
    }

    // 懒创建的预览面板实例（首次按 K 时构建，之后 SetActive 切换）
    private CharacterPreviewPanel _characterPreviewPanel;

    /// <summary>
    /// 切换 Character 预览面板（打开/关闭）—— 首次按 K 时懒创建。
    /// </summary>
    private void ToggleCharacterPreview()
    {
        if (_characterPreviewPanel == null)
        {
            var go = new GameObject("CharacterPreviewPanel");
            go.transform.SetParent(transform, false);
            _characterPreviewPanel = go.AddComponent<CharacterPreviewPanel>();
            Debug.Log("[GameManager] 创建并打开 Character 预览面板 (K键)");
            return;
        }

        var willOpen = !_characterPreviewPanel.gameObject.activeSelf;
        _characterPreviewPanel.gameObject.SetActive(willOpen);
        Debug.Log(willOpen ? "[GameManager] 打开 Character 预览面板 (K键)" : "[GameManager] 关闭 Character 预览面板 (K键)");
    }

}
