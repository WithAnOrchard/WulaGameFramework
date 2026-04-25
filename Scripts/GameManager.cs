using System.Linq;
using UnityEngine;
using EssSystem.Core;

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

}
