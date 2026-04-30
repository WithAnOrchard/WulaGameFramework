using System.Linq;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.EssManager.InventoryManager;
using EssSystem.Core.EssManagers.UIManager;

/// <summary>
/// 游戏管理器 - 用于测试 EssSystem 框架
/// <para>
/// 继承自 AbstractGameManager，自动管理同一 GameObject 上的所有 Manager
/// </para>
/// </summary>
public class GameManager : AbstractGameManager
{
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
    }

    /// <summary>
    /// 切换玩家背包（打开/关闭）
    /// </summary>
    private void TogglePlayerInventory()
    {
        // InventoryManager 缓存了 UI（关闭=隐藏不销毁），所以「实体存在」已不能代表「正在显示」。
        // 改为检查 GameObject 是否处于激活状态。
        var result = EventProcessor.Instance.TriggerEventMethod(UIManager.EVT_GET_ENTITY,
            new System.Collections.Generic.List<object> { "player" });

        var isVisible = false;
        if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
        {
            var entity = result[1] as EssSystem.Core.EssManagers.UIManager.Entity.UIEntity;
            isVisible = entity != null && entity.gameObject != null && entity.gameObject.activeSelf;
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

}
