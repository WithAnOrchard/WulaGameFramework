using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
// §4.1 跨模块 UIManager 走 bare-string，不 using。

namespace Demo.Tribe.Enemy
{
    /// <summary>
    /// 敌人血条 UI —— **完全照抄 <see cref="Demo.Tribe.Player.TribePlayerHud"/> 的红条 + 数值文本结构**：
    /// <list type="bullet">
    /// <item>使用玩家同款 sprite（<c>Bar_1</c> / <c>RedBar</c>）+ 同款 padding (10,6)，保证 fill 可见</item>
    /// <item>4x 超采样的 <see cref="UITextComponent"/> 子节点显示 "<c>hp/max</c>"</item>
    /// <item>父 <see cref="UIPanelComponent"/> 包裹整体，方便锚到屏幕坐标</item>
    /// </list>
    /// <para>每帧 LateUpdate 把宿主头顶世界坐标投到屏幕 → 设根 RectTransform.anchoredPosition。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribeEnemyHealthUI : MonoBehaviour
    {
        [Header("Layout")]
        [Tooltip("血条相对宿主的世界空间偏移（投影前叠加）。")]
        [SerializeField] private Vector3 _worldOffset = new Vector3(0f, 1.2f, 0f);

        [Tooltip("血条在屏幕空间的额外偏移（像素，参考分辨率 1920×1080）。X 正值往右，Y 正值往上。")]
        [SerializeField] private Vector2 _screenOffset = new Vector2(70f, 0f);

        [Tooltip("血条尺寸（参考分辨率单位，1920×1080）。照玩家比例缩小：玩家 280×28 → 这里 140×18。")]
        [SerializeField] private Vector2 _barSize = new Vector2(140f, 18f);

        [Header("Style")]
        [SerializeField] private Color _backgroundColor = Color.white;
        [SerializeField] private Color _fillColor       = new Color(1f, 0.25f, 0.25f);
        [SerializeField] private string _backgroundSpriteId = "Bar_1";
        [SerializeField] private string _fillSpriteId       = "RedBar";

        [Header("Value Text (4x super-sample 同玩家)")]
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Color _textColor = Color.white;

        // ─── 运行时 ────────────────────────────────────────────
        private string _rootId;
        private UIPanelComponent _root;
        private UIBarComponent _bar;
        private UITextComponent _valueText;
        private RectTransform _rootRect;
        private RectTransform _canvasRect;
        private Camera _camera;
        private float _lastHp = -1f, _lastMaxHp = -1f;

        public bool IsBuilt => _root != null;

        /// <summary>注册血条；<paramref name="instanceId"/> 用于派生唯一 Id。</summary>
        public void Build(string instanceId)
        {
            if (IsBuilt) return;
            if (!EventProcessor.HasInstance) { Debug.LogWarning("[TribeEnemyHealthUI] EventProcessor 未就绪"); return; }

            _rootId = $"enemy_hp_{instanceId}_{GetInstanceID()}";

            // 父面板：透明背景，作为 anchor 容器
            _root = new UIPanelComponent(_rootId, "EnemyHealthRoot")
                .SetPosition(0f, 0f).SetSize(_barSize.x, _barSize.y)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(true);

            // 血条 —— 照抄玩家 hpBar：sprite + padding (10,6) + Range(0,1)
            _bar = new UIBarComponent($"{_rootId}_bar", "hp")
                .SetPosition(0f, 0f).SetSize(_barSize.x, _barSize.y)
                .SetRange(0f, 1f).SetValue(1f)
                .SetBackgroundSpriteId(_backgroundSpriteId).SetFillSpriteId(_fillSpriteId)
                .SetFillPadding(10f, 6f)
                .SetBackgroundColor(_backgroundColor).SetFillColor(_fillColor)
                .SetVisible(true);

            // 数值文本 —— 照抄玩家 MakeValueText：4x 超采样 + 缩 1/4 抗走样
            const float s = 4f;
            _valueText = new UITextComponent($"{_rootId}_value", "value")
                .SetPosition(0f, 0f).SetSize(_barSize.x * s, _barSize.y * s).SetScale(1f / s, 1f / s)
                .SetFontSize(Mathf.RoundToInt(_fontSize * s)).SetColor(_textColor)
                .SetAlignment(TextAnchor.MiddleCenter).SetText("0/0").SetVisible(true);

            _bar.AddChild(_valueText);
            _root.AddChild(_bar);

            var registerResult = EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity", new List<object> { _rootId, _root });
            if (!ResultCode.IsOk(registerResult))
            {
                Debug.LogWarning($"[TribeEnemyHealthUI] 注册失败: {_rootId}");
                _root = null; _bar = null; _valueText = null; _rootId = null;
                return;
            }

            // 缓存根 RectTransform 用于每帧定位（避免 SetPosition 每帧触发事件链）
            var goResult = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { _rootId });
            if (ResultCode.IsOk(goResult) && goResult.Count >= 2 && goResult[1] is GameObject go)
                _rootRect = go.GetComponent<RectTransform>();

            var canvasResult = EventProcessor.Instance.TriggerEventMethod(
                "GetUICanvasTransform", new List<object>());
            if (ResultCode.IsOk(canvasResult) && canvasResult.Count >= 2 && canvasResult[1] is Transform canvasT)
                _canvasRect = canvasT as RectTransform;

            _camera = Camera.main;
        }

        /// <summary>提交血量快照（current + max），照玩家 SetStats 风格做 diff 缓存。</summary>
        public void SetValue(float current, float max)
        {
            if (_bar == null) return;
            if (max < 0.0001f) max = 1f;
            if (Mathf.Approximately(current, _lastHp) && Mathf.Approximately(max, _lastMaxHp)) return;
            _lastHp = current; _lastMaxHp = max;

            _bar.SetValue(current, max);
            _valueText?.SetText($"{Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}");
        }

        /// <summary>仅设百分比（兼容旧 API）。</summary>
        public void SetPercent(float percent)
        {
            if (_bar == null) return;
            _bar.SetValue(Mathf.Clamp01(percent), 1f);
        }

        /// <summary>销毁 UI（敌人死亡 / OnDestroy 时调）。</summary>
        public void Dispose()
        {
            var id = _rootId;
            _rootId = null;
            _root = null; _bar = null; _valueText = null; _rootRect = null;
            if (string.IsNullOrEmpty(id)) return;
            // ApplicationLifecycle.IsQuitting 信号已集成到 HasInstance + EventProcessor 内部，
            // teardown 期事件分发会 silent-return，无需 try/catch 兜底。
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "UnregisterUIEntity", new List<object> { id });
        }

        private void OnDestroy() => Dispose();

        private void LateUpdate()
        {
            if (_rootRect == null || _canvasRect == null) return;
            if (_camera == null) _camera = Camera.main;

            UIWorldFollower.UpdatePosition(_camera, _canvasRect, _rootRect,
                transform.position + _worldOffset, _screenOffset);
        }
    }
}
