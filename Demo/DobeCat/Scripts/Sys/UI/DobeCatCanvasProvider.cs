using EssSystem.Core.Presentation.UIManager;
using UnityEngine;

namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// DobeCat 专属 UI Canvas 提供者。
    /// 实现已迁移至 <see cref="OverlayCanvasProvider"/>，本类保留为兼容入口。
    /// </summary>
    public static class DobeCatCanvasProvider
    {
        private const string CanvasName   = "DobeCatUICanvas";
        private const int    SortingOrder = 50;

        /// <summary>返回（或懒加载创建）DobeCat 专属 ConstantPixelSize Canvas 的 Transform。</summary>
        public static Transform GetOrCreate() =>
            OverlayCanvasProvider.GetOrCreate(CanvasName, SortingOrder);
    }
}
