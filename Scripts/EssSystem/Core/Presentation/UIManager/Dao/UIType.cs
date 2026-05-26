namespace EssSystem.Core.Presentation.UIManager.Dao
{
    /// <summary>
    ///     UI组件类型枚举
    /// </summary>
    public enum UIType
    {
        /// <summary>
        ///     按钮
        /// </summary>
        Button,

        /// <summary>
        ///     面板
        /// </summary>
        Panel,

        /// <summary>
        ///     文本
        /// </summary>
        Text,

        Bar,

        /// <summary>
        ///     可滚动容器（ScrollRect + VerticalLayoutGroup 内容区）
        /// </summary>
        ScrollView,

        /// <summary>
        ///     输入框（TMP_InputField）
        /// </summary>
        InputField
    }

    /// <summary>
    ///     UI组件类型扩展方法
    /// </summary>
    public static class UITypeExtensions
    {
        /// <summary>
        ///     获取UI类型的显示名称
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <returns>显示名称</returns>
        public static string GetDisplayName(this UIType uiType)
        {
            return uiType switch
            {
                UIType.Button => "按钮",
                UIType.Panel => "面板",
                UIType.Text => "文本",
                UIType.Bar => "进度条",
                UIType.ScrollView => "滚动视图",
                UIType.InputField => "输入框",
                _ => uiType.ToString()
            };
        }

        /// <summary>
        ///     检查是否为容器类型
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <returns>是否为容器类型</returns>
        public static bool IsContainer(this UIType uiType)
        {
            return uiType == UIType.Panel || uiType == UIType.ScrollView;
        }

        /// <summary>
        ///     检查是否为交互类型
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <returns>是否为交互类型</returns>
        public static bool IsInteractive(this UIType uiType)
        {
            return uiType == UIType.Button || uiType == UIType.InputField;
        }
    }
}