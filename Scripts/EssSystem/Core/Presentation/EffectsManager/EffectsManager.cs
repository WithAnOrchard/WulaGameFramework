using System.Collections;
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.UIManager;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.EffectsManager
{
    /// <summary>
    /// 视觉特效管理器：负责 VFX prefab 池化播放，以及屏幕闪光叠加。
    /// VFX prefab 通过 ResourceManager 事件加载，屏幕闪光 Canvas 通过 UIManager 的 OverlayCanvasProvider 创建。
    /// </summary>
    [Manager(6)]
    public class EffectsManager : Manager<EffectsManager>
    {
        public const string EVT_REGISTER_VFX = "RegisterVFX";
        public const string EVT_UNREGISTER_VFX = "UnregisterVFX";
        public const string EVT_PLAY_VFX = "PlayVFX";
        public const string EVT_STOP_VFX = "StopVFX";
        public const string EVT_STOP_ALL_VFX = "StopAllVFX";
        public const string EVT_SCREEN_FLASH = "PlayScreenFlash";

        [Header("VFX Pool")]
        [Tooltip("启用对象池：相同 vfxId 复用实例，避免反复 Instantiate / Destroy")]
        [SerializeField] private bool _enablePool = true;

        [Tooltip("每个 vfxId 的池化上限，超过后直接销毁")]
        [SerializeField, Min(1)] private int _maxPoolSizePerKey = 8;

        [Header("Screen Flash")]
        [Tooltip("屏幕闪光 Canvas 的 sortingOrder，越大越靠上")]
        [SerializeField] private int _flashCanvasSortingOrder = 32000;

        public EffectsService Service => EffectsService.Instance;

        private readonly Dictionary<string, GameObject> _vfxPrefabs = new();
        private readonly Dictionary<string, Stack<GameObject>> _pool = new();
        private readonly Dictionary<string, ActiveVfx> _active = new();

        private Canvas _flashCanvas;
        private Image _flashImage;
        private Coroutine _flashCoroutine;
        private int _instanceSeq;

        private struct ActiveVfx
        {
            public string VfxId;
            public GameObject Go;
            public Coroutine AutoDestroyCoroutine;
        }

        protected override void Initialize()
        {
            base.Initialize();
            LoadRegistrationsFromService();
            Log($"EffectsManager 初始化完成（{_vfxPrefabs.Count} 个 VFX 注册）", Color.green);
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        private void LoadRegistrationsFromService()
        {
            if (Service == null) return;

            foreach (var (id, path) in Service.GetAllRegistrations())
            {
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(path)) continue;
                _vfxPrefabs[id] = null;
            }
        }

        public void RegisterVFX(string vfxId, string prefabPath)
        {
            if (string.IsNullOrEmpty(vfxId) || string.IsNullOrEmpty(prefabPath)) return;
            _vfxPrefabs[vfxId] = null;
            Service?.SetRegistration(vfxId, prefabPath);
        }

        public void UnregisterVFX(string vfxId)
        {
            if (string.IsNullOrEmpty(vfxId)) return;
            _vfxPrefabs.Remove(vfxId);
            Service?.RemoveRegistration(vfxId);
        }

        public string PlayVFX(string vfxId, Vector3 worldPos, Quaternion? rotation = null, float autoDestroy = 0f)
        {
            if (string.IsNullOrEmpty(vfxId)) return null;

            var prefab = ResolvePrefab(vfxId);
            if (prefab == null)
            {
                LogWarning($"PlayVFX 失败，未找到 prefab: {vfxId}");
                return null;
            }

            var go = TakeFromPoolOrInstantiate(vfxId, prefab);
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(worldPos, rotation ?? Quaternion.identity);
            go.SetActive(true);

            var instanceId = $"{vfxId}#{++_instanceSeq}";
            var info = new ActiveVfx { VfxId = vfxId, Go = go };
            if (autoDestroy > 0f)
                info.AutoDestroyCoroutine = StartCoroutine(AutoStopAfter(instanceId, autoDestroy));

            _active[instanceId] = info;
            return instanceId;
        }

        public bool StopVFX(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId)) return false;
            if (!_active.TryGetValue(instanceId, out var info)) return false;

            if (info.AutoDestroyCoroutine != null) StopCoroutine(info.AutoDestroyCoroutine);
            ReturnToPoolOrDestroy(info.VfxId, info.Go);
            _active.Remove(instanceId);
            return true;
        }

        public void StopAllVFX()
        {
            var ids = new List<string>(_active.Keys);
            foreach (var id in ids) StopVFX(id);
        }

        public void PlayScreenFlash(Color color, float duration = 0.15f)
        {
            EnsureFlashCanvas();
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine(color, Mathf.Max(0.01f, duration)));
        }

        private GameObject ResolvePrefab(string vfxId)
        {
            if (_vfxPrefabs.TryGetValue(vfxId, out var prefab) && prefab != null) return prefab;

            var path = Service?.GetRegistration(vfxId);
            if (string.IsNullOrEmpty(path) || !EventProcessor.HasInstance) return null;

            var result = EventProcessor.Instance.TriggerEventMethod("GetPrefabAsync", new List<object> { path });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject go) return null;

            _vfxPrefabs[vfxId] = go;
            return go;
        }

        private GameObject TakeFromPoolOrInstantiate(string vfxId, GameObject prefab)
        {
            if (_enablePool && _pool.TryGetValue(vfxId, out var stack) && stack.Count > 0)
            {
                var pooled = stack.Pop();
                if (pooled != null) return pooled;
            }

            return Instantiate(prefab);
        }

        private void ReturnToPoolOrDestroy(string vfxId, GameObject go)
        {
            if (go == null) return;
            if (!_enablePool)
            {
                Destroy(go);
                return;
            }

            if (!_pool.TryGetValue(vfxId, out var stack))
            {
                stack = new Stack<GameObject>();
                _pool[vfxId] = stack;
            }

            if (stack.Count >= _maxPoolSizePerKey)
            {
                Destroy(go);
                return;
            }

            go.SetActive(false);
            go.transform.SetParent(transform, false);
            stack.Push(go);
        }

        private IEnumerator AutoStopAfter(string instanceId, float duration)
        {
            yield return new WaitForSeconds(duration);
            StopVFX(instanceId);
        }

        private void EnsureFlashCanvas()
        {
            if (_flashCanvas != null && _flashImage != null) return;

            var canvasTransform = OverlayCanvasProvider.GetOrCreate(
                "EffectsFlashCanvas",
                _flashCanvasSortingOrder,
                pixelPerfect: false);
            _flashCanvas = canvasTransform.GetComponent<Canvas>();
            if (_flashCanvas != null) _flashCanvas.sortingOrder = _flashCanvasSortingOrder;

            var imageTransform = canvasTransform.Find("FlashImage");
            if (imageTransform == null)
            {
                var imageGo = new GameObject("FlashImage");
                imageGo.transform.SetParent(canvasTransform, false);
                imageTransform = imageGo.transform;
            }

            _flashImage = imageTransform.GetComponent<Image>();
            if (_flashImage == null) _flashImage = imageTransform.gameObject.AddComponent<Image>();

            var rt = _flashImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            _flashImage.raycastTarget = false;
            _flashImage.color = new Color(0, 0, 0, 0);
        }

        private IEnumerator FlashRoutine(Color color, float duration)
        {
            var peakT = duration * 0.3f;
            var fadeT = duration - peakT;

            var timer = 0f;
            while (timer < peakT)
            {
                timer += Time.unscaledDeltaTime;
                var a = Mathf.Lerp(0f, color.a, timer / peakT);
                _flashImage.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }

            timer = 0f;
            while (timer < fadeT)
            {
                timer += Time.unscaledDeltaTime;
                var a = Mathf.Lerp(color.a, 0f, timer / fadeT);
                _flashImage.color = new Color(color.r, color.g, color.b, a);
                yield return null;
            }

            _flashImage.color = new Color(color.r, color.g, color.b, 0);
            _flashCoroutine = null;
        }

        [Event(EVT_REGISTER_VFX)]
        public List<object> OnRegisterVFX(List<object> data)
        {
            if (data == null || data.Count < 2 || data[0] is not string id || data[1] is not string path)
                return ResultCode.Fail("参数 [vfxId, prefabPath]");

            RegisterVFX(id, path);
            return ResultCode.Ok(id);
        }

        [Event(EVT_UNREGISTER_VFX)]
        public List<object> OnUnregisterVFX(List<object> data)
        {
            if (data == null || data.Count < 1 || data[0] is not string id)
                return ResultCode.Fail("参数 [vfxId]");

            UnregisterVFX(id);
            return ResultCode.Ok(id);
        }

        [Event(EVT_PLAY_VFX)]
        public List<object> OnPlayVFX(List<object> data)
        {
            if (data == null || data.Count < 2 || data[0] is not string id || data[1] is not Vector3 pos)
                return ResultCode.Fail("参数 [vfxId, Vector3 worldPos, Quaternion? rot, float? autoDestroy]");

            var rot = data.Count > 2 && data[2] is Quaternion q ? (Quaternion?)q : null;
            var auto = data.Count > 3 && data[3] is float f ? f : 0f;
            var instId = PlayVFX(id, pos, rot, auto);
            return string.IsNullOrEmpty(instId)
                ? ResultCode.Fail($"PlayVFX 失败: {id}")
                : ResultCode.Ok(instId);
        }

        [Event(EVT_STOP_VFX)]
        public List<object> OnStopVFX(List<object> data)
        {
            if (data == null || data.Count < 1 || data[0] is not string instId)
                return ResultCode.Fail("参数 [instanceId]");

            return StopVFX(instId)
                ? ResultCode.Ok(instId)
                : ResultCode.Fail($"StopVFX 未命中: {instId}");
        }

        [Event(EVT_STOP_ALL_VFX)]
        public List<object> OnStopAllVFX(List<object> data)
        {
            StopAllVFX();
            return ResultCode.Ok();
        }

        [Event(EVT_SCREEN_FLASH)]
        public List<object> OnScreenFlash(List<object> data)
        {
            if (data == null || data.Count < 1 || data[0] is not Color color)
                return ResultCode.Fail("参数 [Color, float duration?]");

            var duration = data.Count > 1 && data[1] is float d ? d : 0.15f;
            PlayScreenFlash(color, duration);
            return ResultCode.Ok();
        }
    }
}
