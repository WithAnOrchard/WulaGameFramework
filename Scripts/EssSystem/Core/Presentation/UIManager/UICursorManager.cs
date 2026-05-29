using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager
{
    /// <summary>
    /// UIManager 体系内的统一光标管理器。
    /// UIWindowBehavior 等组件通过 Request / Release 注册意图，
    /// 本类决策最终使用的光标并通过 Unity Cursor.SetCursor 应用。
    /// 纹理以向量化三角形 + 矩形定义，经 4× SSAA 超采样后输出 32×32，边缘反锯齿。
    /// </summary>
    public static class UICursorManager
    {
        public enum CursorStyle
        {
            Default,
            ResizeWE,   // ↔
            ResizeNS,   // ↕
            ResizeNWSE, // ↖↘
            ResizeNESW, // ↗↙
            Move        // ✥
        }

        // ── 注册表 ──────────────────────────────────────────────────────────
        private static readonly Dictionary<int, CursorStyle> _registry =
            new Dictionary<int, CursorStyle>();
        private static CursorStyle _applied = CursorStyle.Default;

        // ── 纹理缓存 ─────────────────────────────────────────────────────────
        private static readonly Dictionary<CursorStyle, Texture2D> _textures =
            new Dictionary<CursorStyle, Texture2D>();
        private static readonly Dictionary<CursorStyle, Vector2>   _hotspots =
            new Dictionary<CursorStyle, Vector2>();

        // ── 超采样参数 ───────────────────────────────────────────────────────
        private const int OUT  = 32;   // 输出纹理尺寸
        private const int SS   = 4;    // 超采样倍数
        private const int BUF  = OUT * SS; // 超采样缓冲区尺寸 (128)
        private const int OUTLINE_R = 4;   // 描边半径（SSAA 像素）

        // ── 公共 API ─────────────────────────────────────────────────────────

        /// <summary>注册光标请求。</summary>
        public static void Request(int instanceId, CursorStyle style)
        {
            if (style == CursorStyle.Default) _registry.Remove(instanceId);
            else                              _registry[instanceId] = style;
            Refresh();
        }

        /// <summary>释放光标请求，恢复默认光标。</summary>
        public static void Release(int instanceId)
        {
            _registry.Remove(instanceId);
            Refresh();
        }

        // ── 内部调度 ─────────────────────────────────────────────────────────
        private static void Refresh()
        {
            CursorStyle chosen = CursorStyle.Default;
            foreach (var v in _registry.Values)
                if (v != CursorStyle.Default) { chosen = v; break; }

            if (chosen == _applied) return;
            _applied = chosen;

            if (chosen == CursorStyle.Default)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                return;
            }
            var tex = GetOrCreate(chosen);
            if (tex != null)
                Cursor.SetCursor(tex, _hotspots[chosen], CursorMode.Auto);
        }

        private static Texture2D GetOrCreate(CursorStyle style)
        {
            if (_textures.TryGetValue(style, out var t) && t != null) return t;
            t = BuildTexture(style);
            if (t != null) _textures[style] = t;
            return t;
        }

        // ── 超采样纹理构建 ────────────────────────────────────────────────────
        private static Texture2D BuildTexture(CursorStyle style)
        {
            // 1. 在 BUF×BUF 超采样空间标记形状和描边
            var ssShape   = new bool[BUF * BUF];
            var ssOutline = new bool[BUF * BUF];

            for (int sy = 0; sy < BUF; sy++)
            for (int sx = 0; sx < BUF; sx++)
            {
                // 归一化坐标：(0,0)=左上，(1,1)=右下
                float nx = (sx + 0.5f) / BUF;
                float ny = (sy + 0.5f) / BUF;
                if (IsShape(style, nx, ny))
                    ssShape[sy * BUF + sx] = true;
            }

            // 描边：在 SSAA 空间扩散 OUTLINE_R 像素
            for (int sy = 0; sy < BUF; sy++)
            for (int sx = 0; sx < BUF; sx++)
            {
                if (ssShape[sy * BUF + sx]) continue;
                for (int dy = -OUTLINE_R; dy <= OUTLINE_R; dy++)
                for (int dx = -OUTLINE_R; dx <= OUTLINE_R; dx++)
                {
                    int nx2 = sx + dx, ny2 = sy + dy;
                    if ((uint)nx2 < BUF && (uint)ny2 < BUF && ssShape[ny2 * BUF + nx2])
                    { ssOutline[sy * BUF + sx] = true; goto nextPx; }
                }
                nextPx:;
            }

            // 2. Box-filter 降采样到 OUT×OUT（4×4 = 16 样本/输出像素）
            var pixels = new Color[OUT * OUT];
            for (int oy = 0; oy < OUT; oy++)
            for (int ox = 0; ox < OUT; ox++)
            {
                float shapeSum = 0, outlineSum = 0;
                for (int ky = 0; ky < SS; ky++)
                for (int kx = 0; kx < SS; kx++)
                {
                    int idx = (oy * SS + ky) * BUF + (ox * SS + kx);
                    if (ssShape[idx])   shapeSum++;
                    if (ssOutline[idx]) outlineSum++;
                }
                float sa = shapeSum   / (SS * SS);
                float oa = outlineSum / (SS * SS);

                // shape（黑）覆盖描边（白）
                if (sa > 0f)      pixels[oy * OUT + ox] = new Color(0f, 0f, 0f, sa);
                else if (oa > 0f) pixels[oy * OUT + ox] = new Color(1f, 1f, 1f, oa);
                else              pixels[oy * OUT + ox] = Color.clear;
            }

            var tex = new Texture2D(OUT, OUT, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode   = TextureWrapMode.Clamp;
            tex.name       = "Cursor_" + style;
            // Texture2D SetPixels 行序：y=0 在底部；pixels 数组按 y=0→top 排列，需翻转
            var flipped = new Color[OUT * OUT];
            for (int oy = 0; oy < OUT; oy++)
                for (int ox = 0; ox < OUT; ox++)
                    flipped[oy * OUT + ox] = pixels[(OUT - 1 - oy) * OUT + ox];
            tex.SetPixels(flipped);
            tex.Apply(false, false);

            _hotspots[style] = new Vector2(OUT * 0.5f, OUT * 0.5f);
            return tex;
        }

        // ── 形状判断（归一化坐标，原点左上） ──────────────────────────────────
        private static bool IsShape(CursorStyle s, float x, float y)
        {
            switch (s)
            {
                case CursorStyle.ResizeWE:   return ShapeWE(x, y);
                case CursorStyle.ResizeNS:   return ShapeNS(x, y);
                case CursorStyle.ResizeNWSE: return ShapeNWSE(x, y);
                case CursorStyle.ResizeNESW: return ShapeNESW(x, y);
                case CursorStyle.Move:       return ShapeMove(x, y);
                default: return false;
            }
        }

        // ── ↔ ───────────────────────────────────────────────────────────────
        private static bool ShapeWE(float x, float y)
        {
            if (InTriangle(x,y, 0.08f,0.50f, 0.36f,0.24f, 0.36f,0.76f)) return true;
            if (InTriangle(x,y, 0.92f,0.50f, 0.64f,0.24f, 0.64f,0.76f)) return true;
            return x>=0.32f && x<=0.68f && y>=0.41f && y<=0.59f;
        }

        // ── ↕ ───────────────────────────────────────────────────────────────
        private static bool ShapeNS(float x, float y)
        {
            if (InTriangle(x,y, 0.50f,0.08f, 0.24f,0.36f, 0.76f,0.36f)) return true;
            if (InTriangle(x,y, 0.50f,0.92f, 0.24f,0.64f, 0.76f,0.64f)) return true;
            return x>=0.41f && x<=0.59f && y>=0.32f && y<=0.68f;
        }

        // ── ↖↘ ──────────────────────────────────────────────────────────────
        private static bool ShapeNWSE(float x, float y)
        {
            // NW 箭头尖朝左上
            if (InTriangle(x,y, 0.10f,0.10f, 0.38f,0.17f, 0.17f,0.38f)) return true;
            // SE 箭头尖朝右下
            if (InTriangle(x,y, 0.90f,0.90f, 0.62f,0.83f, 0.83f,0.62f)) return true;
            // 对角轴线（胶囊）
            return SegDist(x,y, 0.28f,0.28f, 0.72f,0.72f) < 0.055f;
        }

        // ── ↗↙ ──────────────────────────────────────────────────────────────
        private static bool ShapeNESW(float x, float y)
        {
            // NE 箭头尖朝右上
            if (InTriangle(x,y, 0.90f,0.10f, 0.83f,0.38f, 0.62f,0.17f)) return true;
            // SW 箭头尖朝左下
            if (InTriangle(x,y, 0.10f,0.90f, 0.17f,0.62f, 0.38f,0.83f)) return true;
            // 对角轴线（胶囊）
            return SegDist(x,y, 0.28f,0.72f, 0.72f,0.28f) < 0.055f;
        }

        // ── ✥ ───────────────────────────────────────────────────────────────
        private static bool ShapeMove(float x, float y)
        {
            if (InTriangle(x,y, 0.50f,0.05f, 0.32f,0.30f, 0.68f,0.30f)) return true;
            if (InTriangle(x,y, 0.50f,0.95f, 0.32f,0.70f, 0.68f,0.70f)) return true;
            if (InTriangle(x,y, 0.05f,0.50f, 0.30f,0.32f, 0.30f,0.68f)) return true;
            if (InTriangle(x,y, 0.95f,0.50f, 0.70f,0.32f, 0.70f,0.68f)) return true;
            if (x>=0.27f && x<=0.73f && y>=0.42f && y<=0.58f) return true;
            if (x>=0.42f && x<=0.58f && y>=0.27f && y<=0.73f) return true;
            return false;
        }

        // ── 几何辅助 ─────────────────────────────────────────────────────────
        private static bool InTriangle(float px, float py,
                                        float ax, float ay,
                                        float bx, float by,
                                        float cx, float cy)
        {
            float d1 = Cross(px,py,ax,ay,bx,by);
            float d2 = Cross(px,py,bx,by,cx,cy);
            float d3 = Cross(px,py,cx,cy,ax,ay);
            bool n = d1<0f || d2<0f || d3<0f;
            bool p = d1>0f || d2>0f || d3>0f;
            return !(n && p);
        }

        private static float Cross(float px, float py, float ax, float ay, float bx, float by)
            => (px - bx) * (ay - by) - (ax - bx) * (py - by);

        private static float SegDist(float px, float py, float ax, float ay, float bx, float by)
        {
            float dx = bx - ax, dy = by - ay;
            float len2 = dx*dx + dy*dy;
            float t = len2 < 1e-8f ? 0f : Mathf.Clamp01(((px-ax)*dx + (py-ay)*dy) / len2);
            float cx = ax + t*dx, cy = ay + t*dy;
            return Mathf.Sqrt((px-cx)*(px-cx) + (py-cy)*(py-cy));
        }
    }
}
