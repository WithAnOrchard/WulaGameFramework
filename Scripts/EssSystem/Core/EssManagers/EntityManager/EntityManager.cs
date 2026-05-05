using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.EssManager.EntityManager.Dao;
// 本文件不 <c>using</c> CharacterManager——跨模块调用一律走 EventProcessor。

namespace EssSystem.EssManager.EntityManager
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
        // ─── Event 名常量（供外部调用方使用，避免魔法字符串）
        /// <summary>创建 Entity。data: [configId, instanceId, parent(Transform?), worldPosition(Vector3?)]. 返回 ResultCode.Ok(Entity) / Fail。</summary>
        public const string EVT_CREATE_ENTITY  = "CreateEntity";
        /// <summary>销毁 Entity。data: [instanceId]. 返回 ResultCode.Ok(instanceId) / Fail。</summary>
        public const string EVT_DESTROY_ENTITY = "DestroyEntity";

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
        [Event(EVT_CREATE_ENTITY)]
        public List<object> OnEventCreateEntity(List<object> data)
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
            return entity != null
                ? ResultCode.Ok(entity)
                : ResultCode.Fail($"创建 Entity 失败: configId={configId}, instanceId={instanceId}");
        }

        /// <summary>
        /// 销毁 Entity 的事件入口。data: <c>[instanceId:string]</c>。
        /// </summary>
        [Event(EVT_DESTROY_ENTITY)]
        public List<object> OnEventDestroyEntity(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("EntityService 尚未初始化");
            if (data == null || data.Count < 1) return ResultCode.Fail("参数无效：需要 [instanceId]");

            var instanceId = data[0] as string;
            if (string.IsNullOrEmpty(instanceId)) return ResultCode.Fail("instanceId 不能为空");

            return Service.DestroyEntity(instanceId)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"Entity 不存在: {instanceId}");
        }

        #endregion
    }
}
