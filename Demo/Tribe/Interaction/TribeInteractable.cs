using System;
using UnityEngine;
using EssSystem.Core.Presentation.InputManager;

namespace Demo.Tribe.Interaction
{
    /// <summary>
    /// <b>已废弃</b> —— 改用框架 <c>IInteractable</c> 能力 + <c>Entity.CanInteract(...)</c>。
    /// <para>迁移示例：参 <c>CampFeature.AttachInteractable</c> —— new 一个 Static Entity，链
    /// <c>CanInteract(radius, label, action)</c>，再 <c>EntityService.AttachEntityHandle</c> 即可，
    /// 由 EntityService.Tick 自动驱动（同 IFlashEffect / IKnockbackEffect 模式）。</para>
    /// <para>本类保留仅为向后兼容；新代码不要再用。</para>
    /// </summary>
    [Obsolete("改用 Entity.CanInteract(...) 能力。参 CampFeature.AttachInteractable 示例。", error: false)]
    [DisallowMultipleComponent]
    public class TribeInteractable : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("玩家进入此半径时显示提示")]
        [SerializeField, Min(0.1f)] private float _radius = 2.5f;
        [SerializeField] private string _interactAction = "TribeInteract";

        [Header("Prompt")]
        [Tooltip("提示文字。可用 emoji / 中文，例如 \"[F] 对话\" 或 \"[F] 制作\"")]
        [SerializeField] private string _promptLabel = "[F] 互动";
        [SerializeField] private Vector3 _promptOffset = new Vector3(0f, 1.6f, 0f);
        [SerializeField] private Color _promptColor = new Color(1f, 0.95f, 0.4f);
        [SerializeField] private int _promptSortingOrder = 1000;

        public float Radius { get => _radius; set => _radius = Mathf.Max(0.1f, value); }
        public string InteractAction { get => _interactAction; set => _interactAction = value; }
        public string PromptLabel { get => _promptLabel; set { _promptLabel = value; if (_promptText != null) _promptText.text = value; } }

        /// <summary>玩家按下 InteractKey 时触发；建议外部通过 <see cref="SetOnInteract"/> 注入业务逻辑。</summary>
        public Action OnInteract;

        // ─── 运行时 ────────────────────────────────────
        private TextMesh _promptText;
        private GameObject _promptGo;
        private Transform _player;
        private bool _inRange;

        public void SetOnInteract(Action handler) => OnInteract = handler;

        private void Awake() => EnsurePrompt();

        private void Update()
        {
            if (!ResolvePlayer())
            {
                SetPromptVisible(false);
                return;
            }

            var dist = (transform.position - _player.position).sqrMagnitude;
            _inRange = dist <= _radius * _radius;
            SetPromptVisible(_inRange);

            var input = InputManager.TryGetInstance();
            if (_inRange && input != null && input.IsDown(_interactAction))
                OnInteract?.Invoke();
        }

        private bool ResolvePlayer()
        {
            if (_player != null && _player.gameObject != null) return true;
            // 按 Tag 优先；fallback 名字搜索
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _player = tagged.transform; return true; }
            // FindWithTag 在没有 Player tag 时抛异常 → 已用 try-safe 等价的 FindGameObjectWithTag
            var go = GameObject.Find("TribePlayer") ?? GameObject.Find("Player");
            if (go != null) { _player = go.transform; return true; }
            return false;
        }

        private void EnsurePrompt()
        {
            if (_promptGo != null) return;
            _promptGo = new GameObject("InteractionPrompt");
            _promptGo.transform.SetParent(transform, false);
            _promptGo.transform.localPosition = _promptOffset;

            _promptText = _promptGo.AddComponent<TextMesh>();
            _promptText.text = _promptLabel;
            _promptText.characterSize = 0.12f;
            _promptText.fontSize = 48;
            _promptText.anchor = TextAnchor.MiddleCenter;
            _promptText.alignment = TextAlignment.Center;
            _promptText.color = _promptColor;

            var mr = _promptGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = _promptSortingOrder;

            _promptGo.SetActive(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (_promptGo == null) EnsurePrompt();
            if (_promptGo != null && _promptGo.activeSelf != visible)
                _promptGo.SetActive(visible);
        }
    }
}
