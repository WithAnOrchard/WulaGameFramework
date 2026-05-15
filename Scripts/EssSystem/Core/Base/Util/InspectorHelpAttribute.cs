using System;
using UnityEngine;

namespace EssSystem.Core.Base.Util
{
    /// <summary>
    /// 在 Inspector 中字段上方直接绘制一个 HelpBox（说明文字默认始终可见，无需悬停）。
    /// <para>
    /// 用法：<c>[InspectorHelp("说明文字...")]</c> 加在序列化字段前。<br/>
    /// 多个 <c>[InspectorHelp]</c> 可叠加，会依次绘制多块。
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true, Inherited = true)]
    public class InspectorHelpAttribute : PropertyAttribute
    {
        /// <summary>HelpBox 显示的多行文字（支持 \n 换行）。</summary>
        public readonly string Text;

        /// <summary>0 = None，1 = Info，2 = Warning，3 = Error（对应 UnityEditor.MessageType）。</summary>
        public readonly int MessageType;

        public InspectorHelpAttribute(string text, int messageType = 1)
        {
            Text = text;
            MessageType = messageType;
        }
    }
}
