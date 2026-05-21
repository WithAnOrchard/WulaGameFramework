using UnityEngine;

namespace Demo.DobeCat.Pet
{
    /// <summary>
    /// 桌宠占位视觉。
    /// <para>M1 阶段：若 <see cref="SpriteResourcePath"/> 在 Resources 下能加载到 Sprite 则使用，
    /// 否则程序生成一个圆形占位图以便能立刻跑起来。</para>
    /// <para>M2 升级：替换为 <c>EssSystem.Core.Presentation.CharacterManager</c> 注册 + 创建。</para>
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PetView : MonoBehaviour
    {
        [Tooltip("Resources 下的 Sprite 路径（不含扩展名）。空 = 用程序生成占位图。")]
        public string SpriteResourcePath = "DobeCat/cat_idle";

        [Tooltip("视觉缩放（占位图等价于 1 单位 = 1 米）。")]
        public float VisualScale = 1f;

        [Tooltip("占位圆的颜色。")]
        public Color PlaceholderColor = new Color(1f, 0.65f, 0.4f, 1f);

        [Tooltip("占位圆贴图分辨率。")]
        public int PlaceholderPixels = 128;

        [Tooltip("使用子物体的 Renderer 组合作为视觉与命中盒（CharacterManager 角色挂在子物体时打开）。\n" +
                 "开启后：本组件的 SpriteRenderer 关闭，不生成占位图；WorldBounds 取所有子 Renderer 的并集。")]
        public bool UseChildRenderers = false;

        private SpriteRenderer _renderer;
        private int _facing = 1;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sortingOrder = 100;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, VisualScale);

            if (UseChildRenderers)
            {
                _renderer.enabled = false;
                _renderer.sprite = null;
                return;
            }

            var sprite = LoadOrFallback();
            _renderer.sprite = sprite;
        }

        /// <summary>设置朝向：+1 右，-1 左。仅翻转 localScale.x。</summary>
        public void SetFacing(int dir)
        {
            if (dir == 0 || dir == _facing) return;
            _facing = dir > 0 ? 1 : -1;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * _facing;
            transform.localScale = s;
        }

        /// <summary>桌宠的世界包围盒（屏幕坐标命中测试用）。
        /// <para>子渲染器模式：每帧扫一次（角色帧切换会改 sprite 大小，但每帧重算成本可忽略）。</para></summary>
        public Bounds WorldBounds
        {
            get
            {
                if (UseChildRenderers)
                {
                    Bounds? acc = null;
                    var rs = GetComponentsInChildren<Renderer>(false);
                    for (var i = 0; i < rs.Length; i++)
                    {
                        var r = rs[i];
                        if (r == _renderer) continue;
                        if (!r.enabled) continue;
                        var b = r.bounds;
                        if (b.size.sqrMagnitude < 1e-6f) continue;
                        if (acc == null) acc = b; else { var bb = acc.Value; bb.Encapsulate(b); acc = bb; }
                    }
                    return acc ?? new Bounds(transform.position, Vector3.one * 0.5f);
                }
                return _renderer != null ? _renderer.bounds : new Bounds(transform.position, Vector3.one);
            }
        }

        // ──────────────────────────────────────────────────────

        private Sprite LoadOrFallback()
        {
            if (!string.IsNullOrEmpty(SpriteResourcePath))
            {
                var sp = Resources.Load<Sprite>(SpriteResourcePath);
                if (sp != null) return sp;
            }
            return BuildPlaceholderSprite();
        }

        private Sprite BuildPlaceholderSprite()
        {
            var size = Mathf.Max(8, PlaceholderPixels);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "DobeCat_Placeholder",
            };
            var clear = new Color(0, 0, 0, 0);
            var center = (size - 1) * 0.5f;
            var radius = size * 0.45f;
            var earH = size * 0.22f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x - center;
                var dy = y - center;
                var distBody = Mathf.Sqrt(dx * dx + dy * dy);
                var inBody = distBody <= radius;

                // 两只耳朵（三角近似）
                var earYThresh = center + radius * 0.55f;
                var leftEar  = (dy > earYThresh - earH) && (dx < -radius * 0.35f) && (dx > -radius * 0.85f) && (dy < earYThresh + earH * 0.6f);
                var rightEar = (dy > earYThresh - earH) && (dx >  radius * 0.35f) && (dx <  radius * 0.85f) && (dy < earYThresh + earH * 0.6f);

                if (inBody || leftEar || rightEar)
                {
                    // 边缘略暗
                    var t = Mathf.Clamp01(distBody / radius);
                    var c = PlaceholderColor;
                    c.r *= Mathf.Lerp(1f, 0.7f, t);
                    c.g *= Mathf.Lerp(1f, 0.7f, t);
                    c.b *= Mathf.Lerp(1f, 0.7f, t);
                    tex.SetPixel(x, y, c);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }

            // 简单眼睛
            DrawDot(tex, (int)(center - radius * 0.3f), (int)(center + radius * 0.05f), 3, Color.black);
            DrawDot(tex, (int)(center + radius * 0.3f), (int)(center + radius * 0.05f), 3, Color.black);

            tex.Apply();
            return Sprite.Create(tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: size); // 1 unit = 整张图
        }

        private static void DrawDot(Texture2D tex, int cx, int cy, int r, Color c)
        {
            for (var y = -r; y <= r; y++)
            for (var x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r) continue;
                var px = cx + x;
                var py = cy + y;
                if (px < 0 || py < 0 || px >= tex.width || py >= tex.height) continue;
                tex.SetPixel(px, py, c);
            }
        }
    }
}
