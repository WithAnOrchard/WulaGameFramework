using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using EssSystem.EssManager.MapManager.Dao;
using EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Config;
using EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Dao;
using UnityEngine;

namespace EssSystem.EssManager.MapManager.Dao.Templates.TopDownRandom.Generator
{
    public static class RiverTracer
    {
        // Region 尺寸（单位：Chunk）。越大：河流连通性越好（跨区边界接缝少），但每 N×ChunkSize Tile 的边界会触发一次
        // 重建 (O((RegionChunks*ChunkSize)²))，肉眼可感知 spike。
        // 16×16=256 Tile 边界：单次 ~6.5 万 Tile 计算，几 ms ~ 几十 ms；现已挪到 worker thread + 提前预热，几乎不阻塞主线程。
        private const int RegionChunks = 16;

        // 紧凑 cache key —— 避免每个 chunk Trace 巨型字符串拼接产生 GC。
        private readonly struct RegionKey : IEquatable<RegionKey>
        {
            public readonly int Seed;
            public readonly int ChunkSize;
            public readonly int RCX;
            public readonly int RCY;

            public RegionKey(int seed, int chunkSize, int rcx, int rcy)
            { Seed = seed; ChunkSize = chunkSize; RCX = rcx; RCY = rcy; }

            public bool Equals(RegionKey o) => Seed == o.Seed && ChunkSize == o.ChunkSize && RCX == o.RCX && RCY == o.RCY;
            public override bool Equals(object obj) => obj is RegionKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + Seed;
                    h = h * 31 + ChunkSize;
                    h = h * 31 + RCX;
                    h = h * 31 + RCY;
                    return h;
                }
            }
        }

        private static readonly ConcurrentDictionary<RegionKey, RiverRegion> Cache = new();
        // 进行中的异步构建 —— 多个调用方共享同一个 Task，避免重复计算。
        private static readonly ConcurrentDictionary<RegionKey, Task<RiverRegion>> InFlight = new();

        private sealed class RiverRegion
        {
            public int OriginX;
            public int OriginY;
            public int Size;
            public bool[] River;
            public bool[] Lake;
            public byte[] FlowByte;
        }

        public static void Trace(Chunk chunk, PerlinMapGenerator gen, PerlinMapConfig cfg)
        {
            if (!cfg.RiverEnabled) return;

            var region = GetRegionBlocking(chunk.ChunkX, chunk.ChunkY, chunk.Size, gen, cfg);
            var cs = chunk.Size;
            for (var ly = 0; ly < cs; ly++)
            for (var lx = 0; lx < cs; lx++)
            {
                var rx = chunk.WorldOriginX + lx - region.OriginX;
                var ry = chunk.WorldOriginY + ly - region.OriginY;
                if (rx < 0 || ry < 0 || rx >= region.Size || ry >= region.Size) continue;
                var i = ry * region.Size + rx;
                if (!region.River[i] && !region.Lake[i]) continue;

                var tile = chunk.GetTile(lx, ly);
                if (IsOcean(tile.TypeId)) continue;
                tile.TypeId = region.River[i] ? TopDownTileTypes.River : TopDownTileTypes.Lake;
                tile.RiverFlow = region.FlowByte[i];
            }
        }

        /// <summary>
        /// 提前在 worker thread 上预热以 (chunkX, chunkY) 为中心的 3x3 RiverRegion 邻域。
        /// MapView 在焦点跨 Chunk 时调用一次 → 区域构建尽可能在玩家走到该区域之前完成，主线程零阻塞。
        /// </summary>
        public static void PrewarmRegionsAround(int chunkX, int chunkY, int chunkSize, PerlinMapGenerator gen, PerlinMapConfig cfg)
        {
            if (!cfg.RiverEnabled) return;
            var rcx = FloorDiv(chunkX, RegionChunks);
            var rcy = FloorDiv(chunkY, RegionChunks);
            for (var dy = -1; dy <= 1; dy++)
            for (var dx = -1; dx <= 1; dx++)
                EnsureRegionAsync(rcx + dx, rcy + dy, chunkSize, gen, cfg);
        }

        /// <summary>
        /// 异步触发指定 Region 构建（若尚未缓存且未在飞行中）。返回的 Task 完成时区域已写入 Cache。
        /// </summary>
        private static Task<RiverRegion> EnsureRegionAsync(int rcx, int rcy, int chunkSize, PerlinMapGenerator gen, PerlinMapConfig cfg)
        {
            var key = new RegionKey(cfg.Seed, chunkSize, rcx, rcy);
            if (Cache.TryGetValue(key, out var cached)) return Task.FromResult(cached);
            return InFlight.GetOrAdd(key, k =>
                Task.Run(() =>
                {
                    var region = BuildRegion(k.RCX * RegionChunks * k.ChunkSize, k.RCY * RegionChunks * k.ChunkSize, RegionChunks * k.ChunkSize, gen, cfg);
                    Cache[k] = region;
                    InFlight.TryRemove(k, out _);
                    return region;
                })
            );
        }

        /// <summary>
        /// 同步获取 Region（命中 cache O(1)；in-flight 则等待 Task；都没命中则当前线程构建一次）。
        /// 在 PrewarmRegionsAround 调用充分时几乎总是命中 cache，主线程不阻塞。
        /// </summary>
        private static RiverRegion GetRegionBlocking(int chunkX, int chunkY, int chunkSize, PerlinMapGenerator gen, PerlinMapConfig cfg)
        {
            var rcx = FloorDiv(chunkX, RegionChunks);
            var rcy = FloorDiv(chunkY, RegionChunks);
            var key = new RegionKey(cfg.Seed, chunkSize, rcx, rcy);

            if (Cache.TryGetValue(key, out var cached)) return cached;

            // 已在 worker 上构建中：等待它完成（极少发生 —— 仅当 prewarm 不及时）
            if (InFlight.TryGetValue(key, out var inflight))
            {
                return inflight.GetAwaiter().GetResult();
            }

            // 完全没人启动：当前线程同步构建（首次进入区域 / 跳跃传送 等罕见情形）
            var region = BuildRegion(rcx * RegionChunks * chunkSize, rcy * RegionChunks * chunkSize, RegionChunks * chunkSize, gen, cfg);
            Cache[key] = region;
            return region;
        }

        private static RiverRegion BuildRegion(int originX, int originY, int size, PerlinMapGenerator gen, PerlinMapConfig cfg)
        {
            var n = size * size;
            var ocean = new bool[n];
            var river = new bool[n];
            var lake = new bool[n];
            var flowByte = new byte[n];
            var riverScale = Mathf.Max(0.00005f, cfg.ErosionScale * 0.45f);
            var detailScale = Mathf.Max(0.00005f, cfg.RidgesScale * 0.65f);
            var baseWidth = Mathf.Lerp(0.018f, 0.065f, Mathf.Clamp01(cfg.RiverWidthPerFlowDecade / 4f));
            var thresholdFactor = Mathf.InverseLerp(160f, 1f, Mathf.Max(1, cfg.RiverFlowThreshold));
            var riverWidth = baseWidth * Mathf.Lerp(0.55f, 1.35f, thresholdFactor);
            var riverSeedA = cfg.Seed + 7001;
            var riverSeedB = cfg.Seed + 17011;
            var lakeSeed = cfg.Seed + 31337;

            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var wx = originX + x;
                var wy = originY + y;
                var idx = y * size + x;
                var h = gen.SampleHeight(wx, wy);
                var e = gen.SampleElevation(wx, wy);
                ocean[idx] = h < cfg.SeaLevel;
                if (ocean[idx]) continue;

                var continentalness = gen.SampleContinentalness(wx, wy);
                var moisture = gen.SampleMoisture(wx, wy, h, e);
                var detail = Noise01(wx, wy, detailScale, riverSeedB);
                var riverAxis = Mathf.Abs(Noise01(wx, wy, riverScale, riverSeedA) * 2f - 1f);
                var meander = (detail - 0.5f) * cfg.RiverMeanderStrength * 0.06f;
                var continentalGate = Mathf.SmoothStep(cfg.SeaLevel + 0.02f, 0.86f, continentalness);
                var lowlandGate = 1f - Mathf.SmoothStep(0.48f, 0.92f, e);
                var moistureGate = cfg.ClimateCoupledToTerrain
                    ? Mathf.SmoothStep(0.18f, 0.75f, moisture)
                    : Mathf.Lerp(0.65f, 1f, moisture);
                var chance = continentalGate * lowlandGate * moistureGate;
                var distance = riverAxis + meander;

                if (distance < riverWidth * chance)
                {
                    var strength = Mathf.Clamp01(1f - distance / Mathf.Max(0.0001f, riverWidth));
                    PaintNoiseRiver(x, y, size, strength, cfg, ocean, river, flowByte);
                    continue;
                }

                var lakeNoise = Noise01(wx, wy, riverScale * 1.7f, lakeSeed);
                if (cfg.RiverLakeChance > 0f
                    && e < 0.45f
                    && moisture > 0.55f
                    && lakeNoise > 1f - cfg.RiverLakeChance * 4f)
                {
                    PaintNoiseLake(x, y, size, cfg.RiverLakeRadius, lakeNoise, ocean, river, lake, flowByte);
                }
            }

            return new RiverRegion
            {
                OriginX = originX,
                OriginY = originY,
                Size = size,
                River = river,
                Lake = lake,
                FlowByte = flowByte
            };
        }

        private static void PaintNoiseRiver(int x, int y, int size, float strength, PerlinMapConfig cfg, bool[] ocean, bool[] river, byte[] flowByte)
        {
            var width = 0;
            if (strength > 0.55f && cfg.RiverWidthPerFlowDecade >= 1f) width = 1;
            if (strength > 0.82f && cfg.RiverWidthPerFlowDecade >= 2f) width = 2;
            for (var dy = -width; dy <= width; dy++)
            for (var dx = -width; dx <= width; dx++)
            {
                if (dx * dx + dy * dy > width * width) continue;
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                var ni = ny * size + nx;
                if (ocean[ni]) continue;
                river[ni] = true;
                var v = (byte)Mathf.Clamp(Mathf.RoundToInt(80f + strength * 175f), 60, 255);
                if (flowByte[ni] < v) flowByte[ni] = v;
            }
        }

        private static void PaintNoiseLake(int x, int y, int size, int radius, float noise, bool[] ocean, bool[] river, bool[] lake, byte[] flowByte)
        {
            radius = Mathf.Max(1, radius);
            for (var dy = -radius; dy <= radius; dy++)
            for (var dx = -radius; dx <= radius; dx++)
            {
                var nx = x + dx;
                var ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) continue;
                var ni = ny * size + nx;
                if (ocean[ni] || river[ni]) continue;
                var edge = Mathf.Sqrt(dx * dx + dy * dy) / radius;
                if (edge > Mathf.Lerp(0.65f, 1.15f, noise)) continue;
                lake[ni] = true;
                if (flowByte[ni] < 180) flowByte[ni] = 180;
            }
        }

        private static bool IsOcean(string typeId)
        {
            return typeId == TopDownTileTypes.DeepOcean || typeId == EssSystem.EssManager.MapManager.Dao.TileTypes.Ocean || typeId == TopDownTileTypes.ShallowOcean;
        }

        private static int FloorDiv(int a, int b)
        {
            var q = a / b;
            var r = a % b;
            if (r != 0 && ((r < 0) != (b < 0))) q--;
            return q;
        }

        private static float Noise01(int x, int y, float scale, int seed)
        {
            var ox = (seed & 0xFFFF) * 0.173f;
            var oy = ((seed >> 8) & 0xFFFF) * 0.197f;
            return Mathf.PerlinNoise((x + ox) * scale, (y + oy) * scale);
        }
    }
}
