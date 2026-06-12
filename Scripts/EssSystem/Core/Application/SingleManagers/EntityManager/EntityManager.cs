using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;
using EssSystem.Core.Application.SingleManagers.EntityManager.Runtime;
using EssSystem.Core.Foundation.DataManager.RuntimeConfig;
// 本文件不 <c>using</c> CharacterManager——跨模块调用一律走 EventProcessor。

namespace EssSystem.Core.Application.SingleManagers.EntityManager
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
        private const string DEFAULT_ENTITY_CONFIG_PATH = "Framework/Entity/default_entity.json";
        // ─── Event 名常量（定义方）—— 跨模块调用走 bare-string（§4.1 方案 B）
        /// <summary>创建 Entity。data: [configId, instanceId, parent(Transform?), worldPosition(Vector3?)].
        /// 返回 ResultCode.Ok(Transform CharacterRoot) / Fail。
        /// E2: 返回 Unity 原生 Transform 而非模块私有 Entity 类型（§2 协议解耦）。</summary>
        public const string EVT_CREATE_ENTITY  = "CreateEntity";
        /// <summary>销毁 Entity。data: [instanceId]. 返回 ResultCode.Ok(instanceId) / Fail。</summary>
        public const string EVT_DESTROY_ENTITY = "DestroyEntity";
        public const string EVT_REGISTER_SCENE_ENTITY = "RegisterSceneEntity";
        public const string EVT_DAMAGE_ENTITY = "DamageEntity";
        public const string EVT_HEAL_ENTITY = "HealEntity";
        public const string EVT_GET_ENTITY_POSITION = "GetEntityPosition";
        public const string EVT_SET_ENTITY_POSITION = "SetEntityPosition";
        public const string EVT_GET_CHARACTER_ROOT = "GetCharacterRoot";
        public const string EVT_GET_ENTITY_ID_FROM_OBJECT = "GetEntityIdFromObject";
        public const string EVT_IS_ENTITY_DEAD = "IsEntityDead";
        public const string EVT_GET_ENTITY_HP = "GetEntityHp";
        public const string EVT_GET_CONTROL_STATE = "GetControlState";
        public const string EVT_PUSH_CONTROL_STATE = "PushControlState";
        public const string EVT_POP_CONTROL_STATE = "PopControlState";
        public const string EVT_GET_SPEED_MULTIPLIER = "GetSpeedMultiplier";
        public const string EVT_SET_SPEED_MULTIPLIER = "SetSpeedMultiplier";
        public const string EVT_GET_DAMAGE_REDUCTION = "GetDamageReduction";
        public const string EVT_SET_DAMAGE_REDUCTION = "SetDamageReduction";
        public const string EVT_REGISTER_DAMAGED_CALLBACK = "RegisterDamagedCallback";
        public const string EVT_REGISTER_DEATH_CALLBACK = "RegisterDeathCallback";
        public const string EVT_SET_ENTITY_MAX_HP = "SetEntityMaxHp";
        public const string EVT_GET_ENTITY_RESOURCE = "GetEntityResource";
        public const string EVT_SET_ENTITY_RESOURCE = "SetEntityResource";
        public const string EVT_CONSUME_ENTITY_RESOURCE = "ConsumeEntityResource";
        public const string EVT_RESTORE_ENTITY_RESOURCE = "RestoreEntityResource";
        public const string EVT_ADD_ENTITY_CAPABILITY = "AddEntityCapability";

        /// <summary>注册 Entity 配置（模板）。data: [EntityConfig config]. → Ok(configId)/Fail.</summary>
        public const string EVT_REGISTER_ENTITY_CONFIG = "RegisterEntityConfig";
        public const string EVT_REGISTER_SIMPLE_ENTITY_CONFIG = "RegisterSimpleEntityConfig";
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
                RegisterDefaultsFromJson();
            }

            Log("EntityManager 初始化完成", Color.green);
        }

        private void RegisterDefaultsFromJson()
        {
            if (Service == null) return;

            if (!RuntimeConfigLoader.TryLoadJson(
                    DEFAULT_ENTITY_CONFIG_PATH,
                    out EntityDefaultConfigFile file,
                    msg => Log(msg, Color.gray)) || file == null)
            {
                Log($"Entity default config not found: {DEFAULT_ENTITY_CONFIG_PATH}", Color.yellow);
                return;
            }

            foreach (var config in file.EntityConfigs ?? new List<EntityConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.ConfigId)) continue;
                Service.RegisterConfig(config);
            }
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
            var source = data.Count > 3 && data[3] is string sourceId ? Service.GetEntity(sourceId) : null;
            var sourcePos = data.Count > 4 && data[4] is Vector3 sp
                ? sp
                : data.Count > 3 && data[3] is Vector3 legacySp
                    ? (Vector3?)legacySp
                    : null;
            var suppressHitSfx = data.Count > 5 && data[5] is bool suppress && suppress;
            var dealt = Service.TryDamage(target, damage, source, damageType, sourcePos, suppressHitSfx);
            return ResultCode.Ok(dealt);
        }

        [Event(EVT_HEAL_ENTITY)]
        public List<object> HealEntity(List<object> data)
        {
            var target = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (target == null || data == null || data.Count < 2) return ResultCode.Fail("HealEntity invalid args");
            var dmg = target.Get<IDamageable>();
            if (dmg == null) return ResultCode.Fail("Entity has no IDamageable");
            var amount = System.Convert.ToSingle(data[1]);
            var source = data.Count > 2 && data[2] is string sourceId ? Service.GetEntity(sourceId) : null;
            return ResultCode.Ok(dmg.Heal(amount, source));
        }

        [Event(EVT_GET_ENTITY_POSITION)]
        public List<object> GetEntityPosition(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (e == null) return ResultCode.Fail("Entity not found");
            return ResultCode.Ok(e.CharacterRoot != null ? e.CharacterRoot.position : e.WorldPosition);
        }

        [Event(EVT_SET_ENTITY_POSITION)]
        public List<object> SetEntityPosition(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (e == null || data == null || data.Count < 2 || !(data[1] is Vector3 pos))
                return ResultCode.Fail("SetEntityPosition invalid args");
            e.WorldPosition = pos;
            if (e.CharacterRoot != null)
            {
                e.CharacterRoot.position = pos;
                var rb = e.CharacterRoot.GetComponent<Rigidbody>();
                if (rb != null) rb.linearVelocity = Vector3.zero;
            }
            return ResultCode.Ok(pos);
        }

        [Event(EVT_GET_CHARACTER_ROOT)]
        public List<object> GetCharacterRoot(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            return e != null ? ResultCode.Ok(e.CharacterRoot) : ResultCode.Fail("Entity not found");
        }

        [Event(EVT_GET_ENTITY_ID_FROM_OBJECT)]
        public List<object> GetEntityIdFromObject(List<object> data)
        {
            if (data == null || data.Count < 1) return ResultCode.Fail("GetEntityIdFromObject invalid args");
            EntityHandle handle = null;
            if (data[0] is Collider col) handle = col.GetComponentInParent<EntityHandle>();
            else if (data[0] is Collider2D col2) handle = col2.GetComponentInParent<EntityHandle>();
            else if (data[0] is GameObject go) handle = go.GetComponentInParent<EntityHandle>();
            else if (data[0] is Transform tr) handle = tr.GetComponentInParent<EntityHandle>();
            return handle != null && !string.IsNullOrEmpty(handle.InstanceId)
                ? ResultCode.Ok(handle.InstanceId)
                : ResultCode.Fail("EntityHandle not found");
        }

        [Event(EVT_IS_ENTITY_DEAD)]
        public List<object> IsEntityDead(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (e == null) return ResultCode.Fail("Entity not found");
            var dmg = e.Get<IDamageable>();
            return ResultCode.Ok(dmg == null || dmg.IsDead);
        }

        [Event(EVT_GET_ENTITY_HP)]
        public List<object> GetEntityHp(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var dmg = e?.Get<IDamageable>();
            return dmg != null
                ? new List<object> { ResultCode.OK, dmg.CurrentHp, dmg.MaxHp, dmg.IsDead }
                : ResultCode.Fail("Entity has no IDamageable");
        }

        [Event(EVT_GET_CONTROL_STATE)]
        public List<object> GetControlState(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var ctrl = e?.Get<IControllable>();
            return ctrl != null
                ? new List<object> { ResultCode.OK, ctrl.Stunned, ctrl.Silenced }
                : new List<object> { ResultCode.OK, false, false };
        }

        [Event(EVT_PUSH_CONTROL_STATE)]
        public List<object> PushControlState(List<object> data) => ChangeControlState(data, true);

        [Event(EVT_POP_CONTROL_STATE)]
        public List<object> PopControlState(List<object> data) => ChangeControlState(data, false);

        private List<object> ChangeControlState(List<object> data, bool push)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var ctrl = e?.Get<IControllable>();
            var state = data != null && data.Count > 1 ? data[1] as string : null;
            if (ctrl == null || string.IsNullOrEmpty(state)) return ResultCode.Fail("Control state not found");
            if (state == "Stun")
            {
                if (push) ctrl.PushStun(); else ctrl.PopStun();
            }
            else if (state == "Silence")
            {
                if (push) ctrl.PushSilence(); else ctrl.PopSilence();
            }
            else return ResultCode.Fail("Invalid control state");
            return ResultCode.Ok(state);
        }

        [Event(EVT_GET_SPEED_MULTIPLIER)]
        public List<object> GetSpeedMultiplier(List<object> data)
        {
            return TryGetSpeedAccess(data, out var value, out _)
                ? ResultCode.Ok(value)
                : ResultCode.Fail("SpeedMultiplier not found");
        }

        [Event(EVT_SET_SPEED_MULTIPLIER)]
        public List<object> SetSpeedMultiplier(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("SetSpeedMultiplier invalid args");
            if (!TryGetSpeedAccess(data, out _, out var setter)) return ResultCode.Fail("SpeedMultiplier not found");
            var value = Mathf.Max(0f, System.Convert.ToSingle(data[1]));
            setter(value);
            return ResultCode.Ok(value);
        }

        private bool TryGetSpeedAccess(List<object> data, out float value, out System.Action<float> setter)
        {
            value = 1f;
            setter = null;
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (e == null) return false;
            var speed = e.Get<ISpeedAffected>();
            if (speed != null)
            {
                value = speed.SpeedMultiplier;
                setter = v => speed.SpeedMultiplier = v;
                return true;
            }
            var mover = e.Get<IMovable>();
            if (mover is MovableComponent m1)
            {
                value = m1.SpeedMultiplier;
                setter = v => m1.SpeedMultiplier = v;
                return true;
            }
            if (mover is Rigidbody2DMoverComponent m2)
            {
                value = m2.SpeedMultiplier;
                setter = v => m2.SpeedMultiplier = v;
                return true;
            }
            return false;
        }

        [Event(EVT_GET_DAMAGE_REDUCTION)]
        public List<object> GetDamageReduction(List<object> data)
        {
            var dc = GetDamageableComponent(data);
            return dc != null ? ResultCode.Ok(dc.DamageReduction) : ResultCode.Fail("DamageReduction not found");
        }

        [Event(EVT_SET_DAMAGE_REDUCTION)]
        public List<object> SetDamageReduction(List<object> data)
        {
            var dc = GetDamageableComponent(data);
            if (dc == null || data == null || data.Count < 2) return ResultCode.Fail("DamageReduction not found");
            dc.DamageReduction = Mathf.Clamp01(System.Convert.ToSingle(data[1]));
            return ResultCode.Ok(dc.DamageReduction);
        }

        private DamageableComponent GetDamageableComponent(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            return e?.Get<IDamageable>() as DamageableComponent;
        }

        [Event(EVT_REGISTER_DAMAGED_CALLBACK)]
        public List<object> RegisterDamagedCallback(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var callback = data != null && data.Count > 1
                ? data[1] as System.Action<string, string, float, string>
                : null;
            var dmg = e?.Get<IDamageable>();
            if (dmg == null || callback == null) return ResultCode.Fail("DamagedCallback invalid args");
            System.Action<Entity, Entity, float, string> handler = (self, src, dealt, damageType) =>
                callback(self?.InstanceId, src?.InstanceId, dealt, damageType);
            dmg.Damaged += handler;
            System.Action unsubscribe = () => dmg.Damaged -= handler;
            return ResultCode.Ok(unsubscribe);
        }

        [Event(EVT_REGISTER_DEATH_CALLBACK)]
        public List<object> RegisterDeathCallback(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var callback = data != null && data.Count > 1
                ? data[1] as System.Action<string, string>
                : null;
            var dmg = e?.Get<IDamageable>();
            if (dmg == null || callback == null) return ResultCode.Fail("DeathCallback invalid args");
            System.Action<Entity, Entity> handler = (self, killer) =>
                callback(self?.InstanceId, killer?.InstanceId);
            dmg.Died += handler;
            System.Action unsubscribe = () => dmg.Died -= handler;
            return ResultCode.Ok(unsubscribe);
        }

        [Event(EVT_SET_ENTITY_MAX_HP)]
        public List<object> SetEntityMaxHp(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (e == null || data == null || data.Count < 2) return ResultCode.Fail("SetEntityMaxHp invalid args");
            var maxHp = Mathf.Max(1f, System.Convert.ToSingle(data[1]));
            var refill = data.Count > 2 && data[2] is bool b && b;
            var dmg = e.Get<IDamageable>();
            if (dmg is DamageableComponent dc)
            {
                dc.SetMaxHp(maxHp, refill);
            }
            else
            {
                e.CanBeAttacked(maxHp);
            }
            return ResultCode.Ok(maxHp);
        }

        [Event(EVT_GET_ENTITY_RESOURCE)]
        public List<object> GetEntityResource(List<object> data)
        {
            var resources = GetEntityResources(data, out var resourceId);
            if (resources == null) return ResultCode.Fail("Entity resource not found");
            return new List<object>
            {
                ResultCode.OK,
                resources.GetCurrent(resourceId),
                resources.GetMax(resourceId),
                resources.GetRegen(resourceId)
            };
        }

        [Event(EVT_SET_ENTITY_RESOURCE)]
        public List<object> SetEntityResource(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var resourceId = data != null && data.Count > 1 ? data[1] as string : null;
            if (e == null || string.IsNullOrEmpty(resourceId) || data.Count < 3)
                return ResultCode.Fail("SetEntityResource invalid args");

            var resources = e.Get<IEntityResources>();
            if (resources == null)
            {
                resources = new EntityResourcesComponent();
                e.Add(resources);
            }

            var current = System.Convert.ToSingle(data[2]);
            if (!resources.Has(resourceId))
            {
                var max = data.Count > 3 ? System.Convert.ToSingle(data[3]) : current;
                var regen = data.Count > 4 ? System.Convert.ToSingle(data[4]) : 0f;
                resources.Define(resourceId, max, current, regen);
            }
            else
            {
                if (data.Count > 3)
                    resources.SetMax(resourceId, System.Convert.ToSingle(data[3]));
                resources.Set(resourceId, current);
            }

            return new List<object> { ResultCode.OK, resources.GetCurrent(resourceId), resources.GetMax(resourceId) };
        }

        [Event(EVT_CONSUME_ENTITY_RESOURCE)]
        public List<object> ConsumeEntityResource(List<object> data)
        {
            var resources = GetEntityResources(data, out var resourceId);
            if (resources == null || data == null || data.Count < 3)
                return ResultCode.Fail("ConsumeEntityResource invalid args");

            var amount = Mathf.Max(0f, System.Convert.ToSingle(data[2]));
            if (!resources.Consume(resourceId, amount))
                return ResultCode.Fail("Resource not enough");

            return new List<object> { ResultCode.OK, resources.GetCurrent(resourceId), resources.GetMax(resourceId) };
        }

        [Event(EVT_RESTORE_ENTITY_RESOURCE)]
        public List<object> RestoreEntityResource(List<object> data)
        {
            var resources = GetEntityResources(data, out var resourceId);
            if (resources == null || data == null || data.Count < 3)
                return ResultCode.Fail("RestoreEntityResource invalid args");

            var restored = resources.Restore(resourceId, Mathf.Max(0f, System.Convert.ToSingle(data[2])));
            return new List<object> { ResultCode.OK, restored, resources.GetCurrent(resourceId), resources.GetMax(resourceId) };
        }

        private IEntityResources GetEntityResources(List<object> data, out string resourceId)
        {
            resourceId = data != null && data.Count > 1 ? data[1] as string : null;
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            if (e == null || string.IsNullOrEmpty(resourceId)) return null;
            var resources = e.Get<IEntityResources>();
            return resources != null && resources.Has(resourceId) ? resources : null;
        }

        [Event(EVT_ADD_ENTITY_CAPABILITY)]
        public List<object> AddEntityCapability(List<object> data)
        {
            var e = Service?.GetEntity(data != null && data.Count > 0 ? data[0] as string : null);
            var capability = data != null && data.Count > 1 ? data[1] as string : null;
            if (e == null || string.IsNullOrEmpty(capability)) return ResultCode.Fail("AddEntityCapability invalid args");

            switch (capability)
            {
                case "ContactDamage":
                {
                    var damage = data.Count > 2 ? System.Convert.ToSingle(data[2]) : 0f;
                    var radius = data.Count > 3 ? System.Convert.ToSingle(data[3]) : 1f;
                    var interval = data.Count > 4 ? System.Convert.ToSingle(data[4]) : 1f;
                    var damageType = data.Count > 5 ? data[5] as string : "ContactDamage";
                    e.CanDamageOnContact(damage, radius, interval, damageType);
                    break;
                }
                case "Aura":
                {
                    var heal = data.Count > 2 ? System.Convert.ToSingle(data[2]) : 0f;
                    var radius = data.Count > 3 ? System.Convert.ToSingle(data[3]) : 1f;
                    var interval = data.Count > 4 ? System.Convert.ToSingle(data[4]) : 1f;
                    var includeSelf = data.Count > 5 && data[5] is bool b && b;
                    e.EmitAura(heal, radius, interval, default, includeSelf);
                    break;
                }
                case "Harvest":
                {
                    var itemId = data.Count > 2 ? data[2] as string : null;
                    var amount = data.Count > 3 ? System.Convert.ToInt32(data[3]) : 1;
                    var interval = data.Count > 4 ? System.Convert.ToSingle(data[4]) : 1f;
                    var inventoryId = data.Count > 5 ? data[5] as string : "player";
                    if (string.IsNullOrEmpty(itemId)) return ResultCode.Fail("Harvest itemId invalid");
                    e.Harvest(itemId, amount, interval, inventoryId);
                    break;
                }
                default:
                    return ResultCode.Fail($"Unsupported capability: {capability}");
            }

            return ResultCode.Ok(capability);
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

        [Event(EVT_REGISTER_SIMPLE_ENTITY_CONFIG)]
        public List<object> RegisterSimpleEntityConfig(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService not initialized");
            if (data == null || data.Count < 5) return ResultCode.Fail("RegisterSimpleEntityConfig invalid args");

            var configId = data[0] as string;
            if (string.IsNullOrEmpty(configId)) return ResultCode.Fail("configId invalid");

            var cfg = new EntityConfig
            {
                ConfigId = configId,
                DisplayName = data[1] as string,
                CharacterConfigId = data[2] as string,
                Collider = ToEntityColliderConfig(data[3]),
                SpawnOffset = data[4] is Vector3 offset ? offset : Vector3.zero,
                Kind = EntityKind.Static,
            };
            if (data.Count > 5 && data[5] is string kind && kind == "Dynamic")
                cfg.Kind = EntityKind.Dynamic;

            Service.RegisterConfig(cfg);
            return ResultCode.Ok(cfg.ConfigId);
        }

        private static EntityColliderConfig ToEntityColliderConfig(object source)
        {
            if (source == null) return null;
            if (source is EntityColliderConfig cfg) return cfg;

            var shape = EntityColliderShape.None;
            var shapeValue = ReadPublicValue(source, "Shape");
            if (shapeValue != null)
                System.Enum.TryParse(shapeValue.ToString(), out shape);

            var sizeValue = ReadPublicValue(source, "Size");
            var offsetValue = ReadPublicValue(source, "Offset");
            var triggerValue = ReadPublicValue(source, "IsTrigger");

            return new EntityColliderConfig(
                shape,
                sizeValue is Vector2 size ? size : Vector2.one,
                offsetValue is Vector2 offset ? offset : Vector2.zero,
                triggerValue is bool isTrigger && isTrigger);
        }

        private static object ReadPublicValue(object source, string name)
        {
            var type = source.GetType();
            var field = type.GetField(name);
            if (field != null) return field.GetValue(source);
            var property = type.GetProperty(name);
            return property != null ? property.GetValue(source) : null;
        }

        [Event(EVT_GET_ENTITY)]
        public List<object> GetEntity(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 1) return ResultCode.Fail("参数无效：需要 [instanceId]");
            var instanceId = data[0] as string;
            var entity = Service.GetEntity(instanceId);
            return entity != null ? ResultCode.Ok(entity) : ResultCode.Fail($"Entity 不存在: {instanceId}");
        }

        [Event(EVT_APPLY_COLLIDER)]
        public List<object> ApplyCollider(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效：需要 [GameObject host, EntityColliderConfig cfg]");
            var host = data[0] as GameObject;
            var cfg = data[1] as EntityColliderConfig;
            if (host == null || cfg == null) return ResultCode.Fail("host / cfg 不能为空");
            EntityService.ApplyCollider(host, cfg);
            return ResultCode.Ok(host);
        }

        [Event(EVT_ATTACH_ENTITY_HANDLE)]
        public List<object> AttachEntityHandle(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数无效：需要 [GameObject host, Entity entity]");
            var host = data[0] as GameObject;
            var entity = data[1] as Entity;
            if (host == null || entity == null) return ResultCode.Fail("host / entity 不能为空");
            EntityService.AttachEntityHandle(host, entity);
            return ResultCode.Ok(host);
        }

        #endregion
    }
}
