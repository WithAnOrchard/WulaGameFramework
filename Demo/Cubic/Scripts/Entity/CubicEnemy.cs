using UnityEngine;

namespace Demo.Cubic.Entity
{
    /// <summary>
    /// 怪物AI控制器
    /// 基于 EntityManager 和 Utility AI 实现敌人智能行为
    /// </summary>
    public class CubicEnemy : CubicEntity
    {
        [Header("AI设置")]
        [SerializeField] private float _detectRange = 8f;
        [SerializeField] private float _attackRange = 2f;
        [SerializeField] private float _patrolSpeed = 2f;
        [SerializeField] private float _chaseSpeed = 4f;
        [SerializeField] private float _patrolRadius = 5f;

        [Header("行为权重")]
        [SerializeField] private float _wanderWeight = 0.3f;
        [SerializeField] private float _chaseWeight = 0.6f;
        [SerializeField] private float _attackWeight = 0.8f;

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

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        }

        private void Update()
        {
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

        /// <summary>
        /// 更新AI状态机
        /// </summary>
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
            {
                SetState(AIState.Attack);
            }
            else if (distanceToPlayer <= _detectRange)
            {
                SetState(AIState.Chase);
            }
            else if (distanceToSpawn > _patrolRadius)
            {
                SetState(AIState.Patrol);
                _patrolTarget = _spawnPosition;
            }
            else
            {
                SetState(AIState.Patrol);
            }

            ExecuteCurrentState();
        }

        /// <summary>
        /// 执行当前状态行为
        /// </summary>
        private void ExecuteCurrentState()
        {
            switch (_currentState)
            {
                case AIState.Idle:
                    Idle();
                    break;
                case AIState.Patrol:
                    Patrol();
                    break;
                case AIState.Chase:
                    Chase();
                    break;
                case AIState.Attack:
                    Attack();
                    break;
                case AIState.Retreat:
                    Retreat();
                    break;
            }
        }

        /// <summary>
        /// 待机行为
        /// </summary>
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

        /// <summary>
        /// 巡逻行为
        /// </summary>
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

        /// <summary>
        /// 追逐行为
        /// </summary>
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

        /// <summary>
        /// 攻击行为
        /// </summary>
        private void Attack()
        {
            if (_player == null) return;

            Move(0);

            if (_attackCooldown <= 0 && _skillIds.Count > 0)
            {
                CastSkill(_skillIds[0]);
                _attackCooldown = Random.Range(1.5f, 3f);
            }
        }

        /// <summary>
        /// 撤退行为
        /// </summary>
        private void Retreat()
        {
            Vector3 direction = (transform.position - _player.position).normalized;
            Move(direction.x * _chaseSpeed);

            float distanceToSpawn = Vector3.Distance(transform.position, _spawnPosition);
            if (distanceToSpawn < 1f)
            {
                SetState(AIState.Idle);
            }
        }

        /// <summary>
        /// 攻击玩家
        /// </summary>
        private void AttackPlayer()
        {
            if (_player == null) return;

            var playerEntity = _player.GetComponent<CubicEntity>();
            if (playerEntity != null)
            {
                float damage = Random.Range(PhysicalAttack * 0.8f, PhysicalAttack * 1.2f);
                playerEntity.TakeDamage(damage, transform.position);
            }
        }

        /// <summary>
        /// 设置AI状态
        /// </summary>
        private void SetState(AIState newState)
        {
            if (_currentState != newState)
            {
                _currentState = newState;
                OnStateChanged(newState);
            }
        }

        /// <summary>
        /// 状态变更回调
        /// </summary>
        private void OnStateChanged(AIState state)
        {
            _stateTimer = Random.Range(0.5f, 1.5f);
        }

        /// <summary>
        /// 获取随机巡逻点
        /// </summary>
        private Vector3 GetRandomPatrolPoint()
        {
            return _spawnPosition + new Vector3(
                Random.Range(-_patrolRadius, _patrolRadius),
                0,
                0
            );
        }

        /// <summary>
        /// 初始化怪物
        /// </summary>
        public void InitializeEnemy(string skillId)
        {
            if (!string.IsNullOrEmpty(skillId))
            {
                AddSkill(skillId);
            }
        }

        public AIState GetCurrentState()
        {
            return _currentState;
        }
    }
}
