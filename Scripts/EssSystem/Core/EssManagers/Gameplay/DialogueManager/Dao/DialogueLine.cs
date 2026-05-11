using System;
using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.DialogueManager.Dao
{
    /// <summary>
    /// 一条对话行 — 单段说话者 + 文本 + 可选选项 + 背景/立绘覆盖。
    /// <para>
    /// 推进规则：<br/>
    /// 1. 当 <see cref="Options"/> 非空 —— UI 显示选项按钮，<see cref="NextLineId"/> 被忽略；<br/>
    /// 2. 当 <see cref="Options"/> 为空 —— UI 显示"下一句"按钮：
    ///    <list type="bullet">
    ///      <item>有 <see cref="NextLineId"/> → 跳到该行</item>
    ///      <item>否则 → 跳到 <c>Dialogue.Lines</c> 中的下一条；列表末尾 → 结束对话</item>
    ///    </list>
    /// </para>
    /// </summary>
    [Serializable]
    public class DialogueLine
    {
        /// <summary>行 Id（同对话内唯一）。</summary>
        public string Id;

        /// <summary>说话者名称（UI 标题文本）。</summary>
        public string Speaker;

        /// <summary>正文文本（支持多行）。</summary>
        public string Text;

        /// <summary>本行的背景 Sprite ID（覆盖对话/配置默认背景）；为空则用对话默认。</summary>
        public string BackgroundSpriteId;

        /// <summary>本行的立绘 Sprite ID；为空则隐藏立绘。</summary>
        public string PortraitSpriteId;

        /// <summary>显式跳转的下一行 Id；为空 + 列表已是最后一行 → 结束。</summary>
        public string NextLineId;

        /// <summary>选项列表；非空时 UI 显示选项按钮取代「下一句」按钮。</summary>
        public List<DialogueOption> Options = new List<DialogueOption>();

        public DialogueLine() { }

        public DialogueLine(string id, string speaker = null, string text = null)
        {
            Id = id;
            Speaker = speaker ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public bool HasOptions => Options != null && Options.Count > 0;

        public DialogueLine WithSpeaker(string speaker) { Speaker = speaker ?? string.Empty; return this; }
        public DialogueLine WithText(string text) { Text = text ?? string.Empty; return this; }
        public DialogueLine WithBackground(string spriteId) { BackgroundSpriteId = spriteId; return this; }
        public DialogueLine WithPortrait(string spriteId) { PortraitSpriteId = spriteId; return this; }
        public DialogueLine WithNextLine(string lineId) { NextLineId = lineId; return this; }
        public DialogueLine AddOption(DialogueOption option)
        {
            if (option == null) return this;
            Options ??= new List<DialogueOption>();
            Options.Add(option);
            return this;
        }
        public DialogueLine AddOption(string text, string nextDialogueId = null, string nextLineId = null)
            => AddOption(new DialogueOption(text)
                .WithNextDialogue(nextDialogueId)
                .WithNextLine(nextLineId));
    }
}
