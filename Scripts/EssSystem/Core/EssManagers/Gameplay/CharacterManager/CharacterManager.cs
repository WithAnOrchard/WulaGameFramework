using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;
using EssSystem.Core.EssManagers.Foundation.ResourceManager;   // C3: 简化 [EventListener] 全限定名

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager
{
    /// <summary>
    /// 角色门面 —— 挂在场景里的单例 MonoBehaviour，负责生命周期、默认配置注册，以及对外 Event API。
    /// <para>业务层通过 <see cref="EventProcessor"/> 触发 <c>EVT_*</c> 调用；
    /// 内部模块（同包或同目录）可直接用 <see cref="CharacterService"/>。</para>
    /// </summary>
    [Manager(11)]
    public class CharacterManager : Manager<CharacterManager>
    {
        // ─── Event 名常量（对外 API）──────────────────────────────
        /// <summary>创建 Character。data: [configId(string), instanceId(string), parent?(Transform), worldPosition?(Vector3)]. 返回 Ok(Transform root) / Fail。</summary>
        public const string EVT_CREATE_CHARACTER       = "CreateCharacter";
        /// <summary>销毁 Character。data: [instanceId]. 返回 Ok(instanceId) / Fail。</summary>
        public const string EVT_DESTROY_CHARACTER      = "DestroyCharacter";
        /// <summary>播放动作。data: [instanceId, actionName, partId?(string)].</summary>
        public const string EVT_PLAY_ACTION            = "PlayCharacterAction";
        /// <summary>停止动作。data: [instanceId, partId?(string)].</summary>
        public const string EVT_STOP_ACTION            = "StopCharacterAction";
        /// <summary>设置 Character 根 localScale。data: [instanceId, Vector3 scale].</summary>
        public const string EVT_SET_CHARACTER_SCALE    = "SetCharacterScale";
        /// <summary>设置 Character 世界坐标。data: [instanceId, Vector3 worldPosition].</summary>
        public const string EVT_SET_CHARACTER_POSITION = "SetCharacterPosition";
        /// <summary>在当前位置基础上平移 Character。data: [instanceId, Vector3 delta].</summary>
        public const string EVT_MOVE_CHARACTER         = "MoveCharacter";

        #region Inspector

        [Header("Default Templates")]
        [Tooltip("是否启动时注册内置示例配置（Warrior / Mage）；业务侧可用同 ConfigId 覆盖")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [Tooltip("是否启动时扫描 Resources/ 下所有 FBX/Model 自动注册为 Prefab3DClips Config（仅 Editor 精确，Build 期依赖持久化的 JSON）")]
        [SerializeField] private bool _autoRegisterAllFBX = true;

        [Tooltip("调 _autoRegisterAllFBX 后生效：仅扫描该 Resources 子目录（如 Models/Characters3D）；为空 = 扫整个 Resources/（默认）")]
        [SerializeField] private string _autoRegisterFBXSubFolder = "";

        #endregion

        public CharacterService Service => CharacterService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates)
            {
                // 内置默认以代码为准 —— 覆盖写入持久化，避免旧版本（如 Static 部件）遗留
                Service.RegisterConfig(DefaultCharacterConfigs.BuildWarrior());
                Service.RegisterConfig(DefaultCharacterConfigs.BuildMage());
                // 树系列 8 份 CharacterConfig（小/中各 4 变体），供 EntityManager 按字符串 ID 引用
                DefaultTreeCharacterConfigs.RegisterAll(Service);
            }

            // _autoRegisterAllFBX 的实际扫描推迟到 ResourceService 加载/索引完成后（见 OnResourcesLoaded）
            // 这样能复用 ResourceService 的 _modelClipNames 缓存，O(1) 查表，避免重复跑 AssetDatabase。

            Log("CharacterManager 初始化完成", Color.green);
        }

        /// <summary>
        /// 监听 ResourceManager 的资源加载完成广播 —— 此时 <c>_modelClipNames</c> 已就绪，扫描走 O(1) 缓存。
        /// </summary>
        // C3: using 后不再全限定名
        [EventListener(ResourceManager.EVT_RESOURCES_LOADED)]
        public List<object> OnResourcesLoaded(List<object> data)
        {
            if (_autoRegisterAllFBX)
            {
                CharacterConfigFactory.RegisterAllFBXInResources(_autoRegisterFBXSubFolder);
            }
            return ResultCode.Ok();
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

        // C1: 遵项目规范 “[Event] 动词开头”，去除 OnEvent 前缀。Service 上同名 typed helper 重载 OK。字符串不变。
        [Event(EVT_CREATE_CHARACTER)]
        public List<object> CreateCharacter(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var configId,   out var fail0)) return fail0;
            if (!TryGetString(data, 1, out var instanceId, out var fail1)) return fail1;
            var parent        = data.Count > 2 ? data[2] as Transform : null;
            var worldPosition = data.Count > 3 && data[3] is Vector3 v ? (Vector3?)v : null;
            var character = Service.CreateCharacter(configId, instanceId, parent, worldPosition);
            if (character == null || character.View == null)
                return ResultCode.Fail($"CreateCharacter 失败: configId={configId}, instanceId={instanceId}");
            // 返回 root Transform —— Unity 原生类型，外部模块无需 using CharacterManager
            return ResultCode.Ok(character.View.transform);
        }

        [Event(EVT_DESTROY_CHARACTER)]
        public List<object> DestroyCharacter(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var instanceId, out var fail)) return fail;
            return Service.DestroyCharacter(instanceId)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"DestroyCharacter 失败: {instanceId}");
        }

        [Event(EVT_PLAY_ACTION)]
        public List<object> PlayAction(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var instanceId, out var fail)) return fail;
            var actionName = data.Count > 1 ? data[1] as string : null;
            if (string.IsNullOrEmpty(actionName)) return ResultCode.Fail("actionName 不能为空");
            var partId = data.Count > 2 ? data[2] as string : null;
            return Service.PlayAction(instanceId, actionName, partId)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"PlayAction 失败: {instanceId}/{actionName}");
        }

        [Event(EVT_STOP_ACTION)]
        public List<object> StopAction(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var instanceId, out var fail)) return fail;
            var partId = data.Count > 1 ? data[1] as string : null;
            return Service.StopAction(instanceId, partId)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"StopAction 失败: {instanceId}");
        }

        [Event(EVT_SET_CHARACTER_SCALE)]
        public List<object> SetScale(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var instanceId, out var fail)) return fail;
            if (!TryGetVec3(data, 1, out var scale, out var fail2)) return fail2;
            return Service.SetScale(instanceId, scale)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"SetScale 失败: {instanceId}");
        }

        [Event(EVT_SET_CHARACTER_POSITION)]
        public List<object> SetPosition(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var instanceId, out var fail)) return fail;
            if (!TryGetVec3(data, 1, out var pos, out var fail2)) return fail2;
            return Service.SetPosition(instanceId, pos)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"SetPosition 失败: {instanceId}");
        }

        [Event(EVT_MOVE_CHARACTER)]
        public List<object> Move(List<object> data)
        {
            if (Service == null) return ResultCode.Fail("CharacterService 未初始化");
            if (!TryGetString(data, 0, out var instanceId, out var fail)) return fail;
            if (!TryGetVec3(data, 1, out var delta, out var fail2)) return fail2;
            return Service.Move(instanceId, delta)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"Move 失败: {instanceId}");
        }

        private static bool TryGetString(List<object> data, int idx, out string value, out List<object> fail)
        {
            value = data != null && data.Count > idx ? data[idx] as string : null;
            if (string.IsNullOrEmpty(value)) { fail = ResultCode.Fail($"参数 [{idx}] 需为非空字符串"); return false; }
            fail = null; return true;
        }

        private static bool TryGetVec3(List<object> data, int idx, out Vector3 value, out List<object> fail)
        {
            if (data == null || data.Count <= idx || !(data[idx] is Vector3 v))
            { value = default; fail = ResultCode.Fail($"参数 [{idx}] 需为 Vector3"); return false; }
            value = v; fail = null; return true;
        }

        #endregion
    }
}
