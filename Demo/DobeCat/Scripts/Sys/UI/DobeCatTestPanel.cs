using System;
using System.Collections.Generic;
using System.Text;
using BiliBiliDanmu;
using BiliBiliDanmu.UI;
using BiliBiliLive;
using BiliBiliDanmu.Auth;

using EssSystem.Core.Base.Event;
using Demo.DobeCat.Sys.Network;
using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// DobeCat 弹幕测试面板（继承 EssSystem 通用版本，扩展 LiveStatus 和 RoomDiscovery 功能）。
    /// <list type="bullet">
    /// <item>基础弹幕/礼物显示由 <see cref="BiliBiliDanmu.UI.DanmuTestPanel"/> 提供。</item>
    /// <item>扩展 Detail 区域显示 LiveStatus 直播信息和 RoomDiscovery 多人房间列表。</item>
    /// <item>由托盘菜单 <see cref="Toggle"/> 显隐。</item>
    /// </list>
    /// </summary>
    public class DobeCatTestPanel : BiliBiliDanmu.UI.DanmuTestPanel
    {
        private static DobeCatTestPanel _instance;
        public static new DobeCatTestPanel Instance => _instance ?? (_instance = new DobeCatTestPanel());

        protected DobeCatTestPanel() { }

        // 房间发现客户端（外部注入；可空）
        private RoomDiscoveryClient _discovery;

        /// <summary>由 <see cref="DobeCatGameManager"/> 调用，注入房间发现客户端，开启面板内房间列表显示。</summary>
        public void AttachDiscovery(RoomDiscoveryClient client)
        {
            if (_discovery != null) _discovery.OnRoomsChanged -= HandleRoomsChanged;
            _discovery = client;
            if (_discovery != null) _discovery.OnRoomsChanged += HandleRoomsChanged;
            UpdateDetail();
        }

        private void HandleRoomsChanged(IReadOnlyList<RoomDiscoveryClient.RoomInfo> rooms)
        {
            UpdateDetail();
        }

        // ─── 覆盖 Open 方法，添加 DobeCat 特定更新 ────────────────────────────────────────────
        public new static void Toggle()
        {
            DobeCatTestPanelView.Toggle();
        }

        public new static void Open()
        {
            DobeCatTestPanelView.Open();
            Instance.UpdateLiveStatus();
            Instance.UpdateDetail();
        }

        // ─── DobeCat 特定数据更新 ─────────────────────────────────────────────
        protected virtual void UpdateLiveStatus()
        {
            var v = DobeCatTestPanelView.Instance;
            if (v == null) return;
            if (!LiveStatusService.HasInstance) { v.LiveText = "开播轮询: 未启动"; return; }
            var s = LiveStatusService.Instance;
            if (!s.IsPolling) { v.LiveText = "开播轮询: 未启动"; return; }
            var statusLabel = s.LiveStatus switch
            {
                1 => "直播中",
                2 => "轮播中",
                0 => "未开播",
                _ => "拉取中...",
            };
            v.LiveText = $"房间 {s.RoomId} · {statusLabel}";
        }

        protected virtual void UpdateDetail()
        {
            var sb = new StringBuilder(256);

            // 段 1：直播间公开信息（如果有）
            if (LiveStatusService.HasInstance && LiveStatusService.Instance.LiveStatus >= 0)
            {
                sb.AppendLine(FormatRoomInfo(LiveStatusService.Instance.Info));
            }
            else
            {
                sb.AppendLine("<尚未获取直播房间信息>");
            }

            // 段 2：联机房间发现状态
            sb.AppendLine();
            sb.AppendLine("─── 多人房间 ───");
            if (_discovery == null)
            {
                sb.AppendLine("<RoomDiscovery 未注入>");
            }
            else if (string.IsNullOrEmpty(_discovery.ServerBaseUrl))
            {
                sb.AppendLine("<未配置发现服务器>");
            }
            else
            {
                sb.Append("自身: ").Append(!string.IsNullOrEmpty(BilibiliAuthSession.Nickname) ? BilibiliAuthSession.Nickname : "(未知)")
                  .Append("  uid=").AppendLine(BilibiliAuthSession.Mid > 0 ? BilibiliAuthSession.Mid.ToString() : "?");
                var rooms = _discovery.LatestRooms;
                if (rooms == null || rooms.Count == 0)
                {
                    sb.AppendLine("在线: 0 个（等心跳…）");
                }
                else
                {
                    sb.Append("在线: ").Append(rooms.Count).AppendLine(" 个");
                    int n = Mathf.Min(rooms.Count, 6); // 最多列 6 行
                    for (int i = 0; i < n; i++)
                    {
                        var r = rooms[i];
                        var marker = r.IsSelf ? "● " : "  ";
                        sb.Append(marker)
                          .Append(Truncate(r.Name, 20))
                          .Append("  uid=").AppendLine(r.BiliUid > 0 ? r.BiliUid.ToString() : "?");
                    }
                    if (rooms.Count > n) sb.AppendLine($"  ...（还有 {rooms.Count - n} 个）");
                }
            }

            // 确保 View 已初始化后再更新
            var view = DobeCatTestPanelView.Instance;
            if (view != null)
                view.DetailText = sb.ToString();
        }

        private static string FormatRoomInfo(LiveRoomInfo info)
        {
            if (info == null) return "<null>";
            var area = string.IsNullOrEmpty(info.ParentAreaName)
                ? info.AreaName
                : $"{info.ParentAreaName} / {info.AreaName}";
            var liveTime = string.IsNullOrEmpty(info.LiveTime) || info.LiveTime.StartsWith("0000")
                ? "--"
                : info.LiveTime;
            var lines = new List<string>
            {
                $"标题: {Truncate(info.Title, 40)}",
                $"分区: {area}",
                $"UID:  {info.Uid}",
                $"在线: {info.Online}    关注: {info.Attention}",
                $"开播: {liveTime}",
            };
            if (!string.IsNullOrEmpty(info.Tags)) lines.Add($"标签: {Truncate(info.Tags, 38)}");
            return string.Join("\n", lines);
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        // ─── LiveStatus 事件钩子 ─────────────────────────────────
        [EventListener(LiveStatusService.EVT_LIVE_STARTED)]
        private List<object> OnLiveStarted(string evt, List<object> data)
        {
            UpdateLiveStatus();
            UpdateDetail();
            var info = ExtractInfo(data);
            if (info != null)
            {
                AppendLine($"[开播] 房间 {info.RoomId}");
                AppendLine($"  标题: {Truncate(info.Title, 38)}");
                if (!string.IsNullOrEmpty(info.ParentAreaName) || !string.IsNullOrEmpty(info.AreaName))
                    AppendLine($"  分区: {info.ParentAreaName} / {info.AreaName}");
                if (!string.IsNullOrEmpty(info.LiveTime) && !info.LiveTime.StartsWith("0000"))
                    AppendLine($"  时间: {info.LiveTime}");
            }
            else
            {
                AppendLine($"[开播] 房间 {(data != null && data.Count > 0 ? data[0] : null)}");
            }
            return null;
        }

        [EventListener(LiveStatusService.EVT_LIVE_ENDED)]
        private List<object> OnLiveEnded(string evt, List<object> data)
        {
            UpdateLiveStatus();
            UpdateDetail();
            var roomId = data != null && data.Count > 0 ? data[0] : null;
            AppendLine($"[下播] 房间 {roomId}");
            return null;
        }

        [EventListener(LiveStatusService.EVT_STATUS_POLLED)]
        private List<object> OnLiveStatusPolled(string evt, List<object> data)
        {
            UpdateLiveStatus();
            UpdateDetail();
            return null;
        }

        private static LiveRoomInfo ExtractInfo(List<object> data)
        {
            if (data == null) return null;
            for (var i = 0; i < data.Count; i++)
                if (data[i] is LiveRoomInfo info) return info;
            return null;
        }
    }
}
