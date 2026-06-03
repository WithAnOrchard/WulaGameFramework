using UnityEngine;
using Demo.Cubic.Entity;
using Demo.Cubic.Map;
using Demo.Cubic.Skill;

namespace Demo.Cubic.Player
{
    /// <summary>
    /// 玩家控制器
    /// 管理玩家的输入、移动、攻击，以及玩家生成（所有生成逻辑都在这里）
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("移动设置")]
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _jumpForce = 10f;

        [Header("攻击设置")]
        [SerializeField] private string _defaultSkillId = "cubic_warrior_slash";
        [SerializeField] private KeyCode _attackKey = KeyCode.J;
        [SerializeField] private KeyCode _skill1Key = KeyCode.K;
        [SerializeField] private KeyCode _skill2Key = KeyCode.L;

        private CubicEntity _entity;
        private Rigidbody2D _rigidbody;
        private bool _isGrounded = false;
        private int _facingDirection = 1;

        private static PlayerController _instance;
        public static PlayerController Instance => _instance;

        private void Awake()
        {
            _instance = this;
            _entity = GetComponent<CubicEntity>();
            _rigidbody = GetComponent<Rigidbody2D>();
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
                transform.localScale = new Vector3(
                    Mathf.Abs(transform.localScale.x) * _facingDirection,
                    transform.localScale.y,
                    transform.localScale.z
                );
            }
            _entity.Move(horizontal * _moveSpeed);
        }

        private void HandleJump()
        {
            if (Input.GetKeyDown(KeyCode.Space) && _isGrounded)
            {
                _rigidbody.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
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

        /// <summary>
        /// 把 SpriteRenderer 切到 Cubic/SpriteLit 材质，自带 Z 轴光照 + 边缘 halo。
        /// </summary>
        private static void ApplyLitSpriteMaterial(SpriteRenderer r)
        {
            Shader sh = Shader.Find("Cubic/SpriteLit");
            if (sh == null) sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh == null) sh = Shader.Find("Sprites/Diffuse");
            if (sh != null) r.sharedMaterial = new Material(sh);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (GroundIdentifier.IsGround(collision.collider))
            {
                _isGrounded = true;
            }
        }

        public int GetFacingDirection() => _facingDirection;

        public void Initialize(CubicCharacterClass jobClass)
        {
            _entity.SetJobClass(jobClass);
            CubicSkillRegistry.GrantDefaultSkillsToEntity(_entity, jobClass);
            Debug.Log($"[PlayerController] 玩家初始化完成，职业: {jobClass}");
        }

        public CubicEntity GetEntity() => _entity;

        // ============================================
        // 静态生成方法 - 所有生成逻辑都在这里
        // ============================================

        private static bool _playerSpawned = false;

        /// <summary>
        /// 生成玩家配置
        /// </summary>
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

        /// <summary>
        /// 完整的玩家生成流程（包括清理、创建、敌人生成器）
        /// 唯一的对外接口 - CubicGameManager 只调用此方法
        /// </summary>
        public static PlayerController SpawnPlayer(SpawnConfig config)
        {
            if (_playerSpawned)
            {
                Debug.LogWarning("[PlayerController] 玩家已生成，跳过重复生成");
                return _instance;
            }
            _playerSpawned = true;

            // 1. 清理场景中所有现有的 Player
            CleanupExistingPlayers();

            // 2. 计算生成位置
            Vector3 spawnPos = config.position;
            if (config.map != null)
            {
                spawnPos.y = config.map.GetGroundY() + 1f;
            }

            // 3. 创建玩家
            var player = Spawn(config.parent, spawnPos, config.jobClass);

            // 4. 创建敌人生成器
            GameObject managerObj = config.parent != null ? config.parent.gameObject : null;
            Entity.EnemySpawner spawner = null;

            if (managerObj != null)
            {
                spawner = managerObj.GetComponent<Entity.EnemySpawner>();
                if (spawner == null)
                {
                    spawner = managerObj.AddComponent<Entity.EnemySpawner>();
                }
            }
            else
            {
                // 没有父对象则创建一个临时 GameObject
                managerObj = new GameObject("EnemySpawner");
                spawner = managerObj.AddComponent<Entity.EnemySpawner>();
            }

            spawner.SetEnemyClass(config.enemyClass);
            spawner.SetEnemySkill(config.enemySkillId);
            spawner.SetMaxEnemies(config.maxEnemies);
            spawner.SetSpawnInterval(config.enemySpawnInterval);
            spawner.SetSpawnX(config.enemySpawnX);

            if (config.map != null)
            {
                spawner.SetSpawnY(config.map.GetGroundY() + 1f);
            }

            Debug.Log($"[PlayerController] 玩家和敌人生成器已就位: {config.jobClass}");
            return player;
        }

        /// <summary>
        /// 清理场景中所有现有的 Player（使用 DestroyImmediate 立即生效）
        /// </summary>
        private static void CleanupExistingPlayers()
        {
            var existingPlayers = FindObjectsOfType<PlayerController>();
            Debug.Log($"[PlayerController] 检查到 {existingPlayers.Length} 个 PlayerController");
            foreach (var p in existingPlayers)
            {
                if (p != null && p.gameObject != null)
                {
                    Debug.Log($"[PlayerController] 销毁旧 Player: {p.gameObject.name}");
                    DestroyImmediate(p.gameObject);
                }
            }

            // 同时销毁名字含 "Player" 的所有对象（包括 Player_Cube 等）
            var allObjects = FindObjectsOfType<GameObject>();
            foreach (var obj in allObjects)
            {
                if (obj != null && obj.name.Contains("Player"))
                {
                    Debug.Log($"[PlayerController] 销毁名字含 Player 的对象: {obj.name}");
                    DestroyImmediate(obj);
                }
            }
        }

        /// <summary>
        /// 重置生成标志（退出 Play 模式时调用）
        /// </summary>
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

            if (parent != null)
            {
                playerObj.transform.SetParent(parent);
            }

            var controller = playerObj.GetComponent<PlayerController>();
            controller.Initialize(jobClass);
            return controller;
        }

        private static GameObject CreatePlayerPrefab(CubicCharacterClass jobClass)
        {
            var playerObj = new GameObject($"Player_{jobClass}");

            var rb = playerObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;

            var collider = playerObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 0.8f);

            var renderer = playerObj.AddComponent<SpriteRenderer>();
            renderer.color = CubicClassColors.GetColor(jobClass);
            renderer.sortingOrder = 10;
            ApplyLitSpriteMaterial(renderer);   // 让玩家接收 LightManager 灯光

            int size = 32;
            var texture = new Texture2D(size, size);
            Color32[] colors = new Color32[size * size];
            for (int i = 0; i < size * size; i++) colors[i] = Color.white;
            texture.SetPixels32(colors);
            texture.Apply();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

            playerObj.transform.localScale = new Vector3(1.5f, 1.5f, 1);

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
