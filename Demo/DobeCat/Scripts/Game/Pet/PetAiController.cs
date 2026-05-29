using System.Collections.Generic;
using Demo.DobeCat.Game.Pet.Ai;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Platform.Windows;
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

        /// <summary>
        /// 手动模式：仅响应玩家 WASD，屏蔽游荡/睡眠/吃饱等自主行为。Brain 保持启动。
        /// </summary>
        public bool ManualMode { get; private set; }

        public void SetManualMode(bool manual)
        {
            ManualMode = manual;
            // Brain 保持启动；PlayerControl Consideration 始终有效
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

            // Needs: rates match DESIGN.md §4.4 (per-second)
            _entity.Add<INeeds>(new NeedsComponent(
                ("Hunger",  0f,   0.0001667f),   // +0.01 / min  → 0.7 in ~70 min
                ("Energy",  1f,  -0.0000833f),   // -0.005 / min → 0.2 in ~160 min
                ("Boredom", 0f,   0.0001333f)    // +0.008 / min → 0.6 in ~75 min
            ));

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

                // 随机游荡（基线，手动模式暂停）
                brain.Add(new Consideration
                {
                    Id = "Wander",
                    Score = _ => ManualMode ? 0f : 0.2f,
                    CreateAction = _ => new PetWanderAction(),
                });

                // Sleep: Energy < 0.2 → score rises as energy drains（手动模式暂停）
                brain.Add(new Consideration
                {
                    Id = "Sleep",
                    Score = ctx =>
                    {
                        if (ManualMode) return 0f;
                        var e = ctx.GetNeed("Energy");
                        return e < 0.2f ? (0.2f - e) / 0.2f * 0.9f : 0f;
                    },
                    CreateAction = _ => new PetSleepAction(),
                });

                // Eat: Hunger > 0.7 → eat cat_food from player inventory（手动模式暂停）
                brain.Add(new Consideration
                {
                    Id = "Eat",
                    Score = ctx =>
                    {
                        if (ManualMode) return 0f;
                        var h = ctx.GetNeed("Hunger");
                        return h > 0.7f ? 0.5f + (h - 0.7f) * 1.0f : 0f;
                    },
                    CreateAction = _ => new EatAction(Demo.DobeCat.Game.Shop.DobeCatShopSetup.FOOD_CAT_FOOD, "player", 0.4f),
                });

                // Boredom wander（手动模式暂停）
                brain.Add(new Consideration
                {
                    Id = "BoredomWander",
                    Score = ctx =>
                    {
                        if (ManualMode) return 0f;
                        var b = ctx.GetNeed("Boredom");
                        return b > 0.6f ? 0.3f + (b - 0.6f) * 0.5f : 0f;
                    },
                    CreateAction = _ => new PetWanderAction(0.3f, 1.0f),
                });

                // Play（手动模式暂停）
                brain.Add(new Consideration
                {
                    Id = "Play",
                    Score = ctx =>
                    {
                        if (ManualMode) return 0f;
                        var b = ctx.GetNeed("Boredom");
                        return b > 0.6f ? 0.4f + (b - 0.6f) * 0.8f : 0f;
                    },
                    CreateAction = _ => new PetPlayAction(),
                });

                // ReactToCursor: cursor within 2.5 world units → look toward it
                const float cursorRange = 2.5f;
                brain.Add(new Consideration
                {
                    Id = "ReactToCursor",
                    Score = ctx =>
                    {
                        var cam = UnityEngine.Camera.main;
                        if (cam == null) return 0f;
                        Vector2 screenPos;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                        screenPos = DesktopOverlay.GetGlobalCursorScreenPos();
#else
                        screenPos = UnityEngine.Input.mousePosition;
#endif
                        var z     = UnityEngine.Mathf.Abs(cam.transform.position.z);
                        var world = cam.ScreenToWorldPoint(new UnityEngine.Vector3(screenPos.x, screenPos.y, z));
                        var dist  = UnityEngine.Vector2.Distance(
                            new UnityEngine.Vector2(world.x, world.y),
                            new UnityEngine.Vector2(ctx.Self.WorldPosition.x, ctx.Self.WorldPosition.y));
                        return dist < cursorRange ? 0.35f * (1f - dist / cursorRange) : 0f;
                    },
                    CreateAction = _ => new PetCursorReactAction(CharacterInstanceId, cursorRange),
                });

                // IdleVariant: fires every ~30 s to show a random phrase
                float lastVariantTime = 0f;
                brain.Add(new Consideration
                {
                    Id = "IdleVariant",
                    Score = _ => UnityEngine.Time.time - lastVariantTime > 30f ? 0.25f : 0f,
                    CreateAction = _ =>
                    {
                        lastVariantTime = UnityEngine.Time.time;
                        return new PetIdleVariantAction();
                    },
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
            var l = (Win32Native.GetAsyncKeyState(Win32Native.VK_A) & 0x8000) != 0;
            var r = (Win32Native.GetAsyncKeyState(Win32Native.VK_D) & 0x8000) != 0;
            var u = (Win32Native.GetAsyncKeyState(Win32Native.VK_W) & 0x8000) != 0;
            var d = (Win32Native.GetAsyncKeyState(Win32Native.VK_S) & 0x8000) != 0;
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

            // Reduce Boredom while moving (activity = entertainment)
            if (ctx.IsMoving && _entity != null)
                _entity.Get<INeeds>()?.Add("Boredom", -Time.deltaTime * 0.0003f);

            // 每帧边界夹紧（玩家手动操控 + AI 游荡都受约束）
            ClampToScreenBounds();
        }

        /// <summary>把宠物位置夹紧到摄像机可见范围内，留一半身位边距（约 0.5 世界单位）。</summary>
        public void ClampToScreenBounds()
        {
            if (_entity == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            var pos = _entity.WorldPosition;
            var z   = Mathf.Abs(cam.transform.position.z);

            var bl = cam.ScreenToWorldPoint(new Vector3(0f,            0f,             z));
            var tr = cam.ScreenToWorldPoint(new Vector3(Screen.width,  Screen.height,  z));

            const float margin = 0.6f; // 世界单位，约半个身位
            float cx = Mathf.Clamp(pos.x, bl.x + margin, tr.x - margin);
            float cy = Mathf.Clamp(pos.y, bl.y + margin, tr.y - margin);

            if (Mathf.Abs(cx - pos.x) > 0.001f || Mathf.Abs(cy - pos.y) > 0.001f)
            {
                var clamped = new Vector3(cx, cy, pos.z);
                _entity.WorldPosition = clamped;
                transform.position    = clamped;
            }
        }
    }
}

