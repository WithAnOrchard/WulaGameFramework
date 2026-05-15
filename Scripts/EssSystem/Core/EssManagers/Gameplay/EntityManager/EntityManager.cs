using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config;
// 本文件不 <c>using</c> CharacterManager——跨模块调用一律走 EventProcessor。

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager
{
    /// <summary>
    /// Entity 门面 —— 场景里的单例 MonoBehaviour，负责生命周期 + 默认模板注册 + 每帧 Tick。
    /// <para>
    /// 业务层直接使用 <see cref="EntityService"/>（通过 <see cref="Service"/> 或 <c>EntityService.Instance</c>）。
    /// </para>
    /// <para>
    /// <b>加载顺序</b>：<c>[Manager(13)]</c> 在 <c>CharacterManager(11)</c> 与 <c>MapManager(12)</c> 之后，
    /// 以确保创建 Entity 时 CharacterService 已就绪。
    /// </para>
    /// </summary>
    [Manager(13)]
    public class EntityManager : Manager<EntityManager>
    {
        // ─── Event 名常量（定义方）—— 跨模块调用走 bare-string（§4.1 方案 B）
        /// <summary>创建 Entity。data: [configId, instanceId, parent(Transform?), worldPosition(Vector3?)].
        /// 返回 ResultCode.Ok(Transform CharacterRoot) / Fail。
        /// E2: 返回 Unity 原生 Transform 而非模块私有 Entity 类型（§2 协议解耦）。</summary>
        public const string EVT_CREATE_ENTITY  = "CreateEntity";
        /// <summary>销毁 Entity。data: [instanceId]. 返回 ResultCode.Ok(instanceId) / Fail。</summary>
        public const string EVT_DESTROY_ENTITY = "DestroyEntity";
        public const string EVT_REGISTER_SCENE_ENTITY = "RegisterSceneEntity";
        public const string EVT_DAMAGE_ENTITY = "DamageEntity";

        /// <summary>注册 Entity 配置（模板）。data: [EntityConfig config]. → Ok(configId)/Fail.</summary>
        public const string EVT_REGISTER_ENTITY_CONFIG = "RegisterEntityConfig";
        /// <summary>查询 Entity 实例。data: [string instanceId]. → Ok(Entity)/Fail.</summary>
        public const string EVT_GET_ENTITY = "GetEntity";
        /// <summary>给 GameObject 应用 Collider。data: [GameObject host, EntityColliderConfig cfg]. → Ok(host)/Fail.</summary>
        public const string EVT_APPLY_COLLIDER = "ApplyCollider";
        /// <summary>挂载 EntityHandle 桥接。data: [GameObject host, Entity entity]. → Ok(host)/Fail.</summary>
        public const string EVT_ATTACH_ENTITY_HANDLE = "AttachEntityHandle";

        #region Inspector

        [Header("Default Templates")]
        [Tooltip("是否启动时注册内置示例 Entity 配置（WarriorEntity / MageEntity）；业务侧可用同 ConfigId 覆盖。")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [Header("Tick")]
        [Tooltip("是否每帧自动调用 EntityService.Tick（同步位置 / 未来的 AI 驱动）。关掉后由业务层自行 tick。")]
        [SerializeField] private bool _autoTick = true;

        #endregion

        public EntityService Service => EntityService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates)
            {
                // 只注册 Entity 配置；Character 外观（含 Tree variants）由 CharacterManager 自己注册
                Service.RegisterConfig(DefaultEntityConfigs.BuildWarriorEntity());    // Dynamic
                Service.RegisterConfig(DefaultEntityConfigs.BuildMageEntity());       // Dynamic
                Service.RegisterConfig(DefaultEntityConfigs.BuildSmallTreeEntity());  // Static
                Service.RegisterConfig(DefaultEntityConfigs.BuildMediumTreeEntity()); // Static
            }

            Log("EntityManager 初始化完成", Color.green);
        }

        protected override void Update()
        {
            base.Update(); // Inspector 节流 + Logging 同步
            if (_autoTick && Service != null) Service.Tick(Time.deltaTime);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        #endregion

        #region Event Methods

        /// <summary>
        /// 创建 Entity 的事件入口 —— 外部模块必须走本 Event，不应直接引用 <see cref="EntityService"/>。
        /// <para>data: <c>[configId:string, instanceId:string, parent:Transform?, worldPosition:Vector3?]</c>。
        /// 后两项可省略或传 <c>null</c>。</para>
        /// </summary>
        // E1: 遵项目规范 “[Event] 动词开头”，去除 OnEvent 前缀。Service 上同名 typed helper 重载 OK。
        [Event(EVT_CREATE_ENTITY)]
        public List<object> CreateEntity(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效：需要 [configId, instanceId, parent?, worldPosition?]");

            var configId   = data[0] as string;
            var instanceId = data[1] as string;
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(instanceId))
                return ResultCode.Fail("configId / instanceId 不能为空");

            var parent        = data.Count > 2 ? data[2] as Transform : null;
            var worldPosition = data.Count > 3 && data[3] is Vector3 v ? (Vector3?)v : null;

            var entity = Service.CreateEntity(configId, instanceId, parent, worldPosition);
            if (entity == null)
                return ResultCode.Fail($"创建 Entity 失败: configId={configId}, instanceId={instanceId}");
            // E2: 返回 Unity 原生 Transform（与 CharacterManager.EVT_CREATE_CHARACTER 一致）不暴露 Entity 私有类型。
            // 静态 entity 无 CharacterRoot 时也返 Ok（以 null 作负载，表示创建成功但无显示）。
            return ResultCode.Ok(entity.CharacterRoot);
        }

        /// <summary>
        /// 销毁 Entity 的事件入口。data: <c>[instanceId:string]</c>。
        /// </summary>
        // E1: 去除 OnEvent 前缀
        [Event(EVT_DESTROY_ENTITY)]
        public List<object> DestroyEntity(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 1) return ResultCode.Fail("参数无效：需要 [instanceId]");

            var instanceId = data[0] as string;
            if (string.IsNullOrEmpty(instanceId)) return ResultCode.Fail("instanceId 不能为空");

            return Service.DestroyEntity(instanceId)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"Entity 不存在: {instanceId}");
        }

        [Event(EVT_REGISTER_SCENE_ENTITY)]
        public List<object> RegisterSceneEntity(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 3) return ResultCode.Fail("参数无效：需要 [instanceId, GameObject, EntityRuntimeDefinition]");

            var instanceId = data[0] as string;
            var host = data[1] as GameObject;
            var definition = data[2] as EntityRuntimeDefinition;
            if (string.IsNullOrEmpty(instanceId) || host == null || definition == null)
                return ResultCode.Fail("instanceId / GameObject / EntityRuntimeDefinition 不能为空");

            var entity = Service.CreateSceneEntity(instanceId, host, definition);
            return entity != null ? ResultCode.Ok(entity.CharacterRoot) : ResultCode.Fail($"注册场景 Entity 失败: {instanceId}");
        }

        [Event(EVT_DAMAGE_ENTITY)]
        public List<object> DamageEntity(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效：需要 [instanceId, damage]");

            var instanceId = data[0] as string;
            var target = Service.GetEntity(instanceId);
            if (target == null) return ResultCode.Fail($"Entity 不存在: {instanceId}");

            var damage = System.Convert.ToSingle(data[1]);
            var damageType = data.Count > 2 ? data[2] as string : null;
            var sourcePos = data.Count > 3 && data[3] is Vector3 sp ? sp : (Vector3?)null;
            var dealt = Service.TryDamage(target, damage, null, damageType, sourcePos);
            return ResultCode.Ok(dealt);
        }

        [Event(EVT_REGISTER_ENTITY_CONFIG)]
        public List<object> RegisterEntityConfig(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 1 || !(data[0] is EntityConfig cfg))
                return ResultCode.Fail("参数无效：需要 [EntityConfig]");
            Service.RegisterConfig(cfg);
            return ResultCode.Ok(cfg.ConfigId);
        }

        [Event(EVT_GET_ENTITY)]
        public List<object> GetEntityEvent(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 1) return ResultCode.Fail("参数无效：需要 [instanceId]");
            var instanceId = data[0] as string;
            var entity = Service.GetEntity(instanceId);
            return entity != null ? ResultCode.Ok(entity) : ResultCode.Fail($"Entity 不存在: {instanceId}");
        }

        [Event(EVT_APPLY_COLLIDER)]
        public List<object> ApplyColliderEvent(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效：需要 [GameObject host, EntityColliderConfig cfg]");
            var host = data[0] as GameObject;
            var cfg = data[1] as EntityColliderConfig;
            if (host == null || cfg == null) return ResultCode.Fail("host / cfg 不能为空");
            EntityService.ApplyCollider(host, cfg);
            return ResultCode.Ok(host);
        }

        [Event(EVT_ATTACH_ENTITY_HANDLE)]
        public List<object> AttachEntityHandleEvent(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效：需要 [GameObject host, Entity entity]");
            var host = data[0] as GameObject;
            var entity = data[1] as Entity;
            if (host == null || entity == null) return ResultCode.Fail("host / entity 不能为空");
            EntityService.AttachEntityHandle(host, entity);
            return ResultCode.Ok(host);
        }

        public static void ApplyRuntimeCollider(GameObject host, EntityColliderConfig cfg)
        {
            if (host == null || cfg == null || cfg.Shape == EntityColliderShape.None) return;
            switch (cfg.Shape)
            {
                case EntityColliderShape.Box:
                {
                    var box = host.GetComponent<BoxCollider2D>();
                    if (box == null) box = host.AddComponent<BoxCollider2D>();
                    box.size = cfg.Size;
                    box.offset = cfg.Offset;
                    box.isTrigger = cfg.IsTrigger;
                    break;
                }
                case EntityColliderShape.Circle:
                {
                    var circle = host.GetComponent<CircleCollider2D>();
                    if (circle == null) circle = host.AddComponent<CircleCollider2D>();
                    circle.radius = Mathf.Max(0.01f, cfg.Size.x);
                    circle.offset = cfg.Offset;
                    circle.isTrigger = cfg.IsTrigger;
                    break;
                }
            }
        }

        #endregion
    }
}
