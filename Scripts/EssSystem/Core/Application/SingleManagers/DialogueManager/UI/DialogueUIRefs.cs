using UnityEngine;
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
        public UIPanelComponent Portrait;       // 立绘 / 头像框（始终显示作为视觉占位）
        public Color            PortraitFrameColor;  // 无 sprite 时显示的框色
        public UITextComponent  SpeakerText;
        public UITextComponent  BodyText;
        public UIButtonComponent NextButton;
        public UIButtonComponent CloseButton;
        public UIButtonComponent[] OptionButtons;   // 长度 = config.Options.MaxOptions；隐藏未用的
        public UITextComponent[]   OptionTexts;     // 与 OptionButtons 同长，超采样的文本覆盖层
    }
}
