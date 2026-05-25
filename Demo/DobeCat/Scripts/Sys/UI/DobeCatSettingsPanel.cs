using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// Static controller for the DobeCat settings panel (DESIGN.md §M5).
    /// Delegates rendering to <see cref="DobeCatSettingsPanelView"/>.
    /// </summary>
    public static class DobeCatSettingsPanel
    {
        private static DobeCatSettingsPanelView EnsureView()
        {
            if (DobeCatSettingsPanelView.Instance != null) return DobeCatSettingsPanelView.Instance;
            var go = new GameObject("DobeCatSettingsPanelView");
            Object.DontDestroyOnLoad(go);
            return go.AddComponent<DobeCatSettingsPanelView>();
        }

        public static void Toggle()
        {
            var v = EnsureView();
            if (v.IsOpen) v.Hide(); else v.Show();
        }

        public static void Open()  => EnsureView().Show();
        public static void Close() => EnsureView().Hide();
    }
}
