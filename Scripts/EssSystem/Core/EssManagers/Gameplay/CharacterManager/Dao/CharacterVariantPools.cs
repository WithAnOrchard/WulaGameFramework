using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao
{
    /// <summary>
    /// 部件变体池 —— 给 <c>CharacterPreviewPanel</c> 用：返回某 (Model, Part) 下可选的 <b>sheet 前缀</b> 列表。
    /// <para>每个 sheet 前缀对应一张 PNG（已被切片工具切成 35 个子 Sprite），
    /// 切换变体即让该部件重建为这张 sheet 对应的 8 个 Action。</para>
    /// <para>实现：<see cref="Resources.LoadAll{T}(string)"/> 加载某目录下的 Texture2D
    /// （列出 PNG 而不是子 Sprite），用 <see cref="DerivePrefix"/> 推导前缀。</para>
    /// </summary>
    public static class CharacterVariantPools
    {
        #region Pool Definition

        private struct Pool
        {
            /// <summary>Resources 相对路径（每个非递归扫描 Texture2D）。</summary>
            public string[] Dirs;
            /// <summary>仅返回文件名以此前缀开头（空 = 不过滤）。</summary>
            public string PrefixFilter;
        }

        private const string PA = "Sprites/Characters/PixArt/";

        // 内置 Model + 部件 → 变体目录映射
        private static readonly Dictionary<(string Model, string Part), Pool> _builtin =
            new Dictionary<(string, string), Pool>
            {
                {("Warrior", "Skin"),  new Pool { Dirs = new[]{ PA + "Skin"  }, PrefixFilter = "warrior_" }},
                {("Warrior", "Cloth"), new Pool { Dirs = new[]{ PA + "Cloth" }, PrefixFilter = "warrior_" }},
                {("Warrior", "Head"),  new Pool { Dirs = new[]{
                    PA + "Headgear/Helmet/Close",
                    PA + "Headgear/Helmet/Open",
                    PA + "Headgear/Helmet/Extra",
                    PA + "Headgear/Helmet/Extra/1",
                }}},
                {("Warrior", "Weapon"), new Pool { Dirs = new[]{ PA + "Weapon/Sword" }}},
                {("Warrior", "Shield"), new Pool { Dirs = new[]{ PA + "Equipment/Shield" }}},

                {("Mage", "Skin"),  new Pool { Dirs = new[]{ PA + "Skin"  }, PrefixFilter = "mage_" }},
                {("Mage", "Cloth"), new Pool { Dirs = new[]{ PA + "Cloth" }, PrefixFilter = "mage_" }},
                {("Mage", "Head"),  new Pool { Dirs = new[]{
                    PA + "Headgear/Hood/Closed",
                    PA + "Headgear/Hood/Open",
                    PA + "Headgear/WitchHat/1",
                    PA + "Headgear/WitchHat/2",
                }}},
                {("Mage", "Weapon"), new Pool { Dirs = new[]{ PA + "Weapon/Rod" }}},
            };

        // 跨职业共用部件
        private static readonly Dictionary<string, Pool> _shared =
            new Dictionary<string, Pool>
            {
                { "Eyes", new Pool { Dirs = new[]{ PA + "Eyes" }}},
                { "Cape", new Pool { Dirs = new[]{ PA + "Cape" }}},
                { "Hair", new Pool { Dirs = new[]{
                    PA + "Hair/1", PA + "Hair/2", PA + "Hair/3", PA + "Hair/4",
                }}},
            };

        #endregion

        /// <summary>
        /// 返回 (modelId, partId) 可选的 sheet 前缀列表（如 <c>Skin_warrior_1</c>），按字母序排序。
        /// 找不到匹配返回空列表。
        /// </summary>
        public static List<string> GetVariants(string modelId, string partId)
        {
            if (_builtin.TryGetValue((modelId, partId), out var pool))
                return Enumerate(pool);

            if (_shared.TryGetValue(partId ?? string.Empty, out var sharedPool))
                return Enumerate(sharedPool);

            return new List<string>();
        }

        /// <summary>取下一个 sheet 前缀（循环）。</summary>
        public static string Next(List<string> variants, string current)
        {
            if (variants == null || variants.Count == 0) return current;
            var idx = variants.IndexOf(current);
            return variants[(idx + 1) % variants.Count];
        }

        /// <summary>取上一个 sheet 前缀（循环）。</summary>
        public static string Prev(List<string> variants, string current)
        {
            if (variants == null || variants.Count == 0) return current;
            var idx = variants.IndexOf(current);
            if (idx < 0) return variants[0];
            return variants[(idx - 1 + variants.Count) % variants.Count];
        }

        #region Enumeration

        private static List<string> Enumerate(Pool pool)
        {
            var result = new List<string>();
            if (pool.Dirs == null) return result;
            foreach (var dir in pool.Dirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                // 加载 Texture2D（即 PNG 本身，每个 PNG 对应一个 sheet）
                var textures = Resources.LoadAll<Texture2D>(dir);
                if (textures == null) continue;
                foreach (var tex in textures)
                {
                    if (tex == null) continue;
                    if (!string.IsNullOrEmpty(pool.PrefixFilter) &&
                        !tex.name.StartsWith(pool.PrefixFilter, System.StringComparison.Ordinal))
                        continue;
                    // 切片工具命名规则：DerivePrefix(资源路径) = "{dir.Replace('/','_')}_{tex.name}"
                    result.Add(DerivePrefix(dir, tex.name));
                }
            }
            result.Sort(System.StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// 推导 sheet 前缀（与切片工具 <c>CharacterSpriteSheetSlicer.DerivePrefix</c> 一致的规则）。
        /// 例：<c>(Sprites/Characters/PixArt/Headgear/Helmet/Close, "1") → Headgear_Helmet_Close_1</c>
        /// </summary>
        public static string DerivePrefix(string resourcesRelativeDir, string textureName)
        {
            var d = (resourcesRelativeDir ?? string.Empty).Replace('\\', '/');
            if (d.StartsWith(PA)) d = d.Substring(PA.Length);
            d = d.Replace('/', '_');
            return string.IsNullOrEmpty(d) ? textureName : d + "_" + textureName;
        }

        #endregion
    }
}
