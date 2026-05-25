using System;
using System.Collections.Generic;
using Demo.DobeCat.Sys.Platform.Windows;
using UnityEngine;
using UnityEngine.EventSystems;
using RaycastList = System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>;

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
        /// <summary>
        /// 其它世界对象（如农场格子）可将自己的水平屏幕坐标命中测试居子加到此列表，
        /// PetClickThroughDriver 每帧将一并检查。<c>Vector2</c> = 屏幕像素坐标。
        /// </summary>
        public static readonly List<Func<Vector2, bool>> AdditionalHitTests =
            new List<Func<Vector2, bool>>();

        public PetDragger Dragger;
        public PetView View;

        [Tooltip("命中边界向外膨胀的像素值（避免边缘抖动）。")]
        public float HitPaddingPixels = 4f;

        // 缓存，避免每帧 GC
        private PointerEventData _uiPointerData;
        private readonly RaycastList _uiRaycastResults = new RaycastList();

        private void Update()
        {
            if (View == null) return;

            // 窗口捕捉模式下窗口有边框可被点击，不需要穿透逻辑
            if (DesktopOverlay.IsWindowCaptureMode)
            {
                DesktopOverlay.SetClickThrough(false);
                return;
            }

            // 拖拽中绝不穿透，否则鼠标会瞬间脱钩
            if (Dragger != null && Dragger.IsDragging)
            {
                DesktopOverlay.SetClickThrough(false);
                return;
            }

            // Win32 直读光标位置（WS_EX_TRANSPARENT 时 Input.mousePosition 不可用）
            var screenPos = DesktopOverlay.GetGlobalCursorScreenPos();

            // uGUI 命中检测：把 Win32 光标坐标注入 EventSystem
            if (EventSystem.current != null)
            {
                if (_uiPointerData == null)
                    _uiPointerData = new PointerEventData(EventSystem.current);
                _uiPointerData.position = screenPos;
                _uiRaycastResults.Clear();
                EventSystem.current.RaycastAll(_uiPointerData, _uiRaycastResults);
                if (_uiRaycastResults.Count > 0)
                {
                    DesktopOverlay.SetClickThrough(false);
                    return;
                }
            }

            // 桌宠精灵 + 额外注册区域（IMGUI 窗口等）
            var hit = HitTestSpriteBounds(screenPos);
            if (!hit)
                for (var i = 0; i < AdditionalHitTests.Count; i++)
                    if (AdditionalHitTests[i]?.Invoke(screenPos) == true) { hit = true; break; }

            DesktopOverlay.SetClickThrough(!hit);
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
