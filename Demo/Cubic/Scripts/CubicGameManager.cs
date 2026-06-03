using UnityEngine;
using EssSystem.Core.Base;
using Demo.Cubic.Player;

namespace Demo.Cubic
{
    /// <summary>
    /// Cubic 游戏主入口管理器
    /// 继承 AbstractGameManager，使用框架提供的 Manager 系统
    /// <para>
    /// 注意：所有的玩家生成逻辑都在 PlayerController.SpawnPlayer 中，
    /// 本类只负责准备配置并调用。
    /// </para>
    /// </summary>
    public class CubicGameManager : AbstractGameManager
    {
        [Header("游戏设置")]
        [SerializeField] private CubicCharacterClass _initialClass = CubicCharacterClass.Warrior;
        [SerializeField] private Vector3 _playerSpawnPos = new Vector3(-5, 0, 0);

        [Header("敌人生成")]
        [SerializeField] private CubicCharacterClass _enemyClass = CubicCharacterClass.Mage;
        [SerializeField] private string _enemySkillId = "cubic_mage_fireball";
        [SerializeField] private int _maxEnemies = 5;
        [SerializeField] private float _enemySpawnInterval = 3f;
        [SerializeField] private float _enemySpawnX = 10f;

        [Header("地图")]
        [SerializeField] private Map.CubicMap _map;

        protected override void Awake()
        {
            base.Awake();
            Debug.Log("[CubicGameManager] Cubic 游戏初始化...");
        }

        private void Start()
        {
            EnsureMap();
            InitializeSystems();
            CallSpawnPlayer();
        }

        private void EnsureMap()
        {
            if (_map == null)
            {
                _map = gameObject.AddComponent<Map.CubicMap>();
            }
        }

        private void InitializeSystems()
        {
            Skill.CubicSkillRegistry.Initialize();
            VFX.CubicVFXManager.Initialize();
        }

        /// <summary>
        /// 唯一的生成入口 - 调用 PlayerController.SpawnPlayer
        /// </summary>
        private void CallSpawnPlayer()
        {
            var config = new PlayerController.SpawnConfig
            {
                parent = null,
                position = _playerSpawnPos,
                jobClass = _initialClass,
                map = _map,
                enemyClass = _enemyClass,
                enemySkillId = _enemySkillId,
                maxEnemies = _maxEnemies,
                enemySpawnInterval = _enemySpawnInterval,
                enemySpawnX = _enemySpawnX
            };

            PlayerController.SpawnPlayer(config);
        }

        public PlayerController GetPlayer() => PlayerController.Instance;

        public Entity.CubicEntity GetPlayerEntity() => PlayerController.Instance?.GetEntity();

        public void SpawnEnemy()
        {
            if (_map == null) return;
            var spawner = gameObject.GetComponent<Entity.EnemySpawner>();
            spawner?.SpawnEnemy();
        }

        public void SpawnEnemy(CubicCharacterClass jobClass, string skillId)
        {
            if (_map == null) return;
            var spawner = gameObject.GetComponent<Entity.EnemySpawner>();
            spawner?.SpawnEnemy(jobClass, skillId);
        }
    }
}
