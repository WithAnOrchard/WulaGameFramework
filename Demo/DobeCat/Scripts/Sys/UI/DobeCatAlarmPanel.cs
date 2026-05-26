using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>闹钟管理面板的静态控制器，供托盘菜单调用。</summary>
    public static class DobeCatAlarmPanel
    {
        private static DobeCatAlarmPanelView EnsureView()
        {
            if (DobeCatAlarmPanelView.Instance != null) return DobeCatAlarmPanelView.Instance;
            var go = new GameObject("DobeCatAlarmPanelView");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<DobeCatAlarmPanelView>();
        }

        public static void Toggle() => EnsureView().Toggle();
        public static void Open()   => EnsureView().Show();
        public static void Close()  => EnsureView().Hide();
    }
}
