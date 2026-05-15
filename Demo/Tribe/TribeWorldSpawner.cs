using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;
using Demo.Tribe.Player;

namespace Demo.Tribe
{
    /// <summary>
    /// 分区段随机世界生成器 —— 使用泊松圆盘采样 + 加权随机表，
    /// 在多个 <see cref="SpawnZone"/> 中撒布资源和敌人。
    /// </summary>
    public class TribeWorldSpawner
    {
        // ─── 数据定义 ─────────────────────────────────────────

        /// <summary>生成物种类。</summary>
        public enum SpawnKind { Resource, Enemy, Animal, Decoration }

        /// <summary>单条生成物定义。</summary>
        [Serializable]
        public class SpawnEntry
        {
            public string Id;               // 唯一标识（如 "tribe_red_mushroom"）
            public string DisplayName;
            public SpawnKind Kind;
            public float Weight = 1f;        // 权重（越高越常出现）
            public float YOffset;            // 相对地面 Y 偏移

            // ─── Resource 专用
            public string SpriteResourcePath;
            public string PickableId;
            public float Hp = 1f;
            public int DropAmount = 1;

            // ─── Enemy 专用
            public int EnemySortingOrderOffset = 2;
            public Enemy.TribeCreatureConfig CreatureConfig; // 有值时使用通用 TribeCreature

            // ─── Decoration 专用（颜色块占位 / 实际素材）
            public Color PlaceholderColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            public Vector2 PlaceholderSize = new Vector2(1f, 1f);
            public float DecorationScale = 1f; // 有素材时的统一缩放
        }

        /// <summary>一个区段的生成规则。</summary>
        [Serializable]
        public class SpawnZone
        {
            public string ZoneName;
            public float StartX;
            public float EndX;
            public float MinSpacing = 2f;     // 泊松最小间距
            public int MaxAttempts = 30;       // 泊松每点最大尝试次数
            public SpawnEntry[] Entries;       // 该区段可生成的物种
        }

        // ─── 运行时状态 ───────────────────────────────────────

        private readonly float _groundY;
        private readonly int _baseSortingOrder;
        private readonly int _enemyLayer;
        private readonly Transform _gatherablesRoot;
        private readonly Transform _enemiesRoot;

        public TribeWorldSpawner(float groundY, int baseSortingOrder, int enemyLayer,
            Transform gatherablesRoot, Transform enemiesRoot)
        {
            _groundY = groundY;
            _baseSortingOrder = baseSortingOrder;
            _enemyLayer = enemyLayer;
            _gatherablesRoot = gatherablesRoot;
            _enemiesRoot = enemiesRoot;
        }

        // ─── 主入口 ───────────────────────────────────────────

        /// <summary>根据 seed 和区段规则执行全部生成。</summary>
        public void Generate(SpawnZone[] zones, int seed)
        {
            var rng = new System.Random(seed);
            var totalSpawned = 0;

            foreach (var zone in zones)
            {
                if (zone.Entries == null || zone.Entries.Length == 0) continue;

                // 泊松圆盘采样：在 [StartX, EndX] 上生成不重叠的 X 坐标
                var points = PoissonDisk1D(zone.StartX, zone.EndX, zone.MinSpacing, zone.MaxAttempts, rng);

                // 预计算权重总和
                var totalWeight = 0f;
                foreach (var e in zone.Entries) totalWeight += Mathf.Max(0f, e.Weight);
                if (totalWeight <= 0f) continue;

                foreach (var x in points)
                {
                    var entry = PickWeighted(zone.Entries, totalWeight, rng);
                    if (entry == null) continue;
                    var worldPos = new Vector3(x, _groundY + entry.YOffset, 0f);
                    SpawnOne(entry, worldPos);
                    totalSpawned++;
                }

                Debug.Log($"[TribeWorldSpawner] 区段 '{zone.ZoneName}': 生成 {points.Count} 个实体 (x={zone.StartX:0.0}~{zone.EndX:0.0})");
            }

            Debug.Log($"[TribeWorldSpawner] 生成完成，共 {totalSpawned} 个实体 (seed={seed})");
        }

        // ─── 生成单个实体 ─────────────────────────────────────

        private void SpawnOne(SpawnEntry entry, Vector3 position)
        {
            switch (entry.Kind)
            {
                case SpawnKind.Resource:
                    SpawnResource(entry, position);
                    break;
                case SpawnKind.Enemy:
                    SpawnEnemy(entry, position);
                    break;
                case SpawnKind.Animal:
                    SpawnAnimal(entry, position);
                    break;
                case SpawnKind.Decoration:
                    SpawnDecoration(entry, position);
                    break;
            }
        }

        private void SpawnResource(SpawnEntry entry, Vector3 position)
        {
            var go = new GameObject(entry.DisplayName);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 6f;
            if (_gatherablesRoot != null) go.transform.SetParent(_gatherablesRoot, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = LoadObjectSprite(entry.SpriteResourcePath);
            sr.sortingOrder = _baseSortingOrder;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            col.isTrigger = true;

            var entity = go.AddComponent<PickableDropEntity>();
            entity.Configure(entry.PickableId, entry.Hp, entry.DropAmount, "player");
        }

        private void SpawnEnemy(SpawnEntry entry, Vector3 position)
        {
            var go = new GameObject(entry.DisplayName);
            go.transform.position = position;
            go.layer = _enemyLayer;
            if (_enemiesRoot != null) go.transform.SetParent(_enemiesRoot, true);

            if (entry.CreatureConfig != null)
            {
                var creature = go.AddComponent<Enemy.TribeCreature>();
                creature.Configure(entry.CreatureConfig);
                creature.SortingOrder = _baseSortingOrder + entry.EnemySortingOrderOffset;
            }
            else
            {
                var enemy = go.AddComponent<TribeSkeletonEnemy>();
                enemy.SortingOrder = _baseSortingOrder + entry.EnemySortingOrderOffset;
            }
        }

        private void SpawnAnimal(SpawnEntry entry, Vector3 position)
        {
            if (entry.CreatureConfig == null) return;
            var go = new GameObject(entry.DisplayName);
            go.transform.position = position;
            if (_gatherablesRoot != null) go.transform.SetParent(_gatherablesRoot, true);

            var creature = go.AddComponent<Enemy.TribeCreature>();
            creature.Configure(entry.CreatureConfig);
            creature.SortingOrder = _baseSortingOrder + entry.EnemySortingOrderOffset;
        }

        private void SpawnDecoration(SpawnEntry entry, Vector3 position)
        {
            var go = new GameObject(entry.DisplayName);
            if (_gatherablesRoot != null) go.transform.SetParent(_gatherablesRoot, true);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _baseSortingOrder;

            // 有素材路径 → 加载实际 Sprite
            var hasSprite = !string.IsNullOrEmpty(entry.SpriteResourcePath);
            if (hasSprite)
            {
                sr.sprite = LoadObjectSprite(entry.SpriteResourcePath);
                var scale = entry.DecorationScale;
                go.transform.position = new Vector3(position.x, position.y, 0f);
                go.transform.localScale = Vector3.one * scale;
            }
            else
            {
                // 颜色块占位
                var size = entry.PlaceholderSize;
                var centerY = position.y + size.y * 0.5f;
                go.transform.position = new Vector3(position.x, centerY, 0f);
                go.transform.localScale = new Vector3(size.x, size.y, 1f);
                sr.sprite = CreatePlaceholderSprite();
                sr.color = entry.PlaceholderColor;

                // 文字标签
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localScale = new Vector3(1f / size.x, 1f / size.y, 1f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = entry.DisplayName;
                tm.characterSize = 0.15f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 48;
                tm.color = Color.white;
                var tmr = labelGo.GetComponent<MeshRenderer>();
                if (tmr != null) tmr.sortingOrder = _baseSortingOrder + 1;
            }
        }

        private static Sprite _placeholderSprite;
        private static Sprite CreatePlaceholderSprite()
        {
            if (_placeholderSprite != null) return _placeholderSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _placeholderSprite;
        }

        // ─── 1D 泊松圆盘采样 ─────────────────────────────────

        /// <summary>
        /// 在 [minX, maxX] 区间上生成满足最小间距 <paramref name="minDist"/> 的随机点集。
        /// 简化的 1D 活跃列表算法。
        /// </summary>
        private static List<float> PoissonDisk1D(float minX, float maxX, float minDist, int maxAttempts, System.Random rng)
        {
            var result = new List<float>();
            if (maxX <= minX || minDist <= 0f) return result;

            var range = maxX - minX;

            // 第一个点随机
            var first = minX + (float)(rng.NextDouble() * range);
            result.Add(first);

            var activeList = new List<int> { 0 };

            while (activeList.Count > 0)
            {
                var idx = rng.Next(activeList.Count);
                var point = result[activeList[idx]];
                var found = false;

                for (var attempt = 0; attempt < maxAttempts; attempt++)
                {
                    // 在 [minDist, 2*minDist] 范围偏移
                    var offset = minDist + (float)(rng.NextDouble() * minDist);
                    if (rng.Next(2) == 0) offset = -offset;
                    var candidate = point + offset;

                    if (candidate < minX || candidate > maxX) continue;

                    // 检查与所有已有点的距离
                    var tooClose = false;
                    for (var j = 0; j < result.Count; j++)
                    {
                        if (Mathf.Abs(result[j] - candidate) < minDist)
                        {
                            tooClose = true;
                            break;
                        }
                    }

                    if (!tooClose)
                    {
                        result.Add(candidate);
                        activeList.Add(result.Count - 1);
                        found = true;
                        break;
                    }
                }

                if (!found) activeList.RemoveAt(idx);
            }

            return result;
        }

        // ─── 加权随机选取 ─────────────────────────────────────

        private static SpawnEntry PickWeighted(SpawnEntry[] entries, float totalWeight, System.Random rng)
        {
            var roll = (float)(rng.NextDouble() * totalWeight);
            var cumulative = 0f;
            foreach (var e in entries)
            {
                cumulative += Mathf.Max(0f, e.Weight);
                if (roll <= cumulative) return e;
            }
            return entries[entries.Length - 1];
        }

        // ─── 工具 ────────────────────────────────────────────

        private static Sprite LoadObjectSprite(string spriteResourcePath)
        {
            if (string.IsNullOrEmpty(spriteResourcePath)) return null;
            var sprites = Resources.LoadAll<Sprite>(spriteResourcePath);
            if (sprites != null && sprites.Length >= 3) return sprites[2];
            if (sprites != null && sprites.Length > 0) return sprites[0];
            return Resources.Load<Sprite>(spriteResourcePath);
        }
    }
}
