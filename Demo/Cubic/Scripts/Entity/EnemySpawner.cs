using UnityEngine;
using System.Collections.Generic;

namespace Demo.Cubic.Entity
{
    /// <summary>
    /// 敌人生成器
    /// 负责创建敌人实体并初始化AI
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

        /// <summary>
        /// 更新生成逻辑
        /// </summary>
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

        /// <summary>
        /// 清理已死亡的敌人
        /// </summary>
        private void CleanupDeadEnemies()
        {
            _activeEnemies.RemoveAll(enemy => enemy == null);
        }

        /// <summary>
        /// 生成敌人
        /// </summary>
        public GameObject SpawnEnemy()
        {
            if (_enemyPrefab == null)
            {
                _enemyPrefab = CreateDefaultEnemyPrefab();
            }

            var enemyObj = Instantiate(_enemyPrefab, new Vector3(_spawnX, _spawnY, 0), Quaternion.identity);
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

        /// <summary>
        /// 生成指定职业的敌人
        /// </summary>
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

        /// <summary>
        /// 创建默认敌人预制体
        /// </summary>
        private GameObject CreateDefaultEnemyPrefab()
        {
            var enemyObj = new GameObject("Enemy_Cubic");

            var rb = enemyObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            
            var collider = enemyObj.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(0.8f, 0.8f);

            var renderer = enemyObj.AddComponent<SpriteRenderer>();
            renderer.color = CubicClassColors.GetColor(_enemyClass);
            renderer.sortingOrder = 10;
            ApplyLitSpriteMaterial(renderer);   // 让敌人接收 LightManager 灯光

            int size = 32;
            var texture = new Texture2D(size, size);
            Color32[] colors = new Color32[size * size];
            for (int i = 0; i < size * size; i++)
            {
                colors[i] = Color.white;
            }
            texture.SetPixels32(colors);
            texture.Apply();
            renderer.sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);

            enemyObj.transform.localScale = new Vector3(1.2f, 1.2f, 1);

            enemyObj.AddComponent<CubicEnemy>();

            return enemyObj;
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

        /// <summary>
        /// 获取所有存活的敌人
        /// </summary>
        public List<GameObject> GetActiveEnemies()
        {
            _activeEnemies.RemoveAll(e => e == null);
            return _activeEnemies;
        }

        /// <summary>
        /// 获取敌人数量
        /// </summary>
        public int GetEnemyCount()
        {
            _activeEnemies.RemoveAll(e => e == null);
            return _activeEnemies.Count;
        }

        /// <summary>
        /// 设置敌人职业
        /// </summary>
        public void SetEnemyClass(CubicCharacterClass jobClass)
        {
            _enemyClass = jobClass;
        }

        /// <summary>
        /// 设置敌人技能
        /// </summary>
        public void SetEnemySkill(string skillId)
        {
            _enemySkillId = skillId;
        }

        /// <summary>
        /// 设置生成间隔
        /// </summary>
        public void SetSpawnInterval(float interval)
        {
            _spawnInterval = Mathf.Max(0.1f, interval);
        }

        /// <summary>
        /// 设置最大敌人数量
        /// </summary>
        public void SetMaxEnemies(int maxCount)
        {
            _maxEnemies = Mathf.Max(0, maxCount);
        }

        /// <summary>
        /// 设置生成X坐标
        /// </summary>
        public void SetSpawnX(float x)
        {
            _spawnX = x;
        }

        /// <summary>
        /// 设置生成Y坐标
        /// </summary>
        public void SetSpawnY(float y)
        {
            _spawnY = y;
        }

        /// <summary>
        /// 清理所有敌人
        /// </summary>
        public void ClearAllEnemies()
        {
            foreach (var enemy in _activeEnemies)
            {
                if (enemy != null)
                {
                    Destroy(enemy);
                }
            }
            _activeEnemies.Clear();
        }

        /// <summary>
        /// 重置生成计数
        /// </summary>
        public void ResetSpawnCount()
        {
            _spawnedCount = 0;
        }
    }
}
