using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 动作客户端 - DobeCat 特定功能（EssSystem 中无对应实现）
    /// 
    /// 此类为存根实现，用于保持代码兼容性。
    /// 如需完整功能，应实现：
    /// - 向服务器发送玩家动作
    /// - SendAction：发送指定动作及数据
    /// - OnActionReceived：接收服务器广播的动作事件
    /// </summary>
    public class ActionsClient : MonoBehaviour
    {
        public string ServerBaseUrl { get; set; }

        public event Action<Dictionary<string, object>> OnActionReceived;

        public void SendAction(string action, Dictionary<string, object> data) { }
        public void Close() { }
    }
}
