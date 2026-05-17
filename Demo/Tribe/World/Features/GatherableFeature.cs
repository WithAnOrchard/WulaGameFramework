using UnityEngine;
using Demo.Tribe.Resource;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 可采集 Feature —— 复用 <see cref="PickableDropEntity"/>。
    /// 与原 <c>TribeGameManager.SpawnTribeAttackableEntity</c> 行为等价，迁移成 Feature 形态。
    /// </summary>
    public class GatherableFeature : TribeFeatureSpec
    {
        /// <summary>Inspector 显示名（场景中 GameObject 的名字）。</summary>
        public string DisplayName;

        /// <summary>Sprite 资源路径（走 Resources.LoadAll&lt;Sprite&gt;）。</summary>
        public string SpriteResourcePath;

        /// <summary>掉落 PickableItem Id（已注册到 InventoryManager）。</summary>
        public string PickableId;

        /// <summary>HP（被攻击次数）。</summary>
        public float Hp = 1f;

        /// <summary>掉落数量。</summary>
        public int DropAmount = 1;

        /// <summary>视觉缩放（与原 hardcoded 6f 对齐）。</summary>
        public float VisualScale = 6f;

        public GatherableFeature(float worldX, string displayName, string spritePath, string pickableId,
            float hp = 1f, int dropAmount = 1, float yOffset = -0.2f)
        {
            WorldX = worldX; YOffset = yOffset;
            DisplayName = displayName;
            SpriteResourcePath = spritePath;
            PickableId = pickableId;
            Hp = hp;
            DropAmount = dropAmount;
        }

        public override void Build(TribeBiomeContext ctx)
        {
            var go = new GameObject(DisplayName);
            go.transform.position = ComputeWorldPosition(ctx);
            go.transform.localScale = Vector3.one * VisualScale;
            if (ctx.GatherablesRoot != null) go.transform.SetParent(ctx.GatherablesRoot, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadObjectSprite(SpriteResourcePath);
            sr.sortingOrder = ctx.BaseSortingOrder;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;

            var entity = go.AddComponent<PickableDropEntity>();
            entity.Configure(PickableId, Hp, DropAmount, "player");
        }

        private static Sprite LoadObjectSprite(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var sprites = Resources.LoadAll<Sprite>(path);
            if (sprites != null && sprites.Length >= 3) return sprites[2];
            if (sprites != null && sprites.Length > 0) return sprites[0];
            return Resources.Load<Sprite>(path);
        }
    }
}
