using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates
{
    /// <summary>
    /// <see cref="IMapTemplate"/> 进程级注册表。
    /// <para>
    /// MapManager 启动时通过 <see cref="Get(string)"/> 拿到当前选中的模板执行默认注册。
    /// 业务侧可在自身 Manager 的 <c>Initialize</c> 里调 <see cref="Register"/> 加入自定义模板，
    /// 然后在 MapManager Inspector 切换 TemplateId 即可生效。
    /// </para>
    /// </summary>
    public static class MapTemplateRegistry
    {
        private static readonly Dictionary<string, IMapTemplate> _templates = new();

        /// <summary>注册或覆盖一个模板实例（key = <see cref="IMapTemplate.TemplateId"/>）。</summary>
        public static void Register(IMapTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.TemplateId)) return;
            _templates[template.TemplateId] = template;
        }

        /// <summary>取模板；未找到返回 null。</summary>
        public static IMapTemplate Get(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;
            return _templates.TryGetValue(templateId, out var t) ? t : null;
        }

        /// <summary>枚举所有已注册模板（用于 Inspector 下拉等）。</summary>
        public static IReadOnlyCollection<IMapTemplate> All => _templates.Values;

        /// <summary>该模板是否已注册。</summary>
        public static bool Contains(string templateId) =>
            !string.IsNullOrEmpty(templateId) && _templates.ContainsKey(templateId);
    }
}
