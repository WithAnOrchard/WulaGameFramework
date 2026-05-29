#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using Microsoft.Win32;
using UnityEngine;

namespace Demo.DobeCat.Sys.Tray
{
    /// <summary>
    /// Windows 开机自启管理器。
    /// <para>通过注册表控制应用是否在系统启动时自动运行。</para>
    /// </summary>
    public static class AutoStartManager
    {
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DobeCat";

        /// <summary>获取应用是否启用开机自启。</summary>
        public static bool IsEnabled
        {
            get
            {
                try
                {
                    using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
                    {
                        if (key == null) return false;
                        var value = key.GetValue(AppName);
                        return value != null;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[AutoStartManager] 读取注册表失败: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>切换开机自启状态。</summary>
        public static void Toggle()
        {
            if (IsEnabled)
                Disable();
            else
                Enable();
        }

        /// <summary>启用开机自启。</summary>
        public static void Enable()
        {
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        key.SetValue(AppName, exePath);
                        Debug.Log("[AutoStartManager] 已启用开机自启");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutoStartManager] 启用开机自启失败: {ex.Message}");
            }
        }

        /// <summary>禁用开机自启。</summary>
        public static void Disable()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true))
                {
                    if (key != null)
                    {
                        key.DeleteValue(AppName, false);
                        Debug.Log("[AutoStartManager] 已禁用开机自启");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AutoStartManager] 禁用开机自启失败: {ex.Message}");
            }
        }
    }
}
#endif
