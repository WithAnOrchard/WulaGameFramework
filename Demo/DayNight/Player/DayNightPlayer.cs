using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.InventoryManager;

namespace Demo.DayNight.Player
{
    /// <summary>
    /// 昼夜求生 Demo 玩家 —— 用框架 <see cref="DefaultCharacterConfigs.WarriorId"/> 战士做角色。
    /// <para>
    /// 行为：
    /// <list type="bullet">
    /// <item>WASD / 方向键平面移动；按 Shift 冲刺</item>
    /// <item>移动时自动播放 <c>Walk</c> 动作，停下播 <c>Idle</c></item>
    /// <item>面朝运动方向（左移翻转 X scale）</item>
    /// <item>可选 Main Camera 跟随；自动注册为 MapView 的 FollowTarget（区块流式渲染跟随玩家）</item>
    /// <item>同 GameObject 自动挂载 <see cref="ChunkInfoOverlay"/> 显示当前 tile 群系信息</item>
    /// </list>
    /// </para>
    /// <para>
    /// **跨模块解耦**：通过 <c>EventProcessor.TriggerEventMethod</c> 调
    /// <see cref="CharacterManager.EVT_CREATE_CHARACTER"/> 等事件，**不直接** <c>using</c> CharacterManager 模块 API。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    public class DayNightPlayer : MonoBehaviour
    {
        [Header("Character")]
        [Tooltip("使用的 CharacterConfig ID。默认 Warrior（CharacterManager 启动时自动注册）。")]
        [SerializeField] private string _characterConfigId = DefaultCharacterConfigs.WarriorId;

        [Tooltip("Character 实例 ID（场景内唯一）。")]
        [SerializeField] private string _instanceId = "DayNightPlayer";

        [Tooltip("Character 视觉缩放（仅作用在 Character 子节点；不影响碰撞体大小）。默认 10x。")]
        [SerializeField, Min(0.1f)] private float _characterVisualScale = 10f;

        [Header("Movement")]
        [Tooltip("移动速度（unit / 秒）。Tilemap 每格 1 unit。")]
        [SerializeField, Min(0.1f)] private float _speed = 6f;

        [Tooltip("按住 Shift 时的速度倍率。")]
        [SerializeField, Min(1f)] private float _sprintMultiplier = 2.5f;

        [Tooltip("低于此速度阈值视为停下，播 Idle。")]
        [SerializeField, Min(0f)] private float _idleSpeedEpsilon = 0.05f;

        [Header("Physics")]
        [Tooltip("CircleCollider2D 世界半径（不受 _characterVisualScale 影响）。")]
        [SerializeField, Min(0.05f)] private float _colliderRadius = 0.45f;

        [Tooltip("Rigidbody2D 阻尼（drag）：值越大停下越快，避免惯性滑行。")]
        [SerializeField, Min(0f)] private float _linearDrag = 12f;

        [Header("Combat")]
        [Tooltip("鼠标左键播放 Attack 动作。")]
        [SerializeField] private bool _enableMouseAttack = true;

        [Tooltip("Attack 锁定时长（秒）。这段时间内忽略 Walk/Idle 切换；动作完整播放后回到默认。")]
        [SerializeField, Min(0.05f)] private float _attackDuration = 0.4f;

        [Header("Inventory")]
        [Tooltip("玩家容器实例 ID（InventoryManager 默认创建 'player'）。")]
        [SerializeField] private string _inventoryId = "player";

        [Tooltip("打开 UI 时使用的 InventoryConfig ID（InventoryManager 默认注册 'PlayerBackPack'）。")]
        [SerializeField] private string _inventoryConfigId = "PlayerBackPack";

        [Tooltip("背包开关键。")]
        [SerializeField] private KeyCode _inventoryToggleKey = KeyCode.B;

        [Header("Dialogue")]
        [Tooltip("是否启用对话测试。按 _dialogueToggleKey 打开/关闭测试对话。")]
        [SerializeField] private bool _enableDialogueTest = true;

        [Tooltip("打开的对话 Id（DialogueManager 默认会注册 DebugDialogue 供调试）。")]
        [SerializeField] private string _dialogueId = "DebugDialogue";

        [Tooltip("对话开关键。")]
        [SerializeField] private KeyCode _dialogueToggleKey = KeyCode.I;

        [Header("Camera Follow")]
        [Tooltip("让 Main Camera 跟随玩家。仅平移，不改 Z 与 orthographicSize。")]
        [SerializeField] private bool _autoFollowMainCamera = true;

        [Tooltip("相机跟随的 Lerp 平滑系数（0=瞬移，1=无延迟）。")]
        [SerializeField, Range(0f, 1f)] private float _cameraFollowSmoothing = 0.18f;

        [Header("Visualization")]
        [Tooltip("是否自动给本节点挂 ChunkInfoOverlay 显示当前 tile 信息。")]
        [SerializeField] private bool _attachChunkInfoOverlay = true;

        // ─── 运行时 ────────────────────────────────────────────────
        private Transform _characterRoot;
        private Camera _cam;
        private string _currentAction;
        private bool _facingRight = true;
        private Rigidbody2D _rb;
        private CircleCollider2D _collider;
        private float _attackLockUntil;     // Time.time 大于此值时 Attack 锁解除
        private Vector2 _moveInput;         // 上一帧输入（FixedUpdate 用）

        private void Awake()
        {
            // RequireComponent 保证两者存在
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CircleCollider2D>();
            ConfigurePhysics();
        }

        private void Start()
        {
            // ① 拿 Main Camera 引用
            if (_autoFollowMainCamera) _cam = Camera.main;

            // ② 通过事件创建战士 Character —— 不直接 using CharacterService
            SpawnCharacter();

            // ③ 挂 ChunkInfoOverlay（同 GO；ChunkInfoOverlay 读 transform.position）
            if (_attachChunkInfoOverlay && GetComponent<ChunkInfoOverlay>() == null)
                gameObject.AddComponent<ChunkInfoOverlay>();
        }

        private void ConfigurePhysics()
        {
            _rb.gravityScale = 0f;
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
            // ── 背包开关（B 键）──
            if (Input.GetKeyDown(_inventoryToggleKey))
                ToggleInventory();

            // ── 对话开关（I 键）──
            if (_enableDialogueTest && Input.GetKeyDown(_dialogueToggleKey))
                ToggleDialogue();

            // ── 攻击输入（Update 处理 GetMouseButtonDown 才能精确捕获单帧）──
            if (_enableMouseAttack && Input.GetMouseButtonDown(0))
                TriggerAttack();

            // ── 移动输入读取（实际位移在 FixedUpdate 走物理）──
            var h = Input.GetAxisRaw("Horizontal");
            var v = Input.GetAxisRaw("Vertical");
            var dir = new Vector2(h, v);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            var attackLocked = Time.time < _attackLockUntil;
            _moveInput = attackLocked ? Vector2.zero : dir;

            var moving = _moveInput.sqrMagnitude > _idleSpeedEpsilon * _idleSpeedEpsilon;
            if (moving && Mathf.Abs(h) > 0.01f) SetFacing(h > 0f);

            // ── 动作切换 ──
            if (attackLocked)
                UpdateAction("Attack");
            else
                UpdateAction(moving ? "Walk" : "Idle");
        }

        private void FixedUpdate()
        {
            // 走 Rigidbody2D.velocity，让 IslandBoundary 的 EdgeCollider2D 能正常拦住玩家。
            // 直接 transform.position += ... 会穿墙；走物理才有碰撞响应。
            var speed = _speed * (Input.GetKey(KeyCode.LeftShift) ? _sprintMultiplier : 1f);
            _rb.velocity = _moveInput * speed;
        }

        /// <summary>触发一次 Attack 动作；锁定窗口期内忽略移动→Walk/Idle 切换。</summary>
        private void TriggerAttack()
        {
            _attackLockUntil = Time.time + _attackDuration;
            // 攻击瞬间清掉残余速度，避免边跑边攻击的"飘"
            if (_rb != null) _rb.velocity = Vector2.zero;
            UpdateAction("Attack");
        }

        /// <summary>
        /// 切换背包 UI —— 通过事件调 <see cref="InventoryManager.EVT_OPEN_UI"/> /
        /// <see cref="InventoryManager.EVT_CLOSE_UI"/>。
        /// <para>**状态以 UIManager 实际可见性为准**，不维护本地 bool —— 防止 UI 上的 × 按钮关闭后
        /// 与本地 flag 失步（之前的 bug：先点 ×，再按 B，会触发"再次关闭"而非打开）。</para>
        /// </summary>
        private void ToggleInventory()
        {
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_inventoryId)) return;
            var visible = IsInventoryUIVisible();
            EventProcessor.Instance.TriggerEventMethod(
                visible ? InventoryManager.EVT_CLOSE_UI : InventoryManager.EVT_OPEN_UI,
                visible
                    ? new List<object> { _inventoryId }
                    : new List<object> { _inventoryId, _inventoryConfigId });
        }

        /// <summary>
        /// 切换调试对话 UI —— 通过事件调 DialogueManager 命令
        /// （§4.1 跨模块 bare-string 协议，**不**直接 using DialogueManager）。
        /// 已有活动对话则关闭，否则打开 <see cref="_dialogueId"/> 对应对话。
        /// </summary>
        private void ToggleDialogue()
        {
            if (!EventProcessor.HasInstance) return;

            // §4.1 跨模块 bare-string：DialogueService.EVT_QUERY_CURRENT
            var current = EventProcessor.Instance.TriggerEventMethod(
                "QueryDialogueCurrent", new List<object>());

            if (ResultCode.IsOk(current))
            {
                // §4.1 跨模块 bare-string：DialogueManager.EVT_CLOSE_UI
                EventProcessor.Instance.TriggerEventMethod(
                    "CloseDialogueUI", new List<object>());
                return;
            }

            // §4.1 跨模块 bare-string：DialogueManager.EVT_OPEN_UI
            var result = EventProcessor.Instance.TriggerEventMethod(
                "OpenDialogueUI",
                new List<object> { _dialogueId });
            if (!ResultCode.IsOk(result))
            {
                var msg = result != null && result.Count >= 2 ? result[1] : "unknown";
                Debug.LogWarning($"[DayNightPlayer] 打开对话失败: {msg}（请确认 DialogueManager 已挂载且对话 Id `{_dialogueId}` 已注册）");
            }
        }

        /// <summary>查 UIManager：背包 UI 实体当前是否处于显示状态。</summary>
        private bool IsInventoryUIVisible()
        {
            // 走字符串协议（UIManager.EVT_GET_UI_GAMEOBJECT），不引用 UIManager 模块类
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject",
                new List<object> { _inventoryId });
            if (!ResultCode.IsOk(r) || r.Count < 2) return false;
            // activeInHierarchy 才反映真正可见（任意祖先 inactive 都算不可见）
            return r[1] is GameObject go && go != null && go.activeInHierarchy;
        }

        private void LateUpdate()
        {
            if (!_autoFollowMainCamera || _cam == null) return;
            var p = transform.position;
            var c = _cam.transform.position;
            var target = new Vector3(p.x, p.y, c.z);
            _cam.transform.position = _cameraFollowSmoothing >= 1f
                ? target
                : Vector3.Lerp(c, target, 1f - Mathf.Pow(1f - _cameraFollowSmoothing, Time.deltaTime * 60f));
        }

        // ─── Character spawn / 朝向 / 动作切换 ─────────────────────
        private void SpawnCharacter()
        {
            if (!EventProcessor.HasInstance)
            {
                Debug.LogWarning("[DayNightPlayer] EventProcessor 未就绪，跳过 Character 创建");
                return;
            }
            // 创建在自身 Transform 下，position 留 0 让 Character 跟随父节点位移
            var result = EventProcessor.Instance.TriggerEventMethod(
                CharacterManager.EVT_CREATE_CHARACTER,
                new List<object>
                {
                    _characterConfigId,
                    _instanceId,
                    transform,                      // parent
                    transform.position,             // 初始世界坐标 = player 当前位置
                });

            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not Transform root)
            {
                Debug.LogWarning($"[DayNightPlayer] 创建 Character 失败: configId={_characterConfigId}, instanceId={_instanceId}");
                return;
            }
            _characterRoot = root;
            _characterRoot.localPosition = Vector3.zero;
            // 视觉放大（仅作用 Character 子节点；不影响 DayNightPlayer 自身的物理碰撞体大小）
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

        // ─── 公开 API ─────────────────────────────────────────────
        /// <summary>对外暴露 Character 的根节点，供 MapView.FollowTarget 等使用。</summary>
        public Transform CharacterRoot => _characterRoot != null ? _characterRoot : transform;
    }
}
