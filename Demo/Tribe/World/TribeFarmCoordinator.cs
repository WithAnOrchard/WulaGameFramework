using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;

namespace Demo.Tribe.World
{
    /// <summary>
    /// Tribe 农场协调器 —— 业务侧的"农场系统胶水"：
    /// <list type="number">
    ///   <item>启动时注册默认 <see cref="FarmConfig"/>（"基础农场"）+ 一份测试用 <see cref="CropConfig"/>（"小麦"）</item>
    ///   <item>监听 FarmService.<c>OnFarmSpawned</c> → 创建占位视觉 + 把世界边界往左推一个农场宽</item>
    ///   <item>开发期按 G 键：在当前左边界处生成一座基础农场（验证完整链路）</item>
    /// </list>
    /// 与框架解耦：所有跨模块调用走 bare-string 事件（§4.1）。
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeFarmCoordinator : MonoBehaviour
    {
        public static TribeFarmCoordinator Instance { get; private set; }

        /// <summary>默认 FarmConfig Id。</summary>
        public const string FARM_CONFIG_BASIC = "farm_basic";

        /// <summary>每座基础农场的视觉/边界推进宽度（世界单位）。</summary>
        public const float BASIC_FARM_WIDTH = 3f;

        /// <summary>默认 CropConfig Id —— 占位"小麦"，便于以后 M3 测试种植循环。</summary>
        public const string CROP_CONFIG_WHEAT = "crop_wheat";

        [Header("Debug")]
        [Tooltip("按下此键在当前左边界生成一座基础农场（验证 SpawnFarm 链路）。")]
        [SerializeField] private KeyCode _debugSpawnKey = KeyCode.G;

        [Tooltip("农场占位视觉颜色（土黄褐）。")]
        [SerializeField] private Color _farmBlockColor = new Color(0.55f, 0.40f, 0.20f, 1f);

        private Transform _farmRoot;
        private bool _registered;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _farmRoot = new GameObject("Farms").transform;
            _farmRoot.SetParent(transform, false);

            RegisterDefaults();
        }

        private void OnEnable()
        {
            if (EventProcessor.HasInstance && !_registered)
            {
                EventProcessor.Instance.AddListener("OnFarmSpawned", OnFarmSpawnedListener);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (_registered && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.RemoveListener("OnFarmSpawned", OnFarmSpawnedListener);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_debugSpawnKey)) DebugSpawnFarmAtLeftBoundary();
        }

        // ─── 单例获取 / 自动挂载 ─────────────────────────────
        public static TribeFarmCoordinator EnsureInstance(Transform parentRoot = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TribeFarmCoordinator");
            if (parentRoot != null) go.transform.SetParent(parentRoot, false);
            return go.AddComponent<TribeFarmCoordinator>();
        }

        // ─── 默认 Config 注册 ────────────────────────────────
        /// <summary>幂等：通过 FarmManager.EVT_REGISTER_FARM_CONFIG / CROP_CONFIG bare-string
        /// 事件灌入默认模板。重启 / 重进 Play 都会覆盖（保证代码改了立即生效）。</summary>
        private void RegisterDefaults()
        {
            if (!EventProcessor.HasInstance) return;

            var farmConfig = new FarmConfig
            {
                Id = FARM_CONFIG_BASIC,
                DisplayName = "基础农场",
                InitialRows = 2,
                InitialCols = 3,
                AllowedCropIds = new List<string> { CROP_CONFIG_WHEAT },
                BuildCosts = new List<BuildCost>(),         // M1 阶段免材料；M4 再接 Inventory
                Upgrades = new List<FarmUpgradeStep>(),
                InteriorSceneInstanceId = null,             // M2 接 SceneInstanceManager 后填
            };
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterFarmConfig", new List<object> { farmConfig });

            var cropConfig = new CropConfig
            {
                Id = CROP_CONFIG_WHEAT,
                DisplayName = "小麦",
                SeedItemId = "tribe_seed_wheat",     // M3 实施时接 Inventory
                OutputItemId = "tribe_food_wheat",   // 同上
                OutputAmount = 1,
                StageDurations = new List<float> { 8f, 12f, 16f },   // 总 36 秒长成
                StageSpriteIds = new List<string>(),                 // 占位 sprite 暂留空
            };
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterCropConfig", new List<object> { cropConfig });
        }

        // ─── 广播监听：扩边界 + 渲染占位 ────────────────────────
        // data: [string instanceId, FarmInstance instance]
        private List<object> OnFarmSpawnedListener(string evt, List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[1] is FarmInstance inst))
                return null;

            // 1) 边界扩展：每座农场把左边界往左推 BASIC_FARM_WIDTH
            //    （从 FarmConfig 读宽度更通用，但首版用常量简化）
            var boundary = TribeWorldBoundary.Instance;
            if (boundary != null) boundary.ExtendLeftBy(BASIC_FARM_WIDTH);

            // 2) 占位视觉：在 WorldPosition 放一个棕色矩形 + 标签
            BuildFarmVisual(inst);

            return null;
        }

        private void BuildFarmVisual(FarmInstance inst)
        {
            if (inst == null) return;
            var go = new GameObject($"Farm_{inst.InstanceId}");
            go.transform.SetParent(_farmRoot, false);
            go.transform.position = inst.WorldPosition;

            // 矩形 = 1×1 白像素 sprite 拉伸；做成"耕地"扁平地块，和地面平齐
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhitePixelSprite();
            sr.color = _farmBlockColor;
            sr.sortingOrder = 40;
            var height = 0.25f;
            go.transform.localScale = new Vector3(BASIC_FARM_WIDTH, height, 1f);
            // pivot 在中心；底边 = WorldPosition.y → 整体上抬 height/2，地块顶面 ~ 离地 0.25
            go.transform.position += new Vector3(0f, height * 0.5f, 0f);

            // 头顶标签（紧贴地块上方）
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localScale = new Vector3(1f / BASIC_FARM_WIDTH, 1f / height, 1f);
            labelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = $"🌾 农场 {inst.InstanceId}\n{inst.Rows}×{inst.Cols} 格";
            tm.characterSize = 0.08f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.85f, 0.55f);
            var mr = labelGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 50;
        }

        // ─── Debug：G 键测试 ─────────────────────────────────
        private void DebugSpawnFarmAtLeftBoundary()
        {
            if (!EventProcessor.HasInstance) return;
            var boundary = TribeWorldBoundary.Instance;
            if (boundary == null)
            {
                Debug.LogWarning("[TribeFarmCoordinator] 没有 TribeWorldBoundary 实例，无法定位农场位置");
                return;
            }

            // 把农场放在左边界正上方 —— SpawnFarm 后边界自动再往左推 BASIC_FARM_WIDTH，
            // 形成"农场紧贴旧边界、新边界在农场左侧"的连续扩展效果
            var farmCenterX = boundary.LeftLimitX - BASIC_FARM_WIDTH * 0.5f;
            var groundY = 0f;   // 与 TribeBiomeContext.GroundY 一致；TODO 后续接配置
            var pos = new Vector3(farmCenterX, groundY, 0f);

            var result = EventProcessor.Instance.TriggerEventMethod(
                "SpawnFarm", new List<object> { FARM_CONFIG_BASIC, pos });
            if (!ResultCode.IsOk(result))
                Debug.LogWarning($"[TribeFarmCoordinator] SpawnFarm 失败: {(result != null && result.Count > 1 ? result[1] : "?")}");
        }

        // 1×1 白像素 sprite 缓存
        private static Sprite _whitePixelSprite;
        private static Sprite GetWhitePixelSprite()
        {
            if (_whitePixelSprite != null) return _whitePixelSprite;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _whitePixelSprite;
        }
    }
}
