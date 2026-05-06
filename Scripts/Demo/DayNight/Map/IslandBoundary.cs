using UnityEngine;
using UnityEngine.Tilemaps;
using Demo.DayNight.Map.Config;
using Demo.DayNight.Map.Generator;

namespace Demo.DayNight.Map
{
    /// <summary>
    /// 海岛碰撞墙 —— 沿陆地外缘（含 shore fBm 扰动）采样多边形顶点，
    /// 写入一个 <see cref="EdgeCollider2D"/>（闭合环），把玩家锁在岛上、不让走入海里。
    /// <para>
    /// 与 <see cref="IslandSurvivalGenerator"/> 共享同一套 shore Perlin 数学
    /// （<see cref="IslandSurvivalGenerator.SampleShoreRadiusTiles"/>），
    /// 保证物理墙和视觉海岸 ≤ 1 tile 误差对齐。
    /// </para>
    /// <para>**坐标系**：本组件挂在 <c>MapView</c> 的 <see cref="Grid"/> 子节点下，
    /// 父 Grid 用 <c>WorldToCell / CellToWorld</c> 把 tile 坐标映射到世界坐标。
    /// 所以这里把"陆地半径（tile 单位）"通过 <c>Grid.CellToWorld</c> 转成世界坐标即可。</para>
    /// </summary>
    [RequireComponent(typeof(EdgeCollider2D))]
    public class IslandBoundary : MonoBehaviour
    {
        [Tooltip("沿 360° 采样的顶点数；越多形状越平滑，但 EdgeCollider2D 性能略降。默认 96 = 3.75°/段。")]
        [SerializeField, Range(16, 512)] private int _segmentCount = 96;

        [Tooltip("把碰撞墙整体往岛内推的 tile 数。0 = 墙刚好在水陆 tile 交界处（默认）；正值=向岛内偏，让玩家不能踩水。")]
        [SerializeField] private float _shoreInsetTiles = 0f;

        [Tooltip("是否在 Scene 视图绘制 Gizmo 调试线（仅 Editor）")]
        [SerializeField] private bool _drawGizmo = true;

        private EdgeCollider2D _collider;
        private IslandSurvivalMapConfig _cfg;
        private Grid _grid;

        /// <summary>由 <see cref="DayNightGameManager"/> 创建后调用一次，触发实际多边形构建。</summary>
        public void Build(IslandSurvivalMapConfig cfg, Grid grid)
        {
            _cfg = cfg;
            _grid = grid;
            if (_collider == null) _collider = GetComponent<EdgeCollider2D>();
            if (_cfg == null || _grid == null)
            {
                Debug.LogWarning("[IslandBoundary] Build 缺参数（cfg 或 grid 为 null），跳过");
                return;
            }
            RebuildPolygon();
        }

        private void RebuildPolygon()
        {
            var center = IslandSurvivalGenerator.GetWorldCenter(_cfg);
            var n = Mathf.Max(16, _segmentCount);
            // EdgeCollider2D 闭合环：首尾点重复
            var pts = new Vector2[n + 1];
            for (var i = 0; i <= n; i++)
            {
                var theta = (i % n) / (float)n * Mathf.PI * 2f;
                // 用 ShallowOceanThreshold 而非 BeachThreshold —— 后者是 land/beach 内部分隔，
                // 前者才是 land tile 与 water tile 的真正交界，碰撞墙正贴在水陆中间。
                var rTiles = IslandSurvivalGenerator.SampleShoreRadiusTiles(theta, _cfg, _cfg.ShallowOceanThreshold)
                             - _shoreInsetTiles;
                if (rTiles < 1f) rTiles = 1f;

                // tile 坐标 → 世界坐标。
                // **关键**：generator 用 tile 整数坐标 (worldX, worldY) 作为 cell 的**左下角**判定；
                // 视觉上 shore tile 的"中心"在 (worldX+0.5, worldY+0.5)。
                // 之前用 CellToWorld(RoundToInt(...)) 拿的是左下角再取整 → 双重偏向圆心，
                // 碰撞墙就比视觉海岸内缩约 0.5~1 tile。
                // 直接用浮点 tile 坐标 + 0.5 修正到 cell 中心（cellSize=1 的 Tilemap）。
                var tileX = center.x + Mathf.Cos(theta) * rTiles;
                var tileY = center.y + Mathf.Sin(theta) * rTiles;
                var cell = _grid.cellSize;          // 默认 (1, 1, 0)，预留非默认 cellSize 兼容
                var world = new Vector3((tileX + 0.5f) * cell.x, (tileY + 0.5f) * cell.y, 0f);

                // EdgeCollider2D 的 points 是 *本组件局部坐标*；本组件作为 Grid 的子节点（localPos=0）
                // → 把世界坐标转回 Grid 的局部空间
                var local = _grid.transform.InverseTransformPoint(world);
                pts[i] = new Vector2(local.x, local.y);
            }
            _collider.points = pts;
            _collider.edgeRadius = 0.05f; // 给玩家一点容差，避免擦边卡住

            Debug.Log($"[IslandBoundary] 构建完成：{n} 段顶点，BeachThreshold={_cfg.BeachThreshold}, " +
                      $"WorldSizeChunks={_cfg.WorldSizeChunks}");
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!_drawGizmo || _collider == null || _collider.pointCount < 2) return;
            Gizmos.color = new Color(0.2f, 1f, 1f, 0.7f);
            var pts = _collider.points;
            var t = transform;
            for (var i = 0; i < pts.Length - 1; i++)
                Gizmos.DrawLine(t.TransformPoint(pts[i]), t.TransformPoint(pts[i + 1]));
        }
#endif
    }
}
