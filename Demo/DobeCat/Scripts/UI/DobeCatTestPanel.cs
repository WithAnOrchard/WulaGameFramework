using System;
using System.Collections.Generic;
using BiliBiliDanmu;
using BiliBiliLive;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine;

namespace Demo.DobeCat.UI
{
    /// <summary>
    /// DobeCat 弹幕测试面板（运行时通过 UIManager 动态注册）。
    /// <list type="bullet">
    /// <item>右上角浮窗，显示 B 站弹幕连接状态 + 最近 N 条弹幕 / 礼物。</item>
    /// <item>由托盘菜单 <see cref="Toggle"/> 显隐。</item>
    /// <item><c>[EventListener]</c> 绑定 DanmuService 广播 → 实时更新文本（事件本身始终注册，面板未打开时空操作）。</item>
    /// </list>
    /// </summary>
    public static class DobeCatTestPanel
    {
        private const string PanelDaoId = "DobeCatTestPanel";
        private const int MaxLines = 12;

        // Reference Resolution = 1920x1080（CanvasScaler 默认值）
        private const float CanvasW = 1920f;
        private const float CanvasH = 1080f;

        private const float W = 380f, H = 480f;
        private static readonly float CenterX = CanvasW - 20f - W * 0.5f;
        private static readonly float CenterY = CanvasH - 20f - H * 0.5f;

        private static UITextComponent _statusText;
        private static UITextComponent _liveText;
        private static UITextComponent _detailText;
        private static UITextComponent _logText;
        private static readonly Queue<string> _lines = new Queue<string>();

        // ─── 公共 API ────────────────────────────────────────────
        public static void Toggle()
        {
            if (IsOpen()) Close(); else Open();
        }

        public static void Open()
        {
            if (!EventProcessor.HasInstance) return;
            if (IsOpen()) return;

            var panel = BuildPanel();
            EventProcessor.Instance.TriggerEventMethod(
                UIManager.EVT_REGISTER_ENTITY,
                new List<object> { PanelDaoId, panel });

            // 重新打开时刷新一次状态 + 历史
            UpdateStatus();
            UpdateLiveStatus();
            UpdateDetail();
            FlushLog();
        }

        public static void Close()
        {
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                UIManager.EVT_UNREGISTER_ENTITY,
                new List<object> { PanelDaoId });
            _statusText = null;
            _liveText = null;
            _detailText = null;
            _logText = null;
        }

        private static bool IsOpen()
        {
            if (!EventProcessor.HasInstance) return false;
            var r = EventProcessor.Instance.TriggerEventMethod(
                UIManager.EVT_GET_UI_GAMEOBJECT,
                new List<object> { PanelDaoId });
            return ResultCode.IsOk(r) && r.Count >= 2 && r[1] is GameObject go && go != null;
        }

        // ─── UI 构建 ──────────────────────────────────────────────
        private static UIPanelComponent BuildPanel()
        {
            var panel = new UIPanelComponent(PanelDaoId, "弹幕测试面板")
                .SetPosition(CenterX, CenterY)
                .SetSize(W, H)
                .SetBackgroundColor(new Color(0.05f, 0.05f, 0.07f, 0.92f));

            // 标题（UIText 中心约定：从面板中心出发）
            panel.AddChild(new UITextComponent($"{PanelDaoId}_Title", "Title", "弹幕测试")
                .SetPosition(0f, H * 0.5f - 28f)
                .SetSize(W - 24f, 36f)
                .SetFontSize(22)
                .SetColor(new Color(1f, 0.85f, 0.4f))
                .SetAlignment(TextAnchor.MiddleCenter));

            // 关闭按钮（UIButton BL 约定：从面板左下角出发，位置=按钮中心）
            var closeBtn = new UIButtonComponent($"{PanelDaoId}_Close", "Close", "×")
                .SetPosition(W - 24f, H - 24f)
                .SetSize(36f, 36f)
                .SetButtonColor(new Color(0.5f, 0.2f, 0.2f, 1f));
            closeBtn.OnClick += _ => Close();
            panel.AddChild(closeBtn);

            // 弹幕连接状态
            _statusText = new UITextComponent($"{PanelDaoId}_Status", "Status", "未连接")
                .SetPosition(0f, H * 0.5f - 60f)
                .SetSize(W - 24f, 22f)
                .SetFontSize(14)
                .SetColor(new Color(0.6f, 0.95f, 0.6f))
                .SetAlignment(TextAnchor.MiddleCenter);
            panel.AddChild(_statusText);

            // 开播状态轮询
            _liveText = new UITextComponent($"{PanelDaoId}_Live", "Live", "开播轮询: 未启动")
                .SetPosition(0f, H * 0.5f - 86f)
                .SetSize(W - 24f, 22f)
                .SetFontSize(13)
                .SetColor(new Color(0.95f, 0.8f, 0.55f))
                .SetAlignment(TextAnchor.MiddleCenter);
            panel.AddChild(_liveText);

            // 房间详细信息（不需身份码即可获取：标题 / 分区 / 在线 / 开播时间）
            _detailText = new UITextComponent($"{PanelDaoId}_Detail", "Detail", "--")
                .SetPosition(0f, H * 0.5f - 165f)
                .SetSize(W - 24f, 130f)
                .SetFontSize(12)
                .SetColor(new Color(0.85f, 0.85f, 0.95f))
                .SetAlignment(TextAnchor.UpperLeft);
            panel.AddChild(_detailText);

            // 事件日志区域（占据剩余空间）
            _logText = new UITextComponent($"{PanelDaoId}_Log", "Log", "等待事件...")
                .SetPosition(0f, -110f)
                .SetSize(W - 24f, H - 350f)
                .SetFontSize(12)
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.UpperLeft);
            panel.AddChild(_logText);

            return panel;
        }

        // ─── 数据更新 ─────────────────────────────────────────────
        private static void UpdateStatus()
        {
            if (_statusText == null) return;
            var s = DanmuService.HasInstance ? DanmuService.Instance : null;
            _statusText.Text = s != null && s.IsConnected
                ? $"已连接 · 房间 {s.RoomId}"
                : "未连接";
        }

        private static void UpdateLiveStatus()
        {
            if (_liveText == null) return;
            if (!LiveStatusService.HasInstance)
            {
                _liveText.Text = "开播轮询: 未启动";
                return;
            }
            var s = LiveStatusService.Instance;
            if (!s.IsPolling)
            {
                _liveText.Text = "开播轮询: 未启动";
                return;
            }
            var statusLabel = s.LiveStatus switch
            {
                1 => "直播中",
                2 => "轮播中",
                0 => "未开播",
                _ => "拉取中...",
            };
            _liveText.Text = $"房间 {s.RoomId} · {statusLabel}";
        }

        private static void UpdateDetail()
        {
            if (_detailText == null) return;
            if (!LiveStatusService.HasInstance || LiveStatusService.Instance.LiveStatus < 0)
            {
                _detailText.Text = "<尚未获取房间信息>";
                return;
            }
            _detailText.Text = FormatRoomInfo(LiveStatusService.Instance.Info);
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
            // 多行详情（公开接口全部字段）
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

        private static void AppendLine(string line)
        {
            _lines.Enqueue(line);
            while (_lines.Count > MaxLines) _lines.Dequeue();
            FlushLog();
        }

        private static void FlushLog()
        {
            if (_logText == null) return;
            _logText.Text = _lines.Count == 0 ? "等待弹幕..." : string.Join("\n", _lines);
        }

        // ─── 事件订阅（启动时由 EventProcessor 反射注册；面板未打开时静默空操作） ───
        [EventListener(DanmuService.EVT_CONNECTED)]
        private static List<object> OnConnected(string evt, List<object> data)
        {
            UpdateStatus();
            AppendLine("[系统] 连接成功");
            return null;
        }

        [EventListener(DanmuService.EVT_DISCONNECTED)]
        private static List<object> OnDisconnected(string evt, List<object> data)
        {
            UpdateStatus();
            AppendLine("[系统] 连接已断开");
            return null;
        }

        [EventListener(DanmuService.EVT_DANMAKU)]
        private static List<object> OnDanmaku(string evt, List<object> data)
        {
            if (data == null || data.Count < 2) return null;
            var name = data[0] as string ?? "?";
            var text = data[1] as string ?? "";
            AppendLine($"[弹] {name}: {text}");
            return null;
        }

        [EventListener(DanmuService.EVT_GIFT)]
        private static List<object> OnGift(string evt, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            var name = data[0] as string ?? "?";
            var gift = data[1] as string ?? "?";
            var n = 1;
            try { n = Convert.ToInt32(data[2]); } catch { /* keep 1 */ }
            AppendLine($"[礼] {name} × {gift} ×{n}");
            return null;
        }

        // ─── LiveStatus 事件钩子 ─────────────────────────────────
        [EventListener(LiveStatusService.EVT_LIVE_STARTED)]
        private static List<object> OnLiveStarted(string evt, List<object> data)
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
        private static List<object> OnLiveEnded(string evt, List<object> data)
        {
            UpdateLiveStatus();
            UpdateDetail();
            var roomId = data != null && data.Count > 0 ? data[0] : null;
            AppendLine($"[下播] 房间 {roomId}");
            return null;
        }

        [EventListener(LiveStatusService.EVT_STATUS_POLLED)]
        private static List<object> OnLiveStatusPolled(string evt, List<object> data)
        {
            // 每次轮询都刷新状态 + 详情贴机
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
