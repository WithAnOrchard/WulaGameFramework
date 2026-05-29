using System.Collections.Generic;
using BiliBiliDanmu.UI;
using Demo.DobeCat.Sys.Network;
using UnityEngine;
using BiliBiliLive;
using EssSystem.Core.Base.Event;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>DobeCat 专用弹幕测试面板 —— 继承 EssSystem 的通用版本，添加房间发现功能 + 直播状态显示。</summary>
    public class DobeCatTestPanel : BiliBiliDanmu.UI.DanmuTestPanel
    {
        private static DobeCatTestPanel _dobeCatInstance;
        public static new DobeCatTestPanel Instance => _dobeCatInstance ?? (_dobeCatInstance = new DobeCatTestPanel());

        private RoomDiscoveryClient _discovery;
        private EventDelegate _livePolledHandler;

        /// <summary>附加房间发现客户端，刷新 IP / 房间列表。</summary>
        public void AttachDiscovery(RoomDiscoveryClient discovery)
        {
            // 先解除旧的订阅
            if (_discovery != null)
            {
                _discovery.OnRoomsChanged -= OnDiscoveryRoomsChanged;
            }

            _discovery = discovery;
            
            // 订阅房间列表变化事件
            if (_discovery != null)
            {
                _discovery.OnRoomsChanged += OnDiscoveryRoomsChanged;
                Debug.Log("[DobeCatTestPanel] AttachDiscovery: 房间发现客户端已附加，已订阅 OnRoomsChanged");
            }
        }

        /// <summary>房间列表更新时触发，重新更新面板显示。</summary>
        private void OnDiscoveryRoomsChanged(IReadOnlyList<RoomDiscoveryClient.RoomInfo> rooms)
        {
            Debug.Log($"[DobeCatTestPanel] OnDiscoveryRoomsChanged: 收到 {rooms.Count} 个房间");
            UpdateStatus();
        }

        /// <summary>覆盖 Toggle，确保使用 DobeCatTestPanel.Instance。</summary>
        public static new void Toggle()
        {
            Instance.ToggleImpl();
        }

        /// <summary>覆盖 Open，确保使用 DobeCatTestPanel.Instance。</summary>
        public static new void Open()
        {
            Instance.OpenImpl();
        }

        /// <summary>覆盖 Close，确保使用 DobeCatTestPanel.Instance。</summary>
        public static new void Close()
        {
            Instance.CloseImpl();
        }

        private void ToggleImpl()
        {
            if (IsOpenImpl()) CloseImpl(); else OpenImpl();
        }

        private void OpenImpl()
        {
            RegisterListeners();
            RegisterLiveStatusListener();
            var view = EnsureViewImpl();
            view.Show();
            UpdateStatus();
            FlushLog();
            Debug.Log("[DobeCatTestPanel] OpenImpl: 面板已打开");
        }

        private void CloseImpl()
        {
            UnregisterListeners();
            UnregisterLiveStatusListener();
            DanmuTestPanelView.Instance?.Hide();
            Debug.Log("[DobeCatTestPanel] CloseImpl: 面板已关闭");
        }

        private bool IsOpenImpl()
            => DanmuTestPanelView.Instance != null && DanmuTestPanelView.Instance.IsOpen;

        private DanmuTestPanelView EnsureViewImpl()
        {
            if (DanmuTestPanelView.Instance != null) return DanmuTestPanelView.Instance;
            var go = new GameObject("DanmuTestPanelView");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<DanmuTestPanelView>();
        }

        private void RegisterLiveStatusListener()
        {
            if (!EventProcessor.HasInstance) return;
            _livePolledHandler = OnLiveStatusPolled;
            EventProcessor.Instance.AddListener(LiveStatusService.EVT_STATUS_POLLED, _livePolledHandler);
            Debug.Log("[DobeCatTestPanel] 直播状态监听器已注册");
        }

        private void UnregisterLiveStatusListener()
        {
            if (_livePolledHandler != null && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.RemoveListener(LiveStatusService.EVT_STATUS_POLLED, _livePolledHandler);
                _livePolledHandler = null;
                Debug.Log("[DobeCatTestPanel] 直播状态监听器已取消注册");
            }
        }

        private List<object> OnLiveStatusPolled(string evt, List<object> data)
        {
            if (data == null || data.Count < 4) return null;
            
            var liveStatus = data[1] is int status ? status : -1;
            var title = data[2] as string ?? "";
            var info = data[3] as LiveRoomInfo;
            
            var v = DanmuTestPanelView.Instance;
            if (v == null) return null;
            
            string statusText;
            if (liveStatus == 1)
            {
                var online = info?.Online ?? 0;
                statusText = $"● 直播中: {title} ({online}人)";
            }
            else if (liveStatus == 2)
            {
                statusText = "● 轮播中";
            }
            else
            {
                statusText = "○ 未开播";
            }
            
            v.LiveText = statusText;
            Debug.Log($"[DobeCatTestPanel] 直播状态更新: {statusText}");
            return null;
        }

        /// <summary>覆盖 UpdateStatus，同时显示 B站直播间信息 + 房间发现的在线房间列表。</summary>
        protected override void UpdateStatus()
        {
            base.UpdateStatus();
            
            var v = DanmuTestPanelView.Instance;
            if (v == null) 
            { 
                Debug.LogWarning("[DobeCatTestPanel] UpdateStatus: DanmuTestPanelView.Instance 为 null");
                return; 
            }

            var sb = new System.Text.StringBuilder();

            // ── B站直播间信息 ──
            sb.AppendLine("=== B站直播间 ===");
            if (LiveStatusService.HasInstance)
            {
                var svc = LiveStatusService.Instance;
                if (svc.IsPolling && svc.RoomId > 0)
                {
                    var status = svc.LiveStatus;
                    var title = svc.Title;
                    var info = svc.Info;
                    
                    if (status == 1)
                    {
                        sb.AppendLine($"● 直播中");
                    }
                    else if (status == 2)
                    {
                        sb.AppendLine($"● 轮播中");
                    }
                    else
                    {
                        sb.AppendLine($"○ 未开播");
                    }
                    
                    // 直播标题
                    if (!string.IsNullOrEmpty(title))
                    {
                        sb.AppendLine($"标题: {title}");
                    }
                    
                    // 简介
                    if (!string.IsNullOrEmpty(info?.Description))
                    {
                        var desc = info.Description;
                        if (desc.Length > 100) desc = desc.Substring(0, 100) + "...";
                        sb.AppendLine($"简介: {desc}");
                    }
                    
                    // 基础信息
                    sb.AppendLine($"房间号: {svc.RoomId}");
                    if (!string.IsNullOrEmpty(info?.AreaName))
                    {
                        sb.AppendLine($"分区: {info.ParentAreaName} > {info.AreaName}");
                    }
                    
                    if (status == 1)
                    {
                        sb.AppendLine($"观众: {info?.Online ?? 0}");
                        if (info?.Attention > 0)
                        {
                            sb.AppendLine($"关注: {info.Attention}");
                        }
                        if (!string.IsNullOrEmpty(info?.LiveTime) && info.LiveTime != "0000-00-00 00:00:00")
                        {
                            sb.AppendLine($"开播: {info.LiveTime}");
                        }
                    }
                    
                    // 标签
                    if (!string.IsNullOrEmpty(info?.Tags))
                    {
                        var tags = info.Tags;
                        if (tags.Length > 50) tags = tags.Substring(0, 50) + "...";
                        sb.AppendLine($"标签: {tags}");
                    }
                }
                else
                {
                    sb.AppendLine("○ 未启动轮询");
                }
            }
            else
            {
                sb.AppendLine("○ 服务未就绪");
            }

            // ── 房间发现（联机房间）──
            sb.AppendLine();
            sb.AppendLine("=== 联机房间 ===");
            if (_discovery != null && _discovery.LatestRooms != null && _discovery.LatestRooms.Count > 0)
            {
                foreach (var room in _discovery.LatestRooms)
                {
                    var mark = room.IsSelf ? "●" : "○";
                    sb.AppendLine($"{mark} {room.Name}");
                    sb.AppendLine($"  {room.Host}:{room.Port}");
                }
                Debug.Log($"[DobeCatTestPanel] UpdateStatus: 显示 {_discovery.LatestRooms.Count} 个联机房间");
            }
            else
            {
                sb.AppendLine("联机房间发现中...");
                Debug.Log($"[DobeCatTestPanel] UpdateStatus: 联机房间发现中");
            }

            v.DetailText = sb.ToString();
        }
    }
}
