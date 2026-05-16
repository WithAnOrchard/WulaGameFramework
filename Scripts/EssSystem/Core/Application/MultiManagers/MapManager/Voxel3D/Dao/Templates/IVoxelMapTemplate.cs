namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao.Templates
{
    /// <summary>
    /// 3D 体素地图生成模板（与 2D 的 <c>IMapTemplate</c> 平行）。
    /// <para>每个模板封装该体素玩法特有的默认设定：BlockType 注册 + 默认 Config + 默认装饰器。
    /// <c>Voxel3DMapManager</c> 启动时按当前 <c>TemplateId</c> 选择实例并调用其默认注册方法。</para>
    /// <para>业务侧也可注册自定义 Template，通过 <see cref="VoxelMapTemplateRegistry.Register"/> 加入即可。</para>
    /// </summary>
    public interface IVoxelMapTemplate
    {
        /// <summary>模板唯一 ID（如 <c>"default_voxel_3d"</c>）。</summary>
        string TemplateId { get; }

        /// <summary>显示名（Inspector / UI）。</summary>
        string DisplayName { get; }

        /// <summary>默认 ConfigId（用作 <see cref="CreateDefaultConfig"/> 的目标）。</summary>
        string DefaultConfigId { get; }

        /// <summary>注册该模板用到的所有 BlockType 元数据（颜色 + atlas slot）。
        /// 实现里调用 <c>service.RegisterBlockType(...)</c> 即可。</summary>
        void RegisterDefaultBlockTypes(Voxel3DMapService service);

        /// <summary>创建该模板的默认 <see cref="VoxelMapConfig"/>（已设置 ConfigId / DisplayName）。
        /// 调用方负责 <see cref="Voxel3DMapService.RegisterConfig"/>。</summary>
        VoxelMapConfig CreateDefaultConfig();

        /// <summary>注册该模板的默认装饰器（可空，未配置直接 return）。</summary>
        void RegisterDefaultDecorators(Voxel3DMapService service);
    }
}
