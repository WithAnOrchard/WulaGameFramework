using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 把 <see cref="VoxelChunk"/>（heightmap）烘成单个 <see cref="Mesh"/>：
    /// <list type="bullet">
    /// <item>每格一个顶面 quad，颜色 = 该格 TopBlock 顶面色</item>
    /// <item>仅在邻居更矮一侧出侧面 quad，从 neighborTop 到 thisTop 一整条（face cull + 列内合并）</item>
    /// <item>水面 (TopBlock = Water) 顶面强制位于 <c>SeaLevel</c>，不画侧面</item>
    /// </list>
    /// 单 Mesh 单 Material，使用顶点色驱动。一帧只 alloc 4 个 List；
    /// 视觉风格 MC，性能 SRP-batcher 友好。
    /// </summary>
    public static class VoxelChunkMesher
    {
        /// <summary>
        /// 生成 chunk Mesh。<paramref name="neighborXM"/> 等为 4 邻居 chunk（X-, X+, Z-, Z+），
        /// 用于跨 chunk 边界的侧面 cull —— 缺省（null）时按"邻居与自己等高"处理（不会画外墙）。
        /// </summary>
        public static Mesh Build(
            VoxelChunk chunk,
            VoxelMapConfig config,
            VoxelBlockType[] palette,
            VoxelChunk neighborXM, VoxelChunk neighborXP,
            VoxelChunk neighborZM, VoxelChunk neighborZP)
        {
            var size = chunk.Size;
            var sea  = config.SeaLevel;

            var verts  = new List<Vector3>(size * size * 4);
            var norms  = new List<Vector3>(size * size * 4);
            var cols   = new List<Color32>(size * size * 4);
            var tris   = new List<int>(size * size * 6);

            for (var lz = 0; lz < size; lz++)
            for (var lx = 0; lx < size; lx++)
            {
                var idx       = lz * size + lx;
                var topId     = chunk.TopBlocks[idx];
                if (topId == VoxelBlockTypes.Air) continue;
                var sideId    = chunk.SideBlocks[idx];
                var realH     = chunk.Heights[idx];

                // 该列的"地表 y"（水强制贴 SeaLevel 平面）
                var surfaceY = topId == VoxelBlockTypes.Water ? sea : realH;
                var topColor = palette[topId].TopColor;

                // ── 顶面 ─────────────────────────────────────────────
                EmitTopQuad(verts, norms, cols, tris, lx, surfaceY, lz, topColor);

                if (topId == VoxelBlockTypes.Water) continue; // 水不画侧面

                var sideColor = palette[sideId].SideColor;

                // ── 4 侧面（仅邻居更矮）──────────────────────────────
                // X-（朝 -X 方向的面，位于 x=lx 平面）
                var nx = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx - 1, lz, sea, surfaceY);
                if (nx < surfaceY) EmitSideQuadXMinus(verts, norms, cols, tris, lx, nx, surfaceY, lz, sideColor);

                // X+
                var px = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx + 1, lz, sea, surfaceY);
                if (px < surfaceY) EmitSideQuadXPlus(verts, norms, cols, tris, lx, px, surfaceY, lz, sideColor);

                // Z-
                var nz = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx, lz - 1, sea, surfaceY);
                if (nz < surfaceY) EmitSideQuadZMinus(verts, norms, cols, tris, lx, nz, surfaceY, lz, sideColor);

                // Z+
                var pz = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx, lz + 1, sea, surfaceY);
                if (pz < surfaceY) EmitSideQuadZPlus(verts, norms, cols, tris, lx, pz, surfaceY, lz, sideColor);
            }

            var mesh = new Mesh
            {
                name = $"VoxelChunk_{chunk.ChunkX}_{chunk.ChunkZ}",
                indexFormat = (verts.Count > 65000)
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16,
            };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0, calculateBounds: true);
            return mesh;
        }

        // ──────────────────────────────────────────────────────────────
        // 邻居高度查询（带 chunk 边界跨越；自动按"水→SeaLevel"投影）
        private static int NeighborTopY(
            VoxelChunk c, VoxelChunk xm, VoxelChunk xp, VoxelChunk zm, VoxelChunk zp,
            int lx, int lz, int seaLevel, int fallbackSelf)
        {
            var size = c.Size;
            VoxelChunk target = c;
            if (lx < 0)        { target = xm; lx += size; }
            else if (lx >= size) { target = xp; lx -= size; }

            if (lz < 0)        { target = zm; lz += size; }
            else if (lz >= size) { target = zp; lz -= size; }

            // 邻居 chunk 还没生成 → 假装等高，跳过画外墙（避免视野边界出现高墙）
            if (target == null) return fallbackSelf;

            var idx = lz * size + lx;
            var nTop = target.TopBlocks[idx];
            var nH   = target.Heights[idx];
            return nTop == VoxelBlockTypes.Water ? seaLevel : nH;
        }

        // ──────────────────────────────────────────────────────────────
        // Quad 发射器（CCW，正面朝外法线）
        // 单元 (lx, lz) 占 [lx, lx+1] × [y, y+...] × [lz, lz+1] 立方空间
        private static void EmitTopQuad(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
                                        int lx, int y, int lz, Color32 color)
        {
            var i = v.Count;
            v.Add(new Vector3(lx,     y, lz));
            v.Add(new Vector3(lx + 1, y, lz));
            v.Add(new Vector3(lx + 1, y, lz + 1));
            v.Add(new Vector3(lx,     y, lz + 1));
            for (var k = 0; k < 4; k++) { n.Add(Vector3.up); c.Add(color); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        // 所有侧面统一约定：4 个顶点按 BL → BR → TR → TL（外部视角 CCW），三角形 (0,2,1)(0,3,2)
        // 该模式与顶面 EmitTopQuad 一致，叉积已验证 4 个方向法线均指向外部。

        private static void EmitSideQuadXMinus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
                                               int lx, int yLow, int yHigh, int lz, Color32 color)
        {
            // 面位于 x = lx，法线 -X；外部视角（看 +X）右 = -Z，故 BL.z = lz+1
            var i = v.Count;
            v.Add(new Vector3(lx, yLow,  lz + 1)); // BL
            v.Add(new Vector3(lx, yLow,  lz));     // BR
            v.Add(new Vector3(lx, yHigh, lz));     // TR
            v.Add(new Vector3(lx, yHigh, lz + 1)); // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.left); c.Add(color); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        private static void EmitSideQuadXPlus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
                                              int lx, int yLow, int yHigh, int lz, Color32 color)
        {
            // 面位于 x = lx + 1，法线 +X；外部视角（看 -X）右 = +Z，故 BL.z = lz
            var i = v.Count;
            v.Add(new Vector3(lx + 1, yLow,  lz));     // BL
            v.Add(new Vector3(lx + 1, yLow,  lz + 1)); // BR
            v.Add(new Vector3(lx + 1, yHigh, lz + 1)); // TR
            v.Add(new Vector3(lx + 1, yHigh, lz));     // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.right); c.Add(color); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        private static void EmitSideQuadZMinus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
                                               int lx, int yLow, int yHigh, int lz, Color32 color)
        {
            // 面位于 z = lz，法线 -Z；外部视角（看 +Z）右 = +X，故 BL.x = lx
            var i = v.Count;
            v.Add(new Vector3(lx,     yLow,  lz)); // BL
            v.Add(new Vector3(lx + 1, yLow,  lz)); // BR
            v.Add(new Vector3(lx + 1, yHigh, lz)); // TR
            v.Add(new Vector3(lx,     yHigh, lz)); // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.back); c.Add(color); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        private static void EmitSideQuadZPlus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<int> t,
                                              int lx, int yLow, int yHigh, int lz, Color32 color)
        {
            // 面位于 z = lz + 1，法线 +Z；外部视角（看 -Z）右 = -X，故 BL.x = lx+1
            var i = v.Count;
            v.Add(new Vector3(lx + 1, yLow,  lz + 1)); // BL
            v.Add(new Vector3(lx,     yLow,  lz + 1)); // BR
            v.Add(new Vector3(lx,     yHigh, lz + 1)); // TR
            v.Add(new Vector3(lx + 1, yHigh, lz + 1)); // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.forward); c.Add(color); }
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }
    }
}
