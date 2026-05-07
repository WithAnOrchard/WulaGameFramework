using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Runtime
{
    /// <summary>
    /// 体素贴图集 —— 把 <see cref="VoxelAtlasSlots"/> 列出的 8 张 16×16 PNG 在运行时拼成
    /// 一张 64×32 的 Texture2D，提供每个 slot 的 UV 矩形（0..1 空间）。
    /// <para>数据流：跨模块 bare-string 调 <c>"GetTexture"</c> 拿单图 → <see cref="Graphics.CopyTexture(Texture, int, int, int, int, int, int, Texture, int, int, int, int)"/>
    /// GPU 拷贝到 atlas（无需源贴图 Read/Write Enabled）→ 缓存复用。</para>
    /// <para>采样设置：Point filter（避免 MC 16×16 像素糊）+ no mipmap（chunk 拉远会用相邻 slot 串色）。
    /// 每帧动画水暂用 first frame；后续可在 <see cref="WaterAnimationFrames"/> 扩展时间索引。</para>
    /// </summary>
    public static class VoxelTextureAtlas
    {
        private const int TileSize = 16;
        private const int Cols     = 4;
        private const int Rows     = 2;
        private const int AtlasW   = TileSize * Cols;   // 64
        private const int AtlasH   = TileSize * Rows;   // 32

        /// <summary>Resources/ 相对路径（不含扩展名）— 与 slot 索引一一对应。</summary>
        private static readonly string[] SlotPaths = new string[VoxelAtlasSlots.Count]
        {
            "DayNight3D/Sprites/Blocks/grass_top",          // 0
            "DayNight3D/Sprites/Blocks/grass_side",         // 1
            "DayNight3D/Sprites/Blocks/grass_side_snowed",  // 2
            "DayNight3D/Sprites/Blocks/dirt",               // 3
            "DayNight3D/Sprites/Blocks/stone",              // 4
            "DayNight3D/Sprites/Blocks/sand",               // 5
            "DayNight3D/Sprites/Blocks/snow",               // 6
            "DayNight3D/Sprites/Blocks/water_still",        // 7
        };

        private static Texture2D _atlas;
        private static Rect[]    _slotUVs;

        /// <summary>已构建的 atlas Texture（懒构建）。</summary>
        public static Texture2D Texture
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

        /// <summary>外部强制重建（贴图热更换时调）。</summary>
        public static void Rebuild()
        {
            if (_atlas != null) Object.Destroy(_atlas);
            _atlas = null;
            _slotUVs = null;
            EnsureBuilt();
        }

        private static void EnsureBuilt()
        {
            if (_atlas != null) return;

            _atlas = new Texture2D(AtlasW, AtlasH, TextureFormat.RGBA32, mipChain: false, linear: false)
            {
                name       = "VoxelTextureAtlas",
                filterMode = FilterMode.Point,
                wrapMode   = TextureWrapMode.Clamp,
            };
            // 初始化为透明，避免 CopyTexture 漏了某个 slot 时显示脏数据
            FillSolid(_atlas, new Color32(255, 0, 255, 255));

            _slotUVs = new Rect[VoxelAtlasSlots.Count];
            for (var slot = 0; slot < VoxelAtlasSlots.Count; slot++)
            {
                var col = slot % Cols;
                var row = slot / Cols;
                // 注意 Texture 坐标 y=0 在底部，而我们想要 slot=0 在左上 → row 翻转
                var dstY = (Rows - 1 - row) * TileSize;
                var dstX = col * TileSize;

                var src = LoadTexture(SlotPaths[slot]);
                if (src == null)
                {
                    Debug.LogWarning($"[VoxelTextureAtlas] slot {slot} 贴图缺失：{SlotPaths[slot]}（已用洋红 fallback）");
                    continue;
                }

                // 源贴图取顶部 16×16（兼容 MC 动画水：water_still.png 16×256，多帧竖排，frame0 在顶端）
                // GetPixels(x,y,w,h) 要求 isReadable=1（meta 已改）；不依赖 GPU 格式匹配
                var srcY = Mathf.Max(0, src.height - TileSize);
                Color[] block;
                try
                {
                    block = src.GetPixels(0, srcY, TileSize, TileSize);
                }
                catch (UnityException ex)
                {
                    Debug.LogWarning($"[VoxelTextureAtlas] slot {slot} 读 '{SlotPaths[slot]}' 失败：{ex.Message}（请确认 meta 已设 isReadable=1 + Uncompressed）");
                    continue;
                }
                _atlas.SetPixels(dstX, dstY, TileSize, TileSize, block);

                _slotUVs[slot] = new Rect(
                    dstX / (float)AtlasW,
                    dstY / (float)AtlasH,
                    TileSize / (float)AtlasW,
                    TileSize / (float)AtlasH);
            }
            _atlas.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            Debug.Log($"[VoxelTextureAtlas] 已构建 {AtlasW}×{AtlasH}（{VoxelAtlasSlots.Count} slots）");
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

        private static void FillSolid(Texture2D tex, Color32 color)
        {
            var w = tex.width; var h = tex.height;
            var pixels = new Color32[w * h];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
        }
    }
}
