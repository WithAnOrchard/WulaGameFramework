using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Runtime
{
    /// <summary>
    /// 运行时按需创建体素 chunk 默认 Material。
    /// <para>优先级：</para>
    /// <list type="number">
    /// <item><c>Wula/VoxelTextured</c> + 绑 <see cref="VoxelTextureAtlas.Texture"/>（atlas × 顶点 tint × 方向光）</item>
    /// <item><c>Wula/VoxelVertexColor</c>（无贴图，纯顶点色 + 方向光）</item>
    /// <item>内置 Particles/Standard Unlit / Sprites/Default / Standard 兜底</item>
    /// </list>
    /// </summary>
    public static class VoxelMaterialFactory
    {
        private const string TexturedShaderName    = "Wula/VoxelTextured";
        private const string VertexColorShaderName = "Wula/VoxelVertexColor";

        public static Material CreateDefault()
        {
            // 优先贴图版
            var textured = Shader.Find(TexturedShaderName);
            if (textured != null)
            {
                var atlas = VoxelTextureAtlas.Texture; // 触发懒构建；EnsureBuilt 会按 SlotPaths 加载 8 张 16² 拼到 64×32
                if (atlas != null)
                {
                    // MC 风视觉：Point 采样 + 关 mipmap，避免 chunk 拉远串到相邻 slot 上
                    atlas.filterMode = FilterMode.Point;
                    atlas.wrapMode   = TextureWrapMode.Clamp;

                    var mat = new Material(textured) { name = "VoxelChunkMaterial_Textured" };
                    mat.mainTexture = atlas;
                    mat.enableInstancing = true;
                    return mat;
                }
                Debug.LogWarning("[VoxelMaterialFactory] VoxelTextured shader 找到但 atlas 构建失败，回退到 VertexColor 渲染");
            }

            // Fallback：纯顶点色
            var sh = Shader.Find(VertexColorShaderName)
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Sprites/Default")
                  ?? Shader.Find("Standard");
            var fallback = new Material(sh) { name = "VoxelChunkMaterial_VertexColor" };
            fallback.enableInstancing = true;
            return fallback;
        }
    }
}
