using UnityEngine;
using EssSystem.Core.Presentation.CameraManager;
using Demo.Cubic.Utils;

namespace Demo.Cubic.Map
{
    /// <summary>
    /// Cubic 地图与场景编排（3D 伪 2D 版）。
    /// <para>
    /// <b>3D 化改造点</b>（相对 2D 版）：
    /// <list type="bullet">
    /// <item>地面/背景：<c>SpriteRenderer</c> → <c>MeshRenderer</c>（低多边形 Cube + URP/Lit 共享材质）</item>
    /// <item>光照：<c>Light2D</c>（URP 2D Renderer）→ <c>Light</c>（Directional 主光 + Fill 补光，URP 3D 通用）</item>
    /// <item>相机：<see cref="Camera.orthographic"/> 仍为 true（侧视伪 2D），但 3D 空间里摆位（0, 0, -10）朝 +Z 看</item>
    /// <item>Bloom 改用 URP Volume 的 Bloom override（场景里挂一个 Global Volume 即可）</item>
    /// </list>
    /// </para>
    /// <para>
    /// 后续美化阶段会加：远景多层 Quad 视差、雾效、装饰物、天空盒、Volume 后期。
    /// </para>
    /// </summary>
    public class CubicMap : MonoBehaviour
    {
        [Header("地图尺寸")]
        [SerializeField] private float _mapWidth = 30f;
        [SerializeField] private float _mapHeight = 20f;

        [Header("地面设置")]
        [SerializeField] private Color _groundColor = new Color(0.32f, 0.24f, 0.18f, 1f);
        [SerializeField] private float _groundHeight = 2f;
        [SerializeField] private float _groundWidth = 1000f;

        [Header("背景")]
        [SerializeField] private Color _skyColor = new Color(0.18f, 0.22f, 0.32f, 1f);
        [SerializeField] private Color _backgroundColor = new Color(0.12f, 0.14f, 0.18f, 1f);

        [Header("摄像机")]
        [SerializeField] private float _cameraZ = -10f;
        [SerializeField] private float _cameraOrthoSize = 8f;
        [Tooltip("2.5D 倾斜俯角（绕 X 轴 pitch down）。0 = 正侧视（旧观感），25~35 = 经典 iso/dead-cells 横版斜视")]
        [SerializeField] private float _cameraTiltAngle = 28f;

        [Header("主光照 (Directional Light)")]
        [SerializeField] private bool _enableLighting = true;
        [SerializeField] private Color _keyLightColor  = new Color(1.0f, 0.88f, 0.66f, 1f);
        [SerializeField] private float _keyLightIntensity = 2.0f;
        [SerializeField] private Vector3 _keyLightEuler = new Vector3(50f, -30f, 0f);

        [SerializeField] private Color _fillLightColor = new Color(0.45f, 0.55f, 0.85f, 1f);
        [SerializeField] private float _fillLightIntensity = 0.6f;
        [SerializeField] private Vector3 _fillLightEuler = new Vector3(30f, 150f, 0f);

        [SerializeField] private Color _ambientColor = new Color(0.45f, 0.42f, 0.48f, 1f);
        [SerializeField] private float _ambientIntensity = 1.0f;

        private GameObject _ground;
        private GameObject _background;
        private Transform _player;
        private Light _keyLight;
        private Light _fillLight;

        private void Awake()
        {
            // 注：旧的 "CreateBackground" 大背景 cube 已删除 —— CubicSceneDecor.BuildParallaxLayers
            //     提供了 4 层更精细的视差背景，原 cube 位置在 Z=15 会盖住 Sky 层和星空，删掉。
            CreateGround();
            SetupCamera();
            SetupLighting();

            // 自动挂上场景美化（多层视差 + 雾 + 装饰 + 氛围光）。已存在则不重复。
            if (GetComponent<CubicSceneDecor>() == null) gameObject.AddComponent<CubicSceneDecor>();
            // 自动挂上 URP 后期处理（Bloom 让篝火/水晶发光,Vignette 暗角,ColorAdjustments 调色温）。已存在则不重复。
            if (GetComponent<CubicPostProcessing>() == null) gameObject.AddComponent<CubicPostProcessing>();
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;

            // 找到玩家后立即挂到 CameraManager 做跟随 + 设正交 size
            if (_player != null && CameraManager.HasInstance)
            {
                // 倾斜视角的 follow offset：相机在玩家"上方+后方"，跟 SetupCamera 用同一组公式
                float rad = _cameraTiltAngle * Mathf.Deg2Rad;
                float lookDist = Mathf.Abs(_cameraZ);
                var offset = new Vector3(0f, Mathf.Sin(rad) * lookDist, _cameraZ * Mathf.Cos(rad));
                CameraManager.Instance.FollowTarget(_player, offset);
                CameraManager.Instance.SetZoom(_cameraOrthoSize);
            }
        }

        private void Update()
        {
            // 相机跟随由 CameraManager.LateUpdate 处理,本类不再手写 lerp
        }

        // ════════════════════════════════════════════════════════════
        //  地图：地面 / 背景
        // ════════════════════════════════════════════════════════════

        private void CreateGround()
        {
            _ground = Cubic3DStyle.CreateLowPolyCube(
                "Ground",
                _groundColor,
                new Vector3(_groundWidth, _groundHeight, 4f)
            );
            _ground.transform.SetParent(transform);

            // 地面中心 Y 坐标 = 地图底 + 厚度一半
            float groundCenterY = -_mapHeight / 2f + _groundHeight / 2f;
            _ground.transform.position = new Vector3(0f, groundCenterY, 0f);

            var collider = _ground.AddComponent<BoxCollider>();
            collider.size = Vector3.one;
            collider.center = Vector3.zero;
            collider.isTrigger = false;

            _ground.AddComponent<GroundIdentifier>();

            Debug.Log($"[CubicMap] 地面已创建: centerY={groundCenterY}, 宽度={_groundWidth}, 厚度={_groundHeight}, 3D BoxCollider 已添加");
        }

        private void CreateBackground()
        {
            // 一块大 cube 放远端做"远山"占位（后续美化阶段会换成多 Quad 视差或 skybox）
            _background = Cubic3DStyle.CreateLowPolyCube(
                "Background",
                _backgroundColor,
                new Vector3(_groundWidth * 1.2f, _mapHeight * 2.5f, 1f)
            );
            _background.transform.SetParent(transform);
            _background.transform.position = new Vector3(0f, _mapHeight * 0.5f, 15f);
            // 关闭背景 cube 的投影 / 接收阴影，避免与近景立方体互相投射
            var br = _background.GetComponent<MeshRenderer>();
            br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            br.receiveShadows = false;

            // 相机的 clearFlags 设为 SolidColor 即可填 skyColor
        }

        // ════════════════════════════════════════════════════════════
        //  相机：3D 正交侧视 + 跟随
        // ════════════════════════════════════════════════════════════

        private void SetupCamera()
        {
            // 相机创建/挂载由场景或 CameraManager._mainCamera 注入负责 —— CubicMap 不再 new GameObject("Main Camera")。
            // 这里只做"从 CameraManager 拿引用 + 配正交投影 + 背景色"的工作。
            if (!CameraManager.HasInstance)
            {
                Debug.LogError("[CubicMap] CameraManager 未初始化（场景里没管理器？），相机配置跳过");
                return;
            }

            var cam = CameraManager.Instance.GetMainCamera();
            if (cam == null)
            {
                Debug.LogWarning("[CubicMap] 未找到主相机 —— 请在 Hierarchy 加一个 Camera 并 tag 为 MainCamera，" +
                                 "或拖到 CameraManager._mainCamera 字段上");
                return;
            }

            // 配置投影 / 背景色（ortho + SolidColor 跟旧的 2D 横版观感一致）
            cam.orthographic = true;
            cam.orthographicSize = _cameraOrthoSize;
            cam.backgroundColor = _skyColor;
            cam.clearFlags = CameraClearFlags.SolidColor;

            // 倾斜 2.5D 视角：绕 X 轴 pitch down，仰角决定能看到多少"顶面"
            // 初始位置按 tilt 算出（与 Start 里的 follow offset 一致），避免第一帧相机在原点
            float rad = _cameraTiltAngle * Mathf.Deg2Rad;
            float lookDist = Mathf.Abs(_cameraZ);
            var initialPos = new Vector3(0f, Mathf.Sin(rad) * lookDist, _cameraZ * Mathf.Cos(rad));
            cam.transform.SetPositionAndRotation(initialPos, Quaternion.Euler(_cameraTiltAngle, 0f, 0f));

            Debug.Log($"[CubicMap] 3D 倾斜 2.5D 相机已配置: tilt={_cameraTiltAngle}°, 初始 pos={initialPos}（相机由 CameraManager 管理,跟随在 Start 挂上）");
        }

        private void UpdateCamera()  // 保留空方法以兼容旧调用方,实际跟随由 CameraManager.LateUpdate 接管
        {
        }

        // ════════════════════════════════════════════════════════════
        //  灯光：Directional Key + Fill
        // ════════════════════════════════════════════════════════════

        private void SetupLighting()
        {
            if (!_enableLighting) return;

            // 环境光（RenderSettings 影响所有 URP/Lit 材质）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = _ambientColor * _ambientIntensity;

            _keyLight = CreateDirectionalLight("KeyLight", _keyLightColor, _keyLightIntensity, _keyLightEuler);
            _fillLight = CreateDirectionalLight("FillLight", _fillLightColor, _fillLightIntensity, _fillLightEuler);
        }

        private Light CreateDirectionalLight(string name, Color color, float intensity, Vector3 euler)
        {
            // 已有同名光就不重复建（避免 Reload Domain 后场景里多份）
            var existing = GameObject.Find(name);
            if (existing != null) return existing.GetComponent<Light>();

            var go = new GameObject(name);
            go.transform.SetParent(transform);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.Soft;
            return l;
        }

        // ════════════════════════════════════════════════════════════
        //  对外接口
        // ════════════════════════════════════════════════════════════

        /// <summary>地面顶面 Y（玩家脚底高度），用于 PlayerController / EnemySpawner 计算生成点。</summary>
        public float GetGroundY() => -_mapHeight / 2f + _groundHeight;

        public Camera GetMainCamera() => CameraManager.HasInstance ? CameraManager.Instance.GetMainCamera() : null;

        // ─── 权威尺寸属性（CubicSceneDecor 等外部装饰 / 物理 / 相机都从这里取，避免和面板值不一致）───
        public float MapWidth    => _mapWidth;
        public float MapHeight   => _mapHeight;
        public float GroundHeight => _groundHeight;
    }
}
