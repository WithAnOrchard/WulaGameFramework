using System.Collections;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 玩家数据同步 - DobeCat 特定功能（EssSystem 中无对应实现）
    /// 
    /// 此类为存根实现，用于保持代码兼容性。
    /// 如需完整功能，应实现：
    /// - 从服务器拉取玩家存档（背包/钱包/农场等）
    /// - FetchAndRestore：异步获取并恢复数据
    /// - ClearMyServerData：清除服务器端数据
    /// </summary>
    public class PlayerDataSync : MonoBehaviour
    {
        private static PlayerDataSync _instance;
        public static PlayerDataSync Instance => _instance ?? (_instance = FindObjectOfType<PlayerDataSync>());

        public string ServerBaseUrl { get; set; }

        private void Awake()
        {
            if (_instance == null) _instance = this;
        }

        public void Sync() { }
        public void Close() { }
        public Coroutine FetchAndRestore() => null;
        public void ClearMyServerData() { }
    }
}
