using UnityEngine;

namespace Demo.DobeCat.Tray
{
    /// <summary>
    /// DobeCat 桌宠的系统托盘集成器（仅 Windows 编译生效）。
    /// <list type="bullet">
    /// <item>右键菜单：显示 / 隐藏、重置位置、置顶切换、退出</item>
    /// <item>双击：切换显示 / 隐藏</item>
    /// </list>
    /// </summary>
    public class DobeCatTray : MonoBehaviour
    {
        [Tooltip("桌宠根 GameObject（隐藏 / 显示用）。")]
        public GameObject PetRoot;

        [Tooltip("重置位置时回到的世界坐标（默认 0,0,0）。")]
        public Vector3 ResetPosition = Vector3.zero;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private SystemTray _tray;
        private bool _petVisible = true;

        private void Start()
        {
            _tray = new SystemTray { Tooltip = "DobeCat 桌宠" };
            _tray.AddItem("显示 / 隐藏", TogglePetVisible);
            _tray.AddItem("重置位置", ResetPetPosition);
            _tray.AddItem("弹幕测试面板", () => Demo.DobeCat.UI.DobeCatTestPanel.Toggle());
            _tray.AddSeparator();
            _tray.AddItem("退出 (Ctrl+Shift+Q)", Quit);
            _tray.OnDoubleClick += TogglePetVisible;
            _tray.Start();
        }

        private void Update()
        {
            _tray?.PumpMainThread();
        }

        private void OnApplicationQuit() => _tray?.Dispose();
        private void OnDestroy() => _tray?.Dispose();

        // ── 菜单回调（在 Unity 主线程执行） ──

        private void TogglePetVisible()
        {
            if (PetRoot == null) return;
            _petVisible = !_petVisible;
            PetRoot.SetActive(_petVisible);
        }

        private void ResetPetPosition()
        {
            if (PetRoot == null) return;
            PetRoot.transform.position = ResetPosition;
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
