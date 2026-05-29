using System;
using System.Collections.Generic;
using UnityEngine;

namespace Demo.DobeCat.Sys.Network
{
    /// <summary>
    /// 房间发现客户端 - DobeCat 特定功能（EssSystem 中无对应实现）
    /// 
    /// 此类为存根实现，用于保持代码兼容性。
    /// 如需完整功能，应实现：
    /// - 房间发现和广告功能
    /// - 多人房间列表管理
    /// - 事件广播（OnRoomDiscovered、OnRoomLost、OnRoomsChanged）
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

        public string ServerBaseUrl { get; set; }
        public string CollectionName { get; set; }
        public string AdvertisedHost { get; set; }
        public ushort AdvertisedPort { get; set; }
        public string RoomDisplayName { get; set; }

        public event Action<RoomInfo> OnRoomDiscovered;
        public event Action<string> OnRoomLost;
        public event Action<List<RoomInfo>> OnRoomsChanged;

        public void StartDiscovery() { }
        public void StopDiscovery() { }
        public List<RoomInfo> GetDiscoveredRooms() => new List<RoomInfo>();
    }
}
