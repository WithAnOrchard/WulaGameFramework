using UnityEngine;
using Demo.Cubic.Utils;

namespace Demo.Cubic.Map
{
    /// <summary>
    /// Cubic 场景美化 —— 程序化搭场景：多层视差背景 + 雾效 + 散布装饰 + 氛围点光源。
    /// <para>
    /// <b>为什么用程序化搭建</b>：.unity 是二进制 YAML，Cubic 走代码生成场景（<see cref="CubicMap.Awake"/> 之后挂本组件），
    /// Editor 不用手摆 prefab，git diff 友好。蓝图化靠本类参数面板，视觉迭代成本极低。
    /// </para>
    /// <para>
    /// <b>图层 Z 序</b>（相机在 Z=-10 看 +Z，X 横向、Y 纵向）：
    /// <list type="number">
    /// <item>Z=22 — 远景天空 / 渐变（无 parallax，背景色填充）</item>
    /// <item>Z=14 — 远山剪影（暗紫/暗蓝）</item>
    /// <item>Z=8  — 中景丘陵（深绿）</item>
    /// <item>Z=0  — 行动平面（地面、玩家、敌人在 CubicMap 已搭好）</item>
    /// <item>Z=-3 — 近景草叶（在玩家与相机之间，做视差"前层遮挡"）</item>
    /// </list>
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CubicSceneDecor : MonoBehaviour
    {
        [Header("总开关")]
        [SerializeField] private bool _enableFog = true;
        [SerializeField] private bool _enableParallax = true;
        [SerializeField] private bool _enableGroundDecor = true;
        [SerializeField] private bool _enableMoodLights = true;

        [Header("地图尺寸（应与 CubicMap 一致）")]
        [SerializeField] private float _mapWidth = 1000f;
        [SerializeField] private float _mapHeight = 20f;

        [Header("视差背景")]
        [SerializeField] private Color _skyColor = new Color(0.18f, 0.22f, 0.32f, 1f);
        [SerializeField] private Color _farMountainColor = new Color(0.12f, 0.10f, 0.18f, 1f);
        [SerializeField] private Color _midHillColor    = new Color(0.14f, 0.18f, 0.12f, 1f);
        [SerializeField] private Color _nearDecorColor  = new Color(0.20f, 0.24f, 0.18f, 1f);
        [SerializeField] private float _farMountainHeight = 16f;
        [SerializeField] private float _midHillHeight    = 10f;
        [SerializeField] private float _nearDecorHeight  = 4f;

        [Header("雾效（线性 Fog）")]
        [SerializeField] private Color _fogColor = new Color(0.18f, 0.22f, 0.32f, 1f);
        [SerializeField] private float _fogStart = 20f;
        [SerializeField] private float _fogEnd   = 80f;

        [Header("地面装饰（程序化散布，确定性 seed）")]
        [SerializeField] private int _rockCount     = 60;
        [SerializeField] private int _mushroomCount = 24;
        [SerializeField] private int _grassCount    = 80;
        [SerializeField] private int _crystalCount  = 8;
        [SerializeField] private float _decorYOffset = 0.05f;
        [SerializeField] private int _seed = 42;

        [Header("氛围点光源")]
        [SerializeField] private int _campfireCount  = 2;
        [SerializeField] private int _crystalLightCount = 3;
        [SerializeField] private Color _campfireColor = new Color(1.0f, 0.55f, 0.2f, 1f);
        [SerializeField] private Color _crystalColor1 = new Color(0.3f, 0.7f, 1.0f, 1f);
        [SerializeField] private Color _crystalColor2 = new Color(0.9f, 0.3f, 0.9f, 1f);

        [Header("星空")]
        [SerializeField] private bool _enableSkyDecor = true;
        [SerializeField] private int _starCount = 200;
        [SerializeField] private float _moonSize = 3.0f;
        [SerializeField] private Color _moonColor = new Color(0.95f, 0.93f, 0.85f, 1f);
        [SerializeField] private Color _starColor = new Color(0.9f, 0.95f, 1.0f, 1f);

        // 运行时生成的容器
        private GameObject _rootParallax;
        private GameObject _rootDecor;
        private GameObject _rootLights;

        // 视差层：transform + 跟随因子（0=不动，1=完全跟相机）
        private struct ParallaxLayer { public Transform t; public float factor; public float baseX; }
        private readonly System.Collections.Generic.List<ParallaxLayer> _layers = new();

        // 相机在 Build 完成时的初始 X（视差相对量以此为锚点）
        private float _initialCamX;

        // 权威尺寸：若同 GO 上有 CubicMap，运行时从它取；否则走面板默认值
        private CubicMap _map;
        private float MapWidth  => _map != null ? _map.MapWidth  : _mapWidthDefault;
        private float MapHeight => _map != null ? _map.MapHeight : _mapHeightDefault;
        private float GroundThickness => _map != null ? _map.GroundHeight : _groundThicknessDefault;
        private float GroundTopY => -MapHeight / 2f + GroundThickness;

        [Header("地图尺寸 fallback（仅当场景里无 CubicMap 时生效）")]
        [SerializeField] private float _mapWidthDefault    = 30f;
        [SerializeField] private float _mapHeightDefault   = 20f;
        [SerializeField] private float _groundThicknessDefault = 2f;

        private void Start()
        {
            // 等 CubicMap.Awake 跑完（先 Start 是因为 CubicMap 也在 Awake 建地面/相机/主光）
            _map = GetComponent<CubicMap>();
            Build();
            // 记录相机初始 X，作为视差位移的零点
            if (_map != null && _map.GetMainCamera() != null)
                _initialCamX = _map.GetMainCamera().transform.position.x;
        }

        private void Update()
        {
            UpdateParallax();
        }

        /// <summary>正交相机下的真视差：每层按 factor 跟随相机 X，factor=0 完全静止，factor=1 紧贴相机。</summary>
        private void UpdateParallax()
        {
            if (_map == null || _layers.Count == 0) return;
            var cam = _map.GetMainCamera();
            if (cam == null) return;
            var camX = cam.transform.position.x;
            var delta = camX - _initialCamX;
            for (var i = 0; i < _layers.Count; i++)
            {
                var l = _layers[i];
                if (l.t == null) continue;
                var p = l.t.position;
                p.x = l.baseX + delta * l.factor;
                l.t.position = p;
            }
        }

        /// <summary>对外暴露的重新构建入口（不重置原对象）。</summary>
        public void Build()
        {
            if (_enableFog)               SetupFog();
            if (_enableParallax)          BuildParallaxLayers();
            if (_enableGroundDecor)       ScatterGroundDecor();
            if (_enableMoodLights)        PlaceMoodLights();
            if (_enableSkyDecor)          BuildSkyDecor();
        }

        // ════════════════════════════════════════════════════════════
        //  雾效
        // ════════════════════════════════════════════════════════════

        private void SetupFog()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = _fogColor;
            RenderSettings.fogStartDistance = _fogStart;
            RenderSettings.fogEndDistance = _fogEnd;
        }

        // ════════════════════════════════════════════════════════════
        //  多层视差背景（远山 / 中景 / 近景）
        // ════════════════════════════════════════════════════════════

        private void BuildParallaxLayers()
        {
            _rootParallax = new GameObject("Decor_Parallax");
            _rootParallax.transform.SetParent(transform, false);

            // factor=0=完全静止，1=紧贴相机。Z 越深（越远），factor 越小。
            CreateQuadLayer("Sky",      _skyColor,         z: 22f, height: _mapHeight * 2.5f, width: _mapWidth * 1.5f,  parallaxFactor: 0.00f);
            CreateQuadLayer("FarMtn",   _farMountainColor, z: 14f, height: _farMountainHeight, width: _mapWidth * 1.2f, parallaxFactor: 0.20f);
            CreateQuadLayer("MidHill",  _midHillColor,     z: 8f,  height: _midHillHeight,     width: _mapWidth * 1.1f, parallaxFactor: 0.45f);
            CreateQuadLayer("NearDecor",_nearDecorColor,   z: 3f,  height: _nearDecorHeight,   width: _mapWidth * 1.05f, parallaxFactor: 0.70f);
        }

        /// <summary>建一块纯色 quad 视差层。Unity 自带 Quad primitive 朝 +Z，本场景里它贴在 XZ 平面上当剪影用。</summary>
        /// <param name="parallaxFactor">0=完全静止，1=紧贴相机。Z 越深的层 factor 越小。</param>
        private void CreateQuadLayer(string name, Color color, float z, float height, float width, float parallaxFactor)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(_rootParallax.transform, false);
            go.transform.position = new Vector3(0f, height * 0.5f - 2f, z);
            go.transform.localScale = new Vector3(width, height, 1f);
            // Quad 默认法线 +Z，相机在 z=-10 看向 +Z 看到的是背面（单面 quad 背面不可见）。
            // 绕 Y 转 180° 让法线指向 -Z，相机看到正面。
            go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            // 视差层不需要任何物理交互
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            // 用共享 URP/Lit 材质，但 emission 关、不接阴影
            var mr = go.GetComponent<MeshRenderer>();
            Cubic3DStyle.ApplyJobColor(mr, color);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            // 注册到视差列表，Update 里按 factor 跟随相机 X
            _layers.Add(new ParallaxLayer
            {
                t = go.transform,
                factor = parallaxFactor,
                baseX = go.transform.position.x,
            });
        }

        // ════════════════════════════════════════════════════════════
        //  地面装饰（程序化散布）
        // ════════════════════════════════════════════════════════════

        private void ScatterGroundDecor()
        {
            _rootDecor = new GameObject("Decor_Ground");
            _rootDecor.transform.SetParent(transform, false);

            var rng = new System.Random(_seed);
            float halfW = MapWidth * 0.5f;
            float groundTopY = GroundTopY;   // 统一走 GroundTopY，与 CubicMap 公式一致

            // 玩家出生点附近留空，避免一开局被石头挡路
            const float safeZone = 4f;

            // ─── 石头：低矮灰色方块 ───
            for (int i = 0; i < _rockCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth - safeZone * 2) - halfW + safeZone);
                float s = 0.3f + (float)rng.NextDouble() * 0.6f;
                var rock = Cubic3DStyle.CreateLowPolyCube("Rock", new Color(0.32f, 0.30f, 0.28f),
                    new Vector3(s, s * 0.6f, s));
                rock.transform.SetParent(_rootDecor.transform, false);
                rock.transform.position = new Vector3(x, groundTopY + s * 0.3f + _decorYOffset, 0f);
            }

            // ─── 蘑菇：红色菌盖 + 白色菌柄 ───
            for (int i = 0; i < _mushroomCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth - safeZone * 2) - halfW + safeZone);
                float stemH = 0.3f + (float)rng.NextDouble() * 0.3f;
                float capR  = 0.18f + (float)rng.NextDouble() * 0.15f;
                var stem = Cubic3DStyle.CreateLowPolyCube("Mush_Stem", new Color(0.92f, 0.88f, 0.80f),
                    new Vector3(0.1f, stemH, 0.1f));
                stem.transform.SetParent(_rootDecor.transform, false);
                stem.transform.position = new Vector3(x, groundTopY + stemH * 0.5f + _decorYOffset, 0f);
                var cap = Cubic3DStyle.CreateLowPolyCube("Mush_Cap",
                    RandomBool(rng, 0.85f) ? new Color(0.85f, 0.18f, 0.18f) : new Color(0.30f, 0.55f, 0.85f),
                    new Vector3(capR * 2f, capR * 0.7f, capR * 2f));
                cap.transform.SetParent(_rootDecor.transform, false);
                cap.transform.position = new Vector3(x, groundTopY + stemH + capR * 0.3f + _decorYOffset, 0f);
            }

            // ─── 草：细长绿色尖刺 ───
            for (int i = 0; i < _grassCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth - safeZone * 2) - halfW + safeZone);
                float h = 0.25f + (float)rng.NextDouble() * 0.4f;
                var blade = Cubic3DStyle.CreateLowPolyCube("GrassBlade",
                    new Color(0.20f + (float)rng.NextDouble() * 0.15f,
                              0.45f + (float)rng.NextDouble() * 0.20f,
                              0.18f),
                    new Vector3(0.05f, h, 0.05f));
                blade.transform.SetParent(_rootDecor.transform, false);
                blade.transform.position = new Vector3(x, groundTopY + h * 0.5f + _decorYOffset, 0f);
                blade.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 30f - 15f, (float)rng.NextDouble() * 15f - 7.5f);
            }

            // ─── 水晶簇：发光（cyan/magenta）的方尖碑 ───
            for (int i = 0; i < _crystalCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth - safeZone * 2) - halfW + safeZone);
                float h = 0.6f + (float)rng.NextDouble() * 0.5f;
                var crystal = Cubic3DStyle.CreateLowPolyCube("Crystal",
                    RandomBool(rng, 0.5f) ? _crystalColor1 : _crystalColor2,
                    new Vector3(0.18f, h, 0.18f), emissive: true);
                crystal.transform.SetParent(_rootDecor.transform, false);
                crystal.transform.position = new Vector3(x, groundTopY + h * 0.5f + _decorYOffset, 0f);
                crystal.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            }
        }

        // ─── 工具：coin flip ───
        private static bool RandomBool(System.Random rng, double pTrue)
            => rng.NextDouble() < pTrue;

        // ════════════════════════════════════════════════════════════
        //  氛围点光源（篝火 / 水晶辉光）
        // ════════════════════════════════════════════════════════════

        private void PlaceMoodLights()
        {
            _rootLights = new GameObject("Decor_Lights");
            _rootLights.transform.SetParent(transform, false);

            var rng = new System.Random(_seed + 7);
            float halfW = MapWidth * 0.5f;
            float groundTopY = GroundTopY;
            const float safeZone = 6f;

            // 篝火：暖橙点光源，挂在"火堆"位置
            for (int i = 0; i < _campfireCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth - safeZone * 2) - halfW + safeZone);
                CreatePointLight($"Campfire_{i}", _campfireColor, intensity: 2.4f, range: 8f,
                    position: new Vector3(x, groundTopY + 0.6f, 0f));
                // 火堆视觉：低矮黑色 + 上方黄色方块
                var pit = Cubic3DStyle.CreateLowPolyCube("CampPit", new Color(0.10f, 0.06f, 0.04f),
                    new Vector3(0.5f, 0.1f, 0.5f));
                pit.transform.SetParent(_rootLights.transform, false);
                pit.transform.position = new Vector3(x, groundTopY + 0.05f + _decorYOffset, 0f);
                var flame = Cubic3DStyle.CreateLowPolyCube("Flame", new Color(1.0f, 0.7f, 0.25f),
                    new Vector3(0.25f, 0.35f, 0.25f), emissive: true);
                flame.transform.SetParent(_rootLights.transform, false);
                flame.transform.position = new Vector3(x, groundTopY + 0.3f + _decorYOffset, 0f);
            }

            // 水晶辉光：冷色点光源，挂在水晶位置
            for (int i = 0; i < _crystalLightCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth - safeZone * 2) - halfW + safeZone);
                var color = RandomBool(rng, 0.5f) ? _crystalColor1 : _crystalColor2;
                CreatePointLight($"CrystalGlow_{i}", color, intensity: 1.5f, range: 5f,
                    position: new Vector3(x, groundTopY + 0.7f, 0f));
            }
        }

        private void CreatePointLight(string name, Color color, float intensity, float range, Vector3 position)
        {
            // 已存在同名光就跳过（避免 Reload Domain 后重复）
            var existing = GameObject.Find(name);
            if (existing != null) return;

            var go = new GameObject(name);
            go.transform.SetParent(_rootLights.transform, false);
            go.transform.position = position;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        // ════════════════════════════════════════════════════════════
        //  星空（月亮 + 散落星点）
        // ════════════════════════════════════════════════════════════

        private void BuildSkyDecor()
        {
            // 单独的容器，独立于 _rootParallax（不参与视差跟随，因为星空视觉上不动）
            var rootSky = new GameObject("Decor_Sky");
            rootSky.transform.SetParent(transform, false);

            var rng = new System.Random(_seed + 99);
            float halfW = MapWidth * 0.5f;

            // 视口参考：相机默认 ortho size=8,follow offset=3,玩家默认 Y=-7 → 相机 Y=-4 → 视口 Y∈[-12, 4]
            // 月亮和星星要落在视口内才有意义。FarMtn 在 Z=14,MidHill Z=8,NearDecor Z=3。
            // Z=4~6 把星空放在视差层前面,不会被山遮住,同时在 action 平面(Z=0)后方不会盖到玩家。
            float skyFrontZ = 5f;   // 星空放在视差层之前 (Z=4~6),不会被 Z=8/14/22 的视差层挡住

            // ─── 月亮：大尺寸发光方块，挂偏右上角 ───
            var moon = Cubic3DStyle.CreateLowPolyCube("Moon", _moonColor,
                new Vector3(_moonSize, _moonSize, 0.4f), emissive: true);
            moon.transform.SetParent(rootSky.transform, false);
            // Y=3 落在默认视口上半部 (视口顶 = 4); X 放偏右 (但仍在视口内,视口宽≈16)
            moon.transform.position = new Vector3(halfW * 0.6f, 3f, skyFrontZ);
            var moonMr = moon.GetComponent<MeshRenderer>();
            moonMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            moonMr.receiveShadows = false;

            // ─── 星星：小尺寸发光方块（用 _starCount 散布在天空） ───
            // Y 集中在视口上半 (-3 ~ 3),X 跨全宽(玩家横向移动时仍能看到),Z 4~6
            for (int i = 0; i < _starCount; i++)
            {
                float x = (float)(rng.NextDouble() * (MapWidth * 1.4f) - MapWidth * 0.7f);
                float y = (float)(rng.NextDouble() * 6.0 - 3.0);   // -3 ~ 3
                float z = skyFrontZ - 1f + (float)rng.NextDouble() * 2f;  // 4 ~ 6
                float s = 0.05f + (float)rng.NextDouble() * 0.12f;

                var star = Cubic3DStyle.CreateLowPolyCube("Star", _starColor,
                    new Vector3(s, s, s * 0.3f), emissive: true);
                star.transform.SetParent(rootSky.transform, false);
                star.transform.position = new Vector3(x, y, z);
                var mr = star.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }
}
