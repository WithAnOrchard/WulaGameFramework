using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 动作客户端 - 存根版本（原功能已迁移）
    /// </summary>
    public class ActionsClient : MonoBehaviour
    {
        public event Action<Dictionary<string, object>> OnActionReceived;

        public void SendAction(string action, Dictionary<string, object> data) { }
        public void Close() { }
    }
}
