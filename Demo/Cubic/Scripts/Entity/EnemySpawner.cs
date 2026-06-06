using UnityEngine;
using System.Collections.Generic;
using Demo.Cubic.Utils;

namespace Demo.Cubic.Entities
{
    /// <summary>
    /// 敌人生成器（3D 伪 2D 版）。
    /// <para>
    /// 3D 化改造点：默认预制体改用低多边形 cube（<see cref="Cubic3DStyle.CreateLowPolyCube"/>），
    /// 物理换 3D <see cref="Rigidbody"/> + <see cref="BoxCollider"/>。
    /// </para>
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("生成设置")]
        [SerializeField] private CubicCharacterClass _enemyClass = CubicCharacterClass.Mage;
        [SerializeField] private string _enemySkillId = "cubic_mage_fireball";
        [SerializeField] private int _maxEnemies = 5;
        [SerializeField] private float _spawnInterval = 3f;

        [Header("生成位置")]
        [SerializeField] private float _spawnX = 10f;
        [SerializeField] private float _spawnY = 0f;

        [Header("预制体")]
        [SerializeField] private GameObject _enemyPrefab;

        private float _spawnTimer = 0f;
        private int _spawnedCount = 0;
        private List<GameObject> _activeEnemies = new List<GameObject>();

        private void Update()
        {
            UpdateSpawning();
            CleanupDeadEnemies();
        }

        private void UpdateSpawning()
        {
            if (_spawnedCount >= _maxEnemies) return;

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer >= _spawnInterval)
            {
                _spawnTimer = 0f;
                SpawnEnemy();
            }
        }

        private void CleanupDeadEnemies()
        {
            _activeEnemies.RemoveAll(enemy => enemy == null);
        }

        public GameObject SpawnEnemy()
        {
            if (_enemyPrefab == null) _enemyPrefab = CreateDefaultEnemyPrefab();

            var enemyObj = Instantiate(_enemyPrefab, new Vector3(_spawnX, _spawnY, 0f), Quaternion.identity);
            _activeEnemies.Add(enemyObj);

            var enemy = enemyObj.GetComponent<CubicEnemy>();
            if (enemy != null)
            {
                enemy.SetJobClass(_enemyClass);
                enemy.InitializeEnemy(_enemySkillId);
            }

            _spawnedCount++;
            Debug.Log($"[EnemySpawner] 敌人已生成 #{_spawnedCount}: {_enemyClass}");
            return enemyObj;
        }

        public GameObject SpawnEnemy(CubicCharacterClass jobClass, string skillId)
        {
            var previousClass = _enemyClass;
            var previousSkill = _enemySkillId;

            _enemyClass = jobClass;
            _enemySkillId = skillId;

            var enemy = SpawnEnemy();

            _enemyClass = previousClass;
            _enemySkillId = previousSkill;
            return enemy;
        }

        /// <summary>默认敌人预制体：低多边形 cube + 3D 物理。</summary>
        private GameObject CreateDefaultEnemyPrefab()
        {
            var enemyObj = new GameObject("Enemy_Cubic");

            var rb = enemyObj.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.05f;

            var collider = enemyObj.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.8f, 0.8f, 0.8f);
            collider.center = new Vector3(0f, 0.4f, 0f);

            // 视觉：低多边形 cube + 略深一档的鼻锥
            var visual = Cubic3DStyle.CreateLowPolyCube(
                "Visual",
                CubicClassColors.GetColor(_enemyClass),
                new Vector3(0.8f, 0.8f, 0.8f)
            );
            visual.transform.SetParent(enemyObj.transform, false);
            visual.transform.localPosition = new Vector3(0f, 0.4f, 0f);

            var nose = Cubic3DStyle.CreateLowPolyCube(
                "Nose",
                CubicClassColors.GetColor(_enemyClass) * 0.7f,
                new Vector3(0.18f, 0.18f, 0.5f)
            );
            nose.transform.SetParent(enemyObj.transform, false);
            nose.transform.localPosition = new Vector3(0.3f, 0.5f, 0f);

            enemyObj.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);

            enemyObj.AddComponent<CubicEnemy>();
            return enemyObj;
        }

        public List<GameObject> GetActiveEnemies()
        {
            _activeEnemies.RemoveAll(e => e == null);
            return _activeEnemies;
        }

        public int GetEnemyCount()
        {
            _activeEnemies.RemoveAll(e => e == null);
            return _activeEnemies.Count;
        }

        public void SetEnemyClass(CubicCharacterClass jobClass) => _enemyClass = jobClass;
        public void SetEnemySkill(string skillId) => _enemySkillId = skillId;
        public void SetSpawnInterval(float interval) => _spawnInterval = Mathf.Max(0.1f, interval);
        public void SetMaxEnemies(int maxCount) => _maxEnemies = Mathf.Max(0, maxCount);
        public void SetSpawnX(float x) => _spawnX = x;
        public void SetSpawnY(float y) => _spawnY = y;
    }
}
