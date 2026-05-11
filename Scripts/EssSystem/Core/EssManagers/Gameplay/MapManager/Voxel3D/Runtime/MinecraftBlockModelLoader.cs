using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Runtime
{
    /// <summary>
    /// Minecraft 资源包 <c>models/block/*.json</c> 解析器（最小可用子集）。
    /// <para>负责把 (blockName, faceName) 解析到具体贴图名（如 <c>"block/grass_block_top"</c>），
    /// 处理 MC 标准的 <b>parent 继承 + #ref 引用链</b>，让上层不必关心 cube / cube_all / cube_bottom_top
    /// 这些 vanilla 父模型。</para>
    /// <para>仅解析 <c>parent</c>（string）+ <c>textures</c>（string→string flat map），不处理 elements / display；
    /// 我们的 voxel 渲染走自家 Mesher，不需要 element box 数据。</para>
    /// <para>JSON 来源：<c>Resources/DayNight3D/Blocks/assets/minecraft/models/block/&lt;name&gt;.json</c>，
    /// Unity 会作为 <see cref="TextAsset"/> 自动导入。</para>
    /// </summary>
    public static class MinecraftBlockModelLoader
    {
        /// <summary>Model JSON 搜索根（单根）。所有需要的 MC 内容已抽离到 <c>Resources/DayNight3D/Blocks/...</c>，
        /// 是项目自带、git 跟踪的"默认资源"；与可选的 <c>Minecraft/</c> 整包解耦。</summary>
        private static readonly string[] ModelRoots =
        {
            "DayNight3D/Blocks/assets/minecraft/models/block/",
        };

        /// <summary>Texture 搜索根（单根）。</summary>
        public static readonly string[] TextureRoots =
        {
            "DayNight3D/Blocks/assets/minecraft/textures/",
        };

        // 缓存：模型 textures map 解析结果（含 parent 合并）
        private static readonly Dictionary<string, Dictionary<string, string>> _texturesCache
            = new Dictionary<string, Dictionary<string, string>>();

        // 正则：parent / textures 块（容忍空白、单层嵌套；够用于我们手写的简单 JSON）
        private static readonly Regex _parentRx   = new Regex("\"parent\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex _texBlockRx = new Regex("\"textures\"\\s*:\\s*\\{([^}]*)\\}", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex _texEntryRx = new Regex("\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);

        /// <summary>清空缓存（方便编辑器热改 JSON / 切包）。</summary>
        public static void ClearCache() => _texturesCache.Clear();

        /// <summary>
        /// 解析 (blockName, faceKey) 到具体贴图名。
        /// <para>blockName 不带扩展名（"grass_block"、"stone"）。faceKey 是 MC 面键（"up" / "down" / "north" 等）；
        /// 若直接传 "all" / "top" / "side" 等已在 textures map 中的别名，也会被正确解析。</para>
        /// </summary>
        /// <returns>形如 <c>"block/grass_block_top"</c> 的贴图引用（已剥 namespace），失败返回 null。</returns>
        public static string ResolveFaceTexture(string blockName, string faceKey)
        {
            if (string.IsNullOrEmpty(blockName) || string.IsNullOrEmpty(faceKey)) return null;
            var textures = LoadMergedTextures(blockName);
            if (textures == null) return null;

            // #ref 链 resolve；最多 8 跳防循环
            var v = faceKey;
            // 第一跳：如果 faceKey 直接是 textures 的 key（如 "all"），用 value；否则按 # 引用查
            if (textures.TryGetValue(v, out var direct))
                v = direct;
            for (var i = 0; i < 8; i++)
            {
                if (string.IsNullOrEmpty(v)) return null;
                if (v[0] != '#') return StripNamespace(v);
                var key = v.Substring(1);
                if (!textures.TryGetValue(key, out v)) return null;
            }
            return null;
        }

        // ── 内部 ───────────────────────────────────────────────────

        /// <summary>递归收集 textures（parent 先入、child 覆盖）。</summary>
        private static Dictionary<string, string> LoadMergedTextures(string blockName)
        {
            if (_texturesCache.TryGetValue(blockName, out var cached)) return cached;

            var dict = new Dictionary<string, string>();
            if (!CollectInto(blockName, dict, depth: 0)) return null;

            _texturesCache[blockName] = dict;
            return dict;
        }

        private static bool CollectInto(string blockName, Dictionary<string, string> outDict, int depth)
        {
            if (depth > 8) { Debug.LogWarning($"[MCModelLoader] '{blockName}' parent 链过深，停止解析"); return false; }

            var json = LoadJson(blockName);
            if (json == null) return false;

            // 1) parent 先 collect（让 child 在后面覆盖）
            var pm = _parentRx.Match(json);
            if (pm.Success)
            {
                var parentName = NormalizeBlockRef(pm.Groups[1].Value);
                if (!string.IsNullOrEmpty(parentName))
                    CollectInto(parentName, outDict, depth + 1); // 父缺失也无所谓（如 base 'cube' 的祖父）
            }

            // 2) 当前层 textures 覆盖
            var tm = _texBlockRx.Match(json);
            if (tm.Success)
            {
                var body = tm.Groups[1].Value;
                foreach (Match e in _texEntryRx.Matches(body))
                {
                    outDict[e.Groups[1].Value] = e.Groups[2].Value;
                }
            }

            return true;
        }

        /// <summary>把 "minecraft:block/cube_all" 之类规范化成 "cube_all"（loader 直接按文件名查）。
        /// 也处理短形 "block/cube_all"。</summary>
        private static string NormalizeBlockRef(string raw)
        {
            var s = StripNamespace(raw); // 去掉 "minecraft:"
            // s 可能是 "block/cube_all" 或 "cube_all"
            const string prefix = "block/";
            return s.StartsWith(prefix) ? s.Substring(prefix.Length) : s;
        }

        /// <summary>剥离 "namespace:" 前缀（MC vanilla 一律 "minecraft:..."）。</summary>
        private static string StripNamespace(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            var i = raw.IndexOf(':');
            return i >= 0 ? raw.Substring(i + 1) : raw;
        }

        private static string LoadJson(string blockName)
        {
            // 多根尝试：MC 包 → scaffolding。先命中先返。
            for (var i = 0; i < ModelRoots.Length; i++)
            {
                var ta = Resources.Load<TextAsset>(ModelRoots[i] + blockName);
                if (ta != null) return ta.text;
            }
            // base/cube 等可能都不存在 → 调用方容忍 null
            return null;
        }

        /// <summary>一步到位：解析 (blockName, faceKey) 后直接加载贴图。
        /// 贴图从 <see cref="TextureRoots"/> 列表依次尝试，先命中先赢。
        /// 返回 null 表示所有根都未找到（或 face 未能 resolve）。</summary>
        public static Texture2D LoadFaceTexture(string blockName, string faceKey, out string usedPath)
        {
            usedPath = null;
            var texRef = ResolveFaceTexture(blockName, faceKey);
            if (string.IsNullOrEmpty(texRef)) return null;

            for (var i = 0; i < TextureRoots.Length; i++)
            {
                var path = TextureRoots[i] + texRef;
                var tex  = Resources.Load<Texture2D>(path);
                if (tex != null) { usedPath = path; return tex; }
            }
            return null;
        }
    }
}
