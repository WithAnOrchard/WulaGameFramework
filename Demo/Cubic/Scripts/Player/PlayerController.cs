using UnityEngine;
using Demo.Cubic.Entities;
using Demo.Cubic.Map;
using Demo.Cubic.Skill;
using Demo.Cubic.Utils;

namespace Demo.Cubic.Player
{
    /// <summary>
    /// 玩家控制器（3D 伪 2D 版）。
    /// <para>
    /// <b>3D 物理改造点</b>（相对 2D 版）：
    /// <list type="bullet">
    /// <item><c>Rigidbody2D.gravityScale</c> → 引擎全局 gravity（3D），不再单体重力倍率</item>
    /// <item><c>OnCollisionEnter2D</c> → <c>OnCollisionEnter</c>，参数 <see cref="Collision"/>，取 <c>collision.collider</c> 调 <see cref="GroundIdentifier.IsGround(Collider)"/></item>
    /// <item><c>transform.localScale.x</c> 控制朝向改为 <c>transform.rotation.y</c>（180° 翻转）或继续用 scale.x ≥ 0（兼容 CubicEntity.CastSkill 判朝向）</item>
    /// <item>跳跃走 <see cref="CubicEntity.Jump"/>，地面检测 <c>_isGrounded</c> 在 <c>OnCollisionEnter/Exit</c> 里维护</item>
    /// </list>
    /// </para>
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("移动设置")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _jumpVelocity = 8f;

        [Header("攻击设置")]
        [SerializeField] private string _defaultSkillId = "cubic_warrior_slash";
        [SerializeField] private KeyCode _attackKey = KeyCode.J;
        [SerializeField] private KeyCode _skill1Key = KeyCode.K;
        [SerializeField] private KeyCode _skill2Key = KeyCode.L;

        private CubicEntity _entity;
        private Rigidbody _rigidbody;
        private bool _isGrounded;
        private int _facingDirection = 1;

        private static PlayerController _instance;
        public static PlayerController Instance => _instance;

        private void Awake()
        {
            _instance = this;
            _entity = GetComponent<CubicEntity>();
            _rigidbody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_entity == null || _entity.IsDead) return;
            HandleMovement();
            HandleJump();
            HandleAttack();
        }

        private void HandleMovement()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(horizontal) > 0.1f)
            {
                _facingDirection = horizontal > 0 ? 1 : -1;
                // 用 Y 轴 180° 旋转做朝向 —— 不用负 scale,避免 3D BoxCollider 负 scale 警告
                // （scale 翻负会强制矫正 collider 尺寸,导致贴墙/卡地/掉出场景）
                transform.rotation = Quaternion.Euler(0f, _facingDirection > 0 ? 0f : 180f, 0f);
            }
            _entity.Move(horizontal * _moveSpeed);
        }

        private void HandleJump()
        {
            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            {
                _entity.Jump(_jumpVelocity);
                _isGrounded = false;
            }
        }

        private void HandleAttack()
        {
            if (Input.GetKeyDown(_attackKey))
            {
                _entity.CastSkill(_defaultSkillId);
            }
            if (Input.GetKeyDown(_skill1Key))
            {
                var skillId = _entity.GetSkillId(0);
                if (!string.IsNullOrEmpty(skillId))
                    _entity.CastSkill(skillId);
            }
            if (Input.GetKeyDown(_skill2Key))
            {
                var skillId = _entity.GetSkillId(1);
                if (!string.IsNullOrEmpty(skillId))
                    _entity.CastSkill(skillId);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            // 取碰撞点法线判定"是否踩到上面"，避免碰到侧墙也算落地
            foreach (var contact in collision.contacts)
            {
                if (Vector3.Dot(contact.normal, Vector3.up) > 0.5f &&
                    GroundIdentifier.IsGround(contact.otherCollider))
                {
                    _isGrounded = true;
                    return;
                }
            }
            if (GroundIdentifier.IsGround(collision.collider)) _isGrounded = true;
        }

        private void OnCollisionExit(Collision collision)
        {
            if (GroundIdentifier.IsGround(collision.collider)) _isGrounded = false;
        }

        public int GetFacingDirection() => _facingDirection;

        public void Initialize(CubicCharacterClass jobClass)
        {
            _entity.SetJobClass(jobClass);

            int learned = 0;
            foreach (var skillId in CubicSkillRegistry.GetClassSkills(jobClass))
            {
                _entity.AddSkill(skillId);
                learned++;
            }

            Debug.Log($"[PlayerController] 玩家初始化完成，职业: {jobClass}，已学 {learned} 个技能");
        }

        public CubicEntity GetEntity() => _entity;

        // ============================================
        // 静态生成方法 - 所有生成逻辑都在这里
        // ============================================

        private static bool _playerSpawned = false;

        public class SpawnConfig
        {
            public Transform parent;
            public Vector3 position;
            public CubicCharacterClass jobClass = CubicCharacterClass.Warrior;
            public Map.CubicMap map;

            public CubicCharacterClass enemyClass = CubicCharacterClass.Mage;
            public string enemySkillId = "cubic_mage_fireball";
            public int maxEnemies = 5;
            public float enemySpawnInterval = 3f;
            public float enemySpawnX = 10f;
        }

        public static PlayerController SpawnPlayer(SpawnConfig config)
        {
            if (_playerSpawned)
            {
                Debug.LogWarning("[PlayerController] 玩家已生成，跳过重复生成");
                return _instance;
            }
            _playerSpawned = true;

            Vector3 spawnPos = config.position;
            // 信任 config.position.y 由调用方（CubicGameManager）算好 —— 之前这里二次覆盖到 GetGroundY()+1 导致玩家浮空 1 米
            if (config.map == null)
            {
                Debug.LogWarning("[PlayerController] SpawnConfig.map == null，玩家 Y 不会自动贴地");
            }

            var player = Spawn(config.parent, spawnPos, config.jobClass);

            GameObject managerObj = config.parent != null ? config.parent.gameObject : null;
            EnemySpawner spawner = null;

            if (managerObj != null)
            {
                spawner = managerObj.GetComponent<EnemySpawner>();
                if (spawner == null) spawner = managerObj.AddComponent<EnemySpawner>();
            }
            else
            {
                managerObj = new GameObject("EnemySpawner");
                spawner = managerObj.AddComponent<EnemySpawner>();
            }

            spawner.SetEnemyClass(config.enemyClass);
            spawner.SetEnemySkill(config.enemySkillId);
            spawner.SetMaxEnemies(config.maxEnemies);
            spawner.SetSpawnInterval(config.enemySpawnInterval);
            spawner.SetSpawnX(config.enemySpawnX);

            if (config.map != null) spawner.SetSpawnY(config.map.GetGroundY() + 1f);

            Debug.Log($"[PlayerController] 玩家和敌人生成器已就位: {config.jobClass}");
            return player;
        }

        private static void CleanupExistingPlayers()
        {
            var existingPlayers = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
            foreach (var p in existingPlayers)
            {
                if (p != null && p.gameObject != null) DestroyImmediate(p.gameObject);
            }
            var allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude);
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.name.Contains("Player")) DestroyImmediate(obj);
            }
        }

        public static void ResetSpawnFlag()
        {
            _playerSpawned = false;
        }

        // ============================================
        // 内部 Spawn 实现
        // ============================================

        private static PlayerController Spawn(Transform parent, Vector3 position, CubicCharacterClass jobClass)
        {
            var playerObj = CreatePlayerPrefab(jobClass);
            playerObj.transform.position = position;

            if (parent != null) playerObj.transform.SetParent(parent);

            var controller = playerObj.GetComponent<PlayerController>();
            controller.Initialize(jobClass);
            return controller;
        }

        /// <summary>
        /// 创建玩家预制体：低多边形 cube（共享 Cube mesh + 职业色 URP/Lit 材质）。
        /// </summary>
        private static GameObject CreatePlayerPrefab(CubicCharacterClass jobClass)
        {
            var playerObj = new GameObject($"Player_{jobClass}");

            // 物理：3D Rigidbody + BoxCollider（CubicEntity.Awake 会锁 Z 轴 + 设 collision detection）
            var rb = playerObj.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.05f;

            var collider = playerObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 0.8f, 0.8f);
            collider.center = new Vector3(0f, 0.4f, 0f);   // 立方体半高 0.4f 中心抬高，避免穿地

            // 视觉：低多边形 cube（共享 mesh，材质按 color 缓存）
            var visual = Cubic3DStyle.CreateLowPolyCube(
                "Visual",
                CubicClassColors.GetColor(jobClass),
                new Vector3(0.8f, 0.8f, 0.8f)
            );
            visual.transform.SetParent(playerObj.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            // 占位朝向指示（往前伸一个小立方体表示"面朝方向"）
            var nose = Cubic3DStyle.CreateLowPolyCube(
                "Nose",
                CubicClassColors.GetColor(jobClass) * 0.7f,
                new Vector3(0.18f, 0.18f, 0.5f)
            );
            nose.transform.SetParent(playerObj.transform, false);
            nose.transform.localPosition = new Vector3(0.3f, 0.5f, 0f);

            playerObj.transform.localScale = Vector3.one;   // 朝向改用 scale.x 翻转，初始 = 1

            playerObj.AddComponent<CubicEntity>();
            playerObj.AddComponent<PlayerController>();

            return playerObj;
        }

        private void OnApplicationQuit()
        {
            ResetSpawnFlag();
        }
    }
}
