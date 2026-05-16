using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities;

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

        private Vector3 _lastDamageSourcePos;

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

        private void OnDestroy()
        {
            if (_hud != null) _hud.Dispose();
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
                  .OnDamaged(OnEntityDamaged);

            // 闪烁 + 击退仍由 TribePlayerDamageEffect 处理（OnEntityDamaged 回调中触发）
            // 不挂框架 IFlashEffect / IKnockbackEffect，避免和 TryDamage 自动触发重复
        }

        /// <summary>IDamageable.Damaged 回调 —— 同步内部 HP 状态 + 强刷 HUD + 触发受伤效果。</summary>
        private void OnEntityDamaged(Entity self, Entity source, float dealt, string damageType)
        {
            var dmg = _movement.Entity?.Get<IDamageable>();
            if (dmg != null) _hp = dmg.CurrentHp;
            PushHudStats(force: true);

            // 受伤效果（闪烁 + velocity-based 击退）
            if (_damageEffect != null)
                _damageEffect.OnDamaged(_lastDamageSourcePos);
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
            _lastDamageSourcePos = damageSource;
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
