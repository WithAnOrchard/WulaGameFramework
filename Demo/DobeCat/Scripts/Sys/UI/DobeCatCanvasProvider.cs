using EssSystem.Core.Presentation.UIManager;
using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>DobeCat 专属 Canvas 提供者（兼容入口，实现已迁移至 OverlayCanvasProvider）。</summary>
    public static class DobeCatCanvasProvider
    {
        public static Transform GetOrCreate() =>
            OverlayCanvasProvider.GetOrCreate("DobeCatUICanvas", 50);
    }
}
