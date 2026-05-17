using EssSystem.Core.Base.Event;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting.Dao;

namespace EssSystem.Core.Application.MultiManagers.MapManager.Voxel3D.Lighting
{
    /// <summary>
    /// 体素光照门面 —— 与 <c>Voxel3DMapManager</c>(13) 平行的独立 Manager 模块。
    /// <para>职责：</para>
    /// <list type="bullet">
    ///   <item>暴露 Inspector 调控（<see cref="DayCycle01"/> / <see cref="AmbientFloor"/>），每帧推到 <see cref="VoxelLightingService"/>。</item>
    ///   <item>提供光源增删 API（火把/萤石/岩浆/灯笼预设 + 通用 AddSource）。</item>
    ///   <item>聚合 <see cref="VoxelLightingService.OnLightingChanged"/> 事件，用于上层（mesher/MapView）触发 chunk 重 mesh。</item>
    /// </list>
    /// <para>查询走 <see cref="Service"/>.SampleLight；上层不必关心这个 Manager 是否存在（缺席时 mesher 退化为全亮）。</para>
    /// </summary>
    [Manager(14)]
    public class VoxelLightingManager : Manager<VoxelLightingManager>
    {
        #region Inspector

        [Header("Day / Night Cycle")]
        [Tooltip("当前时间值 [0, 1]：0 = 午夜全黑，0.5 = 正午全亮。\n" +
                 "曲线见 SkyMultiplierFromDayCycle()：[0, 0.2] 完全黑夜，[0.3, 0.7] 完全白昼，中间平滑过渡（晨/昏 ~0.1 区间）。")]
        [SerializeField, Range(0f, 1f)] private float _dayCycle01 = 0.5f;

        [Tooltip("是否每帧自动推进 DayCycle（按 _daySpeedRealSec 一整轮）。运行时调试关掉，手拖滑块即可。")]
        [SerializeField] private bool _autoAdvance = false;

        [Tooltip("一天 = 多少现实秒。默认 600s = 10 min/day（够 demo 看到日出日落）。")]
        [SerializeField, Min(1f)] private float _daySpeedRealSec = 600f;

        [Header("Ambient")]
        [Tooltip("最低环境光等级（0..15）。0 = 黑夜可全黑（仍受 AmbientFloor01 顶点色兜底）；2~3 = 黑夜略亮便于演示。")]
        [SerializeField, Range(0, 15)] private int _ambientFloor = 1;

        [Header("Auto-rebuild Hook")]
        [Tooltip("光照变更时自动通知所有 Voxel3DMapView 重建 mesh。\n关掉后需自行调 view.MarkAllDirty() 等。")]
        [SerializeField] private bool _autoRebuildOnChange = true;

        #endregion

        // ── 公共 API ────────────────────────────────────────────────

        public VoxelLightingService Service => VoxelLightingService.Instance;

        /// <summary>当前时间循环 [0, 1]。set 后立即通知 Service。</summary>
        public float DayCycle01
        {
            get => _dayCycle01;
            set
            {
                _dayCycle01 = Mathf.Clamp01(value);
                ApplyDayCycle();
            }
        }

        /// <summary>当前 sky 倍率（已根据 DayCycle 折算，0..1）。</summary>
        public float CurrentSkyMultiplier => SkyMultiplierFromDayCycle(_dayCycle01);

        public int AmbientFloor
        {
            get => _ambientFloor;
            set
            {
                _ambientFloor = Mathf.Clamp(value, 0, 15);
                if (Service != null) Service.AmbientFloor = (byte)_ambientFloor;
            }
        }

        // ── 光源增删（Manager → Service 透传，方便外部调用方不暴露 Service 引用）──

        public int AddSource(VoxelLightSource src) => Service.AddSource(src);
        public int AddTorch(Vector3 worldPos)     => Service.AddTorch(worldPos);
        public int AddGlowstone(Vector3 worldPos) => Service.AddGlowstone(worldPos);
        public int AddLava(Vector3 worldPos)      => Service.AddLava(worldPos);
        public int AddLantern(Vector3 worldPos)   => Service.AddLantern(worldPos);
        public bool RemoveSource(int handle)      => Service.RemoveSource(handle);
        public void ClearSources()                => Service.ClearSources();

        // ── 生命周期 ───────────────────────────────────────────────

        protected override void Initialize()
        {
            base.Initialize();
            ApplyDayCycle();
            Service.AmbientFloor = (byte)_ambientFloor;
            Service.OnLightingChanged += HandleLightingChangedRebuildHook;
        }

        protected override void Update()
        {
            base.Update();

            if (_autoAdvance && _daySpeedRealSec > 0f)
            {
                _dayCycle01 = Mathf.Repeat(_dayCycle01 + Time.deltaTime / _daySpeedRealSec, 1f);
                ApplyDayCycle();
            }

            // Inspector 滑块拖动时也会走 setter 路径 —— 这里再保险同步一次（避免直接改 _dayCycle01）
            var expected = SkyMultiplierFromDayCycle(_dayCycle01);
            if (!Mathf.Approximately(Service.SkyMultiplier01, expected))
            {
                Service.SkyMultiplier01 = expected;
                Service.NotifyChanged();
            }

            if (Service.AmbientFloor != (byte)_ambientFloor)
                Service.AmbientFloor = (byte)_ambientFloor;
        }

        protected override void OnDestroy()
        {
            if (Service != null) Service.OnLightingChanged -= HandleLightingChangedRebuildHook;
            base.OnDestroy();
        }

        // ── 内部 ───────────────────────────────────────────────────

        private void ApplyDayCycle()
        {
            Service.SkyMultiplier01 = SkyMultiplierFromDayCycle(_dayCycle01);
            Service.NotifyChanged();
        }

        /// <summary>把 [0, 1] 时间映射到 sky 倍率：
        /// <list type="bullet">
        ///   <item>[0.00, 0.20] → 0（深夜）</item>
        ///   <item>[0.20, 0.30] → smoothstep 上升（黎明）</item>
        ///   <item>[0.30, 0.70] → 1（白昼）</item>
        ///   <item>[0.70, 0.80] → smoothstep 下降（黄昏）</item>
        ///   <item>[0.80, 1.00] → 0（深夜）</item>
        /// </list>
        /// </summary>
        public static float SkyMultiplierFromDayCycle(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            if (t01 <= 0.20f) return 0f;
            if (t01 <  0.30f) { var u = (t01 - 0.20f) / 0.10f; return u * u * (3f - 2f * u); }
            if (t01 <= 0.70f) return 1f;
            if (t01 <  0.80f) { var u = 1f - (t01 - 0.70f) / 0.10f; return u * u * (3f - 2f * u); }
            return 0f;
        }

        private void HandleLightingChangedRebuildHook()
        {
            if (!_autoRebuildOnChange) return;
            // mesher 由上层 Voxel3DMapView 持有，光照仅改顶点色 → 触发重 mesh 即可
            // 通过 EventProcessor 或直接 view.MarkAllDirty()；此处先留 hook 让具体集成方接入
            // 推荐：在 Voxel3DMapView 中订阅 VoxelLightingService.Instance.OnLightingChanged 调 RebuildAllMeshes()
        }
    }
}
