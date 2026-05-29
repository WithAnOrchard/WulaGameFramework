using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.UIManager
{
    /// <summary>
    /// 通用独立 ConstantPixelSize Canvas 工厂。
    /// <para>为需要"1 canvas px = 1 screen px"的 UI 层（如桌宠覆盖层）提供懒加载 Canvas。</para>
    /// <para>与 UIManager 的 ScaleWithScreenSize Canvas 并存，避免参考分辨率缩放导致文字模糊。</para>
    /// 用法：
    /// <code>var canvasT = OverlayCanvasProvider.GetOrCreate("MyOverlay", sortingOrder: 50);</code>
    /// </summary>
    public static class OverlayCanvasProvider
    {
        /// <summary>
        /// 返回（或懒加载创建）指定名称的 ConstantPixelSize Canvas。
        /// 同一 <paramref name="canvasName"/> 多次调用返回同一实例。
        /// </summary>
        /// <param name="canvasName">Canvas GameObject 名称，同时作为缓存 key。</param>
        /// <param name="sortingOrder">Canvas sortingOrder（默认 50，高于 UIManager 默认 0）。</param>
        /// <param name="pixelPerfect">是否开启整数像素对齐（默认 true）。</param>
        public static Transform GetOrCreate(string canvasName, int sortingOrder = 50, bool pixelPerfect = true)
        {
            var existing = GameObject.Find(canvasName);
            if (existing != null && existing.GetComponent<Canvas>() != null)
                return existing.transform;

            var go = new GameObject(canvasName);
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            canvas.pixelPerfect = pixelPerfect;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor  = 1f;

            go.AddComponent<GraphicRaycaster>();

            Debug.Log($"[OverlayCanvasProvider] 已创建 ConstantPixelSize Canvas: {canvasName} (order={sortingOrder})");
            return go.transform;
        }
    }
}
