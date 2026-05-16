using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Runtime
{
    /// <summary>
    /// 体素贴图集 —— 把 <see cref="VoxelAtlasSlots"/> 的 32 张 16×16 PNG（含 dirt/stone 的多变体）
    /// 在运行时拼成一张 128×64 的 <see cref="RenderTexture"/>，提供每个 slot 的 UV 矩形（0..1 空间）。
    /// <para><b>GPU 路径</b>：源贴图任意 import 设置（压缩 / 非可读都行），通过 <see cref="Graphics.Blit(Texture, RenderTexture, Vector2, Vector2)"/>
    /// 把源以 scale/offset UV 采样到一个 16×16 临时 RT，再 <see cref="Graphics.CopyTexture(Texture, int, int, int, int, int, int, Texture, int, int, int, int)"/>
    /// 进 atlas 子矩形 —— <b>无需 isReadable，无需 Uncompressed，无需任何 AssetPostprocessor</b>。</para>
    /// <para>采样设置：Point filter + no mipmap（chunk 拉远不用担心 slot 间串色）。
    /// 动画水（16×N 帧条）取顶部 16×16 = frame 0；未来可扩 <c>WaterAnimationFrames</c> 时间索引。</para>
    /// </summary>
    public static class VoxelTextureAtlas
    {
        private const int TileSize = 16;
        private const int Cols     = 8;
        private const int Rows     = 4;
        private const int AtlasW   = TileSize * Cols;   // 128
        private const int AtlasH   = TileSize * Rows;   // 64

        /// <summary>Atlas slot → (MC blockName, faceKey) 绑定表。
        /// <para>运行时通过 <see cref="MinecraftBlockModelLoader.ResolveFaceTexture"/> 解析 blockName 的 model JSON
        /// （含 parent 继承 + #ref 链）拿到贴图名，再 Resources.Load 实际 PNG。</para>
        /// <para>blockName 对应 <c>Resources/DayNight3D/Blocks/assets/minecraft/models/block/&lt;blockName&gt;.json</c>。
        /// faceKey 用 MC 面键（"up"/"down"/"north"...）或 textures map 内的别名（"all"/"side"/"top" 等）。</para>
        /// </summary>
        private static readonly (string BlockName, string FaceKey, string VariantSuffix)[] SlotBindings = new (string, string, string)[VoxelAtlasSlots.Count]
        {
            // ── 基础（slot 0..2）──
            ("grass_block",      "up",    null),  // 0 GrassTop
            ("grass_block",      "north", null),  // 1 GrassSide
            ("grass_block_snow", "north", null),  // 2 GrassSideSnowed

            // ── Dirt 13 个变体（slot 3..15），suffix 拼到 ResolveFaceTexture 返回的 "block/dirt" 后变成 "block/dirt1" 等 ──
            ("dirt", "up", null),  // 3  = dirt.png
            ("dirt", "up", "1"),   // 4  = dirt1.png
            ("dirt", "up", "2"),   // 5
            ("dirt", "up", "3"),   // 6
            ("dirt", "up", "4"),   // 7
            ("dirt", "up", "5"),   // 8
            ("dirt", "up", "6"),   // 9
            ("dirt", "up", "7"),   // 10
            ("dirt", "up", "8"),   // 11
            ("dirt", "up", "9"),   // 12
            ("dirt", "up", "10"),  // 13
            ("dirt", "up", "11"),  // 14
            ("dirt", "up", "12"),  // 15

            // ── Stone 9 个变体（slot 16..24）──
            ("stone", "up", null), // 16 = stone.png
            ("stone", "up", "1"),  // 17
            ("stone", "up", "2"),  // 18
            ("stone", "up", "3"),  // 19
            ("stone", "up", "4"),  // 20
            ("stone", "up", "5"),  // 21
            ("stone", "up", "6"),  // 22
            ("stone", "up", "7"),  // 23
            ("stone", "up", "8"),  // 24

            // ── 单 slot 方块（25..27）──
            ("sand",       "up", null), // 25 Sand
            ("snow_block", "up", null), // 26 Snow
            ("water",      "up", null), // 27 WaterStill

            // ── Padding（28..31）填洋红，留作未来 ──
            (null, null, null), // 28
            (null, null, null), // 29
            (null, null, null), // 30
            (null, null, null), // 31
        };

        private static RenderTexture _atlas;
        private static Rect[]        _slotUVs;

        /// <summary>已构建的 atlas Texture（懒构建）。返回 <see cref="Texture"/> 基类，
        /// 由 <c>Material.mainTexture</c> 直接使用 RenderTexture。</summary>
        public static Texture Texture
        {
            get { EnsureBuilt(); return _atlas; }
        }

        /// <summary>查询某个 slot 在 atlas 内的 UV 矩形（0..1）。</summary>
        public static Rect GetSlotUV(byte slot)
        {
            EnsureBuilt();
            if (_slotUVs == null || slot >= _slotUVs.Length) return new Rect(0, 0, 1, 1);
            return _slotUVs[slot];
        }

        /// <summary>外部强制重建（贴图热更换 / JSON 改完时调）。</summary>
        public static void Rebuild()
        {
            if (_atlas != null) { _atlas.Release(); Object.DestroyImmediate(_atlas); }
            _atlas = null;
            _slotUVs = null;
            MinecraftBlockModelLoader.ClearCache();
            EnsureBuilt();
        }

        private static void EnsureBuilt()
        {
            if (_atlas != null && _atlas.IsCreated()) return;

            _atlas = new RenderTexture(AtlasW, AtlasH, 0, RenderTextureFormat.ARGB32)
            {
                name             = "VoxelTextureAtlas",
                filterMode       = FilterMode.Point,
                wrapMode         = TextureWrapMode.Clamp,
                useMipMap        = false,
                autoGenerateMips = false,
                anisoLevel       = 0,
            };
            _atlas.Create();

            // 清成洋红：缺贴图的 slot 直接显示 magenta 警示，不会出现脏数据
            var prev = RenderTexture.active;
            RenderTexture.active = _atlas;
            GL.Clear(true, true, new Color(1f, 0f, 1f, 1f));
            RenderTexture.active = prev;

            _slotUVs = new Rect[VoxelAtlasSlots.Count];
            for (var slot = 0; slot < VoxelAtlasSlots.Count; slot++)
            {
                var col  = slot % Cols;
                var row  = slot / Cols;
                // GPU 纹理坐标 y=0 在底部 → slot=0 视觉上左上 → row 翻转
                var dstY = (Rows - 1 - row) * TileSize;
                var dstX = col * TileSize;

                _slotUVs[slot] = new Rect(
                    dstX / (float)AtlasW,
                    dstY / (float)AtlasH,
                    TileSize / (float)AtlasW,
                    TileSize / (float)AtlasH);

                var src = ResolveSlotTexture(slot, out var usedPath);
                if (src == null) continue;

                BlitSlot(src, dstX, dstY);
                #if UNITY_EDITOR
                Debug.Log($"[VoxelTextureAtlas] slot {slot} ← '{usedPath}' (src={src.width}×{src.height}, fmt={(src is Texture2D t ? t.format.ToString() : src.GetType().Name)})");
                #endif
            }
            Debug.Log($"[VoxelTextureAtlas] 已构建 {AtlasW}×{AtlasH} RT（{VoxelAtlasSlots.Count} slots, GPU 路径）");
        }

        /// <summary>解析 slot 的源贴图：走 MC 模型 JSON 拿 texRef → 拼变体后缀 → 加载实际 PNG。
        /// 所有内容都在 <c>Resources/DayNight3D/Blocks/...</c>。padding slot（BlockName=null）跳过留洋红。</summary>
        private static Texture2D ResolveSlotTexture(int slot, out string usedPath)
        {
            usedPath = null;
            var (blockName, faceKey, variantSuffix) = SlotBindings[slot];
            if (string.IsNullOrEmpty(blockName)) return null; // padding slot

            // 1) 通过 model JSON 解析 face → texRef（如 "block/dirt"）
            var texRef = MinecraftBlockModelLoader.ResolveFaceTexture(blockName, faceKey);
            if (string.IsNullOrEmpty(texRef))
            {
                Debug.LogWarning($"[VoxelTextureAtlas] slot {slot} ({blockName}#{faceKey}) face 解析失败");
                return null;
            }

            // 2) 拼变体后缀："block/dirt" + "1" = "block/dirt1"
            if (!string.IsNullOrEmpty(variantSuffix)) texRef += variantSuffix;

            // 3) 多根加载（loader 内部依次试 TextureRoots）
            for (var i = 0; i < MinecraftBlockModelLoader.TextureRoots.Length; i++)
            {
                var path = MinecraftBlockModelLoader.TextureRoots[i] + texRef;
                var tex  = LoadTexture(path);
                if (tex != null) { usedPath = path; return tex; }
            }

            Debug.LogWarning($"[VoxelTextureAtlas] slot {slot} ({blockName}#{faceKey}{variantSuffix}) 贴图缺失：texRef='{texRef}'");
            return null;
        }

        /// <summary>把 src 的 "frame 0"（顶部 16×16）GPU blit 到 atlas RT 的 (dstX, dstY) 子矩形。
        /// 不依赖源 isReadable / 格式 —— Blit 通过 sampler 读，CopyTexture 在两块 ARGB32 RT 间内存级复制。</summary>
        private static void BlitSlot(Texture src, int dstX, int dstY)
        {
            var temp = RenderTexture.GetTemporary(TileSize, TileSize, 0, RenderTextureFormat.ARGB32);
            temp.filterMode = FilterMode.Point;
            temp.wrapMode   = TextureWrapMode.Clamp;

            // src 高 = h；frame 0 占 UV [1 - 16/h, 1] 的 V 区间 = 顶部 16 行
            // 当 h = 16（绝大多数贴图）时：scale=(1,1), offset=(0,0) —— 全图。
            var h = Mathf.Max(1, src.height);
            var scale  = new Vector2(1f, TileSize / (float)h);
            var offset = new Vector2(0f, 1f - TileSize / (float)h);
            Graphics.Blit(src, temp, scale, offset);

            // RT → RT 同格式拷贝 → 一次内存级复制到 atlas 子矩形
            Graphics.CopyTexture(temp, 0, 0, 0, 0, TileSize, TileSize, _atlas, 0, 0, dstX, dstY);
            RenderTexture.ReleaseTemporary(temp);
        }

        private static Texture2D LoadTexture(string path)
        {
            // 优先走 ResourceManager 缓存
            if (EventProcessor.HasInstance)
            {
                var r = EventProcessor.Instance.TriggerEventMethod(
                    "GetTexture", new List<object> { path });
                if (ResultCode.IsOk(r) && r.Count >= 2 && r[1] is Texture2D t) return t;
            }
            // 兜底直接加载（Voxel3D 子系统自包含，subsystem registration 阶段 EventProcessor 可能未就绪）
            return Resources.Load<Texture2D>(path);
        }
    }
}
