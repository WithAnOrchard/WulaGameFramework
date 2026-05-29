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

        [Tooltip("启用精灵像素级 alpha 穿透检测（需纹理开启 Read/Write）；关闭则仅用矩形包围盒。")]
        public bool UseAlphaHitTest = true;

        [Tooltip("低于此 alpha 视为透明区域（点击穿透）。")]
        [Range(0.01f, 1f)] public float AlphaThreshold = 0.1f;

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

            // AABB 层：先快速排除明显远离的属场外点
            var b = View.WorldBounds;
            var pxToWorld = cam.orthographic ? cam.orthographicSize * 2f / Screen.height : 0f;
            var pad = HitPaddingPixels * pxToWorld;
            b.Expand(new Vector3(pad * 2f, pad * 2f, 0f));
            if (!b.Contains(new Vector3(world.x, world.y, b.center.z))) return false;

            // 像素层：查找子节点中第一个有效的 SpriteRenderer，采样 alpha
            if (!UseAlphaHitTest) return true;
            return AlphaHitTest(world) ?? true; // 纹理不可读时退化为包围盒
        }

        /// <returns>返回像素 alpha 是否超阈値；纹理不可读时返回 null。</returns>
        private bool? AlphaHitTest(Vector3 worldPos)
        {
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr == null || sr.sprite == null) continue;
                if (!sr.enabled || !sr.gameObject.activeInHierarchy) continue;

                var sBounds = sr.bounds;
                if (!sBounds.Contains(new Vector3(worldPos.x, worldPos.y, sBounds.center.z)))
                    continue;

                var sprite = sr.sprite;
                var tex    = sprite.texture;
                if (tex == null) continue;

                // 转换到纹理像素坐标
                var local  = sr.transform.InverseTransformPoint(worldPos);
                var ppu    = sprite.pixelsPerUnit;
                var px     = Mathf.FloorToInt(sprite.rect.x + local.x * ppu + sprite.pivot.x);
                var py     = Mathf.FloorToInt(sprite.rect.y + local.y * ppu + sprite.pivot.y);
                if (px < 0 || px >= tex.width || py < 0 || py >= tex.height) continue;

                try
                {
                    return tex.GetPixel(px, py).a > AlphaThreshold;
                }
                catch (UnityException)
                {
                    return null; // 纹理未开启 Read/Write，退化为 AABB
                }
            }
            return null;
        }
    }
}
