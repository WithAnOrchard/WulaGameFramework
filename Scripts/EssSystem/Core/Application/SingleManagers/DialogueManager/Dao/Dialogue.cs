using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.SingleManagers.DialogueManager.Dao
{
    /// <summary>
    /// 一段完整对话 — 一组按顺序排列的 <see cref="DialogueLine"/>。
    /// <para>
    /// 默认从 <see cref="FirstLineId"/>（缺省 = <c>Lines[0].Id</c>）开始；行的推进/结束规则见
    /// <see cref="DialogueLine"/> 与 <see cref="DialogueOption"/>。
    /// </para>
    /// </summary>
    [Serializable]
    public class Dialogue
    {
        /// <summary>对话 Id（全局唯一，注册到 <c>DialogueService</c>）。</summary>
        public string Id;

        /// <summary>展示名 / 调试名。</summary>
        public string Name;

        /// <summary>整段对话的默认背景 Sprite ID；可被行级 <see cref="DialogueLine.BackgroundSpriteId"/> 覆盖。</summary>
        public string DefaultBackgroundSpriteId;

        /// <summary>对话使用的 UI 配置 Id（对应 <c>DialogueService</c> 中注册的 <see cref="Specs.DialogueConfig"/>）。</summary>
        public string ConfigId;

        /// <summary>对话起始行 Id；为空时使用 <c>Lines[0].Id</c>。</summary>
        public string FirstLineId;

        /// <summary>所有行（顺序敏感 — 缺省 NextLineId 时按列表顺序推进）。</summary>
        public List<DialogueLine> Lines = new List<DialogueLine>();

        public Dialogue() { }

        public Dialogue(string id, string name = null)
        {
            Id = id;
            Name = name ?? id;
        }

        public Dialogue WithDefaultBackground(string spriteId) { DefaultBackgroundSpriteId = spriteId; return this; }
        public Dialogue WithConfig(string configId) { ConfigId = configId; return this; }
        public Dialogue WithFirstLine(string lineId) { FirstLineId = lineId; return this; }
        public Dialogue AddLine(DialogueLine line)
        {
            if (line == null) return this;
            Lines ??= new List<DialogueLine>();
            Lines.Add(line);
            return this;
        }

        /// <summary>按 Id 查行；不存在返回 null。</summary>
        public DialogueLine GetLine(string lineId)
        {
            if (string.IsNullOrEmpty(lineId) || Lines == null) return null;
            for (var i = 0; i < Lines.Count; i++)
                if (Lines[i] != null && Lines[i].Id == lineId) return Lines[i];
            return null;
        }

        /// <summary>列表中给定行的下一行；不存在返回 null（= 对话结束）。</summary>
        public DialogueLine GetNextLineInList(string lineId)
        {
            if (Lines == null) return null;
            for (var i = 0; i < Lines.Count - 1; i++)
                if (Lines[i] != null && Lines[i].Id == lineId) return Lines[i + 1];
            return null;
        }

        /// <summary>取首行；空对话返回 null。</summary>
        public DialogueLine GetFirstLine()
        {
            if (Lines == null || Lines.Count == 0) return null;
            return !string.IsNullOrEmpty(FirstLineId) ? GetLine(FirstLineId) : Lines[0];
        }
    }
}
