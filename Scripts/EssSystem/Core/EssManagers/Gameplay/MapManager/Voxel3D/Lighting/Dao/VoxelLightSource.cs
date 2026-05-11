using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.Voxel3D.Lighting.Dao
{
    /// <summary>
    /// 体素点光源 —— 与 MC 火把/萤石等价。
    /// <para><see cref="Intensity"/> 1..15，按 Chebyshev（切比雪夫）距离每格扣
    /// <see cref="FalloffPerBlock"/>。例：14 强度 + 1 衰减 → 14 格半径有效。</para>
    /// <para><see cref="Tint"/> 给侧/顶面顶点色加暖光偏色（火把 #ffcc66、岩浆 #ff7733 等）。
    /// 不染色用 <see cref="Color.white"/>。</para>
    /// </summary>
    public struct VoxelLightSource
    {
        /// <summary>Manager 派发的唯一 ID（1+，0 = 无效）。</summary>
        public int     Handle;
        /// <summary>世界坐标（block 单位整数中心点；Vector3 兼容浮点输入但内部按 round）。</summary>
        public Vector3 WorldPos;
        /// <summary>1..15 整数光级。0 = 关。</summary>
        public byte    Intensity;
        /// <summary>每 Chebyshev 距离扣多少级。典型 1（MC 标准）；2 = 衰减更快、范围更小。</summary>
        public byte    FalloffPerBlock;
        /// <summary>暖光色温 RGBA。alpha 决定染色强度（0..255）；255 = 完全替换 sky 白光。</summary>
        public Color32 Tint;
        /// <summary>临时关闭（不删 source）。</summary>
        public bool    Enabled;

        public VoxelLightSource(Vector3 worldPos, byte intensity = 14, Color32 tint = default, byte falloffPerBlock = 1)
        {
            Handle          = 0;
            WorldPos        = worldPos;
            Intensity       = intensity;
            FalloffPerBlock = falloffPerBlock < 1 ? (byte)1 : falloffPerBlock;
            Tint            = tint.a == 0 ? new Color32(255, 255, 255, 255) : tint;
            Enabled         = true;
        }

        // ── 预设：常见 MC 光源 ──

        /// <summary>火把（14, 暖黄 #ffcc66）。</summary>
        public static VoxelLightSource Torch(Vector3 pos)
            => new VoxelLightSource(pos, 14, new Color32(255, 204, 102, 255));

        /// <summary>萤石（15，亮黄 #ffe060）。</summary>
        public static VoxelLightSource Glowstone(Vector3 pos)
            => new VoxelLightSource(pos, 15, new Color32(255, 224, 96, 255));

        /// <summary>岩浆（15，橙红 #ff7733）。</summary>
        public static VoxelLightSource Lava(Vector3 pos)
            => new VoxelLightSource(pos, 15, new Color32(255, 119, 51, 255));

        /// <summary>灯笼（15，柔白 #fff4d6）。</summary>
        public static VoxelLightSource Lantern(Vector3 pos)
            => new VoxelLightSource(pos, 15, new Color32(255, 244, 214, 255));
    }
}
