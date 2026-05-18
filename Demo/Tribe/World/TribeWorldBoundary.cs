using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace Demo.Tribe.World
{
    /// <summary>
    /// Tribe 部落世界边界（当前仅左边界；右边界保留可扩展位）—— 玩家不能越过此 X 值往左走。
    ///
    /// <para><b>设计动机</b>：营地左侧某处是"地图极限"，玩家被部落保护住；当玩家建造农场扩张时，
    /// 边界跟着往左扩展一个农场宽度。这里只承载"边界数值 + 视觉提示 + 钳制 API"，
    /// 农场扩张的触发逻辑由 FarmManager 业务广播（M1 实施后）。</para>
    ///
    /// <para><b>用法</b>：</para>
    /// <list type="bullet">
    ///   <item>业务侧（CampFeature.Build）调 <see cref="EnsureInstance"/> + <see cref="SetLeftLimit"/> 初始化</item>
    ///   <item>Player 在 FixedUpdate 调 <see cref="Instance"/>.<see cref="ClampX(float)"/> 把 transform.x 钳进合法区间</item>
    ///   <item>未来 FarmManager 广播扩张事件 → 监听器调 <see cref="ExtendLeftBy(float)"/></item>
    ///   <item>跨模块事件入口：bare-string <c>"ExtendTribeBoundaryLeft"</c> data=[float width]</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeWorldBoundary : MonoBehaviour
    {
        /// <summary>跨模块事件名 —— 监听一次扩张请求（width 单位：世界单位 / 米）。</summary>
        public const string EVT_EXTEND_LEFT = "ExtendTribeBoundaryLeft";

        public static TribeWorldBoundary Instance { get; private set; }

        [Tooltip("初始左边界 X（世界坐标）。CampFeature.Build 会用帐篷位置覆盖此值。")]
        [SerializeField] private float _leftLimitX = -10f;

        [Tooltip("视觉指示器 Y 起点（GroundY）—— 文本标签贴这条高度上方约 2 单位显示。")]
        [SerializeField] private float _indicatorBottomY = 0f;

        [Tooltip("是否显示边界文本提示（'⛔ 部落边界'）。")]
        [SerializeField] private bool _showLabel = true;

        // ─── 视觉子节点（动态创建）─────────────────────────────
        private TextMesh _labelMesh;
        private bool _registered;

        // ─── 公开数值访问 ───────────────────────────────────
        public float LeftLimitX => _leftLimitX;

        /// <summary>钳制一个 X 值到合法区间（当前仅左侧约束）。</summary>
        public float ClampX(float x) => x < _leftLimitX ? _leftLimitX : x;

        // ─── 生命周期 ──────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildVisuals();
            RefreshVisuals();
        }

        private void OnEnable()
        {
            if (EventProcessor.HasInstance && !_registered)
            {
                EventProcessor.Instance.AddListener(EVT_EXTEND_LEFT, OnExtendLeftEvent);
                _registered = true;
            }
        }

        private void OnDisable()
        {
            if (_registered && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.RemoveListener(EVT_EXTEND_LEFT, OnExtendLeftEvent);
                _registered = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── 设置 / 扩张 API ───────────────────────────────
        /// <summary>显式设定左边界 X。覆盖式写入；视觉立即刷新。</summary>
        public void SetLeftLimit(float x)
        {
            _leftLimitX = x;
            RefreshVisuals();
        }

        /// <summary>设定视觉指示器的地面基准 Y —— 让红色竖条贴着 GroundY 向上延伸。</summary>
        public void SetIndicatorBottomY(float y)
        {
            _indicatorBottomY = y;
            RefreshVisuals();
        }

        /// <summary>把左边界往左推 <paramref name="width"/> 单位 —— 用于"每建一座农场扩展一格"。
        /// <paramref name="width"/> 非正时忽略。</summary>
        public void ExtendLeftBy(float width)
        {
            if (width <= 0f) return;
            _leftLimitX -= width;
            RefreshVisuals();
        }

        // 事件入口：data = [float width]
        private List<object> OnExtendLeftEvent(string evt, List<object> data)
        {
            if (data != null && data.Count > 0 && data[0] is float w) ExtendLeftBy(w);
            else if (data != null && data.Count > 0 && data[0] is double dw) ExtendLeftBy((float)dw);
            return ResultCode.Ok(_leftLimitX);
        }

        // ─── 单例获取 / 自动挂载 ─────────────────────────────
        /// <summary>幂等保证：场景中存在则返回；不存在则用业务侧 root 自动 new 一个挂载。</summary>
        public static TribeWorldBoundary EnsureInstance(Transform worldRoot = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("TribeWorldBoundary");
            if (worldRoot != null) go.transform.SetParent(worldRoot, false);
            return go.AddComponent<TribeWorldBoundary>();
        }

        // ─── 视觉 ─────────────────────────────────────────
        private void BuildVisuals()
        {
            // 不再绘制红色竖条 —— 仅保留可选的文本标签作为边界提示。
            // 物理钳制由 TribePlayer.ApplyTribeWorldBoundary 保证，视觉不需要强提示。
            if (_showLabel)
            {
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(transform, false);
                _labelMesh = labelGo.AddComponent<TextMesh>();
                _labelMesh.text = "⛔ 部落边界";
                _labelMesh.characterSize = 0.12f;
                _labelMesh.fontSize = 48;
                _labelMesh.anchor = TextAnchor.LowerCenter;
                _labelMesh.alignment = TextAlignment.Center;
                _labelMesh.color = new Color(1f, 0.9f, 0.6f);
                var mr = labelGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sortingOrder = 60;
            }
        }

        private void RefreshVisuals()
        {
            if (_labelMesh != null)
            {
                // 标签贴到玩家可见高度（地面上方 ~2 单位），避免飞到屏幕外
                _labelMesh.transform.position = new Vector3(
                    _leftLimitX, _indicatorBottomY + 2f, 0f);
            }
        }
    }
}
