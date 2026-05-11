using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Runtime;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
using Demo.Tribe;
// §4.1 跨模块 DialogueManager 走 bare-string，不 using。

namespace Demo.Tribe.Player
{
    /// <summary>
    /// 部落 Demo 玩家 —— 使用框架默认 <see cref="DefaultCharacterConfigs.WarriorId"/> 战士做角色。
    /// <para>
    /// 行为：
    /// <list type="bullet">
    /// <item>WASD / 方向键平面移动；Shift 冲刺</item>
    /// <item>移动播 <c>Walk</c> 动作，停下播 <c>Idle</c>；面朝运动方向</item>
    /// <item>鼠标左键播 <c>Attack</c>（锁定窗口期内不切 Walk/Idle）</item>
    /// <item>按 B 切换背包 UI；按 I 切换调试对话</item>
    /// <item>可选 Main Camera 跟随</item>
    /// </list>
    /// </para>
    /// <para>
    /// **跨模块解耦**：所有 Manager 调用一律通过 <c>EventProcessor.TriggerEventMethod</c>
    /// （§4.1 bare-string 协议），**不直接** <c>using</c> 业务 Manager 类。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class TribePlayer : MonoBehaviour
    {
        // ─────────────── Character ───────────────
        [Header("Character")]
        [Tooltip("使用的 CharacterConfig ID。默认 Warrior（CharacterManager 启动时自动注册）。")]
        [SerializeField] private string _characterConfigId = DefaultCharacterConfigs.WarriorId;

        [Tooltip("Character 实例 ID（场景内唯一）。")]
        [SerializeField] private string _instanceId = "TribePlayer";

        [Tooltip("Character 视觉缩放（仅作用 Character 子节点；不影响碰撞体大小）。")]
        [SerializeField, Min(0.1f)] private float _characterVisualScale = 10f;

        // ─────────────── Movement ────────────────
        [Header("Movement")]
        [Tooltip("移动速度（unit / 秒）。")]
        [SerializeField, Min(0.1f)] private float _speed = 6f;

        [Tooltip("按住 Shift 时的速度倍率。")]
        [SerializeField, Min(1f)] private float _sprintMultiplier = 2.5f;

        [Tooltip("低于此速度阈值视为停下，播 Idle。")]
        [SerializeField, Min(0f)] private float _idleSpeedEpsilon = 0.05f;

        // ─────────────── Physics ───────────────
        [Header("Physics")]
        [SerializeField, Min(0.05f)] private float _colliderRadius = 0.45f;

        [Tooltip("角色视觉根节点相对撞撞体的 Y 偏移（世界单位）。\n" +
                 "默认 = _colliderRadius，使角色视觉中心位于撞撞体顶部 → 脚踩在撞撞体上。")]
        [SerializeField] private float _characterVisualYOffset = 0.45f;

        [SerializeField, Min(0f)]    private float _linearDrag     = 0f;

        [Tooltip("是否启用横版物理：重力 ON + 忽略 Y 轴输入 + Space/W 跳跃。关闭后退为俰视自由走位。")]
        [SerializeField] private bool _useSideScrollerPhysics = true;

        [Tooltip("重力倍率（仅横版物理启用时生效）。")]
        [SerializeField, Min(0f)] private float _gravityScale = 5f;

        [Tooltip("跳跃初速（仅横版物理启用时生效）。")]
        [SerializeField, Min(0f)] private float _jumpForce = 12f;

        [Tooltip("设为玩家脚下撞到地面才能跳；ground-check 抔射起点从碍撞体中心向下抔多远。")]
        [SerializeField, Min(0.05f)] private float _groundCheckDistance = 0.1f;

        [Tooltip("跳跃键（额外 Space 总是生效）。")]
        [SerializeField] private KeyCode _jumpKey = KeyCode.Space;

        // ─────────────── Combat ──────────────────
        [Header("Combat")]
        [Tooltip("鼠标左键播放 Attack 动作。")]
        [SerializeField] private bool _enableMouseAttack = true;

        [Tooltip("Attack 锁定时长（秒）。期间忽略 Walk/Idle 切换。")]
        [SerializeField, Min(0.05f)] private float _attackDuration = 0.4f;

        [SerializeField, Min(0.1f)] private float _attackRange = 2.2f;
        [SerializeField, Min(0.1f)] private float _attackHeight = 1.4f;
        [SerializeField] private float _attackYOffset = 0f;
        [SerializeField, Min(1f)] private float _attackDamage = 1f;
        [SerializeField] private bool _showAttackRangeHint = true;
        [SerializeField] private Color _attackRangeHintColor = new Color(1f, 0.15f, 0.05f, 0.35f);

        // ─────────────── Inventory ───────────────
        [Header("Inventory (B 键)")]
        [SerializeField] private bool   _enableInventoryToggle = true;
        [SerializeField] private string _inventoryId           = "player";
        [SerializeField] private string _inventoryConfigId     = "PlayerBackPack";
        [SerializeField] private KeyCode _inventoryToggleKey   = KeyCode.B;

        // ─────────────── Dialogue ────────────────
        [Header("Dialogue (I 键)")]
        [SerializeField] private bool    _enableDialogueTest = true;
        [SerializeField] private string  _dialogueId         = "DebugDialogue";
        [SerializeField] private KeyCode _dialogueToggleKey  = KeyCode.I;

        // ─────────────── Camera ──────────────────
        [Header("Camera Follow")]
        [SerializeField] private bool _autoFollowMainCamera = true;
        [SerializeField, Range(0f, 1f)] private float _cameraFollowSmoothing = 0.18f;

        [Tooltip("是否锁定镜头 Y：横版场景常用，镜头只跟玩家 X，Y 锁定为 <see cref=\"_lockedCameraY\"/>。")]
        [SerializeField] private bool _lockCameraY = false;

        [Tooltip("镜头锁定 Y 值；外部可调 SetLockedCameraY 设置（例：TribeGameManager 根据背景路线高度计算后应用）。")]
        [SerializeField] private float _lockedCameraY = 0f;

        // ─────────────── HUD ──────────────────────
        [Header("HUD")]
        [SerializeField] private bool _showPlayerHud = true;
        [SerializeField, Min(1f)] private float _maxHp = 100f;
        [SerializeField, Min(0f)] private float _hp = 100f;
        [SerializeField, Min(1f)] private float _maxMp = 50f;
        [SerializeField, Min(0f)] private float _mp = 50f;
        [SerializeField, Min(0)] private int _coins = 0;
        [SerializeField, Min(1)] private int _maxExperience = 100;
        [SerializeField, Min(0)] private int _experience = 0;

        // ─── 运行时 ─────────────────────────────────────────────
        private Transform _characterRoot;
        private Camera _cam;
        private string _currentAction;
        private readonly Dictionary<string, string> _partActions = new Dictionary<string, string>();
        private bool _facingRight = true;
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private float _attackLockUntil;
        private Vector2 _moveInput;
        private SpriteRenderer _attackRangeHintRenderer;
        private readonly HashSet<Component> _currentAttackHits = new HashSet<Component>();
        private UIBarComponent _hudHpBar;
        private UIBarComponent _hudMpBar;
        private UIBarComponent _hudExperienceBar;
        private UITextComponent _hudHpValueText;
        private UITextComponent _hudMpValueText;
        private UITextComponent _hudExperienceValueText;
        private UITextComponent _hudCoinsText;
        private UIPanelComponent _hudHeadSprite;
        private bool _hudHeadSpriteReady;
        private float _lastHudHp = -1f;
        private float _lastHudMaxHp = -1f;
        private float _lastHudMp = -1f;
        private float _lastHudMaxMp = -1f;
        private int _lastHudExperience = -1;
        private int _lastHudMaxExperience = -1;
        private int _lastHudCoins = -1;
        private static readonly string[] _movementParts = { "Head", "Hair", "Eyes" };
        private static readonly string[] _bodyMotionParts = { "Skin", "Cloth" };
        private static readonly string[] _attackParts = { "Weapon", "Shield" };

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            ConfigurePhysics();
        }

        private void Start()
        {
            if (_autoFollowMainCamera) _cam = Camera.main;
            SpawnCharacter();
            CreatePlayerHud();
        }

        private void ConfigurePhysics()
        {
            _rb.gravityScale = _useSideScrollerPhysics ? _gravityScale : 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.drag = _linearDrag;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _collider.radius = _colliderRadius;
            _collider.isTrigger = false;
        }

        private void Update()
        {
            // ── 背包开关 ──
            if (_enableInventoryToggle && Input.GetKeyDown(_inventoryToggleKey))
                ToggleInventory();

            // ── 对话开关 ──
            if (_enableDialogueTest && Input.GetKeyDown(_dialogueToggleKey))
                ToggleDialogue();

            // ── 移动输入（实际位移在 FixedUpdate 走物理）──
            var h = Input.GetAxisRaw("Horizontal");
            var v = Input.GetAxisRaw("Vertical");
            // 横版模式：Y 轴交由重力/跳跃控制，不用输入驱动垂直移动
            if (_useSideScrollerPhysics) v = 0f;
            var dir = new Vector2(h, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            // 动画不锁移动：玩家在攻击期间仍可走动/跳跃，避免动画手感低。
            _moveInput = dir;

            // ── 跳跃（仅横版启用时）──
            var jumpPressed = Input.GetKeyDown(_jumpKey) || Input.GetKeyDown(KeyCode.Space);
            var grounded = IsGrounded();
            if (_useSideScrollerPhysics &&
                jumpPressed &&
                grounded)
            {
                _rb.velocity = new Vector2(_rb.velocity.x, _jumpForce);
                grounded = false;
            }

            var moving = _moveInput.sqrMagnitude > _idleSpeedEpsilon * _idleSpeedEpsilon;
            if (moving && Mathf.Abs(h) > 0.01f) SetFacing(h > 0f);

            // ── 攻击 ──
            // 守门：上一次攻击动画未结束前忽略新点击，避免延长 嘴 _attackLockUntil 但不重播动画导致 sprite 卡末帧。
            if (_enableMouseAttack && Input.GetMouseButtonDown(0) && Time.time >= _attackLockUntil)
                TriggerAttack();
            if (Time.time < _attackLockUntil)
                ApplyAttackHit();

            // ── 动作切换 ──
            UpdateLayeredActions(moving, grounded);
            UpdateAttackRangeHintTransform();
            UpdatePlayerHud();
        }

        private void OnDestroy()
        {
            if (!EventProcessor.HasInstance) return;
            var eventProcessor = EventProcessor.Instance;
            if (eventProcessor == null) return;
            eventProcessor.TriggerEventMethod("UnregisterUIEntity", new List<object> { GetHudId() });
        }

        private void FixedUpdate()
        {
            var speed = _speed * (Input.GetKey(KeyCode.LeftShift) ? _sprintMultiplier : 1f);
            if (_useSideScrollerPhysics)
            {
                // 只驱动 X 速度；Y 交物理（重力 + 跳跃），避免覆盖体现为"飘"
                _rb.velocity = new Vector2(_moveInput.x * speed, _rb.velocity.y);
            }
            else
            {
                _rb.velocity = _moveInput * speed;
            }
        }

        public void TakeDamage(float damage)
        {
            _hp = Mathf.Max(0f, _hp - Mathf.Max(0f, damage));
            UpdatePlayerHud(true);
        }

        /// <summary>脚下小距离碍撞检测：以 collider 底点为起点向下抔射。</summary>
        private bool IsGrounded()
        {
            if (_collider == null) return false;
            var origin = (Vector2)transform.position + _collider.offset;
            origin.y -= _collider.radius;
            // 主动忽略自身的 collider：取除本层 → 实际上用 RaycastNonAlloc 更严谨；
            // 为保简洁这里直接 Raycast，后续可改用 LayerMask 过滤。
            var hit = Physics2D.Raycast(origin, Vector2.down, _groundCheckDistance);
            return hit.collider != null && hit.collider != _collider;
        }

        private void LateUpdate()
        {
            if (!_autoFollowMainCamera || _cam == null) return;
            var p = transform.position;
            var c = _cam.transform.position;
            var targetY = _lockCameraY ? _lockedCameraY : p.y;
            var target = new Vector3(p.x, targetY, c.z);
            _cam.transform.position = _cameraFollowSmoothing >= 1f
                ? target
                : Vector3.Lerp(c, target, 1f - Mathf.Pow(1f - _cameraFollowSmoothing, Time.deltaTime * 60f));
        }

        /// <summary>外部（如 TribeGameManager）设置镜头锁定 Y；<paramref name="enable"/>=true 同时启用锁定。</summary>
        public void SetLockedCameraY(float y, bool enable = true)
        {
            _lockedCameraY = y;
            _lockCameraY = enable;
            // 立即渐进一步，避免启动帧眼看镜头从玩家处跳开
            if (_cam != null && enable)
            {
                var c = _cam.transform.position;
                _cam.transform.position = new Vector3(c.x, y, c.z);
            }
        }

        // ─── Character spawn / 朝向 / 动作切换 ────────────────────
        private void SpawnCharacter()
        {
            if (!EventProcessor.HasInstance)
            {
                Debug.LogWarning("[TribePlayer] EventProcessor 未就绪，跳过 Character 创建");
                return;
            }
            var result = EventProcessor.Instance.TriggerEventMethod(
                CharacterManager.EVT_CREATE_CHARACTER,
                new List<object>
                {
                    _characterConfigId,
                    _instanceId,
                    transform,                  // parent
                    transform.position,         // 初始世界坐标
                });

            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not Transform root)
            {
                Debug.LogWarning($"[TribePlayer] 创建 Character 失败: configId={_characterConfigId}, instanceId={_instanceId}");
                return;
            }
            _characterRoot = root;
            _characterRoot.localPosition = new Vector3(0f, _characterVisualYOffset, 0f);
            _characterRoot.localScale = Vector3.one * _characterVisualScale;
            UpdateAction("Idle");
        }

        private void SetFacing(bool right)
        {
            if (_facingRight == right || _characterRoot == null) return;
            _facingRight = right;
            var s = _characterRoot.localScale;
            s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
            _characterRoot.localScale = s;
        }

        private void UpdateAction(string actionName)
        {
            if (string.IsNullOrEmpty(actionName) || actionName == _currentAction) return;
            _currentAction = actionName;
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_instanceId)) return;
            EventProcessor.Instance.TriggerEventMethod(
                CharacterManager.EVT_PLAY_ACTION,
                new List<object> { _instanceId, actionName });
        }

        private void UpdateLayeredActions(bool moving, bool grounded)
        {
            var moveAction = moving ? "Walk" : "Idle";
            var bodyAction = _useSideScrollerPhysics && !grounded ? "Jump" : moveAction;

            for (var i = 0; i < _bodyMotionParts.Length; i++)
                UpdatePartAction(_bodyMotionParts[i], bodyAction);

            for (var i = 0; i < _movementParts.Length; i++)
                UpdatePartAction(_movementParts[i], moveAction);

            if (Time.time >= _attackLockUntil)
            {
                for (var i = 0; i < _attackParts.Length; i++)
                    UpdatePartAction(_attackParts[i], moveAction);
            }
        }

        private void UpdatePartAction(string partId, string actionName)
        {
            if (string.IsNullOrEmpty(partId) || string.IsNullOrEmpty(actionName)) return;
            if (_partActions.TryGetValue(partId, out var current) && current == actionName) return;
            _partActions[partId] = actionName;
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_instanceId)) return;
            EventProcessor.Instance.TriggerEventMethod(
                CharacterManager.EVT_PLAY_ACTION,
                new List<object> { _instanceId, actionName, partId });
        }

        private void TriggerAttack()
        {
            // 仅锁动作状态机（期间 UpdateAction 会强制保持 "Attack"）；不动物理，玩家仍可移动/受重力。
            _attackLockUntil = Time.time + _attackDuration;
            for (var i = 0; i < _attackParts.Length; i++)
            {
                _partActions.Remove(_attackParts[i]);
                UpdatePartAction(_attackParts[i], "Attack");
            }
            _currentAttackHits.Clear();
            ShowAttackRangeHint();
        }

        private void ApplyAttackHit()
        {
            var center = GetAttackCenter();
            var hits = Physics2D.OverlapBoxAll(center, GetAttackBoxSize(), 0f);
            for (var i = 0; i < hits.Length; i++)
            {
                var target = hits[i].GetComponentInParent<PickableDropEntity>();
                if (target != null)
                {
                    if (!_currentAttackHits.Add(target)) continue;
                    target.TakeHit(_attackDamage);
                    continue;
                }
                var enemy = hits[i].GetComponentInParent<TribeSkeletonEnemy>();
                if (enemy == null) continue;
                if (!_currentAttackHits.Add(enemy)) continue;
                enemy.TakeHit(_attackDamage);
            }
        }

        private Vector2 GetAttackCenter()
        {
            var direction = _facingRight ? 1f : -1f;
            return (Vector2)transform.position + new Vector2(direction * (_colliderRadius + _attackRange * 0.5f), _attackYOffset);
        }

        private Vector2 GetAttackBoxSize()
        {
            var height = Mathf.Max(_attackHeight, _colliderRadius * 2f);
            return new Vector2(_attackRange, height);
        }

        private void ShowAttackRangeHint()
        {
            if (!_showAttackRangeHint) return;
            if (_attackRangeHintRenderer == null) CreateAttackRangeHint();
            if (_attackRangeHintRenderer == null) return;

            var size = GetAttackBoxSize();
            UpdateAttackRangeHintTransform(size);
            _attackRangeHintRenderer.color = _attackRangeHintColor;
            _attackRangeHintRenderer.gameObject.SetActive(true);
            CancelInvoke(nameof(HideAttackRangeHint));
            Invoke(nameof(HideAttackRangeHint), _attackDuration);
        }

        private void UpdateAttackRangeHintTransform()
        {
            UpdateAttackRangeHintTransform(GetAttackBoxSize());
        }

        private void UpdateAttackRangeHintTransform(Vector2 size)
        {
            if (_attackRangeHintRenderer == null || !_attackRangeHintRenderer.gameObject.activeSelf) return;
            _attackRangeHintRenderer.transform.position = GetAttackCenter();
            _attackRangeHintRenderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        private void HideAttackRangeHint()
        {
            if (_attackRangeHintRenderer != null)
                _attackRangeHintRenderer.gameObject.SetActive(false);
        }

        private void CreateAttackRangeHint()
        {
            var go = new GameObject("AttackRangeHint");
            go.transform.SetParent(transform, false);
            _attackRangeHintRenderer = go.AddComponent<SpriteRenderer>();
            _attackRangeHintRenderer.sprite = CreateRectSprite();
            _attackRangeHintRenderer.sortingOrder = 1000;
            _attackRangeHintRenderer.gameObject.SetActive(false);
        }

        private static Sprite CreateRectSprite()
        {
            const int size = 16;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, Color.white);
                }
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void CreatePlayerHud()
        {
            if (!_showPlayerHud || !EventProcessor.HasInstance) return;

            var hud = new UIPanelComponent(GetHudId(), "TribePlayerHud")
                .SetPosition(16f, -16f)
                .SetSize(560f, 132f)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(true);

            var headFrame = new UIPanelComponent($"{GetHudId()}_head", "Head")
                .SetPosition(66f, -62f)
                .SetSize(112f, 112f)
                .SetBackgroundSpriteId("Head")
                .SetBackgroundColor(Color.white)
                .SetVisible(true);
            _hudHeadSprite = new UIPanelComponent($"{GetHudId()}_head_sprite", "HeadSprite")
                .SetPosition(56f, 56f)
                .SetSize(140f, 140f)
                .SetBackgroundColor(Color.white)
                .SetVisible(true);

            _hudHpBar = CreateHudBar("hp", 264f, -18f, 280f, 28f, "Bar_1", "RedBar", new Color(1f, 0.25f, 0.25f));
            _hudMpBar = CreateHudBar("mp", 239f, -44f, 230f, 24f, "Bar_2", "BlueBar", new Color(0.25f, 0.55f, 1f));
            _hudExperienceBar = CreateHudBar("exp", 239f, -68f, 230f, 24f, "Bar_2", "BrownBar", new Color(0.55f, 0.32f, 0.15f));

            var coinContainer = new UIPanelComponent($"{GetHudId()}_coins_bg", "CoinContainer")
                .SetPosition(174f, -92.5f)
                .SetSize(100f, 25f)
                .SetBackgroundSpriteId("CoinContainer")
                .SetBackgroundColor(Color.white)
                .SetVisible(true);

            _hudHpValueText = CreateHudValueText("hp_value", 0f, 0f, 280f, 28f);
            _hudMpValueText = CreateHudValueText("mp_value", 0f, 0f, 230f, 24f);
            _hudExperienceValueText = CreateHudValueText("exp_value", 0f, 0f, 230f, 24f);
            _hudCoinsText = CreateHudValueText("coins", 0f, 0f, 100f, 25f);

            _hudHpBar.AddChild(_hudHpValueText);
            _hudMpBar.AddChild(_hudMpValueText);
            _hudExperienceBar.AddChild(_hudExperienceValueText);
            coinContainer.AddChild(_hudCoinsText);
            headFrame.AddChild(_hudHeadSprite);
            hud.AddChild(headFrame);
            hud.AddChild(_hudHpBar);
            hud.AddChild(_hudMpBar);
            hud.AddChild(_hudExperienceBar);
            hud.AddChild(coinContainer);

            var result = EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity", new List<object> { GetHudId(), hud });
            if (!ResultCode.IsOk(result))
            {
                Debug.LogWarning("[TribePlayer] 创建玩家 HUD 失败");
                return;
            }

            AnchorPlayerHudToTopLeft();
            UpdateHudHeadSprite();
            UpdatePlayerHud(true);
        }

        private void AnchorPlayerHudToTopLeft()
        {
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { GetHudId() });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject hudGo) return;
            if (!hudGo.TryGetComponent<RectTransform>(out var rect)) return;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
        }

        private UIBarComponent CreateHudBar(string idSuffix, float x, float y, float width, float height, string backgroundSpriteId, string fillSpriteId, Color fillColor)
        {
            return new UIBarComponent($"{GetHudId()}_{idSuffix}_bar", idSuffix)
                .SetPosition(x, y)
                .SetSize(width, height)
                .SetRange(0f, 1f)
                .SetValue(1f)
                .SetBackgroundSpriteId(backgroundSpriteId)
                .SetFillSpriteId(fillSpriteId)
                .SetFillPadding(10f, 6f)
                .SetBackgroundColor(Color.white)
                .SetFillColor(fillColor)
                .SetVisible(true);
        }

        private UITextComponent CreateHudText(string idSuffix, float x, float y, Color color)
        {
            return CreateHudText(idSuffix, x, y, 180f, 40f, color);
        }

        private UITextComponent CreateHudText(string idSuffix, float x, float y, float width, float height, Color color)
        {
            return new UITextComponent($"{GetHudId()}_{idSuffix}", idSuffix)
                .SetPosition(x, y)
                .SetSize(width, height)
                .SetFontSize(24)
                .SetColor(color)
                .SetAlignment(TextAnchor.MiddleCenter)
                .SetText(string.Empty)
                .SetVisible(true);
        }

        private UITextComponent CreateHudValueText(string idSuffix, float x, float y, float width, float height)
        {
            const float sample = 4f;
            return new UITextComponent($"{GetHudId()}_{idSuffix}", idSuffix)
                .SetPosition(x * sample, y * sample)
                .SetSize(width * sample, height * sample)
                .SetScale(1f / sample, 1f / sample)
                .SetFontSize(Mathf.RoundToInt(18f * sample))
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleCenter)
                .SetText(string.Empty)
                .SetVisible(true);
        }

        private void UpdateHudHeadSprite()
        {
            if (_hudHeadSprite == null || _characterRoot == null) return;
            var head = _characterRoot.Find("Head");
            var renderer = head != null ? head.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null) return;
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { _hudHeadSprite.Id });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject headGo) return;
            var image = headGo.GetComponent<UnityEngine.UI.Image>();
            if (image == null) return;
            image.sprite = renderer.sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            _hudHeadSpriteReady = true;
        }

        private void UpdatePlayerHud(bool force = false)
        {
            if (!_showPlayerHud || _hudHpBar == null) return;
            if (!_hudHeadSpriteReady) UpdateHudHeadSprite();

            if (force || !Mathf.Approximately(_hp, _lastHudHp) || !Mathf.Approximately(_maxHp, _lastHudMaxHp))
            {
                _lastHudHp = _hp;
                _lastHudMaxHp = _maxHp;
                _hudHpBar.SetValue(_hp, _maxHp);
                _hudHpValueText.SetText($"{Mathf.CeilToInt(_hp)}/{Mathf.CeilToInt(_maxHp)}");
            }
            if (force || !Mathf.Approximately(_mp, _lastHudMp) || !Mathf.Approximately(_maxMp, _lastHudMaxMp))
            {
                _lastHudMp = _mp;
                _lastHudMaxMp = _maxMp;
                _hudMpBar.SetValue(_mp, _maxMp);
                _hudMpValueText.SetText($"{Mathf.CeilToInt(_mp)}/{Mathf.CeilToInt(_maxMp)}");
            }
            if (force || _experience != _lastHudExperience || _maxExperience != _lastHudMaxExperience)
            {
                _lastHudExperience = _experience;
                _lastHudMaxExperience = _maxExperience;
                _hudExperienceBar.SetValue(_experience, _maxExperience);
                _hudExperienceValueText.SetText($"{_experience}/{_maxExperience}");
            }
            if (force || _coins != _lastHudCoins)
            {
                _lastHudCoins = _coins;
                _hudCoinsText.SetText(_coins.ToString());
            }
        }

        private Vector2 GetCanvasSize()
        {
            var result = EventProcessor.Instance.TriggerEventMethod("GetUICanvasTransform", null);
            if (ResultCode.IsOk(result) && result.Count >= 2 && result[1] is RectTransform rect)
            {
                var size = rect.rect.size;
                if (size.x > 0f && size.y > 0f) return size;
            }
            return new Vector2(1920f, 1080f);
        }

        private string GetHudId()
        {
            return $"{_instanceId}_Hud";
        }

        // ─── 背包 / 对话 切换 ────────────────────────────────────
        /// <summary>
        /// 切换背包 UI。状态以 <c>UIManager</c> 实际可见性为准（防止 UI 上的 × 关闭后本地 flag 失步）。
        /// </summary>
        private void ToggleInventory()
        {
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_inventoryId)) return;
            var visible = IsUIVisible(_inventoryId);
            EventProcessor.Instance.TriggerEventMethod(
                visible ? InventoryManager.EVT_CLOSE_UI : InventoryManager.EVT_OPEN_UI,
                visible
                    ? new List<object> { _inventoryId }
                    : new List<object> { _inventoryId, _inventoryConfigId });
        }

        /// <summary>切换调试对话 UI（§4.1 bare-string；不直接 using DialogueManager）。</summary>
        private void ToggleDialogue()
        {
            if (!EventProcessor.HasInstance) return;

            var current = EventProcessor.Instance.TriggerEventMethod(
                "QueryDialogueCurrent", new List<object>());
            if (ResultCode.IsOk(current))
            {
                EventProcessor.Instance.TriggerEventMethod("CloseDialogueUI", new List<object>());
                return;
            }

            var result = EventProcessor.Instance.TriggerEventMethod(
                "OpenDialogueUI", new List<object> { _dialogueId });
            if (!ResultCode.IsOk(result))
            {
                var msg = result != null && result.Count >= 2 ? result[1] : "unknown";
                Debug.LogWarning($"[TribePlayer] 打开对话失败: {msg}（请确认 DialogueManager 已挂载且对话 Id `{_dialogueId}` 已注册）");
            }
        }

        /// <summary>查 UIManager：指定 UI 实体当前是否可见（走 §4.1 bare-string）。</summary>
        private bool IsUIVisible(string daoId)
        {
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { daoId });
            if (!ResultCode.IsOk(r) || r.Count < 2) return false;
            return r[1] is GameObject go && go != null && go.activeInHierarchy;
        }

        // ─── 公开 API ─────────────────────────────────────────────
        /// <summary>对外暴露 Character 根节点（供 MapView.FollowTarget 等使用）。</summary>
        public Transform CharacterRoot => _characterRoot != null ? _characterRoot : transform;
    }
}
