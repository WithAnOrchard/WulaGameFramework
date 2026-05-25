using System.Collections.Generic;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;
using UnityEngine;

namespace Demo.DobeCat.Game.Farm
{
    /// <summary>
    /// 单块农田格子的世界实体组件。
    /// <list type="bullet">
    /// <item>SpriteRenderer 显示颜色状态。</item>
    /// <item>BoxCollider2D 用于世界坐标命中测试。</item>
    /// <item>Label（TextMesh）由 <see cref="FarmWorldController"/> 作为独立 GO 管理，避免继承缩放问题。</item>
    /// </list>
    /// </summary>
    public class FarmTileObject : MonoBehaviour
    {
        public int Row, Col;

        private static readonly string[] WiltedSpriteIds = { "Plants_154", "Plants_155", "Plants_156", "Plants_157" };
        private string _wiltedSpriteId;

        internal TextMesh Label;

        /// <summary>植物精灵渲染器（由 FarmWorldController 在 SpawnTiles 时赋值）。</summary>
        internal SpriteRenderer PlantRenderer;
        internal Transform      PlantTransform;

        private static Sprite[]                    _plantsSheet;
        private static readonly Dictionary<string, Sprite> _spriteCache
            = new Dictionary<string, Sprite>();

        internal void Init(int row, int col)
        {
            Row = row; Col = col;
            var c2d  = gameObject.AddComponent<BoxCollider2D>();
            c2d.size = Vector2.one * 0.94f;
        }

        /// <summary>刷新格子显示（带 CropConfig 则显示植物精灵，否则只显示颜色）。</summary>
        public void UpdateVisual(FarmSlot slot, CropConfig config = null)
        {
            if (slot == null || slot.Stage == CropGrowthStage.Empty)
            {
                _wiltedSpriteId = null;
                SetLabel("种植");
                SetPlantSprite(null);
                return;
            }

            var w    = slot.Watered ? "💧" : "";
            var p    = slot.HasPest ? "🐛" : "";
            var name = config != null ? config.DisplayName : slot.CropConfigId;

            switch (slot.Stage)
            {
                case CropGrowthStage.Seed:    SetLabel($"{name}{p}");  break;
                case CropGrowthStage.Sprout:  SetLabel($"{w}{p}");     break;
                case CropGrowthStage.Growing: SetLabel($"{w}{p}");     break;
                case CropGrowthStage.Mature:  SetLabel("✓收获");        break;
                case CropGrowthStage.Wilted:  SetLabel("枯萎");          break;
                default:                      SetLabel("");             break;
            }

            SetPlantSprite(GetStageSprite(config, slot.Stage));
        }

        private void SetPlantSprite(Sprite sprite)
        {
            if (PlantRenderer == null) return;
            if (sprite == null)
            {
                PlantRenderer.gameObject.SetActive(false);
                return;
            }
            PlantRenderer.sprite = sprite;
            PlantRenderer.gameObject.SetActive(true);

            // 自适应缩放：目标高度 ≈ TileH * 0.95，不超过 TileW * 0.85 宽
            var sprH = sprite.bounds.size.y;
            var sprW = sprite.bounds.size.x;
            var targetH = FarmWorldController.TileH * 0.95f * 0.25f;
            var targetW = FarmWorldController.TileW * 0.85f * 0.25f;
            var s = Mathf.Min(
                sprH > 0.001f ? targetH / sprH : 1f,
                sprW > 0.001f ? targetW / sprW : 1f
            );
            if (PlantTransform != null)
                PlantTransform.localScale = Vector3.one * s;
        }

        private Sprite GetStageSprite(CropConfig config, CropGrowthStage stage)
        {
            // 枯萎阶段一律使用通用图片（Plants_154~157 随机一个）
            if (stage == CropGrowthStage.Wilted)
            {
                if (_wiltedSpriteId == null)
                    _wiltedSpriteId = WiltedSpriteIds[UnityEngine.Random.Range(0, WiltedSpriteIds.Length)];
                return LoadPlantSprite(_wiltedSpriteId);
            }

            if (config?.StageSpriteIds == null || config.StageSpriteIds.Count == 0) return null;
            int idx;
            switch (stage)
            {
                case CropGrowthStage.Seed:    idx = 0; break;
                case CropGrowthStage.Sprout:  idx = 1; break;
                case CropGrowthStage.Growing: idx = 2; break;
                case CropGrowthStage.Mature:  idx = Mathf.Min(3, config.StageSpriteIds.Count - 1); break;
                default: return null;
            }
            return idx < config.StageSpriteIds.Count ? LoadPlantSprite(config.StageSpriteIds[idx]) : null;
        }

        private static Sprite LoadPlantSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
            if (_spriteCache.TryGetValue(spriteName, out var cached) && cached != null) return cached;
            if (_plantsSheet == null)
                _plantsSheet = Resources.LoadAll<Sprite>("Sprites/Plants/Plants");
            if (_plantsSheet != null)
                foreach (var s in _plantsSheet)
                    if (s.name == spriteName) { _spriteCache[spriteName] = s; return s; }
            return null;
        }

        /// <summary>世界坐标命中测试（供 FarmWorldController 使用）。</summary>
        public bool HitTest(Vector2 worldPoint)
        {
            var pos = transform.position;
            var scl = transform.localScale;
            float hw = scl.x * 0.5f;
            float hh = scl.y * 0.5f;
            return worldPoint.x >= pos.x - hw && worldPoint.x <= pos.x + hw
                && worldPoint.y >= pos.y - hh && worldPoint.y <= pos.y + hh;
        }

        private void SetLabel(string text)
        {
            if (Label != null) Label.text = text;
        }

    }
}
