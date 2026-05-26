using UnityEngine;
using UnityEngine.UI;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// DobeCat 专属 UI Canvas（ConstantPixelSize，1 canvas unit = 1 screen pixel）。
    /// 独立于 UIManager 的 ScaleWithScreenSize 游戏 Canvas，避免因参考分辨率缩放导致文字模糊。
    /// </summary>
    public static class DobeCatCanvasProvider
    {
        private static Transform _canvasTransform;

        /// <summary>返回（或懒加载创建）DobeCat 专属 Canvas 的 Transform。</summary>
        public static Transform GetOrCreate()
        {
            if (_canvasTransform != null) return _canvasTransform;

            var go = new GameObject("DobeCatUICanvas");
            Object.DontDestroyOnLoad(go);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;       // 高于 UIManager 默认 sortingOrder=0
            canvas.pixelPerfect = true;     // 整数像素对齐，字体/图像更清晰

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor  = 1f;       // 1 canvas px = 1 screen px，无任何缩放

            go.AddComponent<GraphicRaycaster>();

            _canvasTransform = go.transform;
            Debug.Log("[DobeCatCanvasProvider] 已创建独立 ConstantPixelSize Canvas");
            return _canvasTransform;
        }

        /// <summary>应用退出时清理引用（避免跨场景残留）。</summary>
        public static void Reset() => _canvasTransform = null;
    }
}
