using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 房间发现客户端 - 存根版本（原功能已迁移）
    /// </summary>
    public class RoomDiscoveryClient : MonoBehaviour
    {
        public class RoomInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string Host { get; set; }
            public int Port { get; set; }
        }

        public event Action<RoomInfo> OnRoomDiscovered;
        public event Action<string> OnRoomLost;

        public void StartDiscovery() { }
        public void StopDiscovery() { }
        public List<RoomInfo> GetDiscoveredRooms() => new List<RoomInfo>();
    }
}
