using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;

namespace EssSystem.Core.Application.SingleManagers.DialogueManager
{
    /// <summary>
    /// 对话 UI 内部子组件引用集合 — <see cref="DialogueUIBuilder"/> 构建后回传给
    /// <see cref="DialogueManager"/>，供 <c>EVT_LINE_CHANGED</c> 时原地刷新。
    /// </summary>
    internal sealed class DialogueUIRefs
    {
        public UIPanelComponent Root;
        public UIPanelComponent Background;     // 主背景图层（行级 BackgroundSpriteId 会写到这里）
        public UIPanelComponent Portrait;       // 立绘
        public UITextComponent  SpeakerText;
        public UITextComponent  BodyText;
        public UIButtonComponent NextButton;
        public UIButtonComponent CloseButton;
        public UIButtonComponent[] OptionButtons;   // 长度 = config.Options.MaxOptions；隐藏未用的
    }
}
