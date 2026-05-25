using System.Collections.Generic;
using Demo.DobeCat.Game.Pet.Ai;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using UnityEngine;
using EM = EssSystem.Core.Application.SingleManagers.EntityManager.EntityManager;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠 AI 控制器 —— 把桌宠注册为 EntityManager 的 Entity，挂 BrainComponent，
    /// 通过两套 Consideration 实现玩家 / AI 双系统：
    /// PlayerControl 高分（WASD 按下时），Wander 基线分（无操作时随机游荡）。
    /// 每帧把 BrainContext.IsMoving/FacingDirection 同步到 CharacterManager。
    /// </summary>
    public class PetAiController : MonoBehaviour
    {
        public bool AiEnabled = true;
        public float MoveSpeed = 4f;
        public string CharacterInstanceId = "DobeCatLocal";
        public string CharacterConfigId = "Warrior";

        /// <summary>场景中当前活跃的桌宠实例（供农场等系统检测玩家位置）。</summary>
        public static PetAiController Current { get; private set; }

        private Entity _entity;
        private IBrain _brain;
        private int _lastFacingDispatched;
        private bool _lastMovingDispatched;
        private bool _externalPaused;

        public Entity Entity => _entity;
        public IBrain Brain => _brain;

        public void SetPaused(bool paused)
        {
            _externalPaused = paused;
            ApplyEnabled();
        }

        public void SetAiEnabled(bool enabled)
        {
            AiEnabled = enabled;
            ApplyEnabled();
        }

        private void ApplyEnabled()
        {
            if (_brain != null) _brain.Enabled = AiEnabled && !_externalPaused;
        }

        private void Awake()  => Current = this;
        private void OnDestroy() { if (Current == this) Current = null; }

        public void Initialize(Vector3 spawnPosition)
        {
            if (!EventProcessor.HasInstance)
            {
                Debug.LogError("[PetAiController] EventProcessor 未就绪");
                return;
            }

            var definition = new EntityRuntimeDefinition
            {
                Kind = EntityKind.Dynamic,
                Collider = new EntityColliderConfig(EntityColliderShape.None, Vector2.zero),
                CanMove = true,
                MoveSpeed = MoveSpeed,
                CanBeAttacked = false,
                EnableFlashEffect = false,
                EnableKnockbackEffect = false,
            };
            var regResult = EventProcessor.Instance.TriggerEventMethod(
                EM.EVT_REGISTER_SCENE_ENTITY,
                new List<object> { CharacterInstanceId, gameObject, definition });
            if (!ResultCode.IsOk(regResult))
            {
                Debug.LogError($"[PetAiController] RegisterSceneEntity 失败");
                return;
            }

            _entity = EM.Instance != null ? EM.Instance.Service.GetEntity(CharacterInstanceId) : null;
            if (_entity == null) { Debug.LogError("[PetAiController] 取 Entity 失败"); return; }
            _entity.WorldPosition = spawnPosition;

            var root = CharacterViewBridge.CreateCharacter(
                CharacterConfigId, CharacterInstanceId,
                parent: transform, worldPosition: spawnPosition);
            if (root == null)
            {
                Debug.LogError($"[PetAiController] CreateCharacter 失败 configId={CharacterConfigId}");
                return;
            }

            _entity.CanThink(brain =>
            {
                brain.DecisionInterval = 0.1f; // 玩家输入抢占要快
                brain.InertiaBonus = 0f;       // 不要惯性，玩家按键立刻接管

                // 玩家控制（高优先级，仅当 WASD 有输入时活跃）
                brain.Add(new Consideration
                {
                    Id = "PlayerControl",
                    Score = _ =>
                    {
                        var axis = GetWasdAxis();
                        return axis.sqrMagnitude > 1e-3f ? 1.0f : 0f;
                    },
                    CreateAction = _ => new PetPlayerControlAction(GetWasdAxis),
                });

                // 随机游荡（基线，所有时刻可用）
                brain.Add(new Consideration
                {
                    Id = "Wander",
                    Score = _ => 0.2f,
                    CreateAction = _ => new PetWanderAction(),
                });
            });

            _brain = _entity.Get<IBrain>();
            ApplyEnabled();

            // 初始 Idle + 朝右
            CharacterViewBridge.PlayLocomotion(CharacterInstanceId, false, true);
            CharacterViewBridge.SetFacing(CharacterInstanceId, true);
            _lastFacingDispatched = 1;
            _lastMovingDispatched = false;
        }

        private static Vector2 GetWasdAxis()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var l = (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(
                         Demo.DobeCat.Sys.Platform.Windows.Win32Native.VK_A) & 0x8000) != 0;
            var r = (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(
                         Demo.DobeCat.Sys.Platform.Windows.Win32Native.VK_D) & 0x8000) != 0;
            var u = (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(
                         Demo.DobeCat.Sys.Platform.Windows.Win32Native.VK_W) & 0x8000) != 0;
            var d = (Demo.DobeCat.Sys.Platform.Windows.Win32Native.GetAsyncKeyState(
                         Demo.DobeCat.Sys.Platform.Windows.Win32Native.VK_S) & 0x8000) != 0;
            return new Vector2((r ? 1 : 0) - (l ? 1 : 0), (u ? 1 : 0) - (d ? 1 : 0));
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }

        private void Update()
        {
            if (_brain == null) return;
            var ctx = _brain.Context;
            if (ctx == null) return;

            if (ctx.FacingDirection != 0 && ctx.FacingDirection != _lastFacingDispatched)
            {
                _lastFacingDispatched = ctx.FacingDirection;
                CharacterViewBridge.SetFacing(CharacterInstanceId, ctx.FacingDirection > 0);
            }
            if (ctx.IsMoving != _lastMovingDispatched)
            {
                _lastMovingDispatched = ctx.IsMoving;
                CharacterViewBridge.PlayLocomotion(CharacterInstanceId, ctx.IsMoving, true);
            }
        }
    }
}
