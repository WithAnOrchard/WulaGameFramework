namespace EssSystem.Core.Presentation.UIManager.Theme
{
    /// <summary>
    /// UI 主题数据标记接口。
    /// <para>具体主题数据结构（颜色 / 字体 / 间距等）由业务层以 struct 或 class 实现。</para>
    /// <para>本接口作为泛型约束使用，不强制任何成员，以兼容已有字段定义的数据结构。</para>
    /// </summary>
    public interface IThemeData { }
}
