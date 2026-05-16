using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Presentation.CharacterManager.Dao;
// §4.1 跨模块 [EventListener] 走 bare-string；不 using ResourceManager

namespace EssSystem.Core.Presentation.CharacterManager
{
    /// <summary>
    /// 角色门面 —— 单例 MonoBehaviour，统一管理 2D Sprite 与 3D Prefab/FBX 两条渲染路径。
    /// <para>外部模块通过 <see cref="EventProcessor"/> 触发 <c>EVT_*</c> 调用；内部直接调 <see cref="CharacterService"/>。</para>
    /// <para>子目录布局：<c>Common/</c>（共享 DAO + 基类）、<c>Sprite2D/</c>、<c>Prefab3D/</c>。</para>
    /// </summary>
    [Manager(11)]
    public class CharacterManager : Manager<CharacterManager>
    {
        // ============================================================
        // Event 常量（对外 API）
        // ============================================================
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
        /// <summary>分发运动状态动作（按 LocomotionRole 路由）。data: [instanceId, moving(bool), grounded(bool, 可选 默认 true)].</summary>
        public const string EVT_PLAY_LOCOMOTION        = "PlayCharacterLocomotion";
        /// <summary>触发一次攻击锁定（Attack 角色部件播放 Attack）。data: [instanceId, duration(float)].</summary>
        public const string EVT_TRIGGER_ATTACK         = "TriggerCharacterAttack";
        /// <summary>设置 Character 面朝（翻转 localScale.x）。data: [instanceId, facingRight(bool)].</summary>
        public const string EVT_SET_FACING             = "SetCharacterFacing";

        // ============================================================
        // Inspector
        // ============================================================
        [Header("Default Templates")]
        [Tooltip("是否启动时注册内置示例配置（Warrior / Mage）；业务侧可用同 ConfigId 覆盖")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [Tooltip("是否启动时扫描 Resources/ 下所有 FBX/Model 自动注册为 Prefab3D Config（仅 Editor 精确，Build 期依赖持久化的 JSON）")]
        [SerializeField] private bool _autoRegisterAllFBX = true;

        [Tooltip("配合 _autoRegisterAllFBX：仅扫描该 Resources 子目录（如 Models/Characters3D）；为空 = 扫整个 Resources/")]
        [SerializeField] private string _autoRegisterFBXSubFolder = "";

        public CharacterService Service => CharacterService.Instance;

        // ============================================================
        // 生命周期
        // ============================================================
        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates)
            {
                // 内置默认以代码为准，覆盖写入持久化（避免旧版本如 Static 部件遗留）
                Service.RegisterConfig(DefaultCharacterConfigs.BuildWarrior());
                Service.RegisterConfig(DefaultCharacterConfigs.BuildMage());
                DefaultTreeCharacterConfigs.RegisterAll(Service);
            }
            // _autoRegisterAllFBX 推迟到 ResourceService 加载完成后跑（OnResourcesLoaded）。

            Log("CharacterManager 初始化完成", Color.green);
        }

        /// <summary>监听 ResourceManager.EVT_RESOURCES_LOADED 广播 —— 此时 _modelClipNames 就绪，FBX 扫描走 O(1) 缓存。</summary>
        [EventListener("OnResourcesLoaded")]   // §4.1 跨模块 bare-string
        public List<object> OnResourcesLoaded(List<object> data)
        {
            if (_autoRegisterAllFBX) CharacterConfigFactory.RegisterAllFBXInResources(_autoRegisterFBXSubFolder);
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

        // ============================================================
        // Event API
        // ============================================================
        [Event(EVT_CREATE_CHARACTER)]
        public List<object> CreateCharacter(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var configId,   out fail)) return fail;
            if (!TryGetString(data, 1, out var instanceId, out fail)) return fail;
            var parent        = data.Count > 2 ? data[2] as Transform : null;
            var worldPosition = data.Count > 3 && data[3] is Vector3 v ? (Vector3?)v : null;

            var character = Service.CreateCharacter(configId, instanceId, parent, worldPosition);
            return character?.View != null
                ? ResultCode.Ok(character.View.transform)   // 返根 Transform（Unity 中立类型）
                : ResultCode.Fail($"CreateCharacter 失败: configId={configId}, instanceId={instanceId}");
        }

        [Event(EVT_DESTROY_CHARACTER)]
        public List<object> DestroyCharacter(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            return Service.DestroyCharacter(instanceId)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"DestroyCharacter 失败: {instanceId}");
        }

        [Event(EVT_PLAY_ACTION)]
        public List<object> PlayAction(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            var actionName = data.Count > 1 ? data[1] as string : null;
            if (string.IsNullOrEmpty(actionName)) return ResultCode.Fail("actionName 不能为空");
            var partId = data.Count > 2 ? data[2] as string : null;
            return Service.PlayAction(instanceId, actionName, partId)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"PlayAction 失败: {instanceId}/{actionName}");
        }

        [Event(EVT_STOP_ACTION)]
        public List<object> StopAction(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            var partId = data.Count > 1 ? data[1] as string : null;
            return Service.StopAction(instanceId, partId)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"StopAction 失败: {instanceId}");
        }

        [Event(EVT_SET_CHARACTER_SCALE)]
        public List<object> SetScale(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            if (!TryGetVec3(data, 1, out var scale, out fail)) return fail;
            return Service.SetScale(instanceId, scale)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"SetScale 失败: {instanceId}");
        }

        [Event(EVT_SET_CHARACTER_POSITION)]
        public List<object> SetPosition(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            if (!TryGetVec3(data, 1, out var pos, out fail)) return fail;
            return Service.SetPosition(instanceId, pos)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"SetPosition 失败: {instanceId}");
        }

        [Event(EVT_MOVE_CHARACTER)]
        public List<object> Move(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            if (!TryGetVec3(data, 1, out var delta, out fail)) return fail;
            return Service.Move(instanceId, delta)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"Move 失败: {instanceId}");
        }

        [Event(EVT_PLAY_LOCOMOTION)]
        public List<object> PlayLocomotion(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            var moving = data.Count > 1 && data[1] is bool b1 && b1;
            var grounded = data.Count <= 2 || !(data[2] is bool b2) || b2;
            return Service.PlayLocomotion(instanceId, moving, grounded)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"PlayLocomotion 失败: {instanceId}");
        }

        [Event(EVT_TRIGGER_ATTACK)]
        public List<object> TriggerAttack(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            var duration = data.Count > 1 && data[1] is float f ? f : 0.4f;
            return Service.TriggerAttack(instanceId, duration)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"TriggerAttack 失败: {instanceId}");
        }

        [Event(EVT_SET_FACING)]
        public List<object> SetFacing(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            var right = data.Count > 1 && data[1] is bool b && b;
            return Service.SetFacingRight(instanceId, right)
                ? ResultCode.Ok(instanceId) : ResultCode.Fail($"SetFacing 失败: {instanceId}");
        }

        // ============================================================
        // 内部辅助
        // ============================================================
        private bool RequireService(out List<object> fail)
        {
            if (Service == null) { fail = ResultCode.Fail("CharacterService 未初始化"); return false; }
            fail = null; return true;
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
    }
}
