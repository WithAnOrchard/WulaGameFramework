using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;             // GraphicsSettings
using UnityEngine.Rendering.Universal;   // Light2D（URP 2D 灯光）
using EssSystem.Core.Base.Event;

namespace Demo.Cubic.Map
{
    public class CubicMap : MonoBehaviour
    {
        [Header("地图尺寸")]
        [SerializeField] private float _mapWidth = 30f;
        [SerializeField] private float _mapHeight = 20f;

        [Header("地面设置")]
        [SerializeField] private Color _groundColor = new Color(0.32f, 0.24f, 0.18f, 1f);   // 暖陶土
        [SerializeField] private float _groundHeight = 2f;
        [SerializeField] private float _groundWidth = 1000f;

        [Header("背景设置")]
        [Tooltip("场景中那块背景板的颜色。默认接近全黑，便于观察 Sprite 的光照与边缘 halo。")]
        [SerializeField] private Color _backgroundColor = new Color(0.04f, 0.04f, 0.05f, 1f);
        [SerializeField] private float _parallaxDepth = 0.5f;

        [Header("摄像机")]
        [SerializeField] private float _cameraFollowSpeed = 5f;
        [SerializeField] private float _cameraOffsetY = 3f;

        [Header("Z 轴初始光照")]
        [SerializeField] private bool _enableLighting = true;

        [Header("URP 2D 灯光（需要 URP-Default 用 2D Renderer）")]
        [SerializeField] private bool  _enable2DLighting = true;
        [SerializeField] private Color _keyLight2DColor  = new Color(1.0f, 0.88f, 0.66f, 1f);  // 暖金
        [SerializeField] private float _keyLight2DIntensity = 1.6f;
        [SerializeField] private float _keyLight2DRadius  = 28f;
        [SerializeField] private Vector2 _keyLight2DOffset = new Vector2(0.25f, 0.35f);    // 地图相对位置

        [SerializeField] private Color _fillLight2DColor = new Color(0.45f, 0.55f, 0.85f, 1f); // 冷蓝
        [SerializeField] private float _fillLight2DIntensity = 0.9f;
        [SerializeField] private float _fillLight2DRadius = 24f;
        [SerializeField] private Vector2 _fillLight2DOffset = new Vector2(-0.3f, -0.3f);

        [SerializeField] private Color _ambientLight2DColor = new Color(0.35f, 0.28f, 0.32f, 1f); // 暖紫
        [SerializeField] private float _ambientLight2DIntensity = 1.0f;

        [SerializeField] private Color _keyLightColor  = new Color(1.0f, 0.88f, 0.66f, 1f);  // 暖金 key
        [SerializeField] private float _keyLightIntensity = 2.4f;
        [SerializeField] private float _keyLightZ = -50f;       // 从 +Z 远端指向 -Z
        [SerializeField] private Vector2 _keyLightAngle = new Vector2(0f, -30f);

        [SerializeField] private Color _fillLightColor = new Color(0.45f, 0.55f, 0.85f, 1f);  // 冷蓝 fill
        [SerializeField] private float _fillLightIntensity = 0.8f;
        [SerializeField] private float _fillLightZ = 50f;        // 从 -Z 远端补光
        [SerializeField] private Vector2 _fillLightAngle = new Vector2(0f, 150f);

        [SerializeField] private Color _ambientColor   = new Color(0.35f, 0.28f, 0.32f, 1f);  // 暖紫环境
        [SerializeField] private float _ambientIntensity = 1.2f;

        [Header("Bloom 朦胧遮罩")]
        [SerializeField] private bool  _enableBloom = true;
        [SerializeField] private float _bloomIntensity = 0.55f;
        [SerializeField] private float _bloomThreshold = 0.55f;

        [Header("Sprite 光晕")]
        [Tooltip("在主 Sprite 后挂一个稍大的加色 Sprite，做出软遮罩感")]
        [SerializeField] private bool  _enableSpriteHalo = true;
        [SerializeField] private float _haloScale = 1.08f;
        [SerializeField] private float _haloAlpha = 0.18f;

        private Camera _mainCamera;
        private GameObject _ground;
        private GameObject _background;
        private Transform _player;
        private Light _keyLight;
        private Light _fillLight;
        private GameObject _keyLightObj;
        private GameObject _fillLightObj;
        private Light2D _keyLight2D;
        private Light2D _fillLight2D;
        private Light2D _ambientLight2D;
        private GameObject _keyLight2DObj;
        private GameObject _fillLight2DObj;
        private GameObject _ambientLight2DObj;

        private void Awake()
        {
            CreateMap();
            SetupCamera();
            SetupAmbient();
            if (_enableLighting) CreateLighting();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
            UpdateCamera();
        }

        private void CreateMap()
        {
            CreateGround();
            CreateBackground();
        }

        private void CreateGround()
        {
            _ground = new GameObject("Ground");
            _ground.transform.SetParent(transform);

            var renderer = _ground.AddComponent<SpriteRenderer>();
            renderer.color = _groundColor;
            renderer.sortingOrder = 0;
            ApplyLitSpriteMaterial(renderer);   // 让 Sprite 接收 LightManager 的灯光
            AttachHaloGlow(_ground, _groundColor);

            var sprite = CreateSolidSprite(_groundColor);
            renderer.sprite = sprite;

            float groundY = -_mapHeight / 2 + _groundHeight / 2f;
            _ground.transform.position = new Vector3(0, groundY, 0);
            _ground.transform.localScale = new Vector3(_groundWidth, _groundHeight, 1);

            var collider = _ground.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.offset = Vector2.zero;
            collider.isTrigger = false;

            _ground.AddComponent<GroundIdentifier>();

            Debug.Log($"[CubicMap] 地面已创建: Y={groundY}, 宽度={_groundWidth}, 厚度={_groundHeight}, 碰撞体已添加");
        }

        private void CreateBackground()
        {
            _background = new GameObject("Background");
            _background.transform.SetParent(transform);

            var renderer = _background.AddComponent<SpriteRenderer>();
            renderer.color = _backgroundColor;
            renderer.sortingOrder = -1;
            ApplyLitSpriteMaterial(renderer);   // 让 Sprite 接收 LightManager 的灯光
            AttachHaloGlow(_background, _backgroundColor);

            var sprite = CreateSolidSprite(_backgroundColor);
            renderer.sprite = sprite;

            _background.transform.position = new Vector3(0, 0, 1);
            _background.transform.localScale = new Vector3(_groundWidth, _mapHeight * 2, 1);

            Debug.Log("[CubicMap] 背景已创建");
        }

        /// <summary>
        /// 给主 Sprite 挂一个稍大、加色、alpha 较低的子 Sprite，做出"软遮罩/光晕"感。
        /// 这是 2D 像素风做"halo"最稳的技巧，不依赖 Bloom。
        /// </summary>
        private void AttachHaloGlow(GameObject owner, Color baseColor, bool skipHalo = false)
        {
            if (!_enableSpriteHalo || skipHalo) return;
            var halo = new GameObject("HaloGlow");
            halo.transform.SetParent(owner.transform, false);
            halo.transform.localPosition = Vector3.zero;
            halo.transform.localScale    = Vector3.one * _haloScale;
            halo.transform.localRotation = Quaternion.identity;

            var haloR = halo.AddComponent<SpriteRenderer>();
            haloR.sprite         = owner.GetComponent<SpriteRenderer>().sprite;
            haloR.color          = new Color(baseColor.r * 1.15f, baseColor.g * 1.15f, baseColor.b * 1.15f, _haloAlpha);
            haloR.sortingOrder   = owner.GetComponent<SpriteRenderer>().sortingOrder - 1;
            // 加色：URP 没有 Additive BlendMode，但可通过 Unlit 透明材质 + 颜色提亮近似。
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlit == null) unlit = Shader.Find("Unlit/Transparent");
            if (unlit != null) haloR.sharedMaterial = new Material(unlit);
        }

        /// <summary>
        /// 把 SpriteRenderer 切到合适的 Sprite 材质。
        ///
        /// 根据 URP 渲染管线模式自动选择：
        ///   URP 2D Renderer（Renderer2DData）→ URP/2D/Sprite-Lit-Default（支持 Light2D）
        ///   URP 3D Forward / Universal      → Cubic/SpriteLit（Z 轴光照 + 边缘 rim）
        ///
        /// Cubic/SpriteLit 自带 Z 轴光照 + 边缘 halo，但只对 URP 3D Forward 的 UniversalForward pass 生效。
        /// 在 URP 2D Renderer 下要看到 Light2D 真实照明，必须用 URP 自己的 2D sprite shader。
        ///
        /// 显式设一组克制默认参数（不要让 _RimStrength=1.4 / _ZTopBrightness=1.0 等
        /// "默认就过曝"的旧值污染 Cubic demo 整体效果）。
        /// </summary>
        private static void ApplyLitSpriteMaterial(SpriteRenderer r)
        {
            Shader sh = null;
            bool is2D = IsURP2DMode();

            if (is2D)
            {
                // 2D 模式：用 URP 自带 sprite shader（才接得住 Light2D）
                sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            }

            // 3D 模式 / 2D shader 找不到：回退到自定义 Cubic/SpriteLit
            if (sh == null) sh = Shader.Find("Cubic/SpriteLit");

            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Sprites/Diffuse");
            if (sh == null) return;

            var mat = new Material(sh);
            // 显式设默认参数（只对 Cubic/SpriteLit 有意义；URP 自带 shader 自动忽略不存在的属性）
            if (mat.HasProperty("_RimPower"))          mat.SetFloat("_RimPower", 3.0f);
            if (mat.HasProperty("_RimStrength"))       mat.SetFloat("_RimStrength", 0.4f);
            if (mat.HasProperty("_ZTopBrightness"))    mat.SetFloat("_ZTopBrightness", 1.15f);
            if (mat.HasProperty("_ZBottomBrightness")) mat.SetFloat("_ZBottomBrightness", 0.9f);
            if (mat.HasProperty("_ZLightIntensity"))   mat.SetFloat("_ZLightIntensity", 0.3f);

            r.sharedMaterial = mat;
        }

        /// <summary>
        /// 检测当前 GraphicsSettings.defaultRenderPipeline 的 m_RendererDataList[0] 是不是 Renderer2DData。
        /// 用反射避免直接引用 UnityEngine.Rendering.Universal.Renderer2DData 类型。
        /// </summary>
        private static bool IsURP2DMode()
        {
            var rp = GraphicsSettings.defaultRenderPipeline;
            if (rp == null) return false;

            var listField = rp.GetType().GetField(
                "m_RendererDataList",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField == null) return false;

            var list = listField.GetValue(rp) as System.Collections.IList;
            if (list == null || list.Count == 0) return false;

            var first = list[0] as UnityEngine.Object;
            return first != null && first.GetType().Name == "Renderer2DData";
        }

        private Sprite CreateSolidSprite(Color color)
        {
            int texWidth = 64;
            int texHeight = 64;
            var texture = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            var colors = new Color[texWidth * texHeight];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = color;
            }
            texture.SetPixels(colors);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, texWidth, texHeight),
                new Vector2(0.5f, 0.5f),
                texWidth
            );
        }

        private void SetupCamera()
        {
            _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                var camObj = new GameObject("Main Camera");
                camObj.transform.position = new Vector3(0, 0, -10);
                _mainCamera = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            _mainCamera.orthographic = true;
            _mainCamera.orthographicSize = _mapHeight / 2;
            _mainCamera.backgroundColor = _backgroundColor;
            _mainCamera.transform.position = new Vector3(0, 0, -10);

            Debug.Log("[CubicMap] 摄像机已设置");
        }

        private void UpdateCamera()
        {
            if (_player == null || _mainCamera == null) return;

            Vector3 targetPos = new Vector3(
                _player.position.x,
                _player.position.y + _cameraOffsetY,
                -10
            );

            _mainCamera.transform.position = Vector3.Lerp(
                _mainCamera.transform.position,
                targetPos,
                _cameraFollowSpeed * Time.deltaTime
            );

            float clampedX = Mathf.Clamp(
                _mainCamera.transform.position.x,
                -_groundWidth / 2 + _mainCamera.orthographicSize * _mainCamera.aspect,
                 _groundWidth / 2 - _mainCamera.orthographicSize * _mainCamera.aspect
            );

            _mainCamera.transform.position = new Vector3(clampedX, _mainCamera.transform.position.y, -10);
        }

        public Bounds GetMapBounds()
        {
            return new Bounds(Vector3.zero, new Vector3(_groundWidth, _mapHeight, 0));
        }

        public float GetGroundY()
        {
            return -_mapHeight / 2 + _groundHeight / 2f;
        }

        private void OnDestroy()
        {
            // 不再向 LightManager 发 UnregisterLight：
            // 1) LightManager 不在场景时 EventProcessor 会 NRE（被它内部 catch 后 LogError 仍污染 console）
            // 2) Cubic/SpriteLit 是自定义 shader，3D Light 不参与实际渲染
            // 灯随 GameObject 一起销毁，LightManager 字典里最多留个 null id，无影响
        }

        /// <summary>
        /// 设 RenderSettings 环境光（不依赖 LightManager，Awake 阶段 LightManager 可能还没就绪）。
        /// </summary>
        private void SetupAmbient()
        {
            RenderSettings.ambientLight     = _ambientColor;
            RenderSettings.ambientIntensity = _ambientIntensity;
            RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor      = _ambientColor;
            RenderSettings.ambientEquatorColor  = _ambientColor * 0.85f;
            RenderSettings.ambientGroundColor   = _ambientColor * 0.55f;
        }

        private void CreateLighting()
        {
            if (_enable2DLighting) Create2DLighting();
            Create3DLighting();
        }

        /// <summary>
        /// URP 2D 灯光：3 个 Light2D（Key / Fill / Ambient Global）。
        /// 需 URP-Default 用 Renderer2DData（URP 2D Renderer），否则这些 Light2D 不生效。
        /// </summary>
        private void Create2DLighting()
        {
            // Key：暖金，右上角
            _keyLight2DObj = new GameObject("KeyLight2D");
            _keyLight2DObj.transform.SetParent(transform);
            _keyLight2DObj.transform.position = new Vector3(
                _keyLight2DOffset.x * _mapWidth,
                _keyLight2DOffset.y * _mapHeight,
                0
            );
            _keyLight2D = _keyLight2DObj.AddComponent<Light2D>();
            _keyLight2D.lightType            = Light2D.LightType.Point;
            _keyLight2D.color                = _keyLight2DColor;
            _keyLight2D.intensity            = _keyLight2DIntensity;
            _keyLight2D.pointLightOuterRadius = _keyLight2DRadius;
            _keyLight2D.pointLightInnerRadius = _keyLight2DRadius * 0.25f;
            _keyLight2D.shadowIntensity      = 0.6f;        // 让 sprite 之间有遮挡感

            // Fill：冷蓝，左下角
            _fillLight2DObj = new GameObject("FillLight2D");
            _fillLight2DObj.transform.SetParent(transform);
            _fillLight2DObj.transform.position = new Vector3(
                _fillLight2DOffset.x * _mapWidth,
                _fillLight2DOffset.y * _mapHeight,
                0
            );
            _fillLight2D = _fillLight2DObj.AddComponent<Light2D>();
            _fillLight2D.lightType            = Light2D.LightType.Point;
            _fillLight2D.color                = _fillLight2DColor;
            _fillLight2D.intensity            = _fillLight2DIntensity;
            _fillLight2D.pointLightOuterRadius = _fillLight2DRadius;
            _fillLight2D.pointLightInnerRadius = _fillLight2DRadius * 0.3f;

            // Ambient：Global，暖紫，给整个场景打底
            _ambientLight2DObj = new GameObject("AmbientLight2D");
            _ambientLight2DObj.transform.SetParent(transform);
            _ambientLight2D = _ambientLight2DObj.AddComponent<Light2D>();
            _ambientLight2D.lightType = Light2D.LightType.Global;
            _ambientLight2D.color     = _ambientLight2DColor;
            _ambientLight2D.intensity = _ambientLight2DIntensity;

            Debug.Log("[CubicMap] URP 2D 灯光已建立: Key(右上暖金) / Fill(左下冷蓝) / Ambient(暖紫 Global)");
        }

        /// <summary>
        /// 3D Directional Lights（URP 3D Forward 时有用；2D Renderer 下不影响 sprite 但不报错）。
        /// </summary>
        private void Create3DLighting()
        {
            if (!_enableLighting) return;
            // Key：暖金，从 +Z 远端照向 -Z（场景内），形成"Z 轴打光"的方向感
            _keyLightObj = new GameObject("KeyLight_ZAxis");
            _keyLightObj.transform.SetParent(transform);
            _keyLightObj.transform.position = new Vector3(0, 0, _keyLightZ);
            _keyLightObj.transform.rotation = Quaternion.Euler(_keyLightAngle.x, _keyLightAngle.y, 0f);
            _keyLight = _keyLightObj.AddComponent<Light>();
            _keyLight.type      = LightType.Directional;
            _keyLight.color     = _keyLightColor;
            _keyLight.intensity = _keyLightIntensity;
            _keyLight.shadows   = LightShadows.Soft;

            // Fill：冷蓝，从 -Z 远端补光，避免 Sprite 背面纯黑
            _fillLightObj = new GameObject("FillLight_NegZ");
            _fillLightObj.transform.SetParent(transform);
            _fillLightObj.transform.position = new Vector3(0, 0, _fillLightZ);
            _fillLightObj.transform.rotation = Quaternion.Euler(_fillLightAngle.x, _fillLightAngle.y, 0f);
            _fillLight = _fillLightObj.AddComponent<Light>();
            _fillLight.type      = LightType.Directional;
            _fillLight.color     = _fillLightColor;
            _fillLight.intensity = _fillLightIntensity;
            _fillLight.shadows   = LightShadows.None;

            // 注：不再 FireRegister 到 LightManager。EventProcessor 反射调用 OnRegisterLight 时
            //     若 LightManager.Instance 为 null 会 NRE，被框架内部 catch 后 LogError，污染 console。
            //     且 Cubic/SpriteLit 是自定义 shader，3D Light 本身不参与实际渲染，事件注册没价值。
            Debug.Log($"[CubicMap] Z 轴 3D 方向光已建立: key Z={_keyLightZ} 强度={_keyLightIntensity} / fill Z={_fillLightZ} 强度={_fillLightIntensity}");
        }
    }
}
