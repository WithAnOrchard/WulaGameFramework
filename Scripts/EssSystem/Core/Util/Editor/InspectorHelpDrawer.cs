using EssSystem.Core.Util;
using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.Util.EditorTools
{
    /// <summary>
    /// <see cref="InspectorHelpAttribute"/> 的 DecoratorDrawer：在字段上方绘制 HelpBox。
    /// </summary>
    [CustomPropertyDrawer(typeof(InspectorHelpAttribute))]
    public class InspectorHelpDrawer : DecoratorDrawer
    {
        /// <summary>HelpBox 与下方字段的间距。</summary>
        private const float BottomSpacing = 4f;

        /// <summary>HelpBox 内文本左侧 icon 占位 + 边距，估算用。</summary>
        private const float IconReservedWidth = 36f;

        public override float GetHeight()
        {
            var attr = attribute as InspectorHelpAttribute;
            if (attr == null || string.IsNullOrEmpty(attr.Text)) return 0f;

            var style = EditorStyles.helpBox;
            // currentViewWidth 在 Layout 阶段是 Inspector 视图宽度
            var width = Mathf.Max(60f, EditorGUIUtility.currentViewWidth - IconReservedWidth);
            var height = style.CalcHeight(new GUIContent(attr.Text), width);
            // CalcHeight 偶尔贴边时文字溢出，给个最低值
            return Mathf.Max(height, EditorGUIUtility.singleLineHeight * 2f) + BottomSpacing;
        }

        public override void OnGUI(Rect position)
        {
            var attr = attribute as InspectorHelpAttribute;
            if (attr == null || string.IsNullOrEmpty(attr.Text)) return;

            var rect = new Rect(position.x, position.y,
                position.width, position.height - BottomSpacing);
            EditorGUI.HelpBox(rect, attr.Text, (MessageType)attr.MessageType);
        }
    }
}
