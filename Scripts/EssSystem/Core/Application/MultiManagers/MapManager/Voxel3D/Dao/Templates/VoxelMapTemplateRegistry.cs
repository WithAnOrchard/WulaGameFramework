using EssSystem.Core.Base.Singleton;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao.Templates
{
    /// <summary>
    /// <see cref="IVoxelMapTemplate"/> 进程级注册表（与 2D <c>MapTemplateRegistry</c> 平行）。
    /// <para><c>Voxel3DMapManager</c> 启动时通过 <see cref="Get(string)"/> 拿到当前选中的模板执行默认注册。
    /// 业务侧可在自身 Manager 的 <c>Initialize</c> 里调 <see cref="Register"/> 加入自定义模板。</para>
    /// <para>注意：静态字典在 Editor 关闭 Domain Reload 时会跨 Play session 残留，
    /// 必须在 <c>PlayModeResetGuard.ResetStaticRegistries</c> 里同步 <c>_templates.Clear()</c>。</para>
    /// </summary>
    public static class VoxelMapTemplateRegistry
    {
        private static readonly Dictionary<string, IVoxelMapTemplate> _templates = new();

        public static void Register(IVoxelMapTemplate template)
        {
            if (template == null || string.IsNullOrEmpty(template.TemplateId)) return;
            _templates[template.TemplateId] = template;
        }

        public static IVoxelMapTemplate Get(string templateId)
        {
            if (string.IsNullOrEmpty(templateId)) return null;
            return _templates.TryGetValue(templateId, out var t) ? t : null;
        }

        public static IReadOnlyCollection<IVoxelMapTemplate> All => _templates.Values;

        public static bool Contains(string templateId) =>
            !string.IsNullOrEmpty(templateId) && _templates.ContainsKey(templateId);
    }
}
