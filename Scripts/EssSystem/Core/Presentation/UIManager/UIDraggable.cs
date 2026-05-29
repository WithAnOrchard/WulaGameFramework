using EssSystem.Core.Presentation.UIManager.Entity;
using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager
{
    /// <summary>
    /// 已迁移至 <see cref="UIWindowBehavior"/>（拖拽移动 + 边缘缩放 + 滚轮缩放 + 双击复位）。
    /// 本组件现为向后兼容包装器，Awake 时自动添加 UIWindowBehavior 并转发参数。
    /// 新代码请直接使用 <see cref="UIWindowBehavior"/>。
    /// </summary>
    [DisallowMultipleComponent]
    public class UIDraggable : MonoBehaviour
    {
        [Tooltip("拖动 / 缩放时实际操作的目标（留空 = 本 GameObject 自身）。")]
        public RectTransform DragTarget;

        [Tooltip("滚轮每步缩放百分比（默认 0.10 = 10%）。")]
        public float ScaleStep = 0.10f;

        [Tooltip("最小缩放比（默认 0.5 = 50%）。")]
        public float ScaleMin = 0.5f;

        [Tooltip("最大缩放比（默认 2.0 = 200%）。")]
        public float ScaleMax = 2.0f;

        private void Awake()
        {
            var wb       = gameObject.GetComponent<UIWindowBehavior>()
                           ?? gameObject.AddComponent<UIWindowBehavior>();
            wb.Target    = DragTarget;
            wb.ScaleStep = ScaleStep;
            wb.ScaleMin  = ScaleMin;
            wb.ScaleMax  = ScaleMax;
        }
    }
}
