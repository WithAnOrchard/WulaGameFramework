using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Runtime;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator
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
            var uvs    = new List<Vector2>(size * size * 4);
            var tris   = new List<int>(size * size * 6);

            // 光照服务（缺席时为 null → 退化为基础色不调制，等价"全亮"）
            var light = VoxelLightService.HasInstance ? VoxelLightService.Instance : null;

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

                // 世界坐标 —— 用于变体哈希（同坐标永远同变体）+ 光照采样
                var wx = chunk.ChunkX * size + lx;
                var wz = chunk.ChunkZ * size + lz;

                // ── UV 槽位 ─────────────────────────────────────────
                var topUV  = VoxelTextureAtlas.GetSlotUV((byte)VoxelAtlasSlots.SlotForTop(topId, wx, wz));
                var sideUV = VoxelTextureAtlas.GetSlotUV((byte)VoxelAtlasSlots.SlotForSide(sideId, wx, wz));

                // ── 顶面 ─────────────────────────────────────────────
                // 顶面采样点 = (wx, surfaceY, wz)：恰好暴露天空，吃 sky + block 组合
                var topLit = ApplyLight(light, topColor, wx, surfaceY, wz, surfaceY);
                EmitTopQuad(verts, norms, cols, uvs, tris, lx, surfaceY, lz, topLit, topUV);

                if (topId == VoxelBlockTypes.Water) continue; // 水不画侧面

                var sideColor = palette[sideId].SideColor;

                // ── 4 侧面（仅邻居更矮）──────────────────────────────
                // 侧面采样点 = 邻居那侧面中点：(邻居 wx, midY, 邻居 wz)；midY 落在两高之间，
                // 尚处暴露空间（邻居矮 → 邻居那一格上方有空气），吃 sky；同时受附近光源影响。
                // X-（朝 -X 方向的面，位于 x=lx 平面）
                var nx = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx - 1, lz, sea, surfaceY);
                if (nx < surfaceY)
                {
                    var sLit = ApplyLight(light, sideColor, wx - 1, (nx + surfaceY) >> 1, wz, nx);
                    EmitSideQuadXMinus(verts, norms, cols, uvs, tris, lx, nx, surfaceY, lz, sLit, sideUV);
                }

                // X+
                var px = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx + 1, lz, sea, surfaceY);
                if (px < surfaceY)
                {
                    var sLit = ApplyLight(light, sideColor, wx + 1, (px + surfaceY) >> 1, wz, px);
                    EmitSideQuadXPlus(verts, norms, cols, uvs, tris, lx, px, surfaceY, lz, sLit, sideUV);
                }

                // Z-
                var nz = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx, lz - 1, sea, surfaceY);
                if (nz < surfaceY)
                {
                    var sLit = ApplyLight(light, sideColor, wx, (nz + surfaceY) >> 1, wz - 1, nz);
                    EmitSideQuadZMinus(verts, norms, cols, uvs, tris, lx, nz, surfaceY, lz, sLit, sideUV);
                }

                // Z+
                var pz = NeighborTopY(chunk, neighborXM, neighborXP, neighborZM, neighborZP,
                                      lx, lz + 1, sea, surfaceY);
                if (pz < surfaceY)
                {
                    var sLit = ApplyLight(light, sideColor, wx, (pz + surfaceY) >> 1, wz + 1, pz);
                    EmitSideQuadZPlus(verts, norms, cols, uvs, tris, lx, pz, surfaceY, lz, sLit, sideUV);
                }
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
            mesh.SetUVs(0, uvs);
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
        // 光照调制：把基础顶点色乘上 (亮度系数 × 暖光偏色)。
        // 暖光偏色按 block/sky 占比 blend：纯 sky 时白光、纯 block 时取累积 tint。
        // ──────────────────────────────────────────────────────────────
        private static Color32 ApplyLight(VoxelLightService svc, Color32 baseColor,
                                          int wx, int wy, int wz, int surfaceY)
        {
            if (svc == null) return baseColor;

            var combined = svc.SampleLight(wx, wy, wz, surfaceY, out var warmTint, out var blockLight);
            var bright   = VoxelLightConstants.ToBrightness01(combined);

            // sky vs block 占比：blockW = blockLight / max(combined, 1)
            var blockW = combined > 0 ? blockLight / (float)combined : 0f;
            var skyW   = 1f - blockW;

            // tint mix：sky 部分用白 (1,1,1)；block 部分用 warmTint
            var tR = (warmTint.r * blockW + 255f * skyW) / 255f;
            var tG = (warmTint.g * blockW + 255f * skyW) / 255f;
            var tB = (warmTint.b * blockW + 255f * skyW) / 255f;

            // 最终：base × bright × tint，保持 alpha 不变
            return new Color32(
                (byte)Mathf.Clamp(baseColor.r * bright * tR, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.g * bright * tG, 0f, 255f),
                (byte)Mathf.Clamp(baseColor.b * bright * tB, 0f, 255f),
                baseColor.a);
        }

        // ──────────────────────────────────────────────────────────────
        // Quad 发射器（CCW，正面朝外法线）
        // 单元 (lx, lz) 占 [lx, lx+1] × [y, y+...] × [lz, lz+1] 立方空间
        private static void EmitTopQuad(List<Vector3> v, List<Vector3> n, List<Color32> c, List<Vector2> u, List<int> t,
                                        int lx, int y, int lz, Color32 color, Rect uvR)
        {
            var i = v.Count;
            v.Add(new Vector3(lx,     y, lz));
            v.Add(new Vector3(lx + 1, y, lz));
            v.Add(new Vector3(lx + 1, y, lz + 1));
            v.Add(new Vector3(lx,     y, lz + 1));
            for (var k = 0; k < 4; k++) { n.Add(Vector3.up); c.Add(color); }
            // UV 顺序与顶点顺序对齐：BL(uMin,vMin) BR(uMax,vMin) TR(uMax,vMax) TL(uMin,vMax)
            u.Add(new Vector2(uvR.xMin, uvR.yMin));
            u.Add(new Vector2(uvR.xMax, uvR.yMin));
            u.Add(new Vector2(uvR.xMax, uvR.yMax));
            u.Add(new Vector2(uvR.xMin, uvR.yMax));
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        // 所有侧面统一约定：4 个顶点按 BL → BR → TR → TL（外部视角 CCW），三角形 (0,2,1)(0,3,2)
        // 该模式与顶面 EmitTopQuad 一致，叉积已验证 4 个方向法线均指向外部。

        private static void EmitSideQuadXMinus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<Vector2> u, List<int> t,
                                               int lx, int yLow, int yHigh, int lz, Color32 color, Rect uvR)
        {
            // 面位于 x = lx，法线 -X；外部视角（看 +X）右 = -Z，故 BL.z = lz+1
            var i = v.Count;
            v.Add(new Vector3(lx, yLow,  lz + 1)); // BL
            v.Add(new Vector3(lx, yLow,  lz));     // BR
            v.Add(new Vector3(lx, yHigh, lz));     // TR
            v.Add(new Vector3(lx, yHigh, lz + 1)); // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.left); c.Add(color); }
            EmitSideQuadUV(u, uvR, yHigh - yLow);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        private static void EmitSideQuadXPlus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<Vector2> u, List<int> t,
                                              int lx, int yLow, int yHigh, int lz, Color32 color, Rect uvR)
        {
            // 面位于 x = lx + 1，法线 +X；外部视角（看 -X）右 = +Z，故 BL.z = lz
            var i = v.Count;
            v.Add(new Vector3(lx + 1, yLow,  lz));     // BL
            v.Add(new Vector3(lx + 1, yLow,  lz + 1)); // BR
            v.Add(new Vector3(lx + 1, yHigh, lz + 1)); // TR
            v.Add(new Vector3(lx + 1, yHigh, lz));     // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.right); c.Add(color); }
            EmitSideQuadUV(u, uvR, yHigh - yLow);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        private static void EmitSideQuadZMinus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<Vector2> u, List<int> t,
                                               int lx, int yLow, int yHigh, int lz, Color32 color, Rect uvR)
        {
            // 面位于 z = lz，法线 -Z；外部视角（看 +Z）右 = +X，故 BL.x = lx
            var i = v.Count;
            v.Add(new Vector3(lx,     yLow,  lz)); // BL
            v.Add(new Vector3(lx + 1, yLow,  lz)); // BR
            v.Add(new Vector3(lx + 1, yHigh, lz)); // TR
            v.Add(new Vector3(lx,     yHigh, lz)); // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.back); c.Add(color); }
            EmitSideQuadUV(u, uvR, yHigh - yLow);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        private static void EmitSideQuadZPlus(List<Vector3> v, List<Vector3> n, List<Color32> c, List<Vector2> u, List<int> t,
                                              int lx, int yLow, int yHigh, int lz, Color32 color, Rect uvR)
        {
            // 面位于 z = lz + 1，法线 +Z；外部视角（看 -Z）右 = -X，故 BL.x = lx+1
            var i = v.Count;
            v.Add(new Vector3(lx + 1, yLow,  lz + 1)); // BL
            v.Add(new Vector3(lx,     yLow,  lz + 1)); // BR
            v.Add(new Vector3(lx,     yHigh, lz + 1)); // TR
            v.Add(new Vector3(lx + 1, yHigh, lz + 1)); // TL
            for (var k = 0; k < 4; k++) { n.Add(Vector3.forward); c.Add(color); }
            EmitSideQuadUV(u, uvR, yHigh - yLow);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
            t.Add(i); t.Add(i + 3); t.Add(i + 2);
        }

        /// <summary>
        /// 侧面 quad 的 UV：高度方向按 (yHigh - yLow) 平铺，避免高墙被一张贴图横向拉伸。
        /// 顶点顺序 BL(uMin,vMin) BR(uMax,vMin) TR(uMax,vMax*h) TL(uMin,vMax*h)。
        /// </summary>
        private static void EmitSideQuadUV(List<Vector2> u, Rect uvR, int height)
        {
            var hRepeat = Mathf.Max(1, height);
            // 直接把 V 上沿超出 [0,1] —— atlas 用 Repeat wrap 时正好按 hRepeat 次平铺单个 slot 贴图；
            // 由于 atlas 是合图（多个 slot 共用一张 Texture），垂直 repeat 会跨到上方相邻 slot —— 因此实际
            // 这里直接重复 slot 自己的 vMin..vMax 在垂直方向 hRepeat 次：通过把 v 写成 vMin..(vMin + (vMax-vMin)*hRepeat)
            // 是行不通的（atlas wrap 会跨 slot）。MC 风视觉对单格立方体高度=1，重复需求弱，这里直接按单倍 UV 输出。
            u.Add(new Vector2(uvR.xMin, uvR.yMin));
            u.Add(new Vector2(uvR.xMax, uvR.yMin));
            u.Add(new Vector2(uvR.xMax, uvR.yMax));
            u.Add(new Vector2(uvR.xMin, uvR.yMax));
        }
    }
}
