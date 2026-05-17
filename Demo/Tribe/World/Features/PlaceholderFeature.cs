using UnityEngine;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 占位 Feature —— 用纯色块 + 中文文字标签代替缺素材的对象。
    /// <para>
    /// 用途：Puddle / Stone / Portal / House / NPC 等设计中尚无 Sprite 的 Feature 临时替代。
    /// 成品素材到位后逐个换为 PuddleFeature / StoneClusterFeature / ... 具体类。
    /// 缺失素材清单见 <c>Demo/Tribe/need.md</c>。
    /// </para>
    /// </summary>
    public class PlaceholderFeature : TribeFeatureSpec
    {
        /// <summary>显示在色块下方的中文标签。</summary>
        public string Label = "?";

        /// <summary>色块颜色。</summary>
        public Color Color = new Color(0.5f, 0.5f, 0.5f, 1f);

        /// <summary>色块世界尺寸（宽 x 高，单位 = unit）。</summary>
        public Vector2 Size = new Vector2(1f, 1f);

        /// <summary>排序偏移（对前后景调节）。</summary>
        public int SortingOrderOffset;

        public PlaceholderFeature(float worldX, string label, Color color, Vector2 size,
            float yOffset = 0f, int sortingOffset = 0)
        {
            WorldX = worldX; YOffset = yOffset;
            Label = label; Color = color; Size = size;
            SortingOrderOffset = sortingOffset;
        }

        public override void Build(TribeBiomeContext ctx)
        {
            // 锚点：色块底边在 ctx.GroundY + YOffset
            var centerY = ctx.GroundY + YOffset + Size.y * 0.5f;
            var go = new GameObject($"Placeholder_{Label}");
            if (ctx.GatherablesRoot != null) go.transform.SetParent(ctx.GatherablesRoot, true);
            go.transform.position = new Vector3(WorldX, centerY, 0f);
            go.transform.localScale = new Vector3(Size.x, Size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteCache.GetWhitePixel();
            sr.color = Color;
            sr.sortingOrder = ctx.BaseSortingOrder + SortingOrderOffset;

            // 标签子节点（不受父级 scale 影响 → 反向缩放抵消）
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = Vector3.zero;
            labelGo.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Size.x),
                                                       1f / Mathf.Max(0.01f, Size.y), 1f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = Label;
            tm.characterSize = 0.12f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.HSVToRGB(0f, 0f, IsLight(this.Color) ? 0.05f : 0.95f);
            var tmr = labelGo.GetComponent<MeshRenderer>();
            if (tmr != null) tmr.sortingOrder = ctx.BaseSortingOrder + SortingOrderOffset + 1;
        }

        private static bool IsLight(Color c) => (c.r + c.g + c.b) / 3f > 0.55f;
    }

    /// <summary>1x1 白色 Sprite 缓存 —— 给 Placeholder 共用，避免每个 Feature 都建 Texture。</summary>
    internal static class PlaceholderSpriteCache
    {
        private static Sprite _whitePixel;

        public static Sprite GetWhitePixel()
        {
            if (_whitePixel != null) return _whitePixel;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whitePixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _whitePixel.name = "PlaceholderWhitePixel";
            return _whitePixel;
        }
    }
}
