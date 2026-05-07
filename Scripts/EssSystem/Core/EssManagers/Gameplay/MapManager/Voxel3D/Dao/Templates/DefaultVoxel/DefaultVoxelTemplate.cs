namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao.Templates.DefaultVoxel
{
    /// <summary>
    /// 默认体素地图模板：注册 7 个内置 BlockType + 一份 PerlinHeightmap 默认 Config。
    /// <para>对应 <see cref="VoxelBlockTypes.DefaultPalette"/> + 标准 Perlin heightmap 生成器。
    /// 后续要做生物群系/洞穴等定制玩法时新建模板（如 BiomeVoxelTemplate）即可，本模板保持简洁。</para>
    /// </summary>
    public sealed class DefaultVoxelTemplate : IVoxelMapTemplate
    {
        public const string Id = "default_voxel_3d";

        public string TemplateId      => Id;
        public string DisplayName     => "Default Voxel 3D";
        public string DefaultConfigId => Id;

        public void RegisterDefaultBlockTypes(Voxel3DMapService service)
        {
            if (service == null) return;
            // 直接复用 DefaultPalette（含 atlas slot 绑定）；ID 0..6 一次性入库
            var palette = VoxelBlockTypes.DefaultPalette;
            for (var i = 0; i < palette.Length; i++)
            {
                if (palette[i] != null) service.RegisterBlockType(palette[i]);
            }
        }

        public VoxelMapConfig CreateDefaultConfig()
        {
            // ConfigId / DisplayName 已与默认值绑定；保持 VoxelMapConfig 默认参数即可
            return new VoxelMapConfig(DefaultConfigId, DisplayName);
        }

        public void RegisterDefaultDecorators(Voxel3DMapService service)
        {
            // 默认无装饰器；后续接入树木 / 怪物 spawn 时在这里调 service.RegisterDecorator(...)
        }
    }
}
