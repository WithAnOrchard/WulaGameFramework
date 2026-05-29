using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 数据交换会话 - 存根版本（原功能已迁移）
    /// </summary>
    public class DataExchangeSession : MonoBehaviour
    {
        public event Action<Dictionary<string, object>> OnDataReceived;

        public void SendData(Dictionary<string, object> data) { }
        public void Close() { }
    }
}
