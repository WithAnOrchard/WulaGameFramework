using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Demo.DobeCat.Sys.Platform.Windows
{
    /// <summary>
    /// 单实例守卫：利用 Windows 命名互斥量确保桌宠只能运行一份。
    /// <para>若已有实例正在运行，弹出 Win32 MessageBox 提示后立即退出。</para>
    /// <para>挂到场景中最先初始化的 GameObject，<see cref="DefaultExecutionOrderAttribute"/>
    /// 保证在所有其他组件 Awake 之前运行。</para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public class PetSingleInstanceGuard : MonoBehaviour
    {
        private const string MutexName = "Global\\DobeCat_SingleInstance_Mutex";

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int MessageBox(System.IntPtr hWnd, string text, string caption, uint type);

        private const uint MB_OK              = 0x00000000u;
        private const uint MB_ICONINFORMATION = 0x00000040u;
#endif

        private static Mutex _mutex;

        private void Awake()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            bool createdNew;
            _mutex = new Mutex(initiallyOwned: true, MutexName, out createdNew);

            if (!createdNew)
            {
                MessageBox(System.IntPtr.Zero,
                    "DobeCat 桌宠已在运行中，不能重复启动。\n请在系统托盘中找到它。",
                    "DobeCat — 已在运行",
                    MB_OK | MB_ICONINFORMATION);
                Application.Quit();
                return;
            }
#endif
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            try { _mutex?.ReleaseMutex(); _mutex?.Dispose(); } catch { }
            _mutex = null;
#endif
        }
    }
}

