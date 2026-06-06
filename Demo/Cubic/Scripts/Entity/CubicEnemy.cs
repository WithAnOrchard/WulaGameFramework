using UnityEngine;

namespace Demo.Cubic.Entities
{
    /// <summary>
    /// 怪物 AI 控制器（3D 伪 2D 版）。
    /// <para>
    /// AI 状态机逻辑（巡逻 / 追击 / 攻击 / 撤退）和 2D 版完全一致，只是把"水平方向"由 X 轴统一驱动：
    /// <see cref="CubicEntity.Move(float)"/> 已经会把水平速度写到 3D Rigidbody.linearVelocity.x。
    /// 朝向同样靠 <c>transform.localScale.x</c> 的正负切换（与 2D 版一致，方便 <see cref="CubicEntity.CastSkill"/> 判方向）。
    /// </para>
    /// </summary>
    public class CubicEnemy : CubicEntity
    {
        [Header("AI设置")]
        [SerializeField] private float _detectRange = 8f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _chaseSpeed = 4f;
        [SerializeField] private float _patrolRadius = 5f;

        private Transform _player;
        private Vector3 _spawnPosition;
        private Vector3 _patrolTarget;
        private AIState _currentState = AIState.Patrol;
        private float _stateTimer = 0f;
        private float _attackCooldown = 0f;

        public enum AIState
        {
            Idle,
            Patrol,
            Chase,
            Attack,
            Retreat
        }

        public override void Awake()
        {
            base.Awake();
            _spawnPosition = transform.position;
            _patrolTarget = GetRandomPatrolPoint();
        }

        public override void Start()
        {
            base.Start();
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        protected override void Update()
        {
            base.Update();
            if (IsDead) return;

            _attackCooldown = Mathf.Max(0, _attackCooldown - Time.deltaTime);

            UpdateAI();

            if (_player != null)
            {
                float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
                if (distanceToPlayer <= _attackRange && _attackCooldown <= 0)
                {
                    AttackPlayer();
                }
            }
        }

        private void UpdateAI()
        {
            if (_player == null)
            {
                SetState(AIState.Patrol);
                return;
            }

            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
            float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);

            if (distanceToPlayer <= _attackRange)
                SetState(AIState.Attack);
            else if (distanceToPlayer <= _detectRange)
                SetState(AIState.Chase);
            else if (distanceToSpawn > _patrolRadius)
            {
                SetState(AIState.Patrol);
                _patrolTarget = _spawnPosition;
            }
            else
                SetState(AIState.Patrol);

            ExecuteCurrentState();
        }

        private void ExecuteCurrentState()
        {
            switch (_currentState)
            {
                case AIState.Idle:     Idle(); break;
                case AIState.Patrol:   Patrol(); break;
                case AIState.Chase:    Chase(); break;
                case AIState.Attack:   Attack(); break;
                case AIState.Retreat:  Retreat(); break;
            }
        }

        private void Idle()
        {
            Move(0);
            _stateTimer -= Time.deltaTime;
            if (_stateTimer <= 0)
            {
                _patrolTarget = GetRandomPatrolPoint();
                SetState(AIState.Patrol);
            }
        }

        private void Patrol()
        {
            Vector3 direction = (_patrolTarget - transform.position).normalized;
            Move(direction.x * _patrolSpeed);

            if (Vector3.Distance(transform.position, _patrolTarget) < 0.5f)
            {
                _patrolTarget = GetRandomPatrolPoint();
                _stateTimer = Random.Range(1f, 3f);
                SetState(AIState.Idle);
            }
        }

        private void Chase()
        {
            if (_player == null) return;

            Vector3 direction = (_player.position - transform.position).normalized;
            Move(direction.x * _chaseSpeed);

            float distance = Vector3.Distance(transform.position, _player.position);
            if (distance > _detectRange * 1.5f)
            {
                SetState(AIState.Patrol);
                _patrolTarget = _spawnPosition;
            }
        }

        private void Attack()
        {
            if (_player == null) return;

            Move(0);

            if (_attackCooldown <= 0 && _skillIds.Count > 0)
            {
                // 朝玩家方向（横版只取 X），用 Y 轴 180° 旋转代替负 scale 翻面，避免 BoxCollider 负 scale 警告
                var dir = (_player.position - transform.position);
                if (Mathf.Abs(dir.x) > 0.01f)
                {
                    transform.rotation = Quaternion.Euler(0f, dir.x > 0f ? 0f : 180f, 0f);
                }
                dir.y = 0f;
                CastSkill(_skillIds[0], dir.normalized);
                _attackCooldown = Random.Range(1.5f, 3f);
            }
        }

        private void Retreat()
        {
            Vector3 direction = (transform.position - _player.position).normalized;
            Move(direction.x * _chaseSpeed);

            float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);
            if (distanceToSpawn < 1f) SetState(AIState.Idle);
        }

        private void AttackPlayer()
        {
            if (_player == null) return;

            var playerEntity = _player.GetComponent<CubicEntity>();
            if (playerEntity == null) return;

            var stats = CubicJobStats.GetStats(JobClass);
            float damage = Random.Range(stats.AttackPower * 0.8f, stats.AttackPower * 1.2f);
            playerEntity.TakeDamage(damage, transform.position);
        }

        private void SetState(AIState newState)
        {
            if (_currentState != newState)
            {
                _currentState = newState;
                OnStateChanged(newState);
            }
        }

        private void OnStateChanged(AIState state)
        {
            _stateTimer = Random.Range(0.5f, 1.5f);
        }

        private Vector3 GetRandomPatrolPoint()
        {
            return _spawnPosition + new Vector3(
                Random.Range(-_patrolRadius, _patrolRadius),
                0,
                0
            );
        }

        public void InitializeEnemy(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId)) AddSkill(skillId);
        }

        public AIState GetCurrentState() => _currentState;
    }
}
