using UnityEngine;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Generator
{
    /// <summary>
    /// 3D 体素气候采样器（与 2D <c>PerlinMapGenerator</c> 中的 SampleTemperature/SampleMoisture 对齐）。
    /// <para>
    /// 由种子派生独立 Perlin 偏移：温度 / 湿度 / 海拔噪声各一组，相互无关联。
    /// 输出全部归一化到 [0,1]。
    /// </para>
    /// </summary>
    public class VoxelClimateSampler
    {
        private readonly VoxelMapConfig _cfg;

        // 海拔层独立噪声偏移（fBm）
        private readonly Vector2[] _elevationOffsets;
        // 温度局部扰动（单层 Perlin）
        private readonly float _temperatureOffsetX;
        private readonly float _temperatureOffsetY;
        // 湿度 fBm 偏移
        private readonly Vector2[] _moistureOffsets;

        public VoxelClimateSampler(VoxelMapConfig cfg)
        {
            _cfg = cfg;
            // 与 inner heightmap 用 +200000 偏离，避免与 height 噪声相关
            var rng = new System.Random(cfg.Seed ^ 0x6E00CFAB);

            _elevationOffsets = new Vector2[Mathf.Max(1, cfg.ElevationOctaves)];
            for (var i = 0; i < _elevationOffsets.Length; i++)
                _elevationOffsets[i] = new Vector2(NextOff(rng), NextOff(rng));

            _temperatureOffsetX = NextOff(rng);
            _temperatureOffsetY = NextOff(rng);

            _moistureOffsets = new Vector2[Mathf.Max(1, cfg.MoistureOctaves)];
            for (var i = 0; i < _moistureOffsets.Length; i++)
                _moistureOffsets[i] = new Vector2(NextOff(rng), NextOff(rng));
        }

        private static float NextOff(System.Random rng)
            => (float)(rng.NextDouble() * 200000.0 - 100000.0);

        /// <summary>独立海拔噪声（决定丘陵 / 山地划分），[0,1]。</summary>
        public float SampleElevation01(int wx, int wz)
        {
            return Fbm(wx, wz, _elevationOffsets, _cfg.ElevationScale,
                       _cfg.ElevationPersistence, _cfg.ElevationLacunarity);
        }

        /// <summary>
        /// 温度，[0,1]：基础温度 − 纬度衰减 − 海拔降温 + 局部扰动。
        /// </summary>
        public float SampleTemperature01(int wx, int wz, float elevation01)
        {
            // 纬度：北/南极 → 1，赤道 → 0；周期 LatitudePeriod 一圈 sin
            var period = Mathf.Max(1f, _cfg.LatitudePeriod);
            var lat    = Mathf.Abs(Mathf.Sin(wz * Mathf.PI * 2f / period));

            var t = _cfg.BaseTemperature
                  - _cfg.LatitudeStrength * lat
                  - _cfg.ElevationLapseRate * elevation01;

            if (_cfg.TemperatureNoiseStrength > 0f && _cfg.TemperatureNoiseScale > 0f)
            {
                var nx = (wx + _temperatureOffsetX) * _cfg.TemperatureNoiseScale;
                var ny = (wz + _temperatureOffsetY) * _cfg.TemperatureNoiseScale;
                var n  = Mathf.PerlinNoise(nx, ny) * 2f - 1f;
                t += n * _cfg.TemperatureNoiseStrength;
            }
            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// 湿度，[0,1]：fBm + 海岸湿润加成 - 高海拔变干。
        /// </summary>
        /// <param name="heightForLandSea">最终地形高度（block）；用来计算"离海近=湿"的近似。</param>
        public float SampleMoisture01(int wx, int wz, int heightForLandSea, float elevation01)
        {
            var fbm = Fbm(wx, wz, _moistureOffsets, _cfg.MoistureScale,
                          _cfg.MoisturePersistence, _cfg.MoistureLacunarity);

            // 海岸湿润：高度越接近海平面（不论上下）越湿
            var distToSea  = Mathf.Abs(heightForLandSea - _cfg.SeaLevel);
            var coastBoost = _cfg.OceanMoistureBoost * Mathf.Clamp01(1f - distToSea / 16f);

            var dryFromAlt = _cfg.ElevationDryness * elevation01;
            var moisture   = Mathf.Lerp(fbm, _cfg.BaseMoisture, 0.35f) + coastBoost - dryFromAlt;
            return Mathf.Clamp01(moisture);
        }

        // ── fBm 内核 ──────────────────────────────────────────────────
        private static float Fbm(int wx, int wz, Vector2[] offsets,
                                 float scale, float persistence, float lacunarity)
        {
            scale = Mathf.Max(0.0001f, scale);
            float total = 0f, amp = 1f, freq = 1f, max = 0f;
            for (var i = 0; i < offsets.Length; i++)
            {
                var off = offsets[i];
                var sx  = (wx + off.x) * scale * freq;
                var sy  = (wz + off.y) * scale * freq;
                total  += Mathf.PerlinNoise(sx, sy) * amp;
                max    += amp;
                amp    *= persistence;
                freq   *= lacunarity;
            }
            return max > 0f ? total / max : 0f;
        }
    }
}
