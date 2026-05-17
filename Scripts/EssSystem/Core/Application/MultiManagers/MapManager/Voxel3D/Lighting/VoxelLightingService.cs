using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting
{
    /// <summary>
    /// 体素光照服务（纯 C# 单例）。维护光源池 + 组合查询 (sky + block) 整数光级。
    /// <para>查询是 <c>O(N)</c> 线性扫描所有源 —— 对小规模演示（几十~几百源）足够；
    /// 上千源后再加空间索引（chunk grid bucketing）。</para>
    /// <para>线程：所有 API 在主线程调用。Service 内部无锁。</para>
    /// </summary>
    public class VoxelLightingService : Service<VoxelLightingService>
    {
        // ── Sky 调控（由 Manager 每帧从 DayCycle01 推下来）──────────────

        /// <summary>当前 sky light 倍率 [0..1]。0 = 完全黑夜（仍受 AmbientFloor 兜底），1 = 正午。</summary>
        public float SkyMultiplier01 { get; set; } = 1f;

        /// <summary>非 sky 区域的全局环境光最低值（block 系，0..15）。0 = 黑，2 = 略亮，便于看清。</summary>
        public byte AmbientFloor { get; set; } = 0;

        // ── 光源池 ────────────────────────────────────────────────────

        private readonly Dictionary<int, VoxelLightSource> _sources = new Dictionary<int, VoxelLightSource>();
        private int _nextHandle = 1;

        /// <summary>当前活动光源数（含 disabled）。</summary>
        public int SourceCount => _sources.Count;

        /// <summary>遍历所有光源（只读）。</summary>
        public IEnumerable<VoxelLightSource> Sources => _sources.Values;

        /// <summary>添加光源；返回 handle，用于后续移除/修改。</summary>
        public int AddSource(VoxelLightSource src)
        {
            src.Handle = _nextHandle++;
            _sources[src.Handle] = src;
            OnLightingChanged?.Invoke();
            return src.Handle;
        }

        /// <summary>用预设便捷加（火把/萤石/岩浆/灯笼）。</summary>
        public int AddTorch(Vector3 worldPos)     => AddSource(VoxelLightSource.Torch(worldPos));
        public int AddGlowstone(Vector3 worldPos) => AddSource(VoxelLightSource.Glowstone(worldPos));
        public int AddLava(Vector3 worldPos)      => AddSource(VoxelLightSource.Lava(worldPos));
        public int AddLantern(Vector3 worldPos)   => AddSource(VoxelLightSource.Lantern(worldPos));

        /// <summary>按 handle 移除。返回 true 表示成功。</summary>
        public bool RemoveSource(int handle)
        {
            if (!_sources.Remove(handle)) return false;
            OnLightingChanged?.Invoke();
            return true;
        }

        /// <summary>临时启停（不删源）。</summary>
        public bool SetSourceEnabled(int handle, bool enabled)
        {
            if (!_sources.TryGetValue(handle, out var s)) return false;
            if (s.Enabled == enabled) return true;
            s.Enabled = enabled;
            _sources[handle] = s;
            OnLightingChanged?.Invoke();
            return true;
        }

        /// <summary>清空所有光源。</summary>
        public void ClearSources()
        {
            if (_sources.Count == 0) return;
            _sources.Clear();
            OnLightingChanged?.Invoke();
        }

        /// <summary>光照状态变更事件 —— mesher 监听后可标记 chunk 重 mesh。</summary>
        public event System.Action OnLightingChanged;

        /// <summary>外部主动通知（如 SkyMultiplier 大幅变化时）。</summary>
        public void NotifyChanged() => OnLightingChanged?.Invoke();

        // ── 查询 ──────────────────────────────────────────────────────

        /// <summary>查询世界 (wx, wy, wz) 处组合光级 [0..15]。
        /// <para><paramref name="surfaceY"/> = 该 (wx, wz) 列的地表高度（heightmap）。
        /// wy >= surfaceY 视为暴露天空（吃 sky light）；wy &lt; surfaceY 视为地下（仅吃 block light + AmbientFloor）。</para>
        /// <para>同时输出 <paramref name="warmTint"/>：附近暖光源的颜色加权累计（已乘 alpha 强度），
        /// 由 mesher 与 sky 白光按 sky/block 比例 blend 进顶点色。</para></summary>
        public byte SampleLight(int wx, int wy, int wz, int surfaceY, out Color32 warmTint, out byte blockLight)
        {
            // sky: 暴露天空时受 SkyMultiplier 调制；地下为 0
            var skyEffective = (wy >= surfaceY)
                ? Mathf.RoundToInt(VoxelLightConstants.SkyLightFullDay * SkyMultiplier01)
                : 0;

            // block: 累计每个源的贡献，取 max；同时按贡献加权累加 tint（前段亮度强的染色权重大）
            byte best = 0;
            float tintR = 0f, tintG = 0f, tintB = 0f, tintW = 0f;
            foreach (var s in _sources.Values)
            {
                if (!s.Enabled || s.Intensity == 0) continue;
                var dx = Mathf.Abs(wx - Mathf.RoundToInt(s.WorldPos.x));
                var dy = Mathf.Abs(wy - Mathf.RoundToInt(s.WorldPos.y));
                var dz = Mathf.Abs(wz - Mathf.RoundToInt(s.WorldPos.z));
                var dist = Mathf.Max(Mathf.Max(dx, dy), dz); // Chebyshev（与 MC 一致）
                var contrib = (int)s.Intensity - dist * (int)s.FalloffPerBlock;
                if (contrib <= 0) continue;
                if (contrib > best) best = (byte)contrib;
                // 累计染色权重 = 贡献² × alpha，让强光色温 dominate
                var w = (contrib * contrib) * (s.Tint.a / 255f);
                tintR += s.Tint.r * w; tintG += s.Tint.g * w; tintB += s.Tint.b * w; tintW += w;
            }
            blockLight = best;

            warmTint = tintW > 0f
                ? new Color32((byte)(tintR / tintW), (byte)(tintG / tintW), (byte)(tintB / tintW), 255)
                : new Color32(255, 255, 255, 255);

            // 组合 + AmbientFloor
            var combined = Mathf.Max(Mathf.Max(skyEffective, blockLight), AmbientFloor);
            return (byte)Mathf.Clamp(combined, 0, VoxelLightConstants.MaxLight);
        }

        /// <summary>简化版：仅返回组合光级，不要 tint。Mesher 顶面采样用得多（同列同光，不需逐顶点 tint）。</summary>
        public byte SampleLight(int wx, int wy, int wz, int surfaceY)
            => SampleLight(wx, wy, wz, surfaceY, out _, out _);

        // ── Service<T> 必需 hook ──────────────────────────────────────

        protected override void Initialize()
        {
            base.Initialize();
            _sources.Clear();
            _nextHandle     = 1;
            SkyMultiplier01 = 1f;
            AmbientFloor    = 0;
        }
    }
}
