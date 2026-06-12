using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.DataManager.RuntimeConfig;
using EssSystem.Core.Presentation.CharacterManager.Dao;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager
{
    /// <summary>
    /// Character 门面。统一管理 2D Sprite 与 3D Prefab/FBX 渲染路径。
    /// 外部模块通过 EventProcessor 触发事件，内部只直接调用 CharacterService。
    /// </summary>
    [Manager(11)]
    public class CharacterManager : Manager<CharacterManager>
    {
        private const string DEFAULT_CHARACTER_CONFIG_PATH = "Framework/Character/default_character.json";

        public const string EVT_CREATE_CHARACTER = "CreateCharacter";
        public const string EVT_DESTROY_CHARACTER = "DestroyCharacter";
        public const string EVT_PLAY_ACTION = "PlayCharacterAction";
        public const string EVT_STOP_ACTION = "StopCharacterAction";
        public const string EVT_SET_CHARACTER_SCALE = "SetCharacterScale";
        public const string EVT_SET_CHARACTER_POSITION = "SetCharacterPosition";
        public const string EVT_MOVE_CHARACTER = "MoveCharacter";
        public const string EVT_PLAY_LOCOMOTION = "PlayCharacterLocomotion";
        public const string EVT_TRIGGER_ATTACK = "TriggerCharacterAttack";
        public const string EVT_SET_FACING = "SetCharacterFacing";
        public const string EVT_SET_DIRECTION = "SetCharacterDirection";
        public const string EVT_GET_PART_SPRITE_ID = "GetCharacterPartSpriteId";

        [Header("Default Templates")]
        [Tooltip("启动时是否从 FrameworkResources/Config/Framework/Character/default_character.json 注册默认 Character 配置")]
        [SerializeField] private bool _registerDebugTemplates = true;

        [Tooltip("启动时是否扫描 Resources 下所有 FBX/Model 并自动注册 Prefab3D Config")]
        [SerializeField] private bool _autoRegisterAllFBX = true;

        [Tooltip("配合 _autoRegisterAllFBX：仅扫描指定 Resources 子目录。留空表示扫描整个 Resources")]
        [SerializeField] private string _autoRegisterFBXSubFolder = "";

        public CharacterService Service => CharacterService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates)
                RegisterDefaultsFromJson();

            Log("CharacterManager 初始化完成", Color.green);
        }

        private void RegisterDefaultsFromJson()
        {
            if (Service == null) return;

            if (!RuntimeConfigLoader.TryLoadJson(
                    DEFAULT_CHARACTER_CONFIG_PATH,
                    out CharacterDefaultConfigFile file,
                    msg => Log(msg, Color.gray)) || file == null)
            {
                Log($"Character default config not found: {DEFAULT_CHARACTER_CONFIG_PATH}", Color.yellow);
                return;
            }

            foreach (var config in file.CharacterConfigs ?? new List<CharacterConfig>())
            {
                if (config == null || string.IsNullOrEmpty(config.ConfigId)) continue;
                Service.RegisterConfig(config);
            }
        }

        [EventListener("OnResourcesLoaded")]
        public List<object> OnResourcesLoaded(List<object> data)
        {
            if (_autoRegisterAllFBX)
                CharacterConfigFactory.RegisterAllFBXInResources(_autoRegisterFBXSubFolder);
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

        [Event(EVT_CREATE_CHARACTER)]
        public List<object> CreateCharacter(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var configId, out fail)) return fail;
            if (!TryGetString(data, 1, out var instanceId, out fail)) return fail;

            var parent = data.Count > 2 ? data[2] as Transform : null;
            var worldPosition = data.Count > 3 && data[3] is Vector3 v ? (Vector3?)v : null;
            var character = Service.CreateCharacter(configId, instanceId, parent, worldPosition);

            return character?.View != null
                ? ResultCode.Ok(character.View.transform)
                : ResultCode.Fail($"CreateCharacter failed: configId={configId}, instanceId={instanceId}");
        }

        [Event(EVT_DESTROY_CHARACTER)]
        public List<object> DestroyCharacter(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            return Service.DestroyCharacter(instanceId)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"DestroyCharacter failed: {instanceId}");
        }

        [Event(EVT_PLAY_ACTION)]
        public List<object> PlayAction(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;

            var actionName = data.Count > 1 ? data[1] as string : null;
            if (string.IsNullOrEmpty(actionName)) return ResultCode.Fail("actionName can not be empty");

            var partId = data.Count > 2 ? data[2] as string : null;
            return Service.PlayAction(instanceId, actionName, partId)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"PlayAction failed: {instanceId}/{actionName}");
        }

        [Event(EVT_STOP_ACTION)]
        public List<object> StopAction(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;

            var partId = data.Count > 1 ? data[1] as string : null;
            return Service.StopAction(instanceId, partId)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"StopAction failed: {instanceId}");
        }

        [Event(EVT_SET_CHARACTER_SCALE)]
        public List<object> SetScale(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            if (!TryGetVec3(data, 1, out var scale, out fail)) return fail;

            return Service.SetScale(instanceId, scale)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"SetScale failed: {instanceId}");
        }

        [Event(EVT_SET_CHARACTER_POSITION)]
        public List<object> SetPosition(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            if (!TryGetVec3(data, 1, out var pos, out fail)) return fail;

            return Service.SetPosition(instanceId, pos)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"SetPosition failed: {instanceId}");
        }

        [Event(EVT_MOVE_CHARACTER)]
        public List<object> Move(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;
            if (!TryGetVec3(data, 1, out var delta, out fail)) return fail;

            return Service.Move(instanceId, delta)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"Move failed: {instanceId}");
        }

        [Event(EVT_PLAY_LOCOMOTION)]
        public List<object> PlayLocomotion(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;

            var moving = data.Count > 1 && data[1] is bool b1 && b1;
            var grounded = data.Count <= 2 || data[2] is not bool b2 || b2;
            return Service.PlayLocomotion(instanceId, moving, grounded)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"PlayLocomotion failed: {instanceId}");
        }

        [Event(EVT_TRIGGER_ATTACK)]
        public List<object> TriggerAttack(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;

            var duration = data.Count > 1 && data[1] is float f ? f : 0.4f;
            return Service.TriggerAttack(instanceId, duration)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"TriggerAttack failed: {instanceId}");
        }

        [Event(EVT_SET_FACING)]
        public List<object> SetFacing(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;

            var right = data.Count > 1 && data[1] is bool b && b;
            return Service.SetFacingRight(instanceId, right)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"SetFacing failed: {instanceId}");
        }

        [Event(EVT_SET_DIRECTION)]
        public List<object> SetDirection(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var instanceId, out fail)) return fail;

            var direction = data.Count > 1 && data[1] is int d ? d : 1;
            return Service.SetDirection(instanceId, direction)
                ? ResultCode.Ok(instanceId)
                : ResultCode.Fail($"SetDirection failed: {instanceId}");
        }

        [Event(EVT_GET_PART_SPRITE_ID)]
        public List<object> GetPartSpriteId(List<object> data)
        {
            if (!RequireService(out var fail)) return fail;
            if (!TryGetString(data, 0, out var key, out fail)) return fail;
            if (!TryGetString(data, 1, out var partId, out fail)) return fail;

            var actionName = data.Count > 2 && data[2] is string a && !string.IsNullOrEmpty(a) ? a : "Idle";
            var frameIndex = data.Count > 3 && data[3] is int fi ? fi : 0;

            var config = Service.GetConfig(key);
            if (config == null)
                config = Service.GetCharacter(key)?.Config;
            if (config == null) return ResultCode.Fail($"Character config or instance not found: {key}");

            var part = config.GetPart(partId);
            if (part == null) return ResultCode.Fail($"Character config {config.ConfigId} has no part: {partId}");

            if (part.PartType == CharacterPartType.Static)
            {
                return string.IsNullOrEmpty(part.StaticSpriteId)
                    ? ResultCode.Fail($"Static part has no sprite: {partId}")
                    : ResultCode.Ok(part.StaticSpriteId);
            }

            var action = part.GetAction(actionName);
            if (action == null) return ResultCode.Fail($"Part {partId} has no action: {actionName}");
            if (action.SpriteIds == null || action.SpriteIds.Count == 0)
                return ResultCode.Fail($"Action {actionName} has no SpriteIds");

            var idx = Mathf.Clamp(frameIndex, 0, action.SpriteIds.Count - 1);
            var spriteId = action.SpriteIds[idx];
            return string.IsNullOrEmpty(spriteId)
                ? ResultCode.Fail($"Action {actionName} frame {idx} has empty spriteId")
                : ResultCode.Ok(spriteId);
        }

        private bool RequireService(out List<object> fail)
        {
            if (Service == null)
            {
                fail = ResultCode.Fail("CharacterService is not initialized");
                return false;
            }

            fail = null;
            return true;
        }

        private static bool TryGetString(List<object> data, int idx, out string value, out List<object> fail)
        {
            value = data != null && data.Count > idx ? data[idx] as string : null;
            if (!string.IsNullOrEmpty(value))
            {
                fail = null;
                return true;
            }

            fail = ResultCode.Fail($"Argument [{idx}] must be a non-empty string");
            return false;
        }

        private static bool TryGetVec3(List<object> data, int idx, out Vector3 value, out List<object> fail)
        {
            if (data != null && data.Count > idx && data[idx] is Vector3 v)
            {
                value = v;
                fail = null;
                return true;
            }

            value = default;
            fail = ResultCode.Fail($"Argument [{idx}] must be Vector3");
            return false;
        }
    }
}
