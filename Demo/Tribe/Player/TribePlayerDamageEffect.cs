using UnityEngine;

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 玩家受伤效果组件 —— 受伤时变白闪烁 + 击退
    /// </summary>
    [DisallowMultipleComponent]
    public class TribePlayerDamageEffect : MonoBehaviour
    {
        [Header("Flash")]
        [SerializeField] private float _flashDuration = 0.15f;
        [SerializeField] private Color _flashColor = Color.white;

        [Header("Knockback")]
        [SerializeField] private float _knockbackForce = 15f; // 增加击退力度
        [SerializeField] private float _knockbackDuration = 0.2f;

        private SpriteRenderer _renderer;
        private Rigidbody2D _rb;
        private Color _originalColor;
        private float _flashTimer;
        private bool _isFlashing;
        private float _knockbackTimer;

        /// <summary>是否正在击退中（外部读取以暂停移动输入）。</summary>
        public bool IsKnockbacking => _knockbackTimer > 0f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            TryResolveRenderer();
        }

        /// <summary>懒初始化 SpriteRenderer（Character 子节点在 Start 才创建，Awake 时可能还没有）。</summary>
        private bool TryResolveRenderer()
        {
            if (_renderer != null) return true;
            _renderer = GetComponentInChildren<SpriteRenderer>();
            if (_renderer != null) _originalColor = _renderer.color;
            return _renderer != null;
        }

        private void Update()
        {
            // 处理变白闪烁
            if (_isFlashing)
            {
                _flashTimer -= Time.deltaTime;
                if (_flashTimer <= 0f)
                {
                    if (_renderer != null) _renderer.color = _originalColor;
                    _isFlashing = false;
                }
            }

            // 处理击退
            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.deltaTime;
                if (_knockbackTimer <= 0f && _rb != null)
                {
                    _rb.velocity = new Vector2(0f, _rb.velocity.y);
                }
            }
        }

        /// <summary>触发受伤效果</summary>
        /// <param name="damageSource">伤害来源位置，用于计算击退方向</param>
        public void OnDamaged(Vector3 damageSource)
        {
            TriggerFlash();
            TriggerKnockback(damageSource);
        }

        private void TriggerFlash()
        {
            if (!TryResolveRenderer()) return;
            _renderer.color = _flashColor;
            _flashTimer = _flashDuration;
            _isFlashing = true;
        }

        private void TriggerKnockback(Vector3 damageSource)
        {
            if (_rb == null || _rb.bodyType != RigidbodyType2D.Dynamic) return;

            var direction = (transform.position - damageSource).normalized;
            direction.y = 0f; // 只在 X 轴击退
            if (direction.sqrMagnitude < 0.01f)
                direction.x = _rb.transform.localScale.x > 0 ? 1f : -1f;

            var knockbackVelocity = direction * _knockbackForce;
            _rb.velocity = new Vector2(knockbackVelocity.x, _rb.velocity.y);
            _knockbackTimer = _knockbackDuration;
        }
    }
}
