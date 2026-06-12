using System.Collections.Generic;
using EssSystem.Core.Application.MultiManagers.FarmManager.Dao;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.DataManager.RuntimeConfig;
using EssSystem.Core.Presentation.InputManager;
using UnityEngine;

namespace Demo.Tribe.World
{
    /// <summary>
    /// Tribe 农场业务协调器。Demo 侧负责读取 Tribe 默认配置、监听农场生成事件并创建场景可视物。
    /// 跨模块调用保持 bare-string 事件协议，避免 Demo 直接依赖 Manager 常量。
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeFarmCoordinator : MonoBehaviour
    {
        public static TribeFarmCoordinator Instance { get; private set; }

        public const string FARM_CONFIG_BASIC = "farm_basic";
        public const float BASIC_FARM_WIDTH = 3f;
        public const string CROP_CONFIG_WHEAT = "crop_wheat";

        private const string TRIBE_FARM_CONFIG_PATH = "Tribe/Farm/default_farm.json";

        [Header("Debug")]
        [Tooltip("按下此输入动作时，在当前左边界生成一座基础农场。")]
        [SerializeField] private string _debugSpawnAction = "DebugSpawn";

        [Tooltip("农场占位视觉颜色。")]
        [SerializeField] private Color _farmBlockColor = new Color(0.55f, 0.40f, 0.20f, 1f);

        private Transform _farmRoot;
        private bool _registered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

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
            var input = InputManager.TryGetInstance();
            if (input != null && input.IsDown(_debugSpawnAction)) DebugSpawnFarmAtLeftBoundary();
        }

        public static TribeFarmCoordinator EnsureInstance(Transform parentRoot = null)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("TribeFarmCoordinator");
            if (parentRoot != null) go.transform.SetParent(parentRoot, false);
            return go.AddComponent<TribeFarmCoordinator>();
        }

        private static void RegisterDefaults()
        {
            if (!EventProcessor.HasInstance) return;

            if (!RuntimeConfigLoader.TryLoadJson(
                    TRIBE_FARM_CONFIG_PATH,
                    out FarmDefaultConfigFile file,
                    msg => Debug.Log($"[TribeFarmCoordinator] {msg}")))
            {
                Debug.LogWarning($"[TribeFarmCoordinator] 未找到农场配置: {TRIBE_FARM_CONFIG_PATH}");
                return;
            }

            foreach (var farmConfig in file.FarmConfigs ?? new List<FarmConfig>())
            {
                if (farmConfig == null || string.IsNullOrEmpty(farmConfig.Id)) continue;
                EventProcessor.Instance.TriggerEventMethod(
                    "RegisterFarmConfig", new List<object> { farmConfig });
            }

            foreach (var cropConfig in file.CropConfigs ?? new List<CropConfig>())
            {
                if (cropConfig == null || string.IsNullOrEmpty(cropConfig.Id)) continue;
                EventProcessor.Instance.TriggerEventMethod(
                    "RegisterCropConfig", new List<object> { cropConfig });
            }
        }

        private List<object> OnFarmSpawnedListener(string evt, List<object> data)
        {
            if (data == null || data.Count < 2 || data[1] is not FarmInstance inst) return null;

            var boundary = TribeWorldBoundary.Instance;
            if (boundary != null) boundary.ExtendLeftBy(BASIC_FARM_WIDTH);

            BuildFarmVisual(inst);
            return null;
        }

        private void BuildFarmVisual(FarmInstance inst)
        {
            if (inst == null) return;

            var go = new GameObject($"Farm_{inst.InstanceId}");
            go.transform.SetParent(_farmRoot, false);
            go.transform.position = inst.WorldPosition;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GetWhitePixelSprite();
            sr.color = _farmBlockColor;
            sr.sortingOrder = 40;

            const float height = 0.25f;
            go.transform.localScale = new Vector3(BASIC_FARM_WIDTH, height, 1f);
            go.transform.position += new Vector3(0f, height * 0.5f, 0f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localScale = new Vector3(1f / BASIC_FARM_WIDTH, 1f / height, 1f);
            labelGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);

            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = $"农场 {inst.InstanceId}\n{inst.Rows}x{inst.Cols} 格";
            tm.characterSize = 0.08f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.85f, 0.55f);

            var mr = labelGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = 50;
        }

        private void DebugSpawnFarmAtLeftBoundary()
        {
            if (!EventProcessor.HasInstance) return;

            var boundary = TribeWorldBoundary.Instance;
            if (boundary == null)
            {
                Debug.LogWarning("[TribeFarmCoordinator] 没有 TribeWorldBoundary 实例，无法定位农场位置。");
                return;
            }

            var farmCenterX = boundary.LeftLimitX - BASIC_FARM_WIDTH * 0.5f;
            var pos = new Vector3(farmCenterX, 0f, 0f);

            var result = EventProcessor.Instance.TriggerEventMethod(
                "SpawnFarm", new List<object> { FARM_CONFIG_BASIC, pos });
            if (!ResultCode.IsOk(result))
            {
                var message = result != null && result.Count > 1 ? result[1] : "?";
                Debug.LogWarning($"[TribeFarmCoordinator] SpawnFarm 失败: {message}");
            }
        }

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
