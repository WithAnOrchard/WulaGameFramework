using UnityEngine;
using EssSystem.Core.Base;
using EssSystem.Core.Presentation.UIManager;
using EssSystem.Core.Presentation.UIManager.Theme;
using BiliBiliDanmu;
using BiliBiliDanmu.UI;
using BiliBiliLive;
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Foundation.NetworkManager;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Core.Platform;
using EssSystem.Core.Platform.Windows;
using NetMgr = EssSystem.Core.Foundation.NetworkManager.NetworkManager;
using CharMgr = EssSystem.Core.Presentation.CharacterManager.CharacterManager;
using BiliBiliDanmu.Auth;
using Demo.DobeCat.Game.Pet;
using Demo.DobeCat.Sys.Tray;
using Demo.DobeCat.Sys.UI;
using Demo.DobeCat.Game.Farm;
using Demo.DobeCat.Game.Shop;
using Demo.DobeCat.Game;
using Demo.DobeCat.Game.Live;
using Demo.DobeCat.Sys.Network;
using Demo.DobeCat.Sys;
using Demo.DobeCat.Sys.Audio;

namespace Demo.DobeCat
{
    /// <summary>
    /// DobeCat 桌面宠物 Demo 总控。
    /// <para>继承 <see cref="AbstractGameManager"/> 复用框架的基础 Manager 自动接管
    /// （EventProcessor / DataManager / ResourceManager / AudioManager / UIManager）。</para>
    /// <para>M1 阶段职责：</para>
    /// <list type="number">
    /// <item>调整 Camera 为透明背景 + 正交。</item>
    /// <item>生成一个占位桌宠，挂上 wander / drag / click-through 驱动。</item>
    /// <item>提供热键：F1 隐藏/显示，F2 切置顶，Esc 退出。</item>
    /// </list>
    /// </summary>
    public class DobeCatGameManager : AbstractGameManager
    {
        [Header("Camera")]
        [Tooltip("启动时 Camera 的正交大小（决定屏幕 1 单位等于多少世界单位）。")]
        [SerializeField] private float _cameraOrthoSize = 5f;

        [Header("Pet Spawn")]
        [Tooltip("自动生成一只占位桌宠。")]
        [SerializeField] private bool _autoSpawnPet = true;

        [Tooltip("桌宠视觉缩放（影响整个 Character 根）。默认 25x（PixArt sheet 单帧 ~32px，屏幕 1080p 下需要这么大才看得清）。")]
        [SerializeField] private float _petScale = 25f;

        [Tooltip("桌宠生成位置（世界坐标）。")]
        [SerializeField] private Vector3 _petSpawnPos = Vector3.zero;

        [Tooltip("CharacterManager 注册的 ConfigId（默认 'Warrior'，可改 'Mage' / 'SmallTreeChar_1' 等）。")]
        [SerializeField] private string _characterConfigId = "Warrior";

        [Tooltip("远端幽灵桌宠使用的 ConfigId（与本机区分，默认 'Mage'）。")]
        [SerializeField] private string _ghostCharacterConfigId = "Mage";

        [Tooltip("桌宠移动速度（IMovable.MoveSpeed；玩家 WASD 与 wander 共用）。")]
        [SerializeField, Min(0.1f)] private float _wasdMoveSpeed = 4f;

        [Header("BiliBili Danmu — Mode")]
        [Tooltip("弹幕接入模式：\nPolling=零认证（仅文字弹幕，3s 延迟）\nToken=登录 cookie（实时 + 礼物 + SC）\nOpenLive=主播身份码（实时 + 礼物 + SC，仅自己直播间）")]
        [SerializeField] private DanmuMode _danmuMode = DanmuMode.Polling;
        [Tooltip("启动时自动按上面的 Mode 连接。")]
        [SerializeField] private bool _bilibiliAutoConnect = true;

        [Header("BiliBili Danmu — Polling / Token 通用")]
        [Tooltip("直播间号（短号或真实号都可）。OpenLive 模式可留空，会从身份码反查。")]
        [SerializeField] private long _danmuRoomId = 0;
        [Tooltip("Polling 模式轮询间隔（秒），最低 1.5s。")]
        [SerializeField, Min(1.5f)] private float _pollIntervalSeconds = 3f;

        [Header("BiliBili Danmu — Token 模式 cookie")]
        [Tooltip("【SESSDATA】= B 站登录 cookie，等同账号密码。\n\n如何获取：\n  1. 浏览器登录 https://www.bilibili.com\n  2. F12 → Application → Cookies → bilibili.com\n  3. 找名为 SESSDATA 的那一行，复制其 Value（一段 URL-encoded 长串）\n\n⚠️ 安全：泄露后他人可以登录你的账号；不要 commit 到公开仓库。")]
        [TextArea(2, 4)]
        [SerializeField] private string _sessdata = "dff5c774%2C1794564158%2C6d18c%2A51CjA2jc857610MMUb9emwlp5TWnnIVV9FlCCgkOzmxmqWEJdznmr9zN44-sIJzbbUjX4SVnZtUTd6SlNBUjhfVVNwRzlua2FBb0x0Q05UTlhRZHBzWFF4ZEtUM0d1OHJfV2FSWll3N2x5X0NoQ2pTeWo5bkRkWnBRLVNzVDhXQWtMZkRoSmJ0UGt3IIEC";
        [Tooltip("【bili_jct (CSRF Token)】= B 站防 CSRF 令牌，仅做主动写操作（发弹幕、送礼、点赞）时需要。\n\n如何获取：与 SESSDATA 同位置（F12 → Cookies → bilibili.com），找 bili_jct 复制 Value。\n\n纯接收弹幕（订阅 Polling/Token 模式）留空即可。")]
        [SerializeField] private string _biliJct = string.Empty;

        [Header("BiliBili Danmu — OpenLive 模式（主播身份码）")]
        [Tooltip("【主播身份码 (Identity Code)】= B 站官方开放平台给主播的鉴权码，仅自己能用。\n\n如何获取：\n  1. 打开 B 站直播姬\n  2. 顶部菜单「设置 → 开放平台」\n  3. 复制「身份码」字段（短码，约 13 位）\n\n用途：OpenLive 模式连自己的直播间（无需 SESSDATA cookie，但只能连自己）。")]
        [SerializeField] private string _bilibiliIdentityCode = string.Empty;
        [SerializeField] private long _bilibiliAppId = 1651388990835L;
        [SerializeField] private string _bilibiliSignEndpoint = "https://bopen.ceve-market.org/sign";
        [SerializeField] private string _bilibiliStartEndpoint = "https://live-open.biliapi.com/v2/app/start";
        [SerializeField, Min(1f)] private float _bilibiliHttpTimeout = 5f;

        [Header("Live Status Polling (no identity code needed)")]
        [Tooltip("B 站直播间号（不是 UID）。≤ 0 不启动开播检测。")]
        [SerializeField] private long _liveRoomId = 0;
        [Tooltip("启动后是否立即轮询开播状态。")]
        [SerializeField] private bool _liveAutoPoll = true;
        [Tooltip("轮询间隔（秒），建议 ≥ 15s。")]
        [SerializeField, Min(1f)] private float _livePollIntervalSeconds = 30f;

        [Header("Network (Mirror)")]
        [Tooltip("启动时根据该模式自动联网：\nNone=完全不联网\nHost=自动建房（Server+本地Client）\nServerOnly=纯专用服务器\nClient=自动加入服务器")]
        [SerializeField] private NetworkRole _netMode = NetworkRole.Host;
        [Tooltip("Client 模式连接的目标地址（Host/ServerOnly 模式忽略）")]
        [SerializeField] private string _netServerAddress = "localhost";
        [SerializeField] private ushort _netPort = 7777;

        [Header("Room Discovery (data_exchange_server)")]
        [Tooltip("启用房间发现：每个客户端启动时自动作为 Host 上报到中心服务器，\n其他玩家可在系统托盘里看到房间列表并加入。")]
        [SerializeField] private bool _roomDiscoveryEnabled = true;
        [Tooltip("数据收发器 Base URL（运行 tools/data_exchange_server/server.py）。")]
        [SerializeField] private string _roomDiscoveryServerUrl = "http://154.12.90.249:8765";
        [Tooltip("集合名（同名集合内的房间互相可见）。")]
        [SerializeField] private string _roomDiscoveryCollection = "rooms";
        [Tooltip("公布给他人加入的 Mirror Host 地址。留空 = 自动选本机首个非环回 IPv4。")]
        [SerializeField] private string _roomDiscoveryAdvertisedHost = "";
        [Tooltip("展示给他人的房间名。留空 = 设备名。")]
        [SerializeField] private string _roomDiscoveryDisplayName = "";

        [Header("Test Panel")]
        [Tooltip("启动时自动打开 DobeCatTestPanel，方便调试。生产环境改 false。")]
        [SerializeField] private bool _autoOpenTestPanel = true;

        [Header("Pet Randomization")]
        [Tooltip("是否启用部件随机化（启动时随机组合部件变体）。")]
        [SerializeField] private bool _enablePetRandomization = true;

        private GameObject _pet;
        private RoomDiscoveryClient _discovery;
        private DataExchangeSession _dataSession;
        private ActionsClient _actions;
        private bool _wasBKey;
        private bool _backpackOpen;
        private string _randomizedCharacterConfigId;
        private Demo.DobeCat.Game.Pet.PetHUD _petHud;

        protected override void Awake()
        {
            base.Awake();
            // 尽早关闭 VSync 并设定目标帧率，否则登录画面会受 QualitySettings.vSyncCount 限速
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            // 日志写入文件 — 尽早挂载，捕获完整启动日志
            gameObject.AddComponent<DobeCatLogger>();
            Application.runInBackground = true;
            Debug.Log($"[STARTUP] 当前分辨率（Awake 执行前）: {Screen.width}×{Screen.height}, fullscreen={Screen.fullScreen}");

            // 【关键】主题必须最早加载，在任何 UI 面板创建之前
            // 因为 LoginScreen 等面板的静态属性会在类加载时访问 DefaultUITheme
            DefaultUITheme.Instance.LoadSaved();
            Debug.Log($"[STARTUP] 主题已加载，当前索引: {DefaultUITheme.Instance.CurrentIndex}");

            // 礼物统计
            gameObject.AddComponent<GiftQueryService>();
            gameObject.AddComponent<DobeCatGiftStatsPanelView>();
            EnsureCamera();
            // 始终叠加层架构：Awake 同帧同时创建登录 UGUI 面板 + 启动叠加层协程
            // 登录面板作为透明叠加层上的居中 UGUI Canvas，无独立窗口、无任务栏、无可切换
            ShowLoginScreen();
            StartCoroutine(DesktopOverlay.Enter());
            Debug.Log("[DobeCatGameManager] Awake 完成，登录面板 + 叠加层协程已启动。");
        }

        private void ShowLoginScreen()
        {
            var holder = new GameObject(nameof(LoginScreen));
            holder.transform.SetParent(transform);
            var login = holder.AddComponent<LoginScreen>();
            login.OnLoginComplete = RunAfterLogin;
            Debug.Log("[DobeCatGameManager] 等待用户登录...");
        }

        /// <summary>登录通过后的初始化序列 —— 桌宠、网络、托盘、面板全在这里串起来。</summary>
        private void RunAfterLogin()
        {
            // 0) 叠加层已在启动时 Enter()，此处重新启用点击穿透，交给 PetClickThroughDriver 动态管理
            DesktopOverlay.SetClickThrough(true);

            // 1) 房间发现开启时强制以 Host 启动，确保自己能被别人加入
            if (_roomDiscoveryEnabled && _netMode != NetworkRole.Host)
            {
                Debug.Log($"[DobeCatGameManager] 房间发现已启用 → 覆盖 NetMode {_netMode} → Host");
                _netMode = NetworkRole.Host;
            }

            // 2) CharacterManager 须先于 SpawnPet 初始化（SpawnPet 可能用到 CharacterService）
            EnsureFrameworkManagers();
            DobeCatDialogueContent.EnsureRegistered(); // 注册所有对话内容
            
            // 3) 桌宠部件随机化
            if (_enablePetRandomization)
            {
                PrepareRandomizedPetConfig();
            }
            
            if (_autoSpawnPet) SpawnPet();
            TryAutoConnectDanmu();
            TryStartLivePolling();
            TryAutoStartNetwork();
            EnsureDataExchangeSession(); // 必须早于 RoomDiscovery：心跳上行要带它签发的 token
            EnsureRoomDiscovery();
            EnsureTray(); // 必须晚于 EnsureRoomDiscovery，否则 Tray 拿不到 _discovery
            EnsureFarmWorld();
            DobeCatGameContext.OnContextChanged += OnGameContextChanged;

            if (_autoOpenTestPanel)
            {
                // 推迟一帧确保所有 Manager Initialize 完成后再注册 UI
                StartCoroutine(OpenTestPanelDelayed());
            }
        }

        private void EnsureFarmWorld()
        {
            var holder = new GameObject("FarmWorld");
            holder.transform.SetParent(transform);
            holder.AddComponent<FarmWorldController>();

            if (_pet != null)
            {
                _pet.AddComponent<HotbarSelectionDriver>();
                _petHud = _pet.AddComponent<Demo.DobeCat.Game.Pet.PetHUD>();
                _petHud.OnBackpackToggleRequested = ToggleBackpack;
            }

            var shopHolder = new GameObject("ShopWindow");
            shopHolder.transform.SetParent(transform);
            shopHolder.AddComponent<ShopWindow>();

            var syncHolder = new GameObject("PlayerDataSync");
            syncHolder.transform.SetParent(transform);
            var sync = syncHolder.AddComponent<PlayerDataSync>();
            sync.ServerBaseUrl = _roomDiscoveryServerUrl;
            // 登录后立即从服务器拉取存档（背包/钱包/农场）；
            // 协程内部等待 2 帧 + 3 次 3s 间隔重试，足够 DataExchangeSession 拿到 token。
            sync.FetchAndRestore();

            // 直播经济：陪伴时长 / 弹幕 → 银币；礼物 → 金币
            var econHolder = new GameObject("LiveEconomy");
            econHolder.transform.SetParent(transform);
            econHolder.AddComponent<LiveEconomyController>();

            // B站动态 / 投稿提醒（§10.3）— UID 在 Inspector 配置
            var notifierHolder = new GameObject("BiliSpaceNotifier");
            notifierHolder.transform.SetParent(transform);
            notifierHolder.AddComponent<BiliSpaceNotifier>();

            // 天气播报（§7）— API key 在 Inspector 或 PlayerPrefs "WeatherApiKey" 配置
            var weatherHolder = new GameObject("WeatherNotifier");
            weatherHolder.transform.SetParent(transform);
            weatherHolder.AddComponent<WeatherNotifier>();
        }

        private void OnGameContextChanged(bool active)
        {
            if (!EventProcessor.HasInstance) return;
            var ep = EventProcessor.Instance;
            if (active)
            {
                // Hotbar 由 PetHUD 在手动模式按键/滚轮时自行管理，此处不再强制打开
            }
            else
            {
                // Hotbar 可见性由 PetHUD.Update 根据 AI 状态自动決定，不在这里强制关闭
                if (_backpackOpen)
                {
                    _backpackOpen = false;
                    ep.TriggerEventMethod("CloseInventoryUI",
                        new List<object> { "player" });
                }
                _petHud?.SetBackpackOpen(false);
            }
        }

        /// <summary>背包开关（B 键 / 宠物 HUD 按钮共用入口，保证 _backpackOpen 单点管理）。</summary>
        private void ToggleBackpack()
        {
            if (!EventProcessor.HasInstance) return; // 背包无需游戏上下文，随时可开
            _backpackOpen = !_backpackOpen;
            EventProcessor.Instance.TriggerEventMethod(
                _backpackOpen ? "OpenInventoryUI" : "CloseInventoryUI",
                new List<object> { "player", "PlayerBackPack" });
            _petHud?.SetBackpackOpen(_backpackOpen);
        }

        private System.Collections.IEnumerator OpenTestPanelDelayed()
        {
            yield return null; // 等下一帧
            DobeCatTestPanel.Open();
            Debug.Log("[DobeCatGameManager] 测试面板自动打开");
        }

        private void Update()
        {
            // 退出快捷键：Ctrl+Shift+Q
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var quit =
                (Win32Native.GetAsyncKeyState(Win32Native.VK_CONTROL) & 0x8000) != 0 &&
                (Win32Native.GetAsyncKeyState(Win32Native.VK_SHIFT) & 0x8000) != 0 &&
                (Win32Native.GetAsyncKeyState(Win32Native.VK_Q) & 0x8000) != 0;
#else
            var quit = Input.GetKey(KeyCode.LeftControl)
                    && Input.GetKey(KeyCode.LeftShift)
                    && Input.GetKey(KeyCode.Q);
#endif

            if (quit)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }

            // B 键：打开 / 关闭背包（全局，click-through 时也生效）
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var bKey = (Win32Native.GetAsyncKeyState(Win32Native.VK_B) & 0x8000) != 0;
#else
            var bKey = Input.GetKey(KeyCode.B);
#endif
            // B 键仅在手动模式下生效（AI 模式屏蔽所有手动按键）
            if (bKey && !_wasBKey && (_petHud == null || _petHud.IsManualMode)) ToggleBackpack();
            _wasBKey = bKey;
        }

        // ──────────────────────────────────────────────────────

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(0.1f, _cameraOrthoSize);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.clear; // RGBA 全 0，DWM 合成时完全透明
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void SpawnPet()
        {
            _pet = new GameObject("DobeCat");
            _pet.transform.SetParent(transform);
            _pet.transform.position = _petSpawnPos;
            // 必须在 AddComponent<PetView> 之前设 localScale，因为 PetView.Awake 会同步 transform.localScale
            // 且 Awake 在 AddComponent 当帧立即触发，VisualScale 字段赋值已经晚了
            _pet.transform.localScale = Vector3.one * Mathf.Max(0.01f, _petScale);

            var view = _pet.AddComponent<PetView>();
            view.UseChildRenderers = true;
            view.VisualScale = _petScale;

            // EntityManager + CharacterManager 必须就绪
            const string charInstanceId = "DobeCatLocal";
            if (!CharacterService.HasInstance)
            {
                Debug.LogError("[DobeCatGameManager] CharacterService 未就绪，无法生成桌宠");
                return;
            }

            // 玩家/AI 双系统：PetAiController 内部用 EntityManager BrainComponent，
            // PlayerControl Consideration（WASD 按下时 Score=1）压过 Wander Consideration（Score=0.2）
            var ai = _pet.AddComponent<PetAiController>();
            ai.MoveSpeed = _wasdMoveSpeed;
            ai.CharacterInstanceId = charInstanceId;
            
            // 优先使用随机化配置，否则使用默认配置
            var activeConfigId = _enablePetRandomization && !string.IsNullOrEmpty(_randomizedCharacterConfigId)
                ? _randomizedCharacterConfigId
                : _characterConfigId;
            ai.CharacterConfigId = activeConfigId;
            
            Debug.Log($"[DobeCatGameManager] 使用角色配置: {activeConfigId}");
            ai.AiEnabled = true;
            ai.Initialize(_petSpawnPos);

            var dragger = _pet.AddComponent<PetDragger>();
            dragger.View = view;
            dragger.Ai = ai;

            var ctd = _pet.AddComponent<PetClickThroughDriver>();
            ctd.View = view;
            ctd.Dragger = dragger;

            // 联网同步：每节点广播本机桌宠位置，收到陌生 peer 自动生成幽灵跟随
            var sync = _pet.AddComponent<PetNetworkSync>();
            sync.LocalPet = _pet.transform;
            sync.GhostCharacterConfigId = _ghostCharacterConfigId;
            sync.GhostScale = _petScale;

            // 背景层（默认隐藏；托盘菜单可切换）
            var bg = _pet.AddComponent<PetBackgroundLayer>();
            bg.SetVisible(false);

            // 缩放控制器（读取 PlayerPrefs 恢复上次大小）
            _pet.AddComponent<PetScaleController>();

            // 对话气泡 + 直播弹幕/礼物反应（§10.2）+ 陪伴提醒（§7）
            _pet.AddComponent<PetSpeechBubble>();
            _pet.AddComponent<PetReactionController>();
            _pet.AddComponent<PetCompanionReminder>();

            // 好感度 + 左键点击/长按撸猫互动（§5.1 / §6）
            _pet.AddComponent<PetAffectionController>();
            _pet.AddComponent<PetInteractionController>();

            // 帧率控制器：空闲 10fps / 活跃 60fps（§2.2）
            gameObject.AddComponent<FrameRateController>();

            // 音效控制器（§12.1 AudioManager）
            gameObject.AddComponent<PetSoundController>();

            // 前台窗口传感器（§4.5 ForegroundSensor）
            gameObject.AddComponent<ForegroundSensor>();

            // 屏幕边界限制（§4.5 BoundsSensor）
            _pet.AddComponent<BoundsSensor>();

            Debug.Log("[DobeCatGameManager] 占位桌宠已生成");
        }

        /// <summary>
        /// 准备随机化的桌宠配置：注册随机变体，注册到 CharacterService。
        /// </summary>
        private void PrepareRandomizedPetConfig()
        {
            if (!CharacterService.HasInstance)
            {
                Debug.LogError("[DobeCatGameManager] CharacterService 未就绪，无法随机化桌宠");
                return;
            }

            // 注册默认变体配置
            DobeCatPetRandomizer.RegisterDefaultConfigs();

            // 随机选择变体
            var selectedVariants = DobeCatPetRandomizer.Randomize();

            if (selectedVariants.Count == 0)
            {
                Debug.LogWarning("[DobeCatGameManager] 随机化结果为空，使用默认配置");
                return;
            }

            // 构建并注册 CharacterConfig
            _randomizedCharacterConfigId = DobeCatPetConfigBuilder.RegisterRandomConfig(
                CharacterService.Instance, 
                selectedVariants);

            Debug.Log($"[DobeCatGameManager] 随机化完成！选中的变体：\n{DobeCatPetRandomizer.GetSelectedVariantInfo()}");
        }

        private void EnsureTray()
        {
            var tray = GetComponentInChildren<DobeCatTray>(true);
            if (tray == null)
            {
                var holder = new GameObject(nameof(DobeCatTray));
                holder.transform.SetParent(transform);
                tray = holder.AddComponent<DobeCatTray>();
            }
            tray.PetRoot = _pet;
            tray.ResetPosition = _petSpawnPos;
            tray.Discovery = _discovery;
            tray.Ai = _pet != null ? _pet.GetComponent<PetAiController>() : null;
            tray.OnJoinRoomRequested -= HandleJoinRoom; // 防重复订阅
            tray.OnJoinRoomRequested += HandleJoinRoom;

            // 桌宠右键 → 调起托盘菜单（与右下角图标一致）
            if (_pet != null)
            {
                var rc = _pet.GetComponent<PetRightClick>();
                if (rc == null) rc = _pet.AddComponent<PetRightClick>();
                rc.View = _pet.GetComponent<PetView>();
                rc.Tray = tray;
            }
        }

        /// <summary>
        /// 用本地 AuthSession 的 SESSDATA 向 <c>data_exchange_server</c> 换取一个短期 token。
        /// 后续 RoomDiscoveryClient / ActionsClient 的所有上行写请求都会自动带这个 token。
        /// </summary>
        private void EnsureDataExchangeSession()
        {
            if (!_roomDiscoveryEnabled || string.IsNullOrEmpty(_roomDiscoveryServerUrl)) return;
            if (_dataSession != null && _actions != null) return;

            var holder = new GameObject("DataExchangeClients");
            holder.transform.SetParent(transform);
            holder.SetActive(false); // 关掉再配字段，避免 OnEnable 时用默认空 URL 起协程

            _dataSession = holder.AddComponent<DataExchangeSession>();
            _dataSession.ServerBaseUrl = _roomDiscoveryServerUrl;
            _dataSession.AutoLogin = true;

            _actions = holder.AddComponent<ActionsClient>();
            _actions.ServerBaseUrl = _roomDiscoveryServerUrl;

            holder.SetActive(true);
        }

        private void EnsureRoomDiscovery()
        {
            if (!_roomDiscoveryEnabled) return;
            if (_discovery != null) return;

            var holder = new GameObject(nameof(RoomDiscoveryClient));
            holder.transform.SetParent(transform);
            // 先关掉 GameObject，避免 AddComponent 立即触发 OnEnable → 协程用默认 localhost 发出第一次心跳
            holder.SetActive(false);
            _discovery = holder.AddComponent<RoomDiscoveryClient>();
            _discovery.ServerBaseUrl = _roomDiscoveryServerUrl;
            _discovery.CollectionName = _roomDiscoveryCollection;
            _discovery.AdvertisedHost = _roomDiscoveryAdvertisedHost; // 留空则自动检测 LAN IP
            _discovery.AdvertisedPort = _netPort;
            _discovery.RoomDisplayName = _roomDiscoveryDisplayName;  // 留空则用设备名
            holder.SetActive(true); // 此刻 OnEnable 触发，协程读到的已是 Inspector 配置

            // 让测试面板拿到 client 引用，刷新 IP / 房间列表
            Demo.DobeCat.Sys.UI.DobeCatTestPanel.Instance.AttachDiscovery(_discovery);
        }

        /// <summary>用户从托盘点了"加入 xxx 房间"。
        /// <para>停掉本机当前 Host → 切到 Client 模式连过去。PetNetworkSync 会自动开始同步。</para></summary>
        private void HandleJoinRoom(RoomDiscoveryClient.RoomInfo room)
        {
            if (room == null || string.IsNullOrEmpty(room.Host) || room.Port <= 0)
            {
                Debug.LogWarning($"[DobeCatGameManager] 拒绝加入：房间信息无效 {room?.Id}");
                return;
            }
            Debug.Log($"[DobeCatGameManager] 加入房间 {room.Name} → {room.Host}:{room.Port}");

            // 1) 关掉自己（Host 或 Client 都先 Disconnect 一次，幂等）
            EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_DISCONNECT, new List<object>());

            // 2) 切换为 Client 角色，连接到目标 Host
            _netMode = NetworkRole.Client;
            _netServerAddress = room.Host;
            _netPort = (ushort)room.Port;

            EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_CLIENT_CONNECT,
                new List<object> { room.Host, (ushort)room.Port });

            // 3) 通知 RoomDiscovery：自己不再是 Host，就别再上报房间了
            if (_discovery != null) _discovery.enabled = false;
        }

        /// <summary>托底创建所需 Manager 单例（SingletonMono 会自动创建 GameObject）。</summary>
        private void EnsureFrameworkManagers()
        {
            // 访问 Instance 以触发 SingletonMono 自动创建
            _ = UIManager.Instance;
            _ = EssSystem.Core.Presentation.AudioManager.AudioManager.Instance;
            _ = EssSystem.Core.Application.SingleManagers.DialogueManager.DialogueManager.Instance;
            var inv = EssSystem.Core.Application.SingleManagers.InventoryManager.InventoryManager.Instance; // OpenInventoryUI / CloseInventoryUI / RegisterItem
            inv.AutoOpenHotbar = false; // 押制 Start() 里的自动开启，避免登录后 Hotbar 闪一帧；DobeCat 由 OnGameContextChanged 按需开关
            _ = BilibiliDanmuManager.Instance;
            _ = LiveStatusManager.Instance;
            _ = NetMgr.Instance;   // NetworkManager 自动单例（首次访问会触发 Reset → 自动安装 Mirror）
            var charMgr = CharMgr.Instance;  // CharacterManager 注册默认 Warrior / Mage / Tree 配置
            _ = EssSystem.Core.Application.SingleManagers.EntityManager.EntityManager.Instance; // EntityManager 提供 Brain / Capabilities
            _ = EssSystem.Core.Application.MultiManagers.FarmManager.FarmManager.Instance;     // FarmManager 农场系统（HandleSpawnFarm / HandleQuerySlot 等）
            _ = EssSystem.Core.Application.MultiManagers.ShopManager.ShopManager.Instance;      // ShopManager 商店系统
        }

        private void TryAutoStartNetwork()
        {
            if (_netMode == NetworkRole.None) return;
            // 监听网络状态，方便调试
            // （正式接入请在业务侧用 [EventListener(NetworkService.EVT_NET_MESSAGE)] 订阅）
            switch (_netMode)
            {
                case NetworkRole.Host:
                    EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_HOST_START,
                        new List<object> { _netPort });
                    break;
                case NetworkRole.ServerOnly:
                    EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_SERVER_START,
                        new List<object> { _netPort });
                    break;
                case NetworkRole.Client:
                    EventProcessor.Instance.TriggerEventMethod(NetMgr.EVT_CLIENT_CONNECT,
                        new List<object> { _netServerAddress, _netPort });
                    break;
            }
            Debug.Log($"[DobeCatGameManager] 网络自动启动: mode={_netMode} addr={_netServerAddress}:{_netPort}");
        }

        private void TryStartLivePolling()
        {
            if (!_liveAutoPoll || _liveRoomId <= 0) return;
            LiveStatusService.Instance.StartPolling(_liveRoomId, _livePollIntervalSeconds);
            Debug.Log($"[DobeCatGameManager] 开播状态轮询启动: room={_liveRoomId}, interval={_livePollIntervalSeconds}s");
        }

        private void OnApplicationQuit()
        {
            // Screen.SetResolution 是异步的，退出时来不及生效。
            // 直接写注册表，把 Unity 保存的分辨率覆盖为登录窗口大小，
            // 避免下次启动时先出现全屏白色窗口。
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            ResetSavedResolutionInRegistry();
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        public static void ResetSavedResolutionInRegistry()
        {
            try
            {
                var subKeyPath = $@"Software\{Application.companyName}\{Application.productName}";
                Debug.Log($"[QUIT] 注册表路径: HKCU\\{subKeyPath}");
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(subKeyPath, writable: true);
                if (key == null)
                {
                    Debug.LogWarning($"[QUIT] 未找到注册表键: {subKeyPath}，无法重置分辨率。");
                    return;
                }

                // 保存全屏叠加层分辨率，下次启动 Unity 直接以此分辨率创建窗口，Enter() 无需 SetResolution，零闪烁。
                var wa = Win32Native.GetPrimaryWorkArea();
                int targetW = wa.right  - wa.left;
                int targetH = wa.bottom - wa.top;
                int cx = wa.left;  // 叠加层定位于工作区左上角
                int cy = wa.top;

                var names = key.GetValueNames();
                Debug.Log($"[QUIT] 找到 {names.Length} 个注册表值，开始扫描...");
                int changed = 0;
                foreach (var name in names)
                {
                    if (name.StartsWith("Screenmanager Resolution Width"))
                    {
                        var old = key.GetValue(name);
                        key.SetValue(name, targetW, Microsoft.Win32.RegistryValueKind.DWord);
                        Debug.Log($"[QUIT] Width  {name}: {old} → {targetW}");
                        changed++;
                    }
                    else if (name.StartsWith("Screenmanager Resolution Height"))
                    {
                        var old = key.GetValue(name);
                        key.SetValue(name, targetH, Microsoft.Win32.RegistryValueKind.DWord);
                        Debug.Log($"[QUIT] Height {name}: {old} → {targetH}");
                        changed++;
                    }
                    else if (name.StartsWith("Screenmanager Fullscreen mode"))
                    {
                        var old = key.GetValue(name);
                        key.SetValue(name, 3, Microsoft.Win32.RegistryValueKind.DWord);
                        Debug.Log($"[QUIT] Fullscreen {name}: {old} → 3 (Windowed)");
                        changed++;
                    }
                    else if (name.StartsWith("Screenmanager Window Position X"))
                    {
                        var old = key.GetValue(name);
                        key.SetValue(name, cx, Microsoft.Win32.RegistryValueKind.DWord);
                        Debug.Log($"[QUIT] PosX {name}: {old} → {cx}");
                        changed++;
                    }
                    else if (name.StartsWith("Screenmanager Window Position Y"))
                    {
                        var old = key.GetValue(name);
                        key.SetValue(name, cy, Microsoft.Win32.RegistryValueKind.DWord);
                        Debug.Log($"[QUIT] PosY {name}: {old} → {cy}");
                        changed++;
                    }
                }
                Debug.Log($"[QUIT] 注册表重置完成，共修改 {changed} 个键。（分辨率: {targetW}×{targetH}, 位置: {cx},{cy}）");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[QUIT] 重置分辨率注册表失败: {e.Message}");
            }
        }
#endif

        private void TryAutoConnectDanmu()
        {
            if (!_bilibiliAutoConnect) return;
            // 用 DobeCat Inspector 的字段构造 config，覆盖 DanmuManager 自身默认值
            var cfg = new DanmuConnectConfig
            {
                Mode = _danmuMode,
                RoomId = _danmuRoomId > 0 ? _danmuRoomId : _liveRoomId,
                PollIntervalSeconds = _pollIntervalSeconds,
                Sessdata = !string.IsNullOrEmpty(_sessdata) ? _sessdata : BilibiliAuthSession.Token,
                BiliJct = _biliJct,
                IdentityCode = _bilibiliIdentityCode,
                AppId = _bilibiliAppId,
                SignEndpoint = _bilibiliSignEndpoint,
                StartEndpoint = _bilibiliStartEndpoint,
                HttpTimeoutSeconds = _bilibiliHttpTimeout,
            };
            _ = DanmuService.Instance.ConnectAsync(cfg);
            Debug.Log($"[DobeCatGameManager] 弹幕连接 mode={cfg.Mode} room={cfg.RoomId}");
        }
    }
}
