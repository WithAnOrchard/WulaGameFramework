using UnityEngine;
#if UNITY_STANDALONE_WIN
using Microsoft.Win32;
#endif

namespace Demo.DobeCat.Sys.Platform.Windows
{
    /// <summary>
    /// Manages Windows auto-start via HKCU Run registry key.
    /// DESIGN.md §M5 — startup integration.
    /// </summary>
    public static class AutoStartManager
    {
        private const string REG_RUN  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string APP_NAME = "DobeCat";

        /// <summary>Returns true if the auto-start registry entry exists.</summary>
        public static bool IsEnabled
        {
            get
            {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                try
                {
                    using var key = Registry.CurrentUser.OpenSubKey(REG_RUN, false);
                    return key?.GetValue(APP_NAME) != null;
                }
                catch { return false; }
#else
                return false;
#endif
            }
        }

        /// <summary>Write the current executable path to the Run key.</summary>
        public static void Enable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                          ?? Application.dataPath.Replace("_Data", ".exe");
                using var key = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
                key?.SetValue(APP_NAME, $"\"{exe}\"");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AutoStart] Enable failed: {e.Message}");
            }
#endif
        }

        /// <summary>Remove the Run key entry.</summary>
        public static void Disable()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(REG_RUN, true);
                key?.DeleteValue(APP_NAME, throwOnMissingValue: false);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[AutoStart] Disable failed: {e.Message}");
            }
#endif
        }

        /// <summary>Toggle — enables if currently off, disables if on.</summary>
        public static void Toggle()
        {
            if (IsEnabled) Disable(); else Enable();
        }
    }
}
