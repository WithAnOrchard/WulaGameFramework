using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 部落 Demo 玩家 —— **编排器**：负责 Character 创建、玩家数值（HP/MP/Exp/金币）状态，
    /// 以及把行为切分到独立模块。模块均为同 GameObject 上的子组件，自动 AddComponent：
    /// <list type="bullet">
    /// <item><see cref="TribePlayerMovement"/>：输入 / 物理 / 跳跃 / 面朝</item>
    /// <item><see cref="TribePlayerCombat"/>：鼠标攻击 / 命中 / 范围提示</item>
    /// <item><see cref="TribePlayerCameraFollow"/>：主相机跟随 / Y 锁定</item>
    /// <item><see cref="TribePlayerInteraction"/>：背包 / 对话 切换</item>
    /// <item><see cref="TribePlayerHud"/>：顶栏 HUD（HP/MP/Exp/金币 + 头像）</item>
    /// </list>
    /// 动画分派：每帧通过 <see cref="CharacterViewBridge.PlayLocomotion"/> 把 Movement 状态送到 CharacterManager。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TribePlayerMovement))]
    public class TribePlayer : MonoBehaviour
    {
        // ─── Character ──────────────────────────────────────────
        [Header("Character")]
        [Tooltip("使用的 CharacterConfig ID。默认 Warrior（CharacterManager 启动时自动注册）。")]
        [SerializeField] private string _characterConfigId = "Warrior";

        [Tooltip("Character 实例 ID（场景内唯一）。")]
        [SerializeField] private string _instanceId = "TribePlayer";

        [Tooltip("Character 视觉缩放（仅作用 Character 子节点；不影响碰撞体大小）。")]
        [SerializeField, Min(0.1f)] private float _characterVisualScale = 10f;

        [Tooltip("角色视觉根节点相对碰撞体的 Y 偏移（世界单位）。\n" +
                 "默认 0.45，使角色视觉中心位于碰撞体顶部 → 脚踩在碰撞体上。")]
        [SerializeField] private float _characterVisualYOffset = 0.45f;

        // ─── 数值状态（Player 持有的权威游戏数据）──────────────────
        [Header("Stats")]
        [SerializeField] private bool _showPlayerHud = true;
        [SerializeField, Min(1f)] private float _maxHp = 100f;
        [SerializeField, Min(0f)] private float _hp = 100f;
        [SerializeField, Min(0f)] private float _mp = 50f;
        [SerializeField, Min(1f)] private float _maxMp = 50f;
        [SerializeField, Min(0)]  private int _coins = 0;
        [SerializeField, Min(1)]  private int _maxExperience = 100;
        [SerializeField, Min(0)]  private int _experience = 0;

        // ─── 运行时 ─────────────────────────────────────────────
        private Transform _characterRoot;
        private TribePlayerMovement _movement;
        private TribePlayerCombat _combat;
        private TribePlayerInteraction _interaction;
        private TribePlayerCameraFollow _cameraFollow;
        private TribePlayerHud _hud;
        private TribePlayerDamageEffect _damageEffect;

        // ─── 生命周期 ───────────────────────────────────────────
        private void Awake()
        {
            _movement     = GetOrAdd<TribePlayerMovement>();
            _combat       = GetOrAdd<TribePlayerCombat>();
            _cameraFollow = GetOrAdd<TribePlayerCameraFollow>();
            _interaction  = GetOrAdd<TribePlayerInteraction>();
            _damageEffect = GetOrAdd<TribePlayerDamageEffect>();

            _movement.Initialize(_instanceId);
            _combat.Initialize(_instanceId, _movement);
            _cameraFollow.Initialize();
        }

        private void Start()
        {
            SpawnCharacter();
            EquipDamageCapabilities();
            if (_showPlayerHud)
            {
                _hud = GetOrAdd<TribePlayerHud>();
                if (_hud.Build(_instanceId, _characterRoot)) PushHudStats(force: true);
            }
        }

        private void OnEnable()
        {
            // 订阅背包变更事件，"add" 到 player 容器即播放拾取音效（pick）。
            // §4.1 跨模块 bare-string：InventoryService.EVT_CHANGED = "InventoryChanged"
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.AddListener("InventoryChanged", OnInventoryChanged);
        }

        private void OnDisable()
        {
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.RemoveListener("InventoryChanged", OnInventoryChanged);
        }

        private void OnDestroy()
        {
            if (_hud != null) _hud.Dispose();
        }

        /// <summary>InventoryService.EVT_CHANGED 监听 —— args: [inventoryId, op, itemId, amount]。
        /// 仅当 op="add" 且 inventoryId="player" 时播放拾取音效，避免快捷栏 remove / 内部移动也响。</summary>
        private List<object> OnInventoryChanged(string evt, List<object> args)
        {
            if (args == null || args.Count < 4) return null;
            var invId = args[0] as string;
            var op    = args[1] as string;
            if (invId != "player" || op != "add") return null;

            EventProcessor.Instance?.TriggerEventMethod(
                "PlaySFX", new List<object> { "Tribe/Common/Sound/pick" });
            return null;
        }

        private void Update()
        {
            _interaction.Tick();
            _movement.Tick();
            _combat.Tick();

            CharacterViewBridge.PlayLocomotion(_instanceId, _movement.Moving, _movement.Grounded);

            PushHudStats();
        }

        private void FixedUpdate()
        {
            // 击退期间跳过移动输入，避免 Mover.Move 覆盖击退速度
            if (_damageEffect != null && _damageEffect.IsKnockbacking) return;
            _movement.FixedTick();
            ApplyTribeWorldBoundary();
        }

        // 部落世界左边界钳制 —— 玩家不能越过 TribeWorldBoundary.LeftLimitX 往左走。
        // 越界后把 x 拉回边界，同时清掉左向水平速度，避免 Rigidbody 把玩家"撞墙后回弹/卡墙"。
        private void ApplyTribeWorldBoundary()
        {
            var boundary = Demo.Tribe.World.TribeWorldBoundary.Instance;
            if (boundary == null) return;
            var p = transform.position;
            if (p.x >= boundary.LeftLimitX) return;
            p.x = boundary.LeftLimitX;
            transform.position = p;
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null && rb.velocity.x < 0f)
                rb.velocity = new Vector2(0f, rb.velocity.y);
        }
        private void LateUpdate()  => _cameraFollow.LateTick();

        // ─── Character 创建 ─────────────────────────────────────
        private void SpawnCharacter()
        {
            _characterRoot = CharacterViewBridge.CreateCharacter(
                _characterConfigId, _instanceId, transform, transform.position);

            if (_characterRoot == null)
            {
                Debug.LogWarning($"[TribePlayer] 创建 Character 失败: configId={_characterConfigId}, instanceId={_instanceId}");
                return;
            }
            _characterRoot.localPosition = new Vector3(0f, _characterVisualYOffset, 0f);
            _characterRoot.localScale = Vector3.one * _characterVisualScale;
        }

        // ─── Entity 能力装配 ────────────────────────────────────
        /// <summary>给玩家 Entity 装上 IDamageable，接入统一伤害流水线。</summary>
        private void EquipDamageCapabilities()
        {
            var entity = _movement.Entity;
            if (entity == null) return;

            entity.CanBeAttacked(_maxHp)
                  .OnDamaged(OnEntityDamaged)
                  // 框架 IFlashEffect：用 Sprites/Flash shader 闪整个 Character 所有子 SpriteRenderer，
                  // 自带 sourcePos 解析（无需依赖 _lastDamageSourcePos），避免单 renderer 闪烁不可见。
                  .CanFlash(_characterRoot)
                  // 框架 IKnockbackEffect：默认 KnockbackEffectComponent 走逻辑位移（直写 transform.position）
                  // 对 dynamic Rigidbody2D 玩家无效 → 装一个 velocity-based 适配器，把 sourcePos 转给
                  // TribePlayerDamageEffect.ApplyKnockback。这样击退方向永远基于真实攻击者坐标。
                  .With<IKnockbackEffect>(new PlayerVelocityKnockbackAdapter(_damageEffect));
        }

        /// <summary>IDamageable.Damaged 回调 —— 同步内部 HP 状态 + 强刷 HUD。
        /// 受伤效果（闪烁 + 击退）已由框架 <c>IFlashEffect</c> / <c>IKnockbackEffect</c> 自动触发，无需手动调用。</summary>
        private void OnEntityDamaged(Entity self, Entity source, float dealt, string damageType)
        {
            var dmg = _movement.Entity?.Get<IDamageable>();
            if (dmg != null) _hp = dmg.CurrentHp;
            PushHudStats(force: true);
        }

        /// <summary>把框架 <see cref="IKnockbackEffect"/> 协议适配到玩家的 velocity-based 击退实现。
        /// EntityService.TryDamage 会传入正确解析后的 sourcePos（来自攻击者坐标），解决"绕过 _lastDamageSourcePos"问题。</summary>
        private sealed class PlayerVelocityKnockbackAdapter : IKnockbackEffect
        {
            private readonly TribePlayerDamageEffect _impl;
            public PlayerVelocityKnockbackAdapter(TribePlayerDamageEffect impl) { _impl = impl; }
            public void OnAttach(Entity owner) { }
            public void OnDetach(Entity owner) { }
            public void OnKnockback(Vector3 damageSource)
            {
                if (_impl != null) _impl.ApplyKnockback(damageSource);
            }
        }

        // ─── HUD 推送 ───────────────────────────────────────────
        private void PushHudStats(bool force = false)
        {
            if (_hud == null || !_hud.IsBuilt) return;
            _hud.SetStats(_hp, _maxHp, _mp, _maxMp, _experience, _maxExperience, _coins, force);
        }

        private T GetOrAdd<T>() where T : Component
        {
            return TryGetComponent<T>(out var c) ? c : gameObject.AddComponent<T>();
        }

        // ─── 公开 API ─────────────────────────────────────────────
        /// <summary>对外暴露 Character 根节点（供 MapView.FollowTarget 等使用）。</summary>
        public Transform CharacterRoot => _characterRoot != null ? _characterRoot : transform;

        /// <summary>外部受击：走框架 EntityService.TryDamage 统一流水线（含无敌拦截 + 音效）。</summary>
        public void TakeDamage(float damage, Vector3 damageSource = default)
        {
            var entity = _movement.Entity;
            if (entity == null || !EntityService.HasInstance) return;
            EntityService.Instance.TryDamage(entity, damage, source: null,
                damageType: "EnemyContact", damageSourcePosition: damageSource);
        }

        /// <summary>外部（如 TribeGameManager）锁定镜头 Y —— 转发到 <see cref="TribePlayerCameraFollow"/>。</summary>
        public void SetLockedCameraY(float y, bool enable = true)
        {
            if (_cameraFollow == null) _cameraFollow = GetOrAdd<TribePlayerCameraFollow>();
            _cameraFollow.SetLockedY(y, enable);
        }
    }
}
