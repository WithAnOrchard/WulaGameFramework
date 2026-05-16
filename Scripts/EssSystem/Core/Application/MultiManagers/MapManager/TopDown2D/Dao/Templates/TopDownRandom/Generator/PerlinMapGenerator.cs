using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Generator;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn;
using EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Spawn.Dao;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Generator
{
    /// <summary>
    /// Perlin 海陆生成器：fBm 多倍频采样 + 海平面阈值二分。
    /// <para>
    /// **确定性**：种子→随机偏移在构造时决定一次，之后纯函数式，对同一世界坐标永远返回同一结果。
    /// </para>
    /// <para>
    /// 同时实现 <see cref="IMapMetaProvider"/>：把 BiomeClassifier 输出（pre-river 群系）暴露给 spawn 装饰器，
    /// 用于按"群系"过滤 spawn 规则。
    /// </para>
    /// </summary>
    public class PerlinMapGenerator : IMapGenerator, IMapMetaProvider
    {
        private readonly PerlinMapConfig _cfg;
        private readonly float _seedOffsetX;
        private readonly float _seedOffsetY;
        // 大陆掩膜专用偏移：与细节 fBm 解耦，避免两层在原点附近高度相关
        private readonly float _continentOffsetX;
        private readonly float _continentOffsetY;
        // 地幔线专用偏移：独立 fBm，可与海陆 fBm 完全解耦
        private readonly float _elevationOffsetX;
        private readonly float _elevationOffsetY;
        // 温度局部扰动 / 湿度 fBm 各自独立偏移
        private readonly float _temperatureOffsetX;
        private readonly float _temperatureOffsetY;
        private readonly float _moistureOffsetX;
        private readonly float _moistureOffsetY;
        private readonly float _erosionOffsetX;
        private readonly float _erosionOffsetY;
        private readonly float _ridgesOffsetX;
        private readonly float _ridgesOffsetY;

        public PerlinMapGenerator(PerlinMapConfig cfg)
        {
            _cfg = cfg;
            // 用 System.Random 由种子派生固定偏移，避免 Unity PerlinNoise 在原点附近重复
            var rng = new System.Random(cfg.Seed);
            _seedOffsetX = (float)(rng.NextDouble() * 10000.0);
            _seedOffsetY = (float)(rng.NextDouble() * 10000.0);
            _continentOffsetX = (float)(rng.NextDouble() * 10000.0);
            _continentOffsetY = (float)(rng.NextDouble() * 10000.0);
            _elevationOffsetX = (float)(rng.NextDouble() * 10000.0);
            _elevationOffsetY = (float)(rng.NextDouble() * 10000.0);
            _temperatureOffsetX = (float)(rng.NextDouble() * 10000.0);
            _temperatureOffsetY = (float)(rng.NextDouble() * 10000.0);
            _moistureOffsetX = (float)(rng.NextDouble() * 10000.0);
            _moistureOffsetY = (float)(rng.NextDouble() * 10000.0);
            _erosionOffsetX = (float)(rng.NextDouble() * 10000.0);
            _erosionOffsetY = (float)(rng.NextDouble() * 10000.0);
            _ridgesOffsetX = (float)(rng.NextDouble() * 10000.0);
            _ridgesOffsetY = (float)(rng.NextDouble() * 10000.0);
        }

        /// <summary>
        /// 异步预热以 (chunkX, chunkY) 为中心的 RiverRegion 邻域。
        /// 把 ~6.5 万 tile 的 Perlin 噪声计算挪到 worker thread，避免主线程在跨 RiverRegion 时卡顿。
        /// </summary>
        public void PrewarmAround(int chunkX, int chunkY, int chunkSize)
        {
            RiverTracer.PrewarmRegionsAround(chunkX, chunkY, chunkSize, this, _cfg);
        }

        public void FillChunk(Chunk chunk)
        {
            var size = chunk.Size;
            var baseX = chunk.WorldOriginX;
            var baseY = chunk.WorldOriginY;

            for (var ly = 0; ly < size; ly++)
            {
                for (var lx = 0; lx < size; lx++)
                {
                    var wx = baseX + lx;
                    var wy = baseY + ly;
                    var h = SampleHeight(wx, wy);
                    var elevation = SampleElevation(wx, wy);
                    var temperature = SampleTemperature(wx, wy, elevation);
                    var moisture = SampleMoisture(wx, wy, h, elevation);
                    var typeId = BiomeClassifier.Classify(h, _cfg.SeaLevel,
                        elevation, temperature, moisture);
                    chunk.SetTile(lx, ly, new Tile(typeId,
                        ToByte(elevation), ToByte(temperature), ToByte(moisture)));
                }
            }

            // 河流：快速美观追踪（在地形分类完成后二次覆盖 Tile.TypeId）
            RiverTracer.Trace(chunk, this, _cfg);
        }

        /// <summary>
        /// 最终高度采样：分层混合大陆掩膜与 fBm 细节，输出 ∈ [0,1]。
        /// <list type="bullet">
        /// <item>Continent 层：单层超低频 Perlin（<see cref="PerlinMapConfig.ContinentScale"/>），决定大陆尺度</item>
        /// <item>Detail 层：多倍频 fBm，决定海岸线细节</item>
        /// <item>权重由 <see cref="PerlinMapConfig.ContinentWeight"/> 控制</item>
        /// </list>
        /// </summary>
        public float SampleHeight(int worldX, int worldY)
        {
            var detail = SampleFbm(worldX, worldY);
            var continentalness = SampleContinentalness(worldX, worldY);
            var erosion = SampleEffectiveErosion(worldX, worldY, continentalness);
            var ridges = SampleEffectiveRidges(worldX, worldY, continentalness, erosion);

            var cWeight = Mathf.Clamp01(_cfg.ContinentalnessWeight);
            var eWeight = Mathf.Clamp01(_cfg.ErosionWeight);
            var rWeight = Mathf.Clamp01(_cfg.RidgesWeight);
            var vertical = Mathf.Max(0f, _cfg.VerticalScale);

            var baseHeight = Mathf.Lerp(detail, ContinentalnessToBaseHeight(continentalness), cWeight);
            var eroded = Mathf.Lerp(baseHeight, 0.46f + (baseHeight - 0.46f) * 0.35f, erosion * eWeight);
            var ridgeAllowed = Mathf.SmoothStep(0.42f, 0.82f, continentalness) * (1f - erosion * 0.55f);
            var ridgeLift = Mathf.Pow(ridges, 1.7f) * rWeight * ridgeAllowed * 0.38f;
            var scaled = 0.5f + (eroded + ridgeLift - 0.5f) * vertical;

            return Mathf.Clamp01(scaled);
        }

        private static float ContinentalnessToBaseHeight(float c)
        {
            if (c < 0.18f) return Mathf.Lerp(0.08f, 0.22f, c / 0.18f);
            if (c < 0.34f) return Mathf.Lerp(0.22f, 0.42f, (c - 0.18f) / 0.16f);
            if (c < 0.46f) return Mathf.Lerp(0.42f, 0.50f, (c - 0.34f) / 0.12f);
            if (c < 0.72f) return Mathf.Lerp(0.50f, 0.68f, (c - 0.46f) / 0.26f);
            return Mathf.Lerp(0.68f, 0.88f, (c - 0.72f) / 0.28f);
        }

        public float SampleContinentalness(int worldX, int worldY)
        {
            var scale = _cfg.ContinentalnessScale > 0f ? _cfg.ContinentalnessScale : _cfg.ContinentScale;
            if (scale <= 0f) return SampleFbm(worldX, worldY);
            return Fbm(worldX, worldY, _continentOffsetX, _continentOffsetY,
                scale, 3, 0.5f, 2f);
        }

        public float SampleErosion(int worldX, int worldY)
        {
            return Fbm(worldX, worldY, _erosionOffsetX, _erosionOffsetY,
                _cfg.ErosionScale, 4, 0.5f, 2f);
        }

        private float SampleEffectiveErosion(int worldX, int worldY, float continentalness)
        {
            var raw = SampleErosion(worldX, worldY);
            if (!_cfg.ClimateCoupledToTerrain) return raw;
            var coast = 1f - Mathf.Abs(continentalness - _cfg.SeaLevel) / Mathf.Max(0.001f, _cfg.SeaLevel);
            var inlandDryness = Mathf.SmoothStep(0.55f, 1f, continentalness) * 0.35f;
            return Mathf.Clamp01(raw * 0.55f + Mathf.Clamp01(coast) * 0.35f - inlandDryness);
        }

        public float SampleRidges(int worldX, int worldY)
        {
            var n = Fbm(worldX, worldY, _ridgesOffsetX, _ridgesOffsetY,
                _cfg.RidgesScale, 4, 0.5f, 2f);
            return 1f - Mathf.Abs(n * 2f - 1f);
        }

        private float SampleEffectiveRidges(int worldX, int worldY, float continentalness, float erosion)
        {
            var raw = SampleRidges(worldX, worldY);
            if (!_cfg.ClimateCoupledToTerrain) return raw;
            var mountainChance = Mathf.SmoothStep(0.55f, 0.9f, continentalness);
            return Mathf.Clamp01(raw * (0.45f + mountainChance * 0.75f) * (1f - erosion * 0.45f));
        }

        /// <summary>
        /// 多倍频 Perlin 累加（海陆细节层），归一化到 [0,1]。
        /// </summary>
        public float SampleFbm(int worldX, int worldY) =>
            Fbm(worldX, worldY, _seedOffsetX, _seedOffsetY,
                _cfg.NoiseScale, _cfg.Octaves, _cfg.Persistence, _cfg.Lacunarity);

        /// <summary>
        /// 地幔线 / 海拔采样，输出 ∈ [0,1]。
        /// <list type="bullet">
        /// <item>独立 fBm（<see cref="PerlinMapConfig.ElevationScale"/> 等）决定山脉走向</item>
        /// <item><see cref="PerlinMapConfig.ElevationContinentLift"/> 让大陆中心天然抬高、海洋深处天然下沉</item>
        /// </list>
        /// </summary>
        public float SampleElevation(int worldX, int worldY)
        {
            var fbm = Fbm(worldX, worldY, _elevationOffsetX, _elevationOffsetY,
                _cfg.ElevationScale, _cfg.ElevationOctaves,
                _cfg.ElevationPersistence, _cfg.ElevationLacunarity);
            var continentalness = SampleContinentalness(worldX, worldY);
            var erosion = SampleEffectiveErosion(worldX, worldY, continentalness);
            var ridges = SampleEffectiveRidges(worldX, worldY, continentalness, erosion);
            fbm = Mathf.Clamp01(fbm * (1f - Mathf.Clamp01(_cfg.ErosionWeight) * erosion * 0.35f)
                + Mathf.Pow(ridges, 1.4f) * Mathf.Clamp01(_cfg.RidgesWeight) * 0.45f);

            var lift = Mathf.Clamp01(_cfg.ElevationContinentLift);
            if (lift <= 0f) return fbm;

            return Mathf.Clamp01(Mathf.Lerp(fbm, continentalness, lift) * Mathf.Max(0f, _cfg.VerticalScale));
        }

        /// <summary>
        /// 温度采样，输出 ∈ [0,1]。
        /// 公式：BaseTemperature
        ///       - LatitudeStrength * |sin(2π·y/Period)|   (纬度衰减)
        ///       - ElevationLapseRate * elevation           (海拔降温)
        ///       + TemperatureNoise ∈ [-strength, +strength] (局部扰动)
        /// </summary>
        public float SampleTemperature(int worldX, int worldY, float elevation)
        {
            // 纬度比例：赤道=0，极地=1。Period 是「赤道→极地→赤道」整圈，所以用 sin。
            var period = Mathf.Max(1f, _cfg.LatitudePeriod);
            var lat = Mathf.Abs(Mathf.Sin(worldY * Mathf.PI * 2f / period));

            var t = _cfg.BaseTemperature
                  - _cfg.LatitudeStrength * lat
                  - _cfg.ElevationLapseRate * elevation;

            if (_cfg.TemperatureNoiseStrength > 0f && _cfg.TemperatureNoiseScale > 0f)
            {
                var nx = (worldX + _temperatureOffsetX) * _cfg.TemperatureNoiseScale;
                var ny = (worldY + _temperatureOffsetY) * _cfg.TemperatureNoiseScale;
                var n = Mathf.PerlinNoise(nx, ny) * 2f - 1f; // [-1,1]
                t += n * _cfg.TemperatureNoiseStrength;
            }

            if (_cfg.ClimateCoupledToTerrain)
            {
                var continentalness = SampleContinentalness(worldX, worldY);
                var ridges = SampleRidges(worldX, worldY);
                t += (continentalness - 0.5f) * 0.08f;
                t -= ridges * _cfg.RidgesWeight * 0.08f;
            }

            return Mathf.Clamp01(t);
        }

        /// <summary>
        /// 湿度采样，输出 ∈ [0,1]。
        /// 公式：fBm
        ///       + OceanMoistureBoost * (1 - h)              (海陆距离近似：低海拔附近更湿)
        ///       - ElevationDryness * elevation              (高海拔变干)
        /// </summary>
        public float SampleMoisture(int worldX, int worldY, float heightForLandSea, float elevation)
        {
            var fbm = Fbm(worldX, worldY, _moistureOffsetX, _moistureOffsetY,
                _cfg.MoistureScale, _cfg.MoistureOctaves,
                _cfg.MoisturePersistence, _cfg.MoistureLacunarity);

            var oceanBoost = _cfg.OceanMoistureBoost * Mathf.Clamp01(1f - heightForLandSea);
            var dryFromAlt = _cfg.ElevationDryness * elevation;
            var moisture = Mathf.Lerp(fbm, _cfg.BaseMoisture, 0.35f) + oceanBoost - dryFromAlt;

            if (_cfg.ClimateCoupledToTerrain)
            {
                var continentalness = SampleContinentalness(worldX, worldY);
                var erosion = SampleEffectiveErosion(worldX, worldY, continentalness);
                var ridges = SampleEffectiveRidges(worldX, worldY, continentalness, erosion);
                var coastalWater = Mathf.Clamp01(1f - Mathf.Abs(heightForLandSea - _cfg.SeaLevel) / 0.25f);
                var inlandDryness = Mathf.SmoothStep(0.58f, 1f, continentalness) * 0.28f;
                moisture += coastalWater * 0.28f + erosion * 0.16f - ridges * 0.12f - inlandDryness;
            }

            return Mathf.Clamp01(moisture);
        }

        /// <summary>
        /// <see cref="IMapMetaProvider"/> 实现：返回 BiomeClassifier 的 pre-river 群系输出 + 归一化数值。
        /// <para>spawn 装饰器仅在规则带 <c>BiomeIds</c> 过滤时调用本方法（其余字段从 Tile 直接读以节省 fBm 重采样）。</para>
        /// </summary>
        public bool TryGetTileMeta(int worldX, int worldY, out TileMeta meta)
        {
            var h = SampleHeight(worldX, worldY);
            var elevation = SampleElevation(worldX, worldY);
            var temperature = SampleTemperature(worldX, worldY, elevation);
            var moisture = SampleMoisture(worldX, worldY, h, elevation);
            var continentalness = SampleContinentalness(worldX, worldY);
            var biomeId = BiomeClassifier.Classify(h, _cfg.SeaLevel, elevation, temperature, moisture);
            meta = new TileMeta
            {
                BiomeId = biomeId,
                Elevation = elevation,
                Temperature = temperature,
                Moisture = moisture,
                Continentalness = continentalness,
            };
            return true;
        }

        /// <summary>把归一化 [0,1] 浮点压到 byte。</summary>
        private static byte ToByte(float v01) =>
            (byte)Mathf.Clamp(Mathf.RoundToInt(v01 * 255f), 0, 255);

        /// <summary>通用 fBm 内核：调用方传入独立偏移与参数，便于多层独立采样。</summary>
        private static float Fbm(int worldX, int worldY,
            float offsetX, float offsetY,
            float scale, int octaves, float persistence, float lacunarity)
        {
            scale = Mathf.Max(0.0001f, scale);
            float total = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float maxAmplitude = 0f;

            for (var i = 0; i < octaves; i++)
            {
                var sx = (worldX + offsetX) * scale * frequency;
                var sy = (worldY + offsetY) * scale * frequency;
                total += Mathf.PerlinNoise(sx, sy) * amplitude;
                maxAmplitude += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return maxAmplitude > 0f ? total / maxAmplitude : 0f;
        }
    }
}
