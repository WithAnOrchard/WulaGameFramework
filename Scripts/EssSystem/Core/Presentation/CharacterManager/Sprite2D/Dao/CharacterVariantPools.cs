using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    /// <summary>
    /// 通用部件变体池注册表。
    /// <para>框架层只提供注册、枚举和循环切换能力，不内置任何具体素材目录或 Model/Part 业务含义。</para>
    /// <para>业务侧若需要预览面板支持变体切换，应在运行前调用 <see cref="RegisterPool"/> 或 <see cref="RegisterSharedPool"/>。</para>
    /// </summary>
    public static class CharacterVariantPools
    {
        private struct Pool
        {
            public string[] Dirs;
            public string PrefixRoot;
            public string PrefixFilter;
        }

        private static readonly Dictionary<(string Model, string Part), Pool> s_modelPools =
            new Dictionary<(string, string), Pool>();

        private static readonly Dictionary<string, Pool> s_sharedPools =
            new Dictionary<string, Pool>();

        /// <summary>清空所有已注册的业务变体池。</summary>
        public static void Clear()
        {
            s_modelPools.Clear();
            s_sharedPools.Clear();
        }

        /// <summary>
        /// 注册指定 Model + Part 的变体池。
        /// </summary>
        /// <param name="modelId">业务模型 ID；框架不解释其语义。</param>
        /// <param name="partId">部件 ID；框架不解释其语义。</param>
        /// <param name="resourceDirs">Resources 相对目录，每个目录非递归枚举 Texture2D。</param>
        /// <param name="prefixRoot">从目录路径中剥离的公共前缀；为空时使用完整目录参与前缀推导。</param>
        /// <param name="prefixFilter">可选文件名前缀过滤。</param>
        public static void RegisterPool(string modelId, string partId, IEnumerable<string> resourceDirs,
            string prefixRoot = null, string prefixFilter = null)
        {
            if (string.IsNullOrEmpty(modelId) || string.IsNullOrEmpty(partId)) return;
            s_modelPools[(modelId, partId)] = CreatePool(resourceDirs, prefixRoot, prefixFilter);
        }

        /// <summary>
        /// 注册跨 Model 共享的 Part 变体池。仅当没有命中指定 Model + Part 池时使用。
        /// </summary>
        public static void RegisterSharedPool(string partId, IEnumerable<string> resourceDirs,
            string prefixRoot = null, string prefixFilter = null)
        {
            if (string.IsNullOrEmpty(partId)) return;
            s_sharedPools[partId] = CreatePool(resourceDirs, prefixRoot, prefixFilter);
        }

        /// <summary>
        /// 返回 (modelId, partId) 可选 sheet 前缀列表，按字母序排序。找不到匹配返回空列表。
        /// </summary>
        public static List<string> GetVariants(string modelId, string partId)
        {
            if (!string.IsNullOrEmpty(modelId) && !string.IsNullOrEmpty(partId) &&
                s_modelPools.TryGetValue((modelId, partId), out var pool))
                return Enumerate(pool);

            if (s_sharedPools.TryGetValue(partId ?? string.Empty, out var sharedPool))
                return Enumerate(sharedPool);

            return new List<string>();
        }

        /// <summary>取下一个 sheet 前缀（循环）。</summary>
        public static string Next(List<string> variants, string current)
        {
            if (variants == null || variants.Count == 0) return current;
            var idx = variants.IndexOf(current);
            if (idx < 0) return variants[0];
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

        /// <summary>
        /// 从 Resources 相对目录和 Texture 名推导 sheet 前缀。
        /// </summary>
        public static string DerivePrefix(string resourcesRelativeDir, string textureName, string prefixRoot = null)
        {
            var d = (resourcesRelativeDir ?? string.Empty).Replace('\\', '/');
            var root = (prefixRoot ?? string.Empty).Replace('\\', '/');
            if (!string.IsNullOrEmpty(root))
            {
                if (!root.EndsWith("/")) root += "/";
                if (d.StartsWith(root, System.StringComparison.Ordinal)) d = d.Substring(root.Length);
            }

            d = d.Replace('/', '_');
            return string.IsNullOrEmpty(d) ? textureName : d + "_" + textureName;
        }

        private static Pool CreatePool(IEnumerable<string> resourceDirs, string prefixRoot, string prefixFilter)
        {
            var dirs = new List<string>();
            if (resourceDirs != null)
            {
                foreach (var dir in resourceDirs)
                {
                    if (string.IsNullOrEmpty(dir)) continue;
                    dirs.Add(dir.Replace('\\', '/'));
                }
            }

            return new Pool
            {
                Dirs = dirs.ToArray(),
                PrefixRoot = prefixRoot,
                PrefixFilter = prefixFilter
            };
        }

        private static List<string> Enumerate(Pool pool)
        {
            var result = new List<string>();
            if (pool.Dirs == null) return result;

            foreach (var dir in pool.Dirs)
            {
                if (string.IsNullOrEmpty(dir)) continue;
                var textures = Resources.LoadAll<Texture2D>(dir);
                if (textures == null) continue;

                foreach (var tex in textures)
                {
                    if (tex == null) continue;
                    if (!string.IsNullOrEmpty(pool.PrefixFilter) &&
                        !tex.name.StartsWith(pool.PrefixFilter, System.StringComparison.Ordinal))
                        continue;

                    result.Add(DerivePrefix(dir, tex.name, pool.PrefixRoot));
                }
            }

            result.Sort(System.StringComparer.Ordinal);
            return result;
        }
    }
}
