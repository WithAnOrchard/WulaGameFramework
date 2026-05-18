using UnityEngine;
using Demo.Tribe.Entities;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 生物刷新点 Feature —— 在指定 X 放一个 <see cref="TribeCreatureSpawner"/>，
    /// 周期性生成给定 preset 的生物。比 <see cref="CreatureFeature"/> 适合"巢穴/营地外威胁"语义。
    /// </summary>
    public class CreatureSpawnerFeature : TribeFeatureSpec
    {
        public string DisplayName = "刷新点";
        public string SpawnNamePrefix = "怪_";
        public TribeCreatureConfig CreatureConfig;

        public int MaxAlive = 3;
        public float Interval = 8f;
        public int BatchSize = 1;
        public float InitialDelay = 3f;
        public float HorizontalJitter = 1.5f;

        /// <summary>SortingOrder 偏移（默认 +2，匹配 <see cref="CreatureFeature.SortingOffset"/>）。</summary>
        public int SortingOffset = 2;

        /// <summary>是否在生成点画一个简单标记（圆形地痕），便于玩家辨识。</summary>
        public bool DrawMarker = true;

        /// <summary>标记颜色（半透明）。</summary>
        public Color MarkerColor = new Color(0.85f, 0.2f, 0.25f, 0.55f);

        /// <summary>标记半径（世界单位）。</summary>
        public float MarkerRadius = 0.9f;

        public CreatureSpawnerFeature(float worldX, string displayName,
            TribeCreatureConfig config, string spawnNamePrefix,
            int maxAlive = 3, float interval = 8f, float initialDelay = 3f,
            float yOffset = 0f)
        {
            WorldX = worldX;
            YOffset = yOffset;
            DisplayName = displayName;
            SpawnNamePrefix = spawnNamePrefix;
            CreatureConfig = config;
            MaxAlive = maxAlive;
            Interval = interval;
            InitialDelay = initialDelay;
        }

        public override void Build(TribeBiomeContext ctx)
        {
            if (CreatureConfig == null)
            {
                Debug.LogWarning($"[CreatureSpawnerFeature] {DisplayName} 缺 TribeCreatureConfig，跳过");
                return;
            }
            var go = new GameObject($"Spawner_{DisplayName}");
            go.transform.position = ComputeWorldPosition(ctx);
            if (ctx.EnemiesRoot != null) go.transform.SetParent(ctx.EnemiesRoot, true);

            if (DrawMarker) BuildMarker(go.transform, ctx);

            var sp = go.AddComponent<TribeCreatureSpawner>();
            sp.CreatureConfig = CreatureConfig;
            sp.DisplayNamePrefix = SpawnNamePrefix;
            sp.MaxAlive = MaxAlive;
            sp.Interval = Interval;
            sp.BatchSize = BatchSize;
            sp.InitialDelay = InitialDelay;
            sp.HorizontalJitter = HorizontalJitter;
            sp.SortingOrder = ctx.BaseSortingOrder + SortingOffset;
            sp.EnemiesRoot = ctx.EnemiesRoot;
        }

        // ─── 标记视觉（地痕圆环）─────────────────────────────
        private void BuildMarker(Transform parent, TribeBiomeContext ctx)
        {
            var marker = new GameObject("Marker");
            marker.transform.SetParent(parent, false);
            // 略低于地面线，避免遮住生物
            marker.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            marker.transform.localScale = new Vector3(MarkerRadius * 2f, MarkerRadius * 2f, 1f);

            var sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = GetOrCreateDiscSprite();
            sr.color = MarkerColor;
            // 地痕：盖在地面 sprite 之上、生物之下（SortingOffset 默认 2）
            sr.sortingOrder = ctx.BaseSortingOrder + SortingOffset - 1;
        }

        // ─── 内置圆盘 sprite（运行时合成，缓存共享）────────────
        private static Sprite _cachedDisc;
        private static Sprite GetOrCreateDiscSprite()
        {
            if (_cachedDisc != null) return _cachedDisc;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var center = new Vector2(size * 0.5f, size * 0.5f);
            var radius = size * 0.5f - 1f;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                var a = Mathf.Clamp01(1f - d / radius);
                var alpha = (byte)(Mathf.Pow(a, 1.6f) * 255f);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            _cachedDisc = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 64f);
            _cachedDisc.name = "TribeSpawner_Marker_Disc";
            return _cachedDisc;
        }
    }
}
