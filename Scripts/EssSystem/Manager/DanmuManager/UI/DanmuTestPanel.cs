using System;
using System.Collections.Generic;
using System.Text;
using BiliBiliDanmu;
using EssSystem.Core.Base.Event;
using UnityEngine;

namespace BiliBiliDanmu.UI
{
    /// <summary>
    /// 弹幕测试面板（通用版本）。
    /// <list type="bullet">
    /// <item>右上角浮窗，显示 B 站弹幕连接状态 + 最近 N 条弹幕 / 礼物。</item>
    /// <item>由业务方调用 <see cref="Toggle"/> 显隐。</item>
    /// <item><c>[EventListener]</c> 绑定 <see cref="DanmuService"/> 广播 → 实时更新文本（事件始终注册，面板未打开时空操作）。</item>
    /// </list>
    /// <para>业务层可继承此类扩展 Detail 区域显示（如房间发现、联机状态等）。</para>
    /// </summary>
    public class DanmuTestPanel
    {
        private const int MaxLines = 18;

        protected static readonly Queue<string> _lines = new Queue<string>();

        private static DanmuTestPanel _instance;
        public static DanmuTestPanel Instance => _instance ?? (_instance = new DanmuTestPanel());

        private EventDelegate _connectedHandler;
        private EventDelegate _disconnectedHandler;
        private EventDelegate _danmakuHandler;
        private EventDelegate _giftHandler;
        private EventDelegate _scHandler;
        private bool _listenersRegistered;

        public DanmuTestPanel() { }

        protected virtual void RegisterListeners()
        {
            if (_listenersRegistered) return;
            if (!EventProcessor.HasInstance) return;

            _connectedHandler = OnConnected;
            _disconnectedHandler = OnDisconnected;
            _danmakuHandler = OnDanmaku;
            _giftHandler = OnGift;
            _scHandler = OnSC;

            var ep = EventProcessor.Instance;
            ep.AddListener(DanmuService.EVT_CONNECTED, _connectedHandler);
            ep.AddListener(DanmuService.EVT_DISCONNECTED, _disconnectedHandler);
            ep.AddListener(DanmuService.EVT_DANMAKU, _danmakuHandler);
            ep.AddListener(DanmuService.EVT_GIFT, _giftHandler);
            ep.AddListener(DanmuService.EVT_SC, _scHandler);
            _listenersRegistered = true;
            Debug.Log("[DanmuTestPanel] 事件监听器已注册");
        }

        protected virtual void UnregisterListeners()
        {
            if (!_listenersRegistered || !EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            ep.RemoveListener(DanmuService.EVT_CONNECTED, _connectedHandler);
            ep.RemoveListener(DanmuService.EVT_DISCONNECTED, _disconnectedHandler);
            ep.RemoveListener(DanmuService.EVT_DANMAKU, _danmakuHandler);
            ep.RemoveListener(DanmuService.EVT_GIFT, _giftHandler);
            ep.RemoveListener(DanmuService.EVT_SC, _scHandler);
            _listenersRegistered = false;
        }

        private static DanmuTestPanelView EnsureView()
        {
            if (DanmuTestPanelView.Instance != null) return DanmuTestPanelView.Instance;
            var go = new GameObject("DanmuTestPanelView");
            UnityEngine.Object.DontDestroyOnLoad(go);
            return go.AddComponent<DanmuTestPanelView>();
        }

        // ─── 公共 API ────────────────────────────────────────────
        public static void Toggle()
        {
            if (IsOpen()) Close(); else Open();
        }

        public static void Open()
        {
            Instance.RegisterListeners();
            var view = EnsureView();
            view.Show();
            Instance.UpdateStatus();
            Instance.FlushLog();
        }

        public static void Close()
        {
            Instance.UnregisterListeners();
            DanmuTestPanelView.Instance?.Hide();
        }

        private static bool IsOpen()
            => DanmuTestPanelView.Instance != null && DanmuTestPanelView.Instance.IsOpen;

        // ─── 数据更新（虚方法供子类覆盖）───────────────────────────────────────────
        protected virtual void UpdateStatus()
        {
            var v = DanmuTestPanelView.Instance;
            if (v == null) return;
            var s = DanmuService.HasInstance ? DanmuService.Instance : null;
            v.StatusText = s != null && s.IsConnected
                ? $"已连接 · 房间 {s.RoomId}"
                : "未连接";
        }

        protected virtual void AppendLine(string line)
        {
            _lines.Enqueue(line);
            while (_lines.Count > MaxLines) _lines.Dequeue();
            FlushLog();
        }

        protected virtual void FlushLog()
        {
            if (DanmuTestPanelView.Instance == null) 
            { 
                Debug.LogWarning("[DanmuTestPanel] FlushLog: DanmuTestPanelView.Instance 为 null");
                return; 
            }
            var text = _lines.Count == 0 ? "等待弹幕..." : string.Join("\n", _lines);
            Debug.Log($"[DanmuTestPanel] FlushLog: 更新日志，行数={_lines.Count}");
            DanmuTestPanelView.Instance.LogText = text;
        }

        // ─── 事件订阅（通过 RegisterListeners 手动注册） ───
        private List<object> OnConnected(string evt, List<object> data)
        {
            UpdateStatus();
            AppendLine("[系统] 连接成功");
            return null;
        }

        private List<object> OnDisconnected(string evt, List<object> data)
        {
            UpdateStatus();
            AppendLine("[系统] 连接已断开");
            return null;
        }

        private List<object> OnDanmaku(string evt, List<object> data)
        {
            if (data == null || data.Count < 2) return null;
            var name = data[0] as string ?? "?";
            var text = data[1] as string ?? "";
            Debug.Log($"[DanmuTestPanel] 收到弹幕: {name}: {text}");
            AppendLine($"[弹] {name}: {text}");
            return null;
        }

        private List<object> OnGift(string evt, List<object> data)
        {
            if (data == null || data.Count < 3) return null;
            var name = data[0] as string ?? "?";
            var gift = data[1] as string ?? "?";
            var n = 1;
            try { n = Convert.ToInt32(data[2]); } catch { /* keep 1 */ }
            AppendLine($"[礼] {name} × {gift} ×{n}");
            return null;
        }

        private List<object> OnSC(string evt, List<object> data)
        {
            if (data == null || data.Count < 2) return null;
            var name = data[0] as string ?? "?";
            var text = data[1] as string ?? "";
            var price = data.Count > 2 ? Convert.ToInt32(data[2]) : 0;
            AppendLine($"[SC] {name} ¥{price}: {text}");
            return null;
        }
    }
}
