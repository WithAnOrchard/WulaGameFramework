namespace EssSystem.Core.UI.Dao
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
        Text
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
            return uiType == UIType.Panel;
        }

        /// <summary>
        ///     检查是否为交互类型
        /// </summary>
        /// <param name="uiType">UI类型</param>
        /// <returns>是否为交互类型</returns>
        public static bool IsInteractive(this UIType uiType)
        {
            return uiType == UIType.Button;
        }
    }
}