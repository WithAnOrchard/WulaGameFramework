using EssSystem.Core.UI.Dao.CommonComponents;

namespace EssSystem.EssManager.InventoryManager
{
    /// <summary>
    /// 一个已打开 inventory UI 中所有 slot 的子组件引用集合（图标 / 名称 / 数量），
    /// 由 <see cref="InventoryUIBuilder.BuildPanelTree"/> 产出，
    /// 供 <c>EVT_CHANGED</c> 广播 / 点击回调原地刷新使用。
    /// </summary>
    public class SlotUIRefs
    {
        public UIPanelComponent[] Icons;
        public UITextComponent[]  Names;
        public UITextComponent[]  Stacks;
    }

    /// <summary>
    /// 描述子面板的四件套引用 + 占位文本。点击 slot 后由
    /// <see cref="InventoryUIBuilder.ApplyItemToDesc"/> 填充。
    /// </summary>
    public class DescUIRefs
    {
        public UIPanelComponent Icon;
        public UITextComponent  Name;
        public UITextComponent  Stack;
        public UITextComponent  Description;
        public string EmptyPlaceholder;
    }
}
