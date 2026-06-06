using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Demo.Cubic.Editor
{
    /// <summary>
    /// Cubic VFX 占位 Prefab 生成器 —— 给 <see cref="CubicVFXManager"/> 登记的 19 个
    /// vfxId 各造一个最简 Prefab（SpriteRenderer + 软色圆），保证 <c>EffectsManager.EVT_PLAY_VFX</c>
    /// 在第一次运行时能 <c>ResourceManager.GetPrefab</c> 拿到资源、池化播放。
    /// <para>
    /// <b>仅 Editor 期执行</b>。行为：
    /// <list type="bullet">
    /// <item><see cref="InitializeOnLoadMethod"/>：每次脚本重载后扫一遍 <c>Assets/Demo/Cubic/Resources/VFX/Cubic/</c>，缺啥补啥（不覆盖已有）。</item>
/// <item><see cref="MenuItem"/> <c>Tools/WulaSystem/Demo/Cubic/VFX/Regenerate All VFX Prefabs</c>：强制覆盖全部 19 个（改色后用）。</item>
/// <item><see cref="MenuItem"/> <c>Tools/WulaSystem/Demo/Cubic/VFX/Check Missing VFX Prefabs</c>：只报告缺失，不写盘。</item>
    /// </list>
    /// </para>
    /// <para>
    /// 占位美术很糙（纯色圆 + 高 sortingOrder）；用户可随后用真正 VFX Prefab 替换同名文件，
    /// <c>PrefabUtility.SaveAsPrefabAsset</c> 写盘后 git diff 自然可见。
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class CubicVFXPrefabGenerator
    {
        // ─── 路径约定 ───────────────────────────────────────────
        private const string MENU_PREFIX = "Tools/WulaSystem/Demo/Cubic/VFX/";
        public const string PrefabFolder = "Assets/Demo/Cubic/Resources/VFX/Cubic";

        // ─── 19 个 VFX 的色调映射（保持与原 CubicVFXManager 一致；业务方替换 prefab 时仅改资源）──
        private static readonly Dictionary<string, VfxSpec> _specs = new()
        {
            // 战士 — 红
            { "warrior_slash",     new("WarriorSlash",     new Color(1.00f, 0.20f, 0.20f, 1f), 1.6f) },
            { "warrior_shout",     new("WarriorShout",     new Color(1.00f, 0.85f, 0.20f, 1f), 1.8f) },
            { "warrior_whirlwind", new("WarriorWhirlwind", new Color(0.95f, 0.30f, 0.30f, 1f), 2.0f) },
            { "warrior_hit",       new("HitBlood",         new Color(0.85f, 0.10f, 0.10f, 1f), 1.0f) },
            // 魔法师 — 橙/蓝/紫
            { "mage_fireball",     new("MageFireball",     new Color(1.00f, 0.50f, 0.00f, 1f), 1.2f) },
            { "mage_frost_nova",   new("MageFrostNova",    new Color(0.50f, 0.80f, 1.00f, 1f), 2.0f) },
            { "mage_lightning",    new("MageLightning",    new Color(0.60f, 0.20f, 1.00f, 1f), 1.4f) },
            { "mage_impact",       new("MageImpact",       new Color(0.70f, 0.30f, 1.00f, 1f), 1.0f) },
            // 弓箭手 — 绿
            { "archer_arrow",      new("ArcherArrow",      new Color(0.20f, 0.80f, 0.20f, 1f), 1.0f) },
            { "archer_pierce",     new("ArcherPierce",     new Color(0.30f, 0.95f, 0.40f, 1f), 1.6f) },
            { "archer_dash",       new("ArcherDash",       new Color(0.45f, 0.90f, 0.50f, 1f), 1.2f) },
            { "archer_hit",        new("HitBlood",         new Color(0.85f, 0.10f, 0.10f, 1f), 1.0f) },
            // 圣骑士 — 金
            { "paladin_holy",      new("PaladinHoly",      new Color(1.00f, 0.85f, 0.20f, 1f), 1.4f) },
            { "paladin_hammer",    new("PaladinHammer",    new Color(1.00f, 0.80f, 0.10f, 1f), 1.6f) },
            { "paladin_devotion",  new("PaladinDevotion",  new Color(1.00f, 0.90f, 0.40f, 1f), 1.8f) },
            { "paladin_heal",      new("PaladinHeal",      new Color(0.90f, 1.00f, 0.50f, 1f), 1.2f) },
            // 通用
            { "hit_blood",         new("HitBlood",         new Color(0.85f, 0.10f, 0.10f, 1f), 1.0f) },
            { "explosion",         new("Explosion",        new Color(1.00f, 0.50f, 0.00f, 1f), 2.0f) },
            { "shield",            new("Shield",           new Color(0.50f, 0.80f, 1.00f, 1f), 1.6f) },
        };

        private struct VfxSpec
        {
            public string PrefabName;
            public Color  Color;
            public float  Scale;
            public VfxSpec(string name, Color color, float scale) { PrefabName = name; Color = color; Scale = scale; }
        }

        // ─── InitializeOnLoad ────────────────────────────────────
        static CubicVFXPrefabGenerator()
        {
            // 域重载后会重新进入；延迟到 EditorApplication.update 让 AssetDatabase 完全就绪
            EditorApplication.delayCall += EnsureAllPresent;
        }

        [InitializeOnLoadMethod]
        private static void EnsureAllPresent()
        {
            if (Application.isPlaying) return;
            EnsureFolder(PrefabFolder);

            int created = 0;
            foreach (var kv in _specs)
            {
                var path = $"{PrefabFolder}/{kv.Value.PrefabName}.prefab";
                if (File.Exists(path)) continue;
                CreatePrefab(kv.Key, kv.Value, path);
                created++;
            }
            if (created > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[CubicVFXPrefabGenerator] 自动补齐 {created} 个 VFX Prefab → {PrefabFolder}");
            }
        }

        // ─── MenuItem ────────────────────────────────────────────

        [MenuItem(MENU_PREFIX + "Check Missing VFX Prefabs")]
        private static void CheckMissing()
        {
            EnsureFolder(PrefabFolder);
            var missing = new List<string>();
            foreach (var kv in _specs)
            {
                var path = $"{PrefabFolder}/{kv.Value.PrefabName}.prefab";
                if (!File.Exists(path)) missing.Add(kv.Value.PrefabName);
            }
            if (missing.Count == 0)
                Debug.Log($"[CubicVFXPrefabGenerator] 全部 19 个 Prefab 已就位。");
            else
                Debug.LogWarning($"[CubicVFXPrefabGenerator] 缺失 {missing.Count} 个：\n  - {string.Join("\n  - ", missing)}");
        }

        [MenuItem(MENU_PREFIX + "Regenerate All VFX Prefabs")]
        private static void RegenerateAll()
        {
            if (Application.isPlaying)
            {
                Debug.LogWarning("[CubicVFXPrefabGenerator] Play 模式中禁止覆盖 Prefab。");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                "覆盖全部 Cubic VFX Prefab？",
                $"将强制覆盖 {PrefabFolder} 下 19 个 .prefab。\n自定义美术会被冲掉，确认？",
                "确认覆盖", "取消"))
            {
                return;
            }

            EnsureFolder(PrefabFolder);
            int n = 0;
            foreach (var kv in _specs)
            {
                var path = $"{PrefabFolder}/{kv.Value.PrefabName}.prefab";
                CreatePrefab(kv.Key, kv.Value, path);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CubicVFXPrefabGenerator] 强制覆盖 {n} 个 VFX Prefab。");
        }

        // ─── 构造单个 Prefab ─────────────────────────────────────

        private static void CreatePrefab(string vfxId, VfxSpec spec, string assetPath)
        {
            // 1. 临时 GameObject
            var go = new GameObject($"VFX_{spec.PrefabName}");
            try
            {
                // 2. SpriteRenderer + 软色圆贴图
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite         = BuildSoftCircleSprite(spec.Color, out var texture);
                sr.color          = Color.white;          // 颜色已经在贴图里写死
                sr.sortingOrder   = 100;                  // 高于 Gameplay
                sr.transform.localScale = new Vector3(spec.Scale, spec.Scale, 1f);

                // 3. 存盘
                PrefabUtility.SaveAsPrefabAsset(go, assetPath, out var success);
                if (!success) Debug.LogError($"[CubicVFXPrefabGenerator] 保存失败: {assetPath}");

                // 4. 把运行时 texture 嵌进 prefab（避免依赖外部 .png）
                // SaveAsPrefabAsset 已经把 sprite 引用带进去了；texture 是 sub-asset 也带上。
                // 这里不需要额外操作。
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>程序化生成 64x64 软色圆贴图（中心不透明，向外 alpha 衰减）。</summary>
        private static Sprite BuildSoftCircleSprite(Color color, out Texture2D tex)
        {
            const int size = 64;
            tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name       = "VFX_SoftCircle",
            };
            var pixels = new Color32[size * size];
            var center = new Vector2(size * 0.5f, size * 0.5f);
            var maxDist = size * 0.5f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), center) / maxDist;  // 0..1
                    // 中心 1.0 → 边缘 0.0，做两次幂让边缘更柔
                    var a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    var c = new Color(color.r, color.g, color.b, a * color.a);
                    pixels[y * size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // ─── 工具：递归建文件夹 ────────────────────────────────
        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            var parent = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            var leaf   = Path.GetFileName(assetPath);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
