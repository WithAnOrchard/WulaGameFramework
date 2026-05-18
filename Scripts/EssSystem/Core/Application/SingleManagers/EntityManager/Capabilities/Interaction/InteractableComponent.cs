using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 默认 <see cref="IInteractable"/> 实现 —— 每帧 OverlapCircle 找 Player（按 Tag），
    /// 进圈显头顶 <see cref="TextMesh"/> 提示，按键触发回调。
    /// <para>
    /// 提示用 TextMesh（非 uGUI 范畴，参根 Agent.md §5 例外条款）：避免每个交互对象都创建一个
    /// Canvas 子树，且 TextMesh 走 SpriteRenderer 排序系统，与场景渲染层级天然兼容。
    /// </para>
    /// <para>玩家识别规则（与 <c>InventoryManager.PickableItem.IsPlayerCollider</c> 一致）：
    /// Tag = "Player"，或名字包含 "Player"。</para>
    /// </summary>
    public class InteractableComponent : IInteractable, ITickableCapability
    {
        public float   Radius      { get; set; }
        public KeyCode InteractKey { get; set; } = KeyCode.F;
        public string  PromptLabel { get; set; } = "[F] 互动";
        public bool    Enabled     { get; set; } = true;
        public bool    PlayerInRange { get; private set; }
        public Action  OnInteract  { get; set; }

        private Entity _owner;
        private Transform _player;
        private GameObject _promptGo;
        private TextMesh _promptText;

        /// <summary>所有活跃的 Interactable —— 用于"多个同时在范围内只触发最近者"的全局裁决。
        /// OnAttach 时入集，OnDetach 时移除。</summary>
        private static readonly List<InteractableComponent> _active = new List<InteractableComponent>();

        /// <summary>距离 / 最近者裁决的节流间隔（秒）。<b>0.1s（10Hz）远超人类对提示的反应阈值</b>，
        /// 比 60Hz 节省 6×CPU；F 键监听仍然是每帧（保留输入即时响应）。</summary>
        private const float DistanceCheckInterval = 0.1f;
        private float _distanceCheckTimer;

        // 提示视觉参数（构造时可覆盖）
        private readonly Vector3 _promptOffset;
        private readonly Color   _promptColor;
        private readonly int     _promptSortingOrder;
        private readonly float   _promptCharSize;
        private readonly int     _promptFontSize;

        public InteractableComponent(
            float radius = 2.5f,
            string promptLabel = "[F] 互动",
            KeyCode interactKey = KeyCode.F,
            Action onInteract = null,
            Vector3? promptOffset = null,
            Color? promptColor = null,
            int promptSortingOrder = 1000,
            float promptCharSize = 0.12f,
            int promptFontSize = 48)
        {
            Radius = Mathf.Max(0.1f, radius);
            PromptLabel = promptLabel ?? "[F] 互动";
            InteractKey = interactKey;
            OnInteract = onInteract;
            // 默认抬到 2.4 —— 给头顶名牌（典型 1.4）和角色头部留出余量，避免文字重叠
            _promptOffset = promptOffset ?? new Vector3(0f, 2.4f, 0f);
            _promptColor = promptColor ?? new Color(1f, 0.95f, 0.4f);
            _promptSortingOrder = promptSortingOrder;
            _promptCharSize = promptCharSize;
            _promptFontSize = promptFontSize;
        }

        public void OnAttach(Entity owner)
        {
            _owner = owner;
            if (!_active.Contains(this)) _active.Add(this);
        }

        public void OnDetach(Entity owner)
        {
            _active.Remove(this);
            if (_promptGo != null) UnityEngine.Object.Destroy(_promptGo);
            _promptGo = null;
            _promptText = null;
            _owner = null;
        }

        public void Tick(float deltaTime)
        {
            if (!Enabled || _owner == null || _owner.CharacterRoot == null)
            {
                SetPromptVisible(false);
                return;
            }

            if (!ResolvePlayer())
            {
                SetPromptVisible(false);
                return;
            }

            // 节流：距离 / 最近者裁决以 DistanceCheckInterval 频率执行（缓存 _isClosestActive），
            // F 键监听仍每帧 —— 输入响应即时，CPU 节流 6× 左右。
            _distanceCheckTimer -= deltaTime;
            if (_distanceCheckTimer <= 0f)
            {
                _distanceCheckTimer = DistanceCheckInterval;

                var sqrDist = (_owner.CharacterRoot.position - _player.position).sqrMagnitude;
                PlayerInRange = sqrDist <= Radius * Radius;

                // 多个互动体重叠时，只让"在自己范围内 + 距玩家最近"的那一个显示提示 / 响应按键，
                // 避免同时触发两个对话 / 两个面板。等距用 GetHashCode 做稳定 tiebreaker。
                _isClosestActive = PlayerInRange && IsClosestActive(sqrDist);
                SetPromptVisible(_isClosestActive);
            }

            if (_isClosestActive && Input.GetKeyDown(InteractKey))
                OnInteract?.Invoke();
        }

        private bool _isClosestActive;

        private bool IsClosestActive(float mySqrDist)
        {
            for (var i = 0; i < _active.Count; i++)
            {
                var other = _active[i];
                if (other == null || other == this) continue;
                if (!other.Enabled) continue;
                var root = other._owner?.CharacterRoot;
                if (root == null || _player == null) continue;

                var otherSqr = (root.position - _player.position).sqrMagnitude;
                if (otherSqr > other.Radius * other.Radius) continue; // 对方不在自身范围内，不参与裁决

                if (otherSqr < mySqrDist) return false;
                // 等距 tiebreaker：HashCode 小的胜出（确定性，单帧内一致）
                if (Mathf.Approximately(otherSqr, mySqrDist) &&
                    other.GetHashCode() < this.GetHashCode())
                    return false;
            }
            return true;
        }

        // ─── 内部 ───────────────────────────────────────────────
        private bool ResolvePlayer()
        {
            if (_player != null && _player.gameObject != null) return true;
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) { _player = tagged.transform; return true; }
            var go = GameObject.Find("TribePlayer") ?? GameObject.Find("Player");
            if (go != null) { _player = go.transform; return true; }
            return false;
        }

        private void EnsurePrompt()
        {
            if (_promptGo != null || _owner?.CharacterRoot == null) return;

            _promptGo = new GameObject("InteractionPrompt");
            _promptGo.transform.SetParent(_owner.CharacterRoot, false);
            _promptGo.transform.localPosition = _promptOffset;

            _promptText = _promptGo.AddComponent<TextMesh>();
            _promptText.text = PromptLabel;
            _promptText.characterSize = _promptCharSize;
            _promptText.fontSize = _promptFontSize;
            _promptText.anchor = TextAnchor.MiddleCenter;
            _promptText.alignment = TextAlignment.Center;
            _promptText.color = _promptColor;

            var mr = _promptGo.GetComponent<MeshRenderer>();
            if (mr != null) mr.sortingOrder = _promptSortingOrder;

            _promptGo.SetActive(false);
        }

        private void SetPromptVisible(bool visible)
        {
            if (visible && _promptGo == null) EnsurePrompt();
            if (_promptGo == null) return;
            if (_promptGo.activeSelf != visible) _promptGo.SetActive(visible);
            // 标签变更时同步刷新（PromptLabel setter 不会触发，所以这里兜底）
            if (visible && _promptText != null && _promptText.text != PromptLabel)
                _promptText.text = PromptLabel;
        }
    }
}
