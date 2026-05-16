#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.EditorTools
{
    /// <summary>
    /// 一次性 Editor 工具：把 <c>Resources/Sprites/Characters/PixArt/</c> 下所有 PNG
    /// 重新导入为 Multiple 模式，按 8 行 × 6 列等宽网格切片，命名为 <c>{prefix}_{Action}_{frame}</c>。
    /// <para>行 → 动作映射（从顶到底）：Walk(6) → Idle(4) → Jump(3) → Attack(4) → Defend(4) → Damage(3) → Death(5) → Special(6) = 35 帧。
    /// 超出 FrameCount 的格子（如 Idle 第 5/6 列）不生成 Sprite。</para>
    /// <para>前缀由路径推导（去 <c>Sprites/Characters/PixArt/</c> 前缀 + 路径分隔符替换为下划线 + 去扩展名），
    /// 例如 <c>Skin/warrior_1.png → Skin_warrior_1_Walk_0</c>，确保全局唯一。</para>
    /// </summary>
    public static class CharacterSpriteSheetSlicer
    {
        private const string PixArtRoot = "Assets/Resources/Sprites/Characters/PixArt";

        // 动作行（从上到下，与用户列出的顺序一致）
        private static readonly (string Action, int FrameCount)[] ActionRows =
        {
            ("Walk",    6),
            ("Idle",    4),
            ("Jump",    3),
            ("Attack",  4),
            ("Defend",  4),
            ("Damage",  3),
            ("Death",   5),
            ("Special", 6),
        };

        private const int GridCols = 6;
        private const int GridRows = 8;

        [MenuItem("Tools/Character/Slice Selected Sprite Sheets (8x6)")]
        public static void SliceSelected()
        {
            if (!Directory.Exists(PixArtRoot))
            {
                Debug.LogError($"[Slicer] 目录不存在: {PixArtRoot}");
                return;
            }

            var pngs = CollectSelectedPngs();
            if (pngs.Length == 0)
            {
                Debug.LogWarning($"[Slicer] 未在 Project 视图中选中任何位于 {PixArtRoot} 下的 PNG（或包含 PNG 的文件夹）");
                return;
            }

            int ok = 0, skip = 0, fail = 0;

            try
            {
                AssetDatabase.StartAssetEditing();
                for (var i = 0; i < pngs.Length; i++)
                {
                    var assetPath = pngs[i].Replace('\\', '/');
                    EditorUtility.DisplayProgressBar("Slicing Sprite Sheets", assetPath, (float)i / pngs.Length);

                    var status = SliceOne(assetPath);
                    if (status == 1) ok++;
                    else if (status == 0) skip++;
                    else fail++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log($"[Slicer] 完成 —— 成功 {ok} / 跳过 {skip} / 失败 {fail} （共 {pngs.Length} 个 PNG）");
        }

        [MenuItem("Tools/Character/Print Slicer Layout")]
        public static void PrintLayout()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Slicer] 动作行映射（顶到底）：");
            for (var r = 0; r < ActionRows.Length; r++)
                sb.AppendLine($"  Row {r}: {ActionRows[r].Action} × {ActionRows[r].FrameCount}");
            sb.AppendLine($"网格: {GridRows} 行 × {GridCols} 列");
            sb.AppendLine($"PixArt 根: {PixArtRoot}");
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 收集 Project 视图中选中的 PNG（或文件夹下递归的 PNG），只保留位于 <see cref="PixArtRoot"/> 下的。
        /// 没选中则返回空数组（不回退处理全部，避免误操作）。
        /// </summary>
        private static string[] CollectSelectedPngs()
        {
            var selection = Selection.objects;
            if (selection == null || selection.Length == 0) return System.Array.Empty<string>();

            var root = PixArtRoot.Replace('\\', '/');
            var set = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var obj in selection)
            {
                var path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path)) continue;
                path = path.Replace('\\', '/');
                if (!path.StartsWith(root + "/") && path != root) continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    var found = Directory.GetFiles(path, "*.png", SearchOption.AllDirectories);
                    foreach (var f in found) set.Add(f.Replace('\\', '/'));
                }
                else if (path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase))
                {
                    set.Add(path);
                }
            }

            var arr = new string[set.Count];
            set.CopyTo(arr);
            System.Array.Sort(arr, System.StringComparer.Ordinal);
            return arr;
        }

        /// <summary>切片一个 PNG。返回 1 = 成功, 0 = 跳过, -1 = 失败。</summary>
        private static int SliceOne(string assetPath)
        {
            // 1) 读图大小（不依赖 Texture2D 是否已 import）
            int w, h;
            try { (w, h) = ReadPngSize(assetPath); }
            catch (System.Exception ex)
            {
                Debug.LogError($"[Slicer] 无法读取 PNG 尺寸 {assetPath}: {ex.Message}");
                return -1;
            }

            if (w % GridCols != 0 || h % GridRows != 0)
            {
                Debug.LogWarning($"[Slicer] 跳过 {assetPath} —— 尺寸 {w}×{h} 无法被 {GridCols}×{GridRows} 整除");
                return 0;
            }

            int cellW = w / GridCols;
            int cellH = h / GridRows;

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[Slicer] 不是纹理资源: {assetPath}");
                return -1;
            }

            // 2) 设为 Sprite / Multiple
            importer.textureType        = TextureImporterType.Sprite;
            importer.spriteImportMode   = SpriteImportMode.Multiple;
            importer.mipmapEnabled      = false;
            importer.filterMode         = FilterMode.Point;        // 像素风格保持锐利
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // 3) 生成 sub-sprite 列表
            var prefix = DerivePrefix(assetPath);
            var sheet = new List<SpriteMetaData>(35);

            for (var rowIdx = 0; rowIdx < ActionRows.Length; rowIdx++)
            {
                var (action, frameCount) = ActionRows[rowIdx];
                // 像素坐标 Y 向上，row 0 在顶 → y = h - (rowIdx+1)*cellH
                var y = h - (rowIdx + 1) * cellH;
                for (var col = 0; col < frameCount; col++)
                {
                    sheet.Add(new SpriteMetaData
                    {
                        name      = $"{prefix}_{action}_{col}",
                        rect      = new Rect(col * cellW, y, cellW, cellH),
                        alignment = (int)SpriteAlignment.Center,
                        pivot     = new Vector2(0.5f, 0.5f),
                        border    = Vector4.zero,
                    });
                }
            }

#pragma warning disable CS0618 // spritesheet is "obsolete" but仍 functional 且唯一稳定的批处理 API
            importer.spritesheet = sheet.ToArray();
#pragma warning restore CS0618
            importer.SaveAndReimport();
            return 1;
        }

        /// <summary>从 PNG 文件头读出宽高（IHDR 偏移 16/20，big-endian uint32）。</summary>
        private static (int w, int h) ReadPngSize(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                var b = new byte[24];
                if (fs.Read(b, 0, 24) < 24) throw new System.IO.IOException("PNG 头读取不足 24 字节");
                // 8 字节签名 + 4 字节长度 + 4 字节 "IHDR" → 偏移 16 起为 width(4)/height(4)
                if (b[12] != 'I' || b[13] != 'H' || b[14] != 'D' || b[15] != 'R')
                    throw new System.IO.IOException("非合法 PNG IHDR");
                int w = (b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19];
                int h = (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23];
                return (w, h);
            }
        }

        /// <summary>
        /// 由 Asset 路径推导 sheet 唯一前缀。
        /// 例: <c>Assets/Resources/Sprites/Characters/PixArt/Headgear/Helmet/Close/1.png → Headgear_Helmet_Close_1</c>
        /// </summary>
        public static string DerivePrefix(string assetPath)
        {
            const string rootAssets = "Assets/Resources/Sprites/Characters/PixArt/";
            var p = (assetPath ?? string.Empty).Replace('\\', '/');
            if (p.StartsWith(rootAssets)) p = p.Substring(rootAssets.Length);
            var dot = p.LastIndexOf('.');
            if (dot >= 0) p = p.Substring(0, dot);
            return p.Replace('/', '_');
        }
    }
}
#endif
