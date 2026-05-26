using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 番茄钟独立面板的静态控制器，供托盘菜单等外部代码调用。
    /// </summary>
    public static class DobeCatPomodoroPanel
    {
        private static DobeCatPomodoroPanelView EnsureView()
        {
            if (DobeCatPomodoroPanelView.Instance != null) return DobeCatPomodoroPanelView.Instance;
            var go = new GameObject("DobeCatPomodoroPanelView");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<DobeCatPomodoroPanelView>();
        }

        public static void Toggle() => EnsureView().Toggle();
        public static void Open()   => EnsureView().Show();
        public static void Close()  => EnsureView().Hide();
    }
}
