using System;
using System.Collections.Generic;
using Demo.DobeCat.Network;
using Demo.DobeCat.Platform.Windows;
using UnityEngine;

namespace Demo.DobeCat.Tray
{
    /// <summary>
    /// DobeCat 桌宠的系统托盘集成器（仅 Windows 编译生效）。
    /// <list type="bullet">
    /// <item>右键菜单：显示 / 隐藏、重置位置、加入房间动态列表、退出</item>
    /// <item>双击：切换显示 / 隐藏</item>
    /// </list>
    /// </summary>
    public class DobeCatTray : MonoBehaviour
    {
        [Tooltip("桌宠根 GameObject（隐藏 / 显示用）。")]
        public GameObject PetRoot;

        [Tooltip("重置位置时回到的世界坐标（默认 0,0,0）。")]
        public Vector3 ResetPosition = Vector3.zero;

        [Tooltip("可选：房间发现客户端（注入后启用「加入房间」动态菜单）。")]
        public RoomDiscoveryClient Discovery;

        [Tooltip("可选：桌宠 AI 控制器（注入后启用「AI 开关」菜单切换）。")]
        public Demo.DobeCat.Pet.PetAiController Ai;

        /// <summary>当用户从托盘菜单点击 "加入 xxx 房间" 时触发。
        /// <para>由外部（DobeCatGameManager）订阅，执行：停掉当前 Host → 以 Client 模式连过去。</para>
        /// <para>注：仅 Standalone Windows 真实触发；Editor / 非 Windows 下保留声明以便订阅方编译通过。</para></summary>
#pragma warning disable 67 // Editor / 非 Windows 路径不会 invoke 此事件
        public event Action<RoomDiscoveryClient.RoomInfo> OnJoinRoomRequested;
#pragma warning restore 67

        /// <summary>外部（如桌宠右键）请求弹出托盘菜单。Editor / 非 Win 路径下空操作。</summary>
        public void RequestShowMenu()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            _tray?.RequestShowMenu();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private SystemTray _tray;
        private bool _petVisible = true;

        private void Start()
        {
            _tray = new SystemTray { Tooltip = "DobeCat 桌宠" };
            _tray.OnDoubleClick += TogglePetVisible;
            RebuildMenu(); // 初始填一份（房间区为空提示）
            _tray.Start();

            if (Discovery != null)
                Discovery.OnRoomsChanged += OnDiscoveryRoomsChanged;
        }

        private void OnDestroy()
        {
            if (Discovery != null)
                Discovery.OnRoomsChanged -= OnDiscoveryRoomsChanged;
            try { _tray?.Dispose(); } catch { /* swallow */ }
        }

        private void OnDiscoveryRoomsChanged(IReadOnlyList<RoomDiscoveryClient.RoomInfo> rooms)
        {
            // 在 Unity 主线程触发（UnityWebRequest 回调在主线程），直接 SetItems
            RebuildMenu();
        }

        private void RebuildMenu()
        {
            if (_tray == null) return;
            var items = new List<SystemTray.MenuItemDef>
            {
                SystemTray.MenuItemDef.Item("显示 / 隐藏", TogglePetVisible),
                SystemTray.MenuItemDef.Item("重置位置", ResetPetPosition),
            };

            // AI 总开关（包含 PlayerControl 与 Wander 两个 Consideration）
            if (Ai != null)
            {
                var label = Ai.AiEnabled ? "✔ AI / 玩家控制" : "  AI / 玩家控制";
                items.Add(SystemTray.MenuItemDef.Item(label, ToggleAi));
            }
            items.Add(SystemTray.MenuItemDef.Separator());

            // 房间区
            if (Discovery == null || Discovery.LatestRooms == null || Discovery.LatestRooms.Count == 0)
            {
                items.Add(SystemTray.MenuItemDef.Disabled("（暂无在线房间）"));
            }
            else
            {
                items.Add(SystemTray.MenuItemDef.Disabled("─ 加入房间 ─"));
                foreach (var r in Discovery.LatestRooms)
                {
                    var captured = r;
                    var label = captured.IsSelf
                        ? $"● 我的房间: {captured.Name} ({captured.Host}:{captured.Port})"
                        : $"加入: {captured.Name} ({captured.Host}:{captured.Port})";
                    items.Add(SystemTray.MenuItemDef.Item(label,
                        () => OnJoinRoomRequested?.Invoke(captured),
                        enabled: !captured.IsSelf));
                }
            }

            items.Add(SystemTray.MenuItemDef.Separator());
            items.Add(SystemTray.MenuItemDef.Item("弹幕测试面板", () => Demo.DobeCat.UI.DobeCatTestPanel.Toggle()));
            items.Add(SystemTray.MenuItemDef.Separator());
            items.Add(SystemTray.MenuItemDef.Item("退出 (Ctrl+Shift+Q)", Quit));

            _tray.SetItems(items);
        }

        private void Update()
        {
            _tray?.PumpMainThread();
        }

        private void OnApplicationQuit() => _tray?.Dispose();

        // ── 菜单回调（在 Unity 主线程执行） ──

        private void TogglePetVisible()
        {
            if (PetRoot == null) return;
            _petVisible = !_petVisible;
            PetRoot.SetActive(_petVisible);

            // 隐藏后 PetClickThroughDriver.Update 不会再运行；窗口可能停留在"不穿透"状态
            // 导致桌面点击全部被 Unity 窗口吞掉。这里强制把窗口切回穿透。
            // 重新显示时驱动器接管 → 命中检测会自然把穿透切回正确状态。
            if (!_petVisible)
            {
                var win = Demo.DobeCat.Platform.Windows.DesktopWindow.Instance;
                if (win != null) win.SetClickThrough(true);
            }
        }

        private void ResetPetPosition()
        {
            if (PetRoot == null) return;
            PetRoot.transform.position = ResetPosition;
        }

        private void ToggleAi()
        {
            if (Ai == null) return;
            Ai.SetAiEnabled(!Ai.AiEnabled);
            RebuildMenu(); // 刷新菜单的 ✔ 标记
        }

        private void Quit()
        {
            try { _tray?.Dispose(); } catch { /* swallow */ }
            // 先走标准 Quit，再用强杀兜底（Forms 线程 / 其它后台资源可能阻塞退出）。
            Application.Quit();
            try
            {
                System.Diagnostics.Process.GetCurrentProcess().Kill();
            }
            catch { /* 已经在退出流程中也无所谓 */ }
        }
#else
        // Editor / 非 Windows 下空实现，避免编译错误，便于在 Editor 调试整体逻辑。
        private void Start()
        {
            Debug.Log("[DobeCatTray] 当前不在 Standalone Windows，托盘功能跳过");
        }
#endif
    }
}
