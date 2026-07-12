using System;
using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using InputManagerRuntime = EssSystem.Core.Presentation.InputManager.InputManager;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 默认交互能力。使用 TextMesh 在世界空间显示提示，并通过 InputManager 统一输入管道触发回调。
    /// 玩家识别只使用通用约定：Tag = Player，或对象名包含 Player。
    /// </summary>
    public class InteractableComponent : IInteractable, ITickableCapability
    {
        private const float DistanceCheckInterval = 0.1f;

        private static readonly List<InteractableComponent> Active = new List<InteractableComponent>();

        public float Radius { get; set; }
        public string InteractAction { get; set; } = InputManagerRuntime.ACTION_ENTITY_INTERACT;
        public string PromptLabel { get; set; } = "[Space] 互动";
        public bool Enabled { get; set; } = true;
        public bool PlayerInRange { get; private set; }
        public Action OnInteract { get; set; }

        private readonly Vector3 _promptOffset;
        private readonly Color _promptColor;
        private readonly int _promptSortingOrder;
        private readonly float _promptCharSize;
        private readonly int _promptFontSize;

        private Entity _owner;
        private Transform _player;
        private GameObject _promptGo;
        private TextMesh _promptText;
        private float _distanceCheckTimer;
        private bool _isClosestActive;

        public InteractableComponent(
            float radius = 2.5f,
            string promptLabel = "[Space] 互动",
            string interactAction = null,
            Action onInteract = null,
            Vector3? promptOffset = null,
            Color? promptColor = null,
            int promptSortingOrder = 1000,
            float promptCharSize = 0.12f,
            int promptFontSize = 48)
        {
            Radius = Mathf.Max(0.1f, radius);
            PromptLabel = promptLabel ?? "[Space] 互动";
            InteractAction = string.IsNullOrWhiteSpace(interactAction)
                ? InputManagerRuntime.ACTION_ENTITY_INTERACT
                : interactAction;
            OnInteract = onInteract;
            _promptOffset = promptOffset ?? new Vector3(0f, 2.4f, 0f);
            _promptColor = promptColor ?? new Color(1f, 0.95f, 0.4f);
            _promptSortingOrder = promptSortingOrder;
            _promptCharSize = promptCharSize;
            _promptFontSize = promptFontSize;
        }

        public void OnAttach(Entity owner)
        {
            _owner = owner;
            if (!Active.Contains(this)) Active.Add(this);
        }

        public void OnDetach(Entity owner)
        {
            Active.Remove(this);
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

            _distanceCheckTimer -= deltaTime;
            if (_distanceCheckTimer <= 0f)
            {
                _distanceCheckTimer = DistanceCheckInterval;
                var sqrDist = (_owner.CharacterRoot.position - _player.position).sqrMagnitude;
                PlayerInRange = sqrDist <= Radius * Radius;
                _isClosestActive = PlayerInRange && IsClosestActive(sqrDist);
                SetPromptVisible(_isClosestActive);
            }

            var input = InputManagerRuntime.TryGetInstance();
            if (_isClosestActive && input != null && input.IsDown(InteractAction))
                OnInteract?.Invoke();
        }

        private bool IsClosestActive(float mySqrDist)
        {
            for (var i = 0; i < Active.Count; i++)
            {
                var other = Active[i];
                if (other == null || other == this || !other.Enabled) continue;

                var root = other._owner?.CharacterRoot;
                if (root == null || _player == null) continue;

                var otherSqr = (root.position - _player.position).sqrMagnitude;
                if (otherSqr > other.Radius * other.Radius) continue;
                if (otherSqr < mySqrDist) return false;
                if (Mathf.Approximately(otherSqr, mySqrDist) && other.GetHashCode() < GetHashCode())
                    return false;
            }

            return true;
        }

        private bool ResolvePlayer()
        {
            if (_player != null && _player.gameObject != null) return true;

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
            {
                _player = tagged.transform;
                return true;
            }

            var player = GameObject.Find("Player");
            if (player != null)
            {
                _player = player.transform;
                return true;
            }

            _player = FindPlayerByName();
            return _player != null;
        }

        private static Transform FindPlayerByName()
        {
            var transforms = UnityEngine.Object.FindObjectsByType<Transform>();
            for (var i = 0; i < transforms.Length; i++)
            {
                var transform = transforms[i];
                if (transform == null) continue;

                var root = transform.root != null ? transform.root : transform;
                if (IsPlayerObject(root.gameObject)) return root;
                if (IsPlayerObject(transform.gameObject)) return transform;
            }

            return null;
        }

        private static bool IsPlayerObject(GameObject go)
        {
            if (go == null) return false;
            return go.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
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
            if (visible && _promptText != null && _promptText.text != PromptLabel)
                _promptText.text = PromptLabel;
        }
    }
}
