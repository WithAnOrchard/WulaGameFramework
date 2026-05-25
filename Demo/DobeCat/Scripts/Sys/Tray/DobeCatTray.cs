using System;
using System.Collections.Generic;
using Demo.DobeCat.Sys.Network;
using Demo.DobeCat.Sys.Platform.Windows;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Base.Event;
using UnityEngine;

namespace Demo.DobeCat.Sys.Tray
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
        public Demo.DobeCat.Game.Pet.PetAiController Ai;

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
            // 菜单弹出前立即刷新房间列表，避免用户看到过期数据
            Discovery?.RefreshNow();
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
            // 显示模式切换（桌面叠加 ↔ 窗口捕捉）
            var captureLabel = Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.IsWindowCaptureMode
                ? "✔ 窗口捕捉模式 (OBS)" : "  窗口捕捉模式 (OBS)";
            items.Add(SystemTray.MenuItemDef.Item(captureLabel, ToggleWindowCaptureMode));

            // 背景层开关
            var bgVisible = Demo.DobeCat.Game.Pet.PetBackgroundLayer.Instance?.Visible ?? false;
            var bgLabel = bgVisible ? "✔ 显示背景" : "  显示背景";
            items.Add(SystemTray.MenuItemDef.Item(bgLabel, ToggleBackground));

            // 桌宠大小预设（在托盘里以文字标记当前挡位）
            var curScale = Demo.DobeCat.Game.Pet.PetScaleController.Instance?.ScaleFactor ?? 1f;
            items.Add(SystemTray.MenuItemDef.Item(Mark(curScale, 0.5f, "桌宠大小 50%"),  () => SetScale(0.5f)));
            items.Add(SystemTray.MenuItemDef.Item(Mark(curScale, 1.0f, "桌宠大小 100%"), () => SetScale(1.0f)));
            items.Add(SystemTray.MenuItemDef.Item(Mark(curScale, 1.5f, "桌宠大小 150%"), () => SetScale(1.5f)));
            items.Add(SystemTray.MenuItemDef.Item(Mark(curScale, 2.0f, "桌宠大小 200%"), () => SetScale(2.0f)));

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
            items.Add(SystemTray.MenuItemDef.Item("喂食 (猫粮)", FeedPet));
            var reminder = Demo.DobeCat.Game.Pet.PetCompanionReminder.Instance;
            if (reminder != null && reminder.PomodoroActive)
                items.Add(SystemTray.MenuItemDef.Item("⏹ 取消番茄钟",
                    () => Demo.DobeCat.Game.Pet.PetCompanionReminder.Instance?.StopPomodoro()));
            else
                items.Add(SystemTray.MenuItemDef.Item("🍅 启动番茄钟 (25min)",
                    () => Demo.DobeCat.Game.Pet.PetCompanionReminder.Instance?.StartPomodoro()));
            // 自定义闹钟（格式：先复制 "HH:mm 备注" 到剪贴板再点击）
            if (reminder != null)
            {
                items.Add(SystemTray.MenuItemDef.Item(
                    $"⏰ 闹钟: {reminder.GetAlarmsDisplay()}  [粘贴时间设置]",
                    SetAlarmFromClipboard));
                if (reminder.GetAlarmsDisplay() != "无闹钟")
                    items.Add(SystemTray.MenuItemDef.Item("❌ 清除所有闹钟",
                        () => { reminder.ClearAlarms(); RebuildMenu(); }));
            }
            items.Add(SystemTray.MenuItemDef.Item("农场", () => Demo.DobeCat.Game.Farm.FarmWorldController.ToggleVisibility()));
            items.Add(SystemTray.MenuItemDef.Item("商店", () => Demo.DobeCat.Game.Shop.ShopWindow.Instance?.Toggle()));
            items.Add(SystemTray.MenuItemDef.Item("弹幕测试面板", () => Demo.DobeCat.Sys.UI.DobeCatTestPanel.Toggle()));
            items.Add(SystemTray.MenuItemDef.Item("⚙ 设置", () => Demo.DobeCat.Sys.UI.DobeCatSettingsPanel.Toggle()));
            items.Add(SystemTray.MenuItemDef.Separator());
            var autoLabel = Demo.DobeCat.Sys.Platform.Windows.AutoStartManager.IsEnabled
                ? "✔ 开机自启" : "  开机自启";
            items.Add(SystemTray.MenuItemDef.Item(autoLabel,
                () => Demo.DobeCat.Sys.Platform.Windows.AutoStartManager.Toggle()));
            items.Add(SystemTray.MenuItemDef.Separator());
            items.Add(SystemTray.MenuItemDef.Item("退出 (Ctrl+Shift+Q)", Quit));

            _tray.SetItems(items);
        }

        private void FeedPet()
        {
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;

            // Try to consume 1 cat_food from player inventory
            var res = ep.TriggerEventMethod("InventoryRemove",
                new List<object> { "player", Demo.DobeCat.Game.Shop.DobeCatShopSetup.FOOD_CAT_FOOD, 1 });
            if (!EssSystem.Core.Base.Util.ResultCode.IsOk(res))
            {
                Demo.DobeCat.Game.Pet.PetSpeechBubble.Instance?.Show("没有猫粮了……", 2.5f);
                return;
            }

            // Reduce Hunger on the pet entity
            var needs = Ai?.Entity?.Get<INeeds>();
            needs?.Add("Hunger", -0.4f);

            // Add affection
            Demo.DobeCat.Game.Pet.PetAffectionController.Instance?.Add(5f);

            // Show bubble
            Demo.DobeCat.Game.Pet.PetSpeechBubble.Instance?.Show("谢谢喂食！ >\'ω\'<", 3f);
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

        private void SetAlarmFromClipboard()
        {
            var r = Demo.DobeCat.Game.Pet.PetCompanionReminder.Instance;
            if (r == null) return;
            var text = GUIUtility.systemCopyBuffer?.Trim();
            if (r.AddAlarmFromString(text))
            {
                Demo.DobeCat.Game.Pet.PetSpeechBubble.Instance?.Show($"闹钟已设置：{text}", 3f);
                RebuildMenu();
            }
            else
            {
                Demo.DobeCat.Game.Pet.PetSpeechBubble.Instance?.Show(
                    "格式错误！请先复制 \"HH:mm 备注\" 再点击", 4f);
            }
        }

        private void ToggleWindowCaptureMode()
        {
            var next = !Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.IsWindowCaptureMode;
            Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.SetWindowCaptureMode(next);
            RebuildMenu();
        }

        private void ToggleBackground()
        {
            var bg = Demo.DobeCat.Game.Pet.PetBackgroundLayer.Instance;
            if (bg == null) return;
            bg.SetVisible(!bg.Visible);
            RebuildMenu();
        }

        private void SetScale(float factor)
        {
            Demo.DobeCat.Game.Pet.PetScaleController.Instance?.SetScale(factor);
            RebuildMenu();
        }

        /// <summary>根据当前值是否匹配目标档位决定显示 ✔ 还是空格。</summary>
        private static string Mark(float current, float target, string text)
            => Mathf.Approximately(current, target) ? $"✔ {text}" : $"  {text}";

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
