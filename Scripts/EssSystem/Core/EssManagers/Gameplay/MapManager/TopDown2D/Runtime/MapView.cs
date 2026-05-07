using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using EssSystem.Core;
using EssSystem.Core.Event;
// §4.1 跨模块 EVT 走 bare-string 协议，不 using ResourceManager
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao;
using EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Runtime
{
    /// <summary>
    /// 地图视图 —— 把 <see cref="Map"/> 渲染到 Unity Tilemap。
    /// <para>
    /// 自身挂在带 <see cref="Grid"/> 的 GameObject 上，下方一个子 GameObject 持有
    /// <see cref="Tilemap"/> + <see cref="TilemapRenderer"/>。这两个 GO 由
    /// <see cref="MapService.CreateMapView"/> 工厂自动建好后再 <see cref="Bind"/> 绑定。
    /// </para>
    /// <para>
    /// **TileTypeId → RuleTile** 翻译流程：
    /// <list type="number">
    /// <item><see cref="MapService.GetTileType"/> 取 <see cref="TileTypeDef"/></item>
    /// <item>用 <c>RuleTileResourceId</c> 走 <see cref="ResourceManager.EVT_GET_RULE_TILE"/> 同步取缓存</item>
    /// <item>结果按 TypeId 缓存到 <see cref="_ruleTileCache"/>，避免重复事件调度</item>
    /// </list>
    /// </para>
    /// </summary>
    public enum DebugColorMode
    {
        /// <summary>不上色，使用 RuleTile 原色。</summary>
        None = 0,
        /// <summary>按 <c>Tile.Elevation</c> 上色（蓝→绿→黄→红，从低到高）。</summary>
        Elevation = 1,
        /// <summary>按 <c>Tile.Temperature</c> 上色（深蓝寒→蓝→绿→黄→红酷热）。</summary>
        Temperature = 2,
        /// <summary>按 <c>Tile.Moisture</c> 上色（褐干→黄→绿→青→蓝湿）。</summary>
        Moisture = 3,
        /// <summary>按 Biome（<c>Tile.TypeId</c>）上色，每种 Biome 一种独立调色。</summary>
        Biome = 4,
    }

    [RequireComponent(typeof(Grid))]
    public class MapView : MonoBehaviour
    {
        // ─── Inspector ─────────────────────────────────────────────
        [Header("Streaming")]
        [Tooltip("渲染半径：以焦点为中心，渲染 (2*radius+1)² 个区块。0 = 仅中心一块。")]
        [SerializeField, Range(0, 32)] private int _renderRadius = 3;

        [Tooltip("每帧最多渲染区块数；越大首帧越快，但 frame spike 越明显。")]
        [SerializeField, Min(1)] private int _chunksPerFrame = 1;

        [Tooltip("每帧最多渲染【行条】数（行 = ChunkSize 个 tile）。\n" +
                 "RuleTile.RefreshTile 会级联扫描邻居（ITilemap.FindAllRefreshPositions），\n" +
                 "一次 SetTiles 整块 Chunk 会在单帧堆积上万次 Refresh 调用 → 跑图卡顿。\n" +
                 "把一块 Chunk 拆成 ChunkSize 个行条跨帧渲染即可显著削平 spike。\n" +
                 "默认 4：ChunkSize=16 → 每帧 64 tile，一块 Chunk 在 4 帧内补齐，肉眼基本无感。")]
        [SerializeField, Min(1)] private int _rowsPerFrame = 4;

        [Tooltip("保活半径（Chebyshev，单位：区块）：已渲染区块只要仍在焦点 ±keepAlive 范围内就保留 Tilemap 数据，\n" +
                 "不会因为走出 renderRadius 而被卸载清空。用于在玩家短距离回头时避免重绘。\n" +
                 "必须 ≥ renderRadius；默认 100（覆盖 201×201=40401 区块范围）。\n" +
                 "内存代价：保留的 Tilemap Cell 数 ≤ (2*keepAlive+1)² × ChunkSize²，请按显存上限权衡。")]
        [SerializeField, Min(0)] private int _keepAliveRadius = 100;

        [Tooltip("预加载半径（Chebyshev，单位：区块）：在焦点 ±preloadRadius 内的区块会被提前 GetOrGenerateChunk —\n" +
                 "触发持久化读盘（若有 chunk 文件）+ 跑装饰器 + 入队 spawn 实体，但**不渲染** Tilemap。\n" +
                 "玩家走入 renderRadius 时只需画行条，不再卡顿在生成 / spawn 上。\n" +
                 "约束：preloadRadius ≥ renderRadius；< renderRadius 会自动按 renderRadius 处理。\n" +
                 "默认 = renderRadius + 2，行为温和；调大可让 spawn 更早就绪，但每多一圈 ~ (2r+1)² 次生成。")]
        [SerializeField, Min(0)] private int _preloadRadius = 5; // = default _renderRadius(3) + 2

        [Tooltip("自动跟随的 Transform；非 null 时每帧用其 position 作为焦点。" +
                 "null 时使用手动 SetFocus* 设置的焦点（默认 (0,0,0))。")]
        [SerializeField] private Transform _followTarget;

        [Header("Debug Coloring")]
        [Tooltip("调试着色模式：\n" +
                 "• None      = 正常渲染，使用 RuleTile 原色\n" +
                 "• Elevation = 按 Tile.Elevation 上色（蓝→绿→黄→红，从低到高）\n" +
                 "运行时切换会自动重渲染所有可见区块。")]
        [SerializeField] private DebugColorMode _debugColorMode = DebugColorMode.Biome;

        // ─── 运行时状态 ────────────────────────────────────────────
        private Map _map;
        private Tilemap _tilemap;
        private readonly Dictionary<string, TileBase> _ruleTileCache = new();

        /// <summary>当前焦点对应的区块坐标（初始 sentinel 触发首次构建队列）。</summary>
        private Vector2Int _focusChunk = new(int.MinValue, int.MinValue);

        /// <summary>当前已渲染区块集合。</summary>
        private readonly HashSet<Vector2Int> _renderedChunks = new();

        /// <summary>已预加载（仅 GetOrGenerateChunk 但不渲染 Tilemap）的区块集合，避免重复请求。</summary>
        private readonly HashSet<Vector2Int> _preloadedChunks = new();

        /// <summary>待渲染区块队列（按到焦点的距离排序，近的先来）。</summary>
        private readonly Queue<Vector2Int> _renderQueue = new();

        /// <summary>待预加载区块队列（[renderRadius+1, preloadRadius] 圈层；不参与 Tilemap 渲染）。</summary>
        private readonly Queue<Vector2Int> _preloadQueue = new();

        /// <summary>当前正在分帧渲染的 Chunk；<c>null</c> 代表没有正在切片的块。</summary>
        private Vector2Int? _partialChunk;

        /// <summary>当前分帧渲染 Chunk 的下一行（local y）。</summary>
        private int _partialRow;

        /// <summary>分帧渲染临时复用缓冲（行长 = ChunkSize），避免每行 new 数组。</summary>
        private Vector3Int[] _rowPositions;
        private TileBase[] _rowTiles;
        private Dao.Tile[] _rowDebugTiles;

        /// <summary>RebuildStreamingState 复用容器，避免每次重算时 GC alloc。</summary>
        private readonly HashSet<Vector2Int> _desiredScratch = new();
        private readonly List<Vector2Int> _toUnrenderScratch = new();
        private readonly List<Vector2Int> _newcomersScratch = new();

        /// <summary>排序比较器（Manhattan to focus），实例化一次避免 lambda 闭包 alloc。</summary>
        private sealed class FocusManhattanComparer : IComparer<Vector2Int>
        {
            public Vector2Int Center;
            public int Compare(Vector2Int a, Vector2Int b)
            {
                var da = Mathf.Abs(a.x - Center.x) + Mathf.Abs(a.y - Center.y);
                var db = Mathf.Abs(b.x - Center.x) + Mathf.Abs(b.y - Center.y);
                return da.CompareTo(db);
            }
        }
        private readonly FocusManhattanComparer _focusComparer = new();

        /// <summary>是否需要重算 desired chunks（焦点 / 半径变化时置 true）。</summary>
        private bool _streamingDirty = true;

        // ─── 公开属性 ──────────────────────────────────────────────
        /// <summary>当前绑定的运行时地图（可能为 null）。</summary>
        public Map Map => _map;

        /// <summary>底层 Tilemap，用于外部需要直接操作（如设置 Color、清除、查询）。</summary>
        public Tilemap Tilemap => _tilemap;

        /// <summary>渲染半径，运行时修改会触发流式重算。</summary>
        public int RenderRadius
        {
            get => _renderRadius;
            set
            {
                var v = Mathf.Max(0, value);
                if (v == _renderRadius) return;
                _renderRadius = v;
                _streamingDirty = true;
            }
        }

        /// <summary>每帧最多渲染区块数（异步流式预算）。</summary>
        public int ChunksPerFrame
        {
            get => _chunksPerFrame;
            set => _chunksPerFrame = Mathf.Max(1, value);
        }

        /// <summary>
        /// 预加载半径（≥ <see cref="RenderRadius"/>）。在 [renderRadius+1, preloadRadius] 范围内的 chunk
        /// 会被提前 <c>Map.GetOrGenerateChunk</c>（触发持久化读盘 + 装饰器 + spawn 入队），但**不画 Tilemap**。
        /// </summary>
        public int PreloadRadius
        {
            get => _preloadRadius;
            set
            {
                var v = Mathf.Max(0, value);
                if (v == _preloadRadius) return;
                _preloadRadius = v;
                _streamingDirty = true;
            }
        }

        /// <summary>
        /// 保活半径（Chebyshev 区块数）：已渲染区块在焦点 ±keepAlive 之内永不被卸载；
        /// 设为 0 等价于关闭保活，回退到「跟随 renderRadius 立刻卸载」旧行为。
        /// 运行时缩小会在下一帧重建时卸载超范围区块。
        /// </summary>
        public int KeepAliveRadius
        {
            get => _keepAliveRadius;
            set
            {
                var v = Mathf.Max(0, value);
                if (v == _keepAliveRadius) return;
                _keepAliveRadius = v;
                _streamingDirty = true;
            }
        }

        /// <summary>跟随的 Transform，运行时可换；置 null 则停止自动跟随。</summary>
        public Transform FollowTarget
        {
            get => _followTarget;
            set => _followTarget = value;
        }

        /// <summary>调试着色模式；运行时切换会重渲染所有可见区块。</summary>
        public DebugColorMode ColorMode
        {
            get => _debugColorMode;
            set
            {
                if (_debugColorMode == value) return;
                _debugColorMode = value;
                ForceRerenderVisible();
            }
        }

        /// <summary>由 <see cref="MapService.CreateMapView"/> 在添加完组件后调用一次。</summary>
        public void Bind(Map map, Tilemap tilemap)
        {
            _map = map;
            _tilemap = tilemap;
            _ruleTileCache.Clear();
            _renderedChunks.Clear();
            _preloadedChunks.Clear();
            _renderQueue.Clear();
            _preloadQueue.Clear();
            _partialChunk = null;
            _partialRow = 0;
            _focusChunk = new Vector2Int(int.MinValue, int.MinValue);
            _streamingDirty = true;

            // 预分配行缓冲（ChunkSize 固定，不重分配）。
            var size = _map?.ChunkSize ?? 0;
            if (size > 0)
            {
                _rowPositions = new Vector3Int[size];
                _rowTiles = new TileBase[size];
                _rowDebugTiles = new Dao.Tile[size];
            }
        }

        // ─────────────────────────────────────────────────────────────
        #region 流式焦点 API

        /// <summary>用世界坐标（Unity world position）设置焦点；自动转换为区块坐标。</summary>
        public void SetFocusWorldPosition(Vector3 worldPos)
        {
            if (_tilemap == null || _map == null) return;
            var cell = _tilemap.WorldToCell(worldPos);
            var cx = FloorDiv(cell.x, _map.ChunkSize);
            var cy = FloorDiv(cell.y, _map.ChunkSize);
            SetFocusChunk(cx, cy);
        }

        /// <summary>用世界 Tile 坐标设置焦点。</summary>
        public void SetFocusTile(int tileX, int tileY)
        {
            if (_map == null) return;
            SetFocusChunk(FloorDiv(tileX, _map.ChunkSize), FloorDiv(tileY, _map.ChunkSize));
        }

        /// <summary>直接用区块坐标设置焦点。</summary>
        public void SetFocusChunk(int chunkX, int chunkY)
        {
            var v = new Vector2Int(chunkX, chunkY);
            if (v == _focusChunk) return;
            var prev = _focusChunk;
            _focusChunk = v;
            _streamingDirty = true;

            // 焦点跨 Chunk 时立即把 RiverRegion（及其它实现挂的重计算）扔到 worker thread 预热，
            // 等玩家走到时主线程只读 cache，不再阻塞。
            if (_map != null && _map.Generator != null)
            {
                _map.Generator.PrewarmAround(chunkX, chunkY, _map.ChunkSize);
            }

            Debug.Log($"[MapView] 焦点变化 ({prev.x},{prev.y}) → ({v.x},{v.y})");
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Unity 生命周期 - 流式驱动

        private void Update()
        {
            if (_map == null || _tilemap == null) return;

            // ① 自动跟随
            if (_followTarget != null)
            {
                SetFocusWorldPosition(_followTarget.position);
            }
            // 焦点尚未初始化（无 follow + 无手动调用），用默认 (0,0) 触发首次构建
            else if (_focusChunk.x == int.MinValue)
            {
                SetFocusChunk(0, 0);
            }

            // ② 焦点 / 半径变化时重算 desired set + 卸载离开范围的 + 重排队列
            if (_streamingDirty)
            {
                RebuildStreamingState();
                _streamingDirty = false;
            }

            // ③ 本帧按【行条】预算分帧渲染（削平 RuleTile 级联 spike）。
            //    预算 = _rowsPerFrame × _chunksPerFrame，一行 = ChunkSize 个 tile。
            var size = _map.ChunkSize;
            var rowBudget = Mathf.Max(1, _rowsPerFrame) * Mathf.Max(1, _chunksPerFrame);
            while (rowBudget > 0)
            {
                // 若没有正在分片的 Chunk，从队列取下一块；
                // 取出时【独占 1 帧预算】只跑生成（Perlin + 生物群分类 + RiverTracer），
                // 把生成 spike 与行渲染解耦。下一帧再开始 row 0..ChunkSize-1 的行渲染。
                if (_partialChunk == null)
                {
                    Vector2Int? picked = null;
                    while (_renderQueue.Count > 0)
                    {
                        var next = _renderQueue.Dequeue();
                        if (_renderedChunks.Contains(next)) continue;
                        picked = next;
                        break;
                    }
                    if (picked == null) break; // 队列空，本帧结束

                    var pickedV = picked.Value;
                    _partialChunk = pickedV;
                    _partialRow = 0;

                    // 重生成（含可能的 RiverTracer.BuildRegion）独占当前 budget 单位，
                    // 不在同一帧再叠加行渲染 → 避免 "生成 + 16 行 RuleTile 级联" 同帧叠加 spike。
                    _map.GetOrGenerateChunk(pickedV.x, pickedV.y);
                    rowBudget--;
                    continue;
                }

                var pc = _partialChunk.Value;
                RenderChunkRow(pc.x, pc.y, _partialRow);
                _partialRow++;
                rowBudget--;

                if (_partialRow >= size)
                {
                    _renderedChunks.Add(pc);
                    _preloadedChunks.Remove(pc);   // 进入渲染态后从 preload 集合迁出
                    _partialChunk = null;
                    _partialRow = 0;
                }
            }

            // ④ 预加载：渲染队列已空 + 无分片中 chunk → 每帧最多 preload 1 个
            //    把生成 spike 与渲染分开，且严格限速避免一次铺开整圈。
            if (_renderQueue.Count == 0 && _partialChunk == null && _preloadQueue.Count > 0)
            {
                while (_preloadQueue.Count > 0)
                {
                    var pre = _preloadQueue.Dequeue();
                    if (_renderedChunks.Contains(pre) || _preloadedChunks.Contains(pre)) continue;
                    // GetOrGenerateChunk 触发 PostFillHook(读盘) + 装饰器 + spawn 入队，但不画 Tilemap。
                    _map.GetOrGenerateChunk(pre.x, pre.y);
                    _preloadedChunks.Add(pre);
                    break;
                }
            }
        }

        /// <summary>重算 desired chunks，卸载越界区块，新增入队（按距离近→远）。</summary>
        private void RebuildStreamingState()
        {
            var center = _focusChunk;
            var radius = _renderRadius;
            // 保活半径至少要 ≥ 渲染半径，否则保活语义就废了。
            var keep = Mathf.Max(_keepAliveRadius, radius);

            // (a) 收集本帧需要（新）渲染的区块集合（只限 renderRadius 范围）
            var desired = _desiredScratch;
            desired.Clear();
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    desired.Add(new Vector2Int(center.x + dx, center.y + dy));
                }
            }

            // (b) 卸载：仅卸载超出 keepAlive 的已渲染区块。
            //     在 [renderRadius+1, keepAlive] 区间的区块保留 Tilemap 数据、不重绘、也不卸载。
            var toUnrender = _toUnrenderScratch;
            toUnrender.Clear();
            foreach (var c in _renderedChunks)
            {
                var dx = Mathf.Abs(c.x - center.x);
                var dy = Mathf.Abs(c.y - center.y);
                if (dx > keep || dy > keep) toUnrender.Add(c); // Chebyshev 距离 > keep
            }
            for (var i = 0; i < toUnrender.Count; i++)
            {
                var c = toUnrender[i];
                UnrenderChunk(c.x, c.y);
                _renderedChunks.Remove(c);
            }

            // 若正在分帧渲染的 Chunk 已滑出 keepAlive 范围 → 直接放弃，避免做无用功。
            if (_partialChunk.HasValue)
            {
                var pc = _partialChunk.Value;
                var pdx = Mathf.Abs(pc.x - center.x);
                var pdy = Mathf.Abs(pc.y - center.y);
                if (pdx > keep || pdy > keep)
                {
                    // 已画出的部分行留在 Tilemap（keepAlive 逻辑会适时清理），这里只需丢状态。
                    _partialChunk = null;
                    _partialRow = 0;
                }
            }

            // (c) 重建队列（去掉旧未渲染项），把新的按距离近→远入队
            _renderQueue.Clear();
            var newcomers = _newcomersScratch;
            newcomers.Clear();
            foreach (var c in desired)
            {
                if (_renderedChunks.Contains(c)) continue;
                if (_partialChunk.HasValue && _partialChunk.Value == c) continue; // 正在分帧渲染，不重复排队
                newcomers.Add(c);
            }
            _focusComparer.Center = center;
            newcomers.Sort(_focusComparer);
            for (var i = 0; i < newcomers.Count; i++) _renderQueue.Enqueue(newcomers[i]);

            // (d) 预加载圈层 [renderRadius+1, preloadRadius]：仅 GetOrGenerateChunk + 装饰器 + spawn 入队，
            //     不画 Tilemap。让玩家走到时只需画行条，不再卡顿在生成 / 大量 spawn 上。
            var preload = Mathf.Max(_preloadRadius, radius);
            if (preload > radius)
            {
                _preloadQueue.Clear();
                var preloadList = _newcomersScratch;   // 复用 List
                preloadList.Clear();
                for (var dy = -preload; dy <= preload; dy++)
                {
                    for (var dx = -preload; dx <= preload; dx++)
                    {
                        var rdx = Mathf.Abs(dx);
                        var rdy = Mathf.Abs(dy);
                        // 已在 renderRadius 范围 → 走 _renderQueue，不重复入预加载
                        if (rdx <= radius && rdy <= radius) continue;
                        var c = new Vector2Int(center.x + dx, center.y + dy);
                        if (_renderedChunks.Contains(c)) continue;
                        if (_preloadedChunks.Contains(c)) continue;
                        preloadList.Add(c);
                    }
                }
                preloadList.Sort(_focusComparer);
                for (var i = 0; i < preloadList.Count; i++) _preloadQueue.Enqueue(preloadList[i]);
            }
            else
            {
                _preloadQueue.Clear();
            }

            // (e) 清理 _preloadedChunks 中超出 keepAlive 的项（避免无限增长）
            if (_preloadedChunks.Count > 0)
            {
                _toUnrenderScratch.Clear();
                foreach (var c in _preloadedChunks)
                {
                    if (Mathf.Abs(c.x - center.x) > keep || Mathf.Abs(c.y - center.y) > keep)
                        _toUnrenderScratch.Add(c);
                }
                for (var i = 0; i < _toUnrenderScratch.Count; i++)
                    _preloadedChunks.Remove(_toUnrenderScratch[i]);
            }
        }

        /// <summary>清空指定区块在 Tilemap 上的所有瓦片（不影响 Map 数据）。</summary>
        private void UnrenderChunk(int chunkX, int chunkY)
        {
            if (_tilemap == null || _map == null) return;
            var size = _map.ChunkSize;
            var baseX = chunkX * size;
            var baseY = chunkY * size;
            var count = size * size;
            var positions = new Vector3Int[count];
            var tiles = new TileBase[count]; // 全 null = 清除

            var i = 0;
            for (var ly = 0; ly < size; ly++)
            {
                for (var lx = 0; lx < size; lx++)
                {
                    positions[i++] = new Vector3Int(baseX + lx, baseY + ly, 0);
                }
            }
            _tilemap.SetTiles(positions, tiles);
        }

        /// <summary>向下取整除（处理负坐标）。</summary>
        private static int FloorDiv(int a, int b)
        {
            var q = a / b;
            if ((a % b) != 0 && ((a < 0) ^ (b < 0))) q--;
            return q;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region 渲染入口

        /// <summary>
        /// 渲染单个区块（同步、单帧内完成）。外部直接调用会产生 spike；
        /// 流式路径请用 <see cref="RenderChunkRow"/>。此方法内部循环调用 RenderChunkRow。
        /// </summary>
        public void RenderChunk(int chunkX, int chunkY)
        {
            if (_map == null || _tilemap == null) return;
            var size = _map.ChunkSize;
            for (var ly = 0; ly < size; ly++) RenderChunkRow(chunkX, chunkY, ly);
        }

        /// <summary>
        /// 渲染单个区块的一行（<c>ly</c> ∈ [0, ChunkSize)）。
        /// <para>
        /// 流式渲染的最小工作单元：一次 <c>SetTilesBlock</c> 连续 <c>ChunkSize</c> 个 tile，
        /// RuleTile.RefreshTile 级联规模从"一整块 Chunk"降为"一行"，单帧 Refresh 工作量降为 1/ChunkSize。
        /// </para>
        /// </summary>
        public void RenderChunkRow(int chunkX, int chunkY, int ly)
        {
            if (_map == null || _tilemap == null) return;

            var chunk = _map.GetOrGenerateChunk(chunkX, chunkY);
            if (chunk == null) return;

            var size = chunk.Size;
            if ((uint)ly >= (uint)size) return;

            // 确保行缓冲已分配（兼容 Bind 之前被外部调用的极端场景）
            if (_rowPositions == null || _rowPositions.Length != size)
            {
                _rowPositions = new Vector3Int[size];
                _rowTiles = new TileBase[size];
                _rowDebugTiles = new Dao.Tile[size];
            }

            var baseX = chunk.WorldOriginX;
            var baseY = chunk.WorldOriginY;
            var worldY = baseY + ly;

            for (var lx = 0; lx < size; lx++)
            {
                _rowPositions[lx] = new Vector3Int(baseX + lx, worldY, 0);
                var t = chunk.GetTile(lx, ly);
                _rowTiles[lx] = ResolveRuleTile(t.TypeId);
                _rowDebugTiles[lx] = t;
            }

            // SetTilesBlock 连续块 API：比 SetTiles(Vector3Int[],TileBase[]) 内部 invalidation 更紧凑。
            var bounds = new BoundsInt(baseX, worldY, 0, size, 1, 1);
            _tilemap.SetTilesBlock(bounds, _rowTiles);

            // 调试着色：只在调试模式开启 或 水体需要水色时才走 SetColor，
            // 避免 None 模式下对每个陆地 tile 无意义 invalidation。
            var needColorPass = _debugColorMode != DebugColorMode.None;
            if (!needColorPass)
            {
                for (var k = 0; k < size; k++)
                {
                    if (IsWaterTile(_rowDebugTiles[k].TypeId)) { needColorPass = true; break; }
                }
            }
            if (needColorPass)
            {
                for (var k = 0; k < size; k++)
                {
                    var pos = _rowPositions[k];
                    _tilemap.SetTileFlags(pos, TileFlags.None);
                    _tilemap.SetColor(pos, ResolveTileColor(_rowDebugTiles[k]));
                }
            }
        }

        private static bool IsWaterTile(string typeId)
        {
            return typeId == TopDownTileTypes.DeepOcean
                || typeId == Dao.TileTypes.Ocean
                || typeId == TopDownTileTypes.ShallowOcean
                || typeId == TopDownTileTypes.River
                || typeId == TopDownTileTypes.Lake;
        }

        private Color ResolveTileColor(Dao.Tile tile)
        {
            if (_debugColorMode != DebugColorMode.None) return ResolveDebugColor(tile);
            if (tile.TypeId == TopDownTileTypes.DeepOcean
                || tile.TypeId == Dao.TileTypes.Ocean
                || tile.TypeId == TopDownTileTypes.ShallowOcean
                || tile.TypeId == TopDownTileTypes.River
                || tile.TypeId == TopDownTileTypes.Lake) return new Color(0.82f, 0.96f, 1.00f);
            return Color.white;
        }

        /// <summary>把当前 ColorMode 应用到一个 Tile，得到调试色。</summary>
        private Color ResolveDebugColor(Dao.Tile tile)
        {
            switch (_debugColorMode)
            {
                case DebugColorMode.Elevation:
                    return ElevationToColor(tile.ElevationNormalized);
                case DebugColorMode.Temperature:
                    return TemperatureToColor(tile.TemperatureNormalized);
                case DebugColorMode.Moisture:
                    return MoistureToColor(tile.MoistureNormalized);
                case DebugColorMode.Biome:
                    return BiomeToColor(tile.TypeId);
                case DebugColorMode.None:
                default:
                    return Color.white;
            }
        }

        /// <summary>0~1 → 蓝(深海) → 青 → 绿(平原) → 黄(丘陵) → 红(高山) 的伪彩色。</summary>
        private static Color ElevationToColor(float t)
        {
            t = Mathf.Clamp01(t);
            // 4 段线性渐变
            if (t < 0.25f) return Color.Lerp(new Color(0f, 0f, 0.5f), Color.cyan, t / 0.25f);
            if (t < 0.50f) return Color.Lerp(Color.cyan, Color.green, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Color.Lerp(Color.green, Color.yellow, (t - 0.50f) / 0.25f);
            return Color.Lerp(Color.yellow, Color.red, (t - 0.75f) / 0.25f);
        }

        /// <summary>0~1 → 深蓝(极寒) → 蓝 → 绿(温) → 黄 → 红(酷热)。</summary>
        private static Color TemperatureToColor(float t)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.25f) return Color.Lerp(new Color(0.05f, 0.05f, 0.5f), Color.blue, t / 0.25f);
            if (t < 0.50f) return Color.Lerp(Color.blue, Color.green, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Color.Lerp(Color.green, Color.yellow, (t - 0.50f) / 0.25f);
            return Color.Lerp(Color.yellow, Color.red, (t - 0.75f) / 0.25f);
        }

        /// <summary>0~1 → 棕(沙漠) → 黄 → 绿(草原) → 青 → 蓝(雨林/沼泽)。</summary>
        private static Color MoistureToColor(float t)
        {
            t = Mathf.Clamp01(t);
            var brown = new Color(0.55f, 0.35f, 0.10f);
            if (t < 0.25f) return Color.Lerp(brown, Color.yellow, t / 0.25f);
            if (t < 0.50f) return Color.Lerp(Color.yellow, Color.green, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Color.Lerp(Color.green, Color.cyan, (t - 0.50f) / 0.25f);
            return Color.Lerp(Color.cyan, new Color(0.05f, 0.2f, 0.7f), (t - 0.75f) / 0.25f);
        }

        /// <summary>Biome → 唯一调色（生物群系示意图）。未识别 → 紫红。</summary>
        private static Color BiomeToColor(string typeId)
        {
            switch (typeId)
            {
                // 海洋 / 河流 / 湖泊：统一亮青蓝（与正常渲染色保持一致）
                case TopDownTileTypes.DeepOcean:    return new Color(0.82f, 0.96f, 1.00f);
                case Dao.TileTypes.Ocean:        return new Color(0.82f, 0.96f, 1.00f);
                case TopDownTileTypes.ShallowOcean: return new Color(0.82f, 0.96f, 1.00f);
                case TopDownTileTypes.River:        return new Color(0.82f, 0.96f, 1.00f);
                case TopDownTileTypes.Lake:         return new Color(0.82f, 0.96f, 1.00f);
                // 高度类
                case TopDownTileTypes.Beach:        return new Color(0.95f, 0.88f, 0.60f);
                case TopDownTileTypes.Hill:         return new Color(0.55f, 0.45f, 0.25f);
                case TopDownTileTypes.Mountain:     return new Color(0.40f, 0.35f, 0.30f);
                // 沙地基底 (≈0.95, 0.85, 0.6) × HDR 冷光 → LDR clamp 出近白冷色
                // 绿/蓝通道故意 >1：乘沙地 (0.85/0.6) 后分别落到 ≈1.02/1.08，framebuffer 写入时 clamp 到 1
                // 最终可见 RGB ≈ (0.95, 1.00, 1.00) → 明显的冰白
                case TopDownTileTypes.SnowPeak:     return new Color(1.00f, 1.20f, 1.80f);
                // 寒带
                case TopDownTileTypes.Tundra:       return new Color(0.75f, 0.78f, 0.78f);
                case TopDownTileTypes.Taiga:        return new Color(0.30f, 0.50f, 0.45f);
                // 温带
                case TopDownTileTypes.Grassland:    return new Color(0.55f, 0.80f, 0.35f);
                case TopDownTileTypes.Forest:       return new Color(0.20f, 0.55f, 0.20f);
                case TopDownTileTypes.Swamp:        return new Color(0.30f, 0.40f, 0.25f);
                // 热带
                case TopDownTileTypes.Desert:       return new Color(0.95f, 0.80f, 0.40f);
                case TopDownTileTypes.Savanna:      return new Color(0.80f, 0.70f, 0.30f);
                case TopDownTileTypes.Rainforest:   return new Color(0.05f, 0.45f, 0.20f);
                // 兼容旧 ID
                case Dao.TileTypes.Land:         return new Color(0.55f, 0.80f, 0.35f);
                default:                          return Color.magenta; // 兜底：未注册类型
            }
        }

        /// <summary>清空已渲染缓存并触发流式重建（保持现有 desired 区块）。</summary>
        private void ForceRerenderVisible()
        {
            if (_tilemap == null) return;
            _tilemap.ClearAllTiles();
            _renderedChunks.Clear();
            _renderQueue.Clear();
            _partialChunk = null;
            _partialRow = 0;
            _streamingDirty = true;
        }

#if UNITY_EDITOR
        /// <summary>Inspector 中切换 ColorMode 时重渲染。</summary>
        private DebugColorMode _lastValidatedMode;
        private void OnValidate()
        {
            if (Application.isPlaying && _lastValidatedMode != _debugColorMode)
            {
                _lastValidatedMode = _debugColorMode;
                ForceRerenderVisible();
            }
        }
#endif

        /// <summary>
        /// 渲染中心区块附近 <c>(2*radius+1)²</c> 个区块（含中心）。
        /// </summary>
        public void RenderRegion(int centerChunkX, int centerChunkY, int radius)
        {
            if (radius < 0) radius = 0;
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    RenderChunk(centerChunkX + dx, centerChunkY + dy);
                }
            }
        }

        /// <summary>渲染当前 Map 已加载的全部区块。</summary>
        public void RenderAll()
        {
            if (_map == null) return;
            foreach (var kv in _map.LoadedChunks)
            {
                var c = kv.Value;
                RenderChunk(c.ChunkX, c.ChunkY);
            }
        }

        /// <summary>清空 Tilemap 所有已绘制的瓦片（不影响 Map 数据）。</summary>
        public void ClearTilemap()
        {
            if (_tilemap != null) _tilemap.ClearAllTiles();
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region TileTypeId → RuleTile 解析

        private TileBase ResolveRuleTile(string typeId)
        {
            if (string.IsNullOrEmpty(typeId)) return null;

            if (_ruleTileCache.TryGetValue(typeId, out var cached)) return cached;

            var def = MapService.Instance.GetTileType(typeId);
            if (def == null || string.IsNullOrEmpty(def.RuleTileResourceId))
            {
                Debug.LogWarning($"[MapView] TileType '{typeId}' 未注册或缺 RuleTileResourceId，将渲染为空");
                _ruleTileCache[typeId] = null;
                return null;
            }

            // §4.1 跨模块 bare-string：ResourceManager.EVT_GET_RULE_TILE
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetRuleTile",
                new List<object> { def.RuleTileResourceId });

            TileBase tile = null;
            if (ResultCode.IsOk(result) && result.Count >= 2)
            {
                tile = result[1] as TileBase;
            }

            if (tile == null)
            {
                Debug.LogWarning(
                    $"[MapView] 获取 RuleTile 失败：typeId='{typeId}', resourceId='{def.RuleTileResourceId}'。" +
                    $"请确认 Resources 下存在该 RuleTile 资产，且 m_Name 与文件名一致" +
                    $"（必要时在 Unity 内右键资产 → Reimport 触发刷新）");
            }
            _ruleTileCache[typeId] = tile;
            return tile;
        }

        #endregion
    }
}
