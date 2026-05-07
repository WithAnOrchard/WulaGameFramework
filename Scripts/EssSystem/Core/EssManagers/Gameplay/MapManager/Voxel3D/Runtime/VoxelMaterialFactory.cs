using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Runtime
{
    /// <summary>
    /// 运行时按需创建体素 chunk 默认 Material。
    /// 优先 <c>Wula/VoxelVertexColor</c>（包内自带）；找不到时 fallback 到内置 vertex color shader，
    /// 仍找不到则 fallback 到 Standard（损失光照层次但不会黑屏）。
    /// </summary>
    public static class VoxelMaterialFactory
    {
        private const string PreferredShaderName = "Wula/VoxelVertexColor";

        public static Material CreateDefault()
        {
            var sh = Shader.Find(PreferredShaderName)
                  ?? Shader.Find("Particles/Standard Unlit")     // 兼容旧 Built-in：respects vertex color
                  ?? Shader.Find("Sprites/Default")              // 极端兜底
                  ?? Shader.Find("Standard");
            var mat = new Material(sh) { name = "VoxelChunkMaterial" };
            mat.enableInstancing = true;
            return mat;
        }
    }
}
