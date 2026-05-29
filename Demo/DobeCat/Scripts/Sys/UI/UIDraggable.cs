namespace Demo.DobeCat.Sys.UI
{
    /// <summary>
    /// 向后兼容包装器，继承自 <see cref="EssSystem.Core.Presentation.UIManager.UIDraggable"/>。
    /// 实现已迁移至 EssSystem；本类仅用于保持场景序列化引用不变。
    /// 新代码请直接使用 <see cref="EssSystem.Core.Presentation.UIManager.UIManager"/> 事件或
    /// <see cref="EssSystem.Core.Presentation.UIManager.Entity.UIWindowBehavior"/>。
    /// </summary>
    public class UIDraggable : EssSystem.Core.Presentation.UIManager.UIDraggable { }
}
