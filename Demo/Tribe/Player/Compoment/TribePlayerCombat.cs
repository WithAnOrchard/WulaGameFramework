using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;
// §4.1 跨模块 AudioManager 走 bare-string，不 using。

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 玩家近战攻击模块 —— 鼠标左键触发攻击窗口，期间用 OverlapBox 检测命中范围内的
    /// <see cref="EntityHandle"/>（统一桥接：任何注册到 EntityManager 的实体都自动带上 Handle），
    /// 走框架 <c>EVT_DAMAGE_ENTITY</c> 走 <see cref="EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.IDamageable"/> 结算。
    /// <para>本模块不再耦合具体业务类型（PickableDropEntity / TribeSkeletonEnemy）。</para>
    /// <para>可选半透明矩形提示攻击范围（仅视觉，不影响判定）。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribePlayerCombat : MonoBehaviour
    {
        [Header("Combat")]
        [SerializeField] private bool _enableMouseAttack = true;
        [SerializeField, Min(0.05f)] private float _attackDuration = 0.4f;
        [SerializeField, Min(0.1f)] private float _attackRange = 2.2f;
        [SerializeField, Min(0.1f)] private float _attackHeight = 1.4f;
        [SerializeField] private float _attackYOffset = 0f;
        [SerializeField, Min(1f)] private float _attackDamage = 1f;

        [Header("Range Hint")]
        [SerializeField] private bool _showAttackRangeHint = true;
        [SerializeField] private Color _attackRangeHintColor = new Color(1f, 0.15f, 0.05f, 0.35f);

        // ─── 运行时 ─────────────────────────────────────────────
        private string _instanceId;
        private TribePlayerMovement _movement;
        private float _attackLockUntil;
        private SpriteRenderer _hintRenderer;
        private readonly HashSet<Component> _currentAttackHits = new HashSet<Component>();

        public bool IsAttacking => Time.time < _attackLockUntil;

        /// <summary>由 Player 注入：instanceId 用于事件分发，movement 用于读取面朝/碰撞半径。</summary>
        public void Initialize(string instanceId, TribePlayerMovement movement)
        {
            _instanceId = instanceId;
            _movement = movement;
        }

        /// <summary>每帧调用：处理鼠标左键、推进命中、刷新提示框 transform。</summary>
        public void Tick()
        {
            if (_enableMouseAttack && Input.GetMouseButtonDown(0) && !IsAttacking)
                TriggerAttack();
            if (IsAttacking)
                ApplyAttackHit();
            UpdateHintTransform();
        }

        private void TriggerAttack()
        {
            _attackLockUntil = Time.time + _attackDuration;
            _currentAttackHits.Clear();
            CharacterViewBridge.TriggerAttack(_instanceId, _attackDuration);
            
            // 播放攻击音效
            EventProcessor.Instance?.TriggerEventMethod("PlayAttackSFX", null);
            
            ShowHint();
        }

        private void ApplyAttackHit()
        {
            var hits = Physics2D.OverlapBoxAll(GetAttackCenter(), GetAttackBoxSize(), 0f);
            for (var i = 0; i < hits.Length; i++)
            {
                var handle = hits[i].GetComponentInParent<EntityHandle>();
                if (handle == null || !handle.CanBeAttacked) continue;
                if (!_currentAttackHits.Add(handle)) continue;
                handle.TakeDamage(_attackDamage, "TribePlayerAttack", transform.position);
            }
        }

        private Vector2 GetAttackCenter()
        {
            var d = (_movement != null && _movement.FacingRight) ? 1f : -1f;
            var radius = _movement != null ? _movement.ColliderRadius : 0.45f;
            return (Vector2)transform.position + new Vector2(d * (radius + _attackRange * 0.5f), _attackYOffset);
        }

        private Vector2 GetAttackBoxSize()
        {
            var radius = _movement != null ? _movement.ColliderRadius : 0.45f;
            return new Vector2(_attackRange, Mathf.Max(_attackHeight, radius * 2f));
        }

        // ─── 范围提示（视觉）────────────────────────────────────
        private void ShowHint()
        {
            if (!_showAttackRangeHint) return;
            if (_hintRenderer == null) CreateHint();
            if (_hintRenderer == null) return;

            UpdateHintTransform();
            _hintRenderer.color = _attackRangeHintColor;
            _hintRenderer.gameObject.SetActive(true);
            CancelInvoke(nameof(HideHint));
            Invoke(nameof(HideHint), _attackDuration);
        }

        private void UpdateHintTransform()
        {
            if (_hintRenderer == null || !_hintRenderer.gameObject.activeSelf) return;
            var size = GetAttackBoxSize();
            _hintRenderer.transform.position = GetAttackCenter();
            _hintRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private void HideHint()
        {
            if (_hintRenderer != null) _hintRenderer.gameObject.SetActive(false);
        }

        private void CreateHint()
        {
            var go = new GameObject("AttackRangeHint");
            go.transform.SetParent(transform, false);
            _hintRenderer = go.AddComponent<SpriteRenderer>();
            _hintRenderer.sprite = CreateRectSprite();
            _hintRenderer.sortingOrder = 1000;
            _hintRenderer.gameObject.SetActive(false);
        }

        private static Sprite CreateRectSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
