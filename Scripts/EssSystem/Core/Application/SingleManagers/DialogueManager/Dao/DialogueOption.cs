using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.SingleManagers.DialogueManager.Dao
{
    /// <summary>
    /// 对话选项 — 一条点击后可触发事件 / 跳到指定行 / 切换到另一段对话的可选项。
    /// <para>
    /// 三类副作用可任意组合（执行顺序：<c>EventName</c> 广播 → <c>OnSelected</c> 回调 → 跳转）：<br/>
    /// 1. 广播 <c>EventName(EventArgs...)</c> —— 适合配置驱动 / 跨模块解耦<br/>
    /// 2. 触发 <c>OnSelected</c> 回调 —— 适合代码运行时构建对话<br/>
    /// 3. 根据 <c>NextDialogueId</c> / <c>NextLineId</c> 跳转：
    ///    <list type="bullet">
    ///      <item>两者都为空 → 结束对话</item>
    ///      <item>仅 <c>NextLineId</c> → 同对话内跳转</item>
    ///      <item>仅 <c>NextDialogueId</c> → 切到新对话首行</item>
    ///      <item>都设置 → 切到新对话指定行</item>
    ///    </list>
    /// </para>
    /// </summary>
    [Serializable]
    public class DialogueOption
    {
        /// <summary>选项按钮显示文本</summary>
        public string Text;

        /// <summary>切换到的对话 Id；为空表示停留在当前对话。</summary>
        public string NextDialogueId;

        /// <summary>跳转到的行 Id；为空 + NextDialogueId 也为空 → 结束对话。</summary>
        public string NextLineId;

        /// <summary>选中后广播的事件名（供配置驱动使用，可为空）。</summary>
        public string EventName;

        /// <summary>广播事件的参数（字符串列表，调用方自行解析）。</summary>
        public List<string> EventArgs = new List<string>();

        /// <summary>选中时同步触发的运行时回调（不参与序列化，供代码构建对话时使用）。</summary>
        [NonSerialized] public Action OnSelected;

        public DialogueOption() { }

        public DialogueOption(string text)
        {
            Text = text ?? string.Empty;
        }

        public DialogueOption WithText(string text) { Text = text ?? string.Empty; return this; }
        public DialogueOption WithNextDialogue(string dialogueId) { NextDialogueId = dialogueId; return this; }
        public DialogueOption WithNextLine(string lineId) { NextLineId = lineId; return this; }
        public DialogueOption WithEvent(string eventName, params string[] args)
        {
            EventName = eventName;
            EventArgs = args == null ? new List<string>() : new List<string>(args);
            return this;
        }
        public DialogueOption WithCallback(Action callback) { OnSelected = callback; return this; }
    }
}
