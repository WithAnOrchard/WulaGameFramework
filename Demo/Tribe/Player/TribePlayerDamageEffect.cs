using UnityEngine;

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 玩家击退效果组件 —— velocity-based 击退（dynamic Rigidbody2D 必须）。
    /// <para>闪烁效果改由框架 <c>IFlashEffect</c> 统一处理 —— 通过 <c>entity.CanFlash(root)</c> 注册，
    /// 走 Flash shader 同时点亮全部子 SpriteRenderer，这里不再实现。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribePlayerDamageEffect : MonoBehaviour
    {
        [Header("Knockback")]
        [SerializeField] private float _knockbackForce = 15f;
        [SerializeField] private float _knockbackDuration = 0.2f;

        private Rigidbody2D _rb;
        private float _knockbackTimer;

        /// <summary>是否正在击退中（外部读取以暂停移动输入）。</summary>
        public bool IsKnockbacking => _knockbackTimer > 0f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            if (_knockbackTimer > 0f)
            {
                _knockbackTimer -= Time.deltaTime;
                if (_knockbackTimer <= 0f && _rb != null)
                {
                    _rb.velocity = new Vector2(0f, _rb.velocity.y);
                }
            }
        }

        /// <summary>触发击退 —— 由框架 <c>IKnockbackEffect</c> 适配器调用，sourcePos 由 EntityService 解析。</summary>
        public void ApplyKnockback(Vector3 damageSource)
        {
            if (_rb == null || _rb.bodyType != RigidbodyType2D.Dynamic) return;

            var direction = ((Vector3)transform.position - damageSource).normalized;
            direction.y = 0f; // 只在 X 轴击退
            if (direction.sqrMagnitude < 0.01f)
                direction.x = _rb.transform.localScale.x > 0 ? 1f : -1f;

            var knockbackVelocity = direction * _knockbackForce;
            _rb.velocity = new Vector2(knockbackVelocity.x, _rb.velocity.y);
            _knockbackTimer = _knockbackDuration;
        }
    }
}
