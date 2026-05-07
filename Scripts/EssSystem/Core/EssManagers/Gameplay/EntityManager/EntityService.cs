using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config;
// 碰撞体类型都在 UnityEngine 命名空间 —— 已由 `using UnityEngine;` 覆盖
// 本模块不 <c>using</c> 任何 CharacterManager 依赖——跨模块调用一律走 EventProcessor。

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager
{
    /// <summary>
    /// Entity 业务服务 —— 直接 C# API。
    /// <list type="bullet">
    /// <item>持久化 <see cref="EntityConfig"/>（<see cref="CAT_CONFIGS"/>）</item>
    /// <item>仅内存 <see cref="Entity"/> 运行时实例（<see cref="CAT_INSTANCES"/>）</item>
    /// <item>需显示 Entity 时通过 <c>EVT_CREATE_CHARACTER</c> / <c>EVT_DESTROY_CHARACTER</c> 调 CharacterManager，
    /// <b>不直接引用 CharacterService</b>（跨模块解耦）。</item>
    /// </list>
    /// </summary>
    public class EntityService : Service<EntityService>
    {
        #region 数据分类

        /// <summary>持久化：所有 <see cref="EntityConfig"/></summary>
        public const string CAT_CONFIGS = "Configs";

        /// <summary>仅内存：所有运行时 <see cref="Entity"/> 实例（未写盘）</summary>
        public const string CAT_INSTANCES = "Entities";

        #endregion

        /// <summary>实例字典属于运行时态（持有 Unity Transform 引用），绝不持久化。
        /// 否则下次 Play 加载会得到一份 <c>CharacterRoot</c> 已被 Unity 销毁但 C# 引用仍在的"僵尸 Entity"，
        /// 导致 <see cref="CreateEntity"/> 命中重复并跳过 EVT_CREATE_CHARACTER → 没有 GameObject。</summary>
        protected override bool IsTransientCategory(string category) => category == CAT_INSTANCES;

        protected override void Initialize()
        {
            base.Initialize();
            Log("EntityService 初始化完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region Config Management

        /// <summary>注册或覆盖配置（持久化）。</summary>
        public void RegisterConfig(EntityConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空配置或缺 ConfigId 的配置");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册 Entity 配置: {config.ConfigId} ({config.DisplayName})", Color.blue);
        }

        public EntityConfig GetConfig(string configId) =>
            string.IsNullOrEmpty(configId) ? null : GetData<EntityConfig>(CAT_CONFIGS, configId);

        public IEnumerable<EntityConfig> GetAllConfigs()
        {
            if (!_dataStorage.TryGetValue(CAT_CONFIGS, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is EntityConfig c) yield return c;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Instance Management

        /// <summary>取运行时实例。</summary>
        public Entity GetEntity(string instanceId) =>
            string.IsNullOrEmpty(instanceId) ? null : GetData<Entity>(CAT_INSTANCES, instanceId);

        /// <summary>枚举所有运行时实例。</summary>
        public IEnumerable<Entity> GetAllEntities()
        {
            if (!_dataStorage.TryGetValue(CAT_INSTANCES, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is Entity e) yield return e;
        }

        /// <summary>
        /// 实例化 Entity —— 主线程调用。
        /// <para>
        /// 若 <see cref="EntityConfig.CharacterConfigId"/> 非空，会通过 <c>EVT_CREATE_CHARACTER</c>
        /// 事件让 CharacterManager 创建一个 Character 作为显示，
        /// 其 <c>InstanceId</c> 使用 Entity 的同一 <paramref name="instanceId"/>，方便双向映射。
        /// </para>
        /// <para>重复 <paramref name="instanceId"/> 不会重建，返回已有实例。</para>
        /// </summary>
        public Entity CreateEntity(string configId, string instanceId, Transform parent = null, Vector3? worldPosition = null)
        {
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(instanceId))
            {
                LogWarning("CreateEntity 参数无效");
                return null;
            }

            if (HasData(CAT_INSTANCES, instanceId))
            {
                // spawn: 前缀属于 MapManager 装饰器派生的确定性 id —— 多次入队是正常 idempotent 路径，
                // 不应当作错误（已被 EntitySpawnService 上游去重，此处仅作兜底）。
                if (!instanceId.StartsWith("spawn:", System.StringComparison.Ordinal))
                    LogWarning($"Entity 实例 {instanceId} 已存在，忽略重复创建");
                return GetEntity(instanceId);
            }

            var config = GetConfig(configId);
            if (config == null)
            {
                LogWarning($"Entity 配置不存在: {configId}");
                return null;
            }

            var entity = new Entity
            {
                InstanceId = instanceId,
                ConfigId = configId,
                Config = config,
                Kind = config.Kind,
                WorldPosition = worldPosition ?? Vector3.zero,
            };

            // 解析用于显示的 CharacterConfig ID —— variants 非空则随机挑一个，否则回落到单值
            var charConfigId = PickCharacterConfigId(config);
            if (!string.IsNullOrEmpty(charConfigId))
            {
                // 跨模块调用：走 Event，不引用 CharacterService
                var createResult = EventProcessor.HasInstance
                    ? EventProcessor.Instance.TriggerEventMethod(
                        "CreateCharacter",   // = CharacterManager.EVT_CREATE_CHARACTER
                        new List<object> { charConfigId, instanceId, parent, worldPosition ?? Vector3.zero })
                    : null;

                if (ResultCode.IsOk(createResult) && createResult.Count >= 2 && createResult[1] is Transform root)
                {
                    entity.CharacterInstanceId = instanceId;
                    entity.CharacterRoot       = root;
                }
                else
                {
                    var msg = createResult != null && createResult.Count >= 2 ? createResult[1] : "<no event handler>";
                    LogWarning($"创建显示 Character 失败（CharacterConfigId={charConfigId}）：{msg}");
                }
            }

            // 碰撞体挂到 Character 根 GameObject 上（无 Character 则跳过）
            if (config.Collider != null && config.Collider.Shape != EntityColliderShape.None &&
                entity.CharacterRoot != null)
            {
                ApplyCollider(entity.CharacterRoot.gameObject, config.Collider);
            }

            // 世界空间偏移 —— 走 EVT_MOVE_CHARACTER（与缩放解耦）
            if (config.SpawnOffset != Vector3.zero && !string.IsNullOrEmpty(entity.CharacterInstanceId)
                && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEventMethod(
                    "MoveCharacter",   // = CharacterManager.EVT_MOVE_CHARACTER
                    new List<object> { entity.CharacterInstanceId, config.SpawnOffset });
            }

            // 仅内存（不走 SetData，避免写盘）
            if (!_dataStorage.ContainsKey(CAT_INSTANCES))
                _dataStorage[CAT_INSTANCES] = new Dictionary<string, object>();
            _dataStorage[CAT_INSTANCES][instanceId] = entity;

            Log($"创建 Entity 实例: {instanceId} (config={configId})", Color.green);
            return entity;
        }

        /// <summary>从 <paramref name="config"/> 解析本次创建用的 CharacterConfig ID —— variants 非空则随机挑一个。</summary>
        private static string PickCharacterConfigId(EntityConfig config)
        {
            if (config.CharacterConfigVariants != null && config.CharacterConfigVariants.Length > 0)
            {
                var idx = UnityEngine.Random.Range(0, config.CharacterConfigVariants.Length);
                var picked = config.CharacterConfigVariants[idx];
                if (!string.IsNullOrEmpty(picked)) return picked;
            }
            return config.CharacterConfigId;
        }

        /// <summary>
        /// 按 <paramref name="cfg"/> 在 <paramref name="host"/> 上挂 2D Collider。不重复挂。
        /// <para>cfg.Size / Offset 以**世界 tile**为单位（1 = 1 tile）。如果 host（或其祖先）有非 1 缩放，
        /// 这里会用 <c>lossyScale</c> 反向补偿，确保 config 写多大世界里就多大，与 RootScale 解耦。</para>
        /// <para>注意：Unity 组件的 <c>==</c> 是重载的"假 null"，必须用 <c>if (x == null)</c> 判断，不能用 <c>??</c>。</para>
        /// </summary>
        private static void ApplyCollider(GameObject host, EntityColliderConfig cfg)
        {
            // 反向补偿世界缩放——这样 cfg.Size 真正代表世界 tile 数（与 CharacterConfig.RootScale 解耦）
            var ls = host.transform.lossyScale;
            var sx = Mathf.Approximately(ls.x, 0f) ? 1f : 1f / ls.x;
            var sy = Mathf.Approximately(ls.y, 0f) ? 1f : 1f / ls.y;
            var localSize   = new Vector2(cfg.Size.x   * sx, cfg.Size.y   * sy);
            var localOffset = new Vector2(cfg.Offset.x * sx, cfg.Offset.y * sy);

            switch (cfg.Shape)
            {
                case EntityColliderShape.Box:
                {
                    var box = host.GetComponent<BoxCollider2D>();
                    if (box == null) box = host.AddComponent<BoxCollider2D>();
                    box.size = localSize;
                    box.offset = localOffset;
                    box.isTrigger = cfg.IsTrigger;
                    break;
                }
                case EntityColliderShape.Circle:
                {
                    var circle = host.GetComponent<CircleCollider2D>();
                    if (circle == null) circle = host.AddComponent<CircleCollider2D>();
                    // Circle radius 用 X 方向缩放补偿（CircleCollider2D 没有椭圆形，沿用 X 比例最稳）
                    circle.radius = Mathf.Max(0.01f, cfg.Size.x * sx);
                    circle.offset = localOffset;
                    circle.isTrigger = cfg.IsTrigger;
                    break;
                }
            }
        }

        /// <summary>销毁 Entity + 级联 DetachAllCapabilities + 级联销毁 Character。</summary>
        public bool DestroyEntity(string instanceId)
        {
            var e = GetEntity(instanceId);
            if (e == null) return false;

            // 先卸能力，再销毁 Character —— 能力可能在 OnDetach 里用到 Character
            e.DetachAllCapabilities();

            if (!string.IsNullOrEmpty(e.CharacterInstanceId) && EventProcessor.HasInstance)
            {
                EventProcessor.Instance.TriggerEventMethod(
                    "DestroyCharacter",   // = CharacterManager.EVT_DESTROY_CHARACTER
                    new List<object> { e.CharacterInstanceId });
            }
            e.CharacterInstanceId = null;
            e.CharacterRoot       = null;

            if (_dataStorage.TryGetValue(CAT_INSTANCES, out var dict))
                dict.Remove(instanceId);

            Log($"销毁 Entity 实例: {instanceId}", Color.yellow);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Capability 交互辅助

        /// <summary>
        /// 框架级伤害结算入口 —— 统一处理 <see cref="IInvulnerable"/> 拦截后再转发给 <see cref="IDamageable"/>。
        /// 所有走攻击流水的地方建议走这个而不是直接调 <c>target.Get&lt;IDamageable&gt;().TakeDamage</c>，
        /// 否则会绕过无敌检查。
        /// </summary>
        /// <returns>实际造成的伤害（未命中 / 无敌 / 无 IDamageable 均返回 0）。</returns>
        public float TryDamage(Entity target, float amount, Entity source = null, string damageType = null)
        {
            if (target == null || amount <= 0f) return 0f;

            // 无敌短路
            var inv = target.Get<IInvulnerable>();
            if (inv != null && inv.Active) return 0f;

            var dmg = target.Get<IDamageable>();
            if (dmg == null) return 0f;

            return dmg.TakeDamage(amount, source, damageType);
        }

        /// <summary>便捷查询：该 Entity 当前是否"可被攻击"（有 IDamageable 且未无敌）。</summary>
        public bool IsAttackable(Entity target)
        {
            if (target == null) return false;
            if (!target.Has<IDamageable>()) return false;
            var inv = target.Get<IInvulnerable>();
            return inv == null || !inv.Active;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Tick / Sync

        /// <summary>
        /// 每帧 tick —— 由 <see cref="EntityManager"/> 在 Update 中调用。
        /// 静态实体跳过位置同步（位置不可变，省掉 transform 赋值开销）。
        /// 后续 AI / 物理 / 状态机等在此分支驱动。
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_dataStorage.TryGetValue(CAT_INSTANCES, out var dict)) return;

            foreach (var kv in dict)
            {
                if (!(kv.Value is Entity e)) continue;

                // TODO: AI / 物理 / 状态机…（仅 Dynamic）

                // 位置同步到显示层（仅 Dynamic）—— 直接用缓存的 Transform（Unity 原生类型，非跨模块耦合）
                if (e.Kind == EntityKind.Dynamic && e.CharacterRoot != null)
                {
                    if (e.CharacterRoot.position != e.WorldPosition) e.CharacterRoot.position = e.WorldPosition;
                }
            }
        }

        #endregion
    }
}
