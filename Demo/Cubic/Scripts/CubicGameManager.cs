using UnityEngine;
using EssSystem.Core.Base;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Presentation.CameraManager;
using EssSystem.Core.Presentation.EffectsManager;
using EssSystem.Core.Presentation.LightManager;
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
        [Tooltip("玩家初始 X 坐标（Y 会被 map.GetGroundY() 覆盖为贴地高度，Z 始终=0）")]
        [SerializeField] private float _playerSpawnX = -5f;

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
            EnsureFrameworkManager<SkillManager>("SkillManager");
            EnsureFrameworkManager<EntityManager>("EntityManager");
            EnsureFrameworkManager<CameraManager>("CameraManager");
            EnsureFrameworkManager<EffectsManager>("EffectsManager");
            EnsureFrameworkManager<LightManager>("LightManager");
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

        /// <summary>
        /// 确保框架 Manager 存在 —— CubicSkillRegistry / CubicVFXManager.Initialize()
        /// 会通过 EventProcessor 触发对应 [Event] 方法，事件处理器挂在这些 Manager 上；
        /// 场景里没有该组件时 EventProcessor 解析不到 Target，会 NRE。
        /// </summary>
        private void EnsureFrameworkManager<T>(string goName) where T : MonoBehaviour
        {
            if (GetComponentInChildren<T>(true) != null) return;
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            go.AddComponent<T>();
        }

        private void InitializeSystems()
        {
            Skill.CubicSkillRegistry.Initialize();
            VFX.CubicVFXManager.Initialize();
        }

        /// <summary>
        /// 唯一的生成入口 - 调用 PlayerController.SpawnPlayer
        /// <para>玩家 X 用 _playerSpawnX（Inspector 可调），Y 走 map.GetGroundY()（贴地），Z 强制 0（横版 2.5D 锁定 Z）。</para>
        /// </summary>
        private void CallSpawnPlayer()
        {
            // 兜底：如果 _map 还是 null，主动拿一遍（防止 EnsureMap 时机问题）
            if (_map == null) _map = gameObject.GetComponent<Map.CubicMap>() ?? gameObject.AddComponent<Map.CubicMap>();
            float groundY = _map != null ? _map.GetGroundY() : 0f;
            var spawnPos = new Vector3(_playerSpawnX, groundY, 0f);
            string mapName = _map != null ? _map.name : "<null>";
            Debug.Log($"[CubicGameManager] 玩家 spawn 位置 → {spawnPos} (groundY={groundY}, map={mapName})");

            var config = new PlayerController.SpawnConfig
            {
                parent = null,
                position = spawnPos,
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

        public Entities.CubicEntity GetPlayerEntity() => PlayerController.Instance?.GetEntity();
        public void SpawnEnemy()
        {
            if (_map == null) return;
            var spawner = gameObject.GetComponent<Entities.EnemySpawner>();
            spawner?.SpawnEnemy();
        }

        public void SpawnEnemy(CubicCharacterClass jobClass, string skillId)
        {
            if (_map == null) return;
            var spawner = gameObject.GetComponent<Entities.EnemySpawner>();
            spawner?.SpawnEnemy(jobClass, skillId);
        }
    }
}
