using UnityEngine;

namespace EssSystem.Core.Base.Util
{
    /// <summary>
    /// 世界坐标 → Screen-space UI 投影工具 —— 统一"HUD 跟随世界物体"的重复逻辑。
    /// <para><see cref="BuildingCostHud"/> 和 <see cref="TribeEnemyHealthUI"/> 等
    /// 均使用 Camera.WorldToScreenPoint → 写 anchoredPosition 模式，本类将其抽为一次调用。</para>
    /// </summary>
    public static class UIWorldFollower
    {
        /// <summary>
        /// 将世界坐标投影到 Canvas 本地坐标，同时自动管理可见性（摄像机背后 / 超出屏幕 → 隐藏）。
        /// </summary>
        /// <param name="camera">主摄像机。</param>
        /// <param name="canvasRect">Canvas RectTransform（ScreenSpace-Overlay 模式）。</param>
        /// <param name="targetRect">要定位的 UI RectTransform。</param>
        /// <param name="worldPos">世界坐标（含偏移后的位置）。</param>
        /// <param name="screenOffset">额外的屏幕空间偏移（像素/参考分辨率）。</param>
        /// <param name="viewportMargin">视口边缘容差（默认 0.05，即超出屏幕 5% 仍可见）。</param>
        /// <returns>是否可见（在屏幕范围内且不在摄像机背后）。</returns>
        public static bool UpdatePosition(
            Camera camera,
            RectTransform canvasRect,
            RectTransform targetRect,
            Vector3 worldPos,
            Vector2 screenOffset = default,
            float viewportMargin = 0.05f)
        {
            if (camera == null || canvasRect == null || targetRect == null) return false;

            var viewport = camera.WorldToViewportPoint(worldPos);
            var visible = viewport.z > 0f
                && viewport.x > -viewportMargin && viewport.x < 1f + viewportMargin
                && viewport.y > -viewportMargin && viewport.y < 1f + viewportMargin;

            if (targetRect.gameObject.activeSelf != visible)
                targetRect.gameObject.SetActive(visible);
            if (!visible) return false;

            // Viewport → Canvas local（适用于 anchor=(0,0) 或 ScreenSpace-Overlay）
            var size = canvasRect.rect.size;
            targetRect.anchoredPosition = new Vector2(
                viewport.x * size.x + screenOffset.x,
                viewport.y * size.y + screenOffset.y);

            return true;
        }

        /// <summary>
        /// <see cref="UpdatePosition"/> 的简化版：使用 ScreenPointToLocalPointInRectangle
        /// 做 Overlay Canvas 精确投影（无额外 screenOffset）。
        /// </summary>
        public static bool UpdatePositionOverlay(
            Camera camera,
            RectTransform canvasRect,
            RectTransform targetRect,
            Vector3 worldPos)
        {
            if (camera == null || canvasRect == null || targetRect == null) return false;

            var screenPos = camera.WorldToScreenPoint(worldPos);
            var visible = screenPos.z > 0f;

            if (targetRect.gameObject.activeSelf != visible)
                targetRect.gameObject.SetActive(visible);
            if (!visible) return false;

            // ScreenSpace-Overlay Canvas 传 null camera
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect, screenPos, null, out var local))
            {
                targetRect.anchoredPosition = local;
            }
            return true;
        }
    }
}
