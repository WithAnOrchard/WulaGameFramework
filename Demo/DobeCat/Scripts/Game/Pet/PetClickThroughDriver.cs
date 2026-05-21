using UnityEngine;
using Demo.DobeCat.Sys.Platform.Windows;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 每帧根据鼠标是否覆盖桌宠，把 <see cref="DesktopWindow.SetClickThrough"/> 切到合适状态。
    /// <para>
    /// 注意：当窗口处于 click-through 时 Unity 不再收到鼠标事件，因此判断使用
    /// <see cref="DesktopWindow.GetGlobalCursorScreenPos"/>（基于 Win32 GetCursorPos）兜底。
    /// </para>
    /// </summary>
    public class PetClickThroughDriver : MonoBehaviour
    {
        public PetDragger Dragger;
        public PetView View;

        [Tooltip("命中边界向外膨胀的像素值（避免边缘抖动）。")]
        public float HitPaddingPixels = 4f;

        private void Update()
        {
            var win = DesktopWindow.Instance;
            if (win == null || View == null) return;

            // 拖拽中绝不能穿透，否则鼠标会瞬间脱钩。
            if (Dragger != null && Dragger.IsDragging)
            {
                win.SetClickThrough(false);
                return;
            }

            var screenPos = win.GetGlobalCursorScreenPos();
            var hit = HitTestSpriteBounds(screenPos);
            win.SetClickThrough(!hit);
        }

        private bool HitTestSpriteBounds(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return false;
            var z = Mathf.Abs(cam.transform.position.z);
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
            var b = View.WorldBounds;
            // 转 padding（像素 → 世界单位）
            var pxToWorld = (cam.orthographic ? cam.orthographicSize * 2f / Screen.height : 0f);
            var pad = HitPaddingPixels * pxToWorld;
            b.Expand(new Vector3(pad * 2f, pad * 2f, 0f));
            return b.Contains(new Vector3(world.x, world.y, b.center.z));
        }
    }
}
