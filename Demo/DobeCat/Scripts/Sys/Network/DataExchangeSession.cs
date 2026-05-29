using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 数据交换会话 - DobeCat 特定功能（EssSystem 中无对应实现）
    /// 
    /// 此类为存根实现，用于保持代码兼容性。
    /// 如需完整功能，应在此基础上实现：
    /// - ServerBaseUrl：数据服务器地址
    /// - AutoLogin：自动登录功能
    /// - OnDataReceived：数据接收事件
    /// - SendData：发送数据到服务器
    /// </summary>
    public class DataExchangeSession : MonoBehaviour
    {
        public string ServerBaseUrl { get; set; }
        public bool AutoLogin { get; set; }

        public event Action<Dictionary<string, object>> OnDataReceived;

        public void SendData(Dictionary<string, object> data) { }
        public void Close() { }
    }
}
