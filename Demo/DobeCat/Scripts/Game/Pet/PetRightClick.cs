using Demo.DobeCat.Sys.Tray;
using Demo.DobeCat.Sys.Platform.Windows;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠右键 → 调起托盘右键菜单（与系统右下角图标完全一致）。
    /// <list type="bullet">
    /// <item>采用 <see cref="DesktopWindow.GetGlobalCursorScreenPos"/> + <see cref="PetView.WorldBounds"/> 做命中检测，
    /// 兼容 click-through 状态下 Unity 收不到鼠标事件的场景。</item>
    /// <item>命中后调 <see cref="DobeCatTray.RequestShowMenu"/> 由 SystemTray 在自己线程 TrackPopupMenu。</item>
    /// </list>
    /// </summary>
    public class PetRightClick : MonoBehaviour
    {
        public PetView View;
        public DobeCatTray Tray;

        [Tooltip("命中边界向外膨胀的像素值（避免边缘抖动）。")]
        public float HitPaddingPixels = 4f;

        private bool _wasDown;

        private void Update()
        {
            if (View == null || Tray == null) return;

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            // Win32 GetAsyncKeyState VK_RBUTTON = 0x02。click-through 时 Unity 收不到鼠标事件，必须走 Win32。
            var down = (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(0x02) & 0x8000) != 0;
#else
            var down = Input.GetMouseButton(1);
#endif
            // 仅在按下→抬起的下降沿触发（也就是经典的"右键点击"语义）
            if (_wasDown && !down)
            {
                var win = DesktopWindow.Instance;
                var screenPos = win != null ? win.GetGlobalCursorScreenPos() : (Vector2)Input.mousePosition;
                if (HitTest(screenPos))
                {
                    Tray.RequestShowMenu();
                }
            }
            _wasDown = down;
        }

        private bool HitTest(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var z = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
            var b = View.WorldBounds;
            var pxToWorld = cam.orthographic ? cam.orthographicSize * 2f / Screen.height : 0f;
            var pad = HitPaddingPixels * pxToWorld;
            b.Expand(new Vector3(pad * 2f, pad * 2f, 0f));
            return b.Contains(new Vector3(world.x, world.y, b.center.z));
        }
    }
}
