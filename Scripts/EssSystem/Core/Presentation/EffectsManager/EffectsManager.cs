using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.Presentation.EffectsManager
{
    /// <summary>
    /// 视觉特效管理器 —— VFX prefab 池化播放 + 屏幕闪光叠加。
    /// <para>VFX prefab 走 <c>ResourceManager</c> 加载（bare-string <c>"GetPrefab"</c>，§4.1）。</para>
    /// <para>屏幕闪光建独立 overlay Canvas（高 sortingOrder，不依赖 UIManager）。</para>
    /// </summary>
    [Manager(6)]
    public class EffectsManager : Manager<EffectsManager>
    {
        // ============================================================
        // Event 常量
        // ============================================================
        /// <summary>注册 VFX 资源映射。data: [string vfxId, string prefabPath].</summary>
        public const string EVT_REGISTER_VFX   = "RegisterVFX";
        /// <summary>解除 VFX 资源映射（不影响已实例化）。data: [string vfxId].</summary>
        public const string EVT_UNREGISTER_VFX = "UnregisterVFX";
        /// <summary>播放 VFX。data: [string vfxId, Vector3 worldPos, Quaternion? rot, float autoDestroy?]. 返回 Ok(string instanceId) / Fail。</summary>
        public const string EVT_PLAY_VFX       = "PlayVFX";
        /// <summary>停止 VFX 实例并回收。data: [string instanceId].</summary>
        public const string EVT_STOP_VFX       = "StopVFX";
        /// <summary>清空所有正在播的 VFX。data: 空。</summary>
        public const string EVT_STOP_ALL_VFX   = "StopAllVFX";
        /// <summary>屏幕闪光。data: [Color color, float duration?=0.15f].</summary>
        public const string EVT_SCREEN_FLASH   = "PlayScreenFlash";

        // ============================================================
        // Inspector
        // ============================================================
        [Header("VFX Pool")]
        [Tooltip("启用对象池：相同 vfxId 复用实例，避免反复 Instantiate / Destroy")]
        [SerializeField] private bool _enablePool = true;

        [Tooltip("每个 vfxId 池化上限；超出按 LRU 淘汰")]
        [SerializeField, Min(1)] private int _maxPoolSizePerKey = 8;

        [Header("Screen Flash")]
        [Tooltip("屏幕闪光 Canvas 的 sortingOrder（越大越靠上）")]
        [SerializeField] private int _flashCanvasSortingOrder = 32000;

        public EffectsService Service => EffectsService.Instance;

        // ============================================================
        // 运行时状态
        // ============================================================
        /// <summary>已加载的 VFX prefab 缓存（vfxId → Prefab）</summary>
        private readonly Dictionary<string, GameObject> _vfxPrefabs = new();

        /// <summary>对象池（vfxId → 闲置实例栈）</summary>
        private readonly Dictionary<string, Stack<GameObject>> _pool = new();

        /// <summary>正在播放的实例（instanceId → 实例信息）</summary>
        private readonly Dictionary<string, ActiveVfx> _active = new();

        /// <summary>屏幕闪光 overlay Canvas + Image（按需创建）</summary>
        private Canvas _flashCanvas;
        private Image  _flashImage;
        private Coroutine _flashCoroutine;

        private int _instanceSeq;

        private struct ActiveVfx
        {
            public string VfxId;
            public GameObject Go;
            public Coroutine AutoDestroyCoroutine;
        }

        // ============================================================
        // 生命周期
        // ============================================================
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
                // prefab 延迟到首次 Play 时加载，启动期不阻塞
                _vfxPrefabs[id] = null;
            }
        }

        // ============================================================
        // C# API
        // ============================================================
        public void RegisterVFX(string vfxId, string prefabPath)
        {
            if (string.IsNullOrEmpty(vfxId) || string.IsNullOrEmpty(prefabPath)) return;
            _vfxPrefabs[vfxId] = null;   // 占位，prefab 延迟加载
            Service?.SetRegistration(vfxId, prefabPath);
        }

        public void UnregisterVFX(string vfxId)
        {
            if (string.IsNullOrEmpty(vfxId)) return;
            _vfxPrefabs.Remove(vfxId);
            Service?.RemoveRegistration(vfxId);
        }

        /// <summary>播放 VFX。返回 instanceId（空表示失败）。</summary>
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
            go.transform.SetParent(transform, false);   // 默认挂在 Manager 下；外部可后续 reparent
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
            // 拷贝 keys 避免迭代时改集合
            var ids = new List<string>(_active.Keys);
            foreach (var id in ids) StopVFX(id);
        }

        public void PlayScreenFlash(Color color, float duration = 0.15f)
        {
            EnsureFlashCanvas();
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashRoutine(color, Mathf.Max(0.01f, duration)));
        }

        // ============================================================
        // 内部辅助
        // ============================================================
        private GameObject ResolvePrefab(string vfxId)
        {
            if (_vfxPrefabs.TryGetValue(vfxId, out var prefab) && prefab != null) return prefab;

            var path = Service?.GetRegistration(vfxId);
            if (string.IsNullOrEmpty(path)) return null;

            // §4.1 跨模块 bare-string："GetPrefab"
            if (!EventProcessor.HasInstance) return null;
            var r = EventProcessor.Instance.TriggerEventMethod("GetPrefab", new List<object> { path });
            if (!ResultCode.IsOk(r) || r.Count < 2 || !(r[1] is GameObject go)) return null;
            _vfxPrefabs[vfxId] = go;
            return go;
        }

        private GameObject TakeFromPoolOrInstantiate(string vfxId, GameObject prefab)
        {
            if (_enablePool && _pool.TryGetValue(vfxId, out var stack) && stack.Count > 0)
            {
                var go = stack.Pop();
                if (go != null) return go;
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
            if (stack.Count >= _maxPoolSizePerKey) { Destroy(go); return; }
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
            if (_flashCanvas != null) return;
            var go = new GameObject("EffectsFlashCanvas");
            go.transform.SetParent(transform, false);

            _flashCanvas = go.AddComponent<Canvas>();
            _flashCanvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _flashCanvas.sortingOrder = _flashCanvasSortingOrder;

            var imgGo = new GameObject("FlashImage");
            imgGo.transform.SetParent(go.transform, false);
            _flashImage = imgGo.AddComponent<Image>();
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
            // 0 → 峰值 → 0 的三角波，峰值在 30% 时刻
            var peakT = duration * 0.3f;
            var fadeT = duration - peakT;

            float timer = 0f;
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

        // ============================================================
        // Event API
        // ============================================================
        [Event(EVT_REGISTER_VFX)]
        public List<object> OnRegisterVFX(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is string path))
                return ResultCode.Fail("参数 [vfxId, prefabPath]");
            RegisterVFX(id, path);
            return ResultCode.Ok(id);
        }

        [Event(EVT_UNREGISTER_VFX)]
        public List<object> OnUnregisterVFX(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string id))
                return ResultCode.Fail("参数 [vfxId]");
            UnregisterVFX(id);
            return ResultCode.Ok(id);
        }

        [Event(EVT_PLAY_VFX)]
        public List<object> OnPlayVFX(List<object> data)
        {
            if (data == null || data.Count < 2 || !(data[0] is string id) || !(data[1] is Vector3 pos))
                return ResultCode.Fail("参数 [vfxId, Vector3 worldPos, Quaternion? rot, float? autoDestroy]");
            var rot = data.Count > 2 && data[2] is Quaternion q ? (Quaternion?)q : null;
            var auto = data.Count > 3 && data[3] is float f ? f : 0f;
            var instId = PlayVFX(id, pos, rot, auto);
            return string.IsNullOrEmpty(instId) ? ResultCode.Fail($"PlayVFX 失败: {id}") : ResultCode.Ok(instId);
        }

        [Event(EVT_STOP_VFX)]
        public List<object> OnStopVFX(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string instId))
                return ResultCode.Fail("参数 [instanceId]");
            return StopVFX(instId) ? ResultCode.Ok(instId) : ResultCode.Fail($"StopVFX 未命中: {instId}");
        }

        [Event(EVT_STOP_ALL_VFX)]
        public List<object> OnStopAllVFX(List<object> data) { StopAllVFX(); return ResultCode.Ok(); }

        [Event(EVT_SCREEN_FLASH)]
        public List<object> OnScreenFlash(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is Color c))
                return ResultCode.Fail("参数 [Color, float duration?]");
            var d = data.Count > 1 && data[1] is float dur ? dur : 0.15f;
            PlayScreenFlash(c, d);
            return ResultCode.Ok();
        }
    }
}
