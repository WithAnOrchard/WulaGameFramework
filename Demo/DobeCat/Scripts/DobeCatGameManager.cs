using UnityEngine;
using EssSystem.Core.Base;
using EssSystem.Core.Presentation.UIManager;
using BiliBiliDanmu;
using BiliBiliLive;
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Manager.NetworkManager;
using NetMgr = EssSystem.Manager.NetworkManager.NetworkManager;
using Demo.DobeCat.Window;
using Demo.DobeCat.Pet;
using Demo.DobeCat.Tray;
using Demo.DobeCat.UI;

namespace Demo.DobeCat
{
    /// <summary>
    /// DobeCat 桌面宠物 Demo 总控。
    /// <para>继承 <see cref="AbstractGameManager"/> 复用框架的基础 Manager 自动接管
    /// （EventProcessor / DataManager / ResourceManager / AudioManager / UIManager）。</para>
    /// <para>M1 阶段职责：</para>
    /// <list type="number">
    /// <item>初始化 <see cref="DesktopWindow"/>（透明 / 置顶 / 穿透）。</item>
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

        [Tooltip("桌宠占位贴图的 Resources 路径，留空走程序生成。")]
        [SerializeField] private string _petSpritePath = "";

        [Tooltip("桌宠视觉缩放。")]
        [SerializeField] private float _petScale = 1f;

        [Tooltip("桌宠生成位置（世界坐标）。")]
        [SerializeField] private Vector3 _petSpawnPos = Vector3.zero;

        [Header("Hotkeys")]
        [SerializeField] private KeyCode _quitKey = KeyCode.Escape;

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
        [Tooltip("浏览器 DevTools 复制的 SESSDATA。\n⚠️ 安全：等同登录密码，不要 commit 到公开仓库。")]
        [TextArea(2, 4)]
        [SerializeField] private string _sessdata = "dff5c774%2C1794564158%2C6d18c%2A51CjA2jc857610MMUb9emwlp5TWnnIVV9FlCCgkOzmxmqWEJdznmr9zN44-sIJzbbUjX4SVnZtUTd6SlNBUjhfVVNwRzlua2FBb0x0Q05UTlhRZHBzWFF4ZEtUM0d1OHJfV2FSWll3N2x5X0NoQ2pTeWo5bkRkWnBRLVNzVDhXQWtMZkRoSmJ0UGt3IIEC";
        [Tooltip("可选 bili_jct，仅发弹幕/打赏需要。")]
        [SerializeField] private string _biliJct = string.Empty;

        [Header("BiliBili Danmu — OpenLive 模式（主播身份码）")]
        [Tooltip("主播身份码（B 站直播姬「开放平台」面板获取）。")]
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

        [Header("Test Panel")]
        [Tooltip("启动时自动打开 DobeCatTestPanel，方便调试。生产环境改 false。")]
        [SerializeField] private bool _autoOpenTestPanel = true;

        private DesktopWindow _window;
        private GameObject _pet;

        protected override void Awake()
        {
            base.Awake(); // 框架基础 Manager 接管完成
            // 桌宠窗口 click-through 时失去焦点；若 Unity 暂停 Update，托盘菜单点击 / 主线程查询会全卡死
            Application.runInBackground = true;
            EnsureCamera();
            EnsureWindow();
            Debug.Log("[DobeCatGameManager] 框架 Manager 初始化完成（runInBackground=true）");
        }

        private void Start()
        {
            if (_autoSpawnPet) SpawnPet();
            EnsureTray();
            EnsureFrameworkManagers();
            TryAutoConnectDanmu();
            TryStartLivePolling();
            TryAutoStartNetwork();


            if (_autoOpenTestPanel)
            {
                // 推迟一帧确保所有 Manager Initialize 完成后再注册 UI
                StartCoroutine(OpenTestPanelDelayed());
            }
        }

        private System.Collections.IEnumerator OpenTestPanelDelayed()
        {
            yield return null; // 等下一帧
            DobeCatTestPanel.Open();
            Debug.Log("[DobeCatGameManager] 测试面板自动打开");
        }

        private void Update()
        {
            // 1) Unity 普通输入：编辑器调试用 / Standalone 下当窗口可聚焦时也能触发
            var quit = Input.GetKeyDown(_quitKey);

            // 2) 全局热键兜底：click-through 时窗口失去焦点，Unity 收不到键盘 → 用 Win32 GetAsyncKeyState
            if (!quit && _window != null)
            {
                if (_window.IsGlobalEscapePressed() || _window.IsGlobalQuitHotkeyPressed())
                    quit = true;
            }

            if (quit)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
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
            var c = cam.backgroundColor; c.a = 0f; cam.backgroundColor = c;
            cam.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void EnsureWindow()
        {
            _window = GetComponentInChildren<DesktopWindow>(true);
            if (_window != null) return;
            var holder = new GameObject(nameof(DesktopWindow));
            holder.transform.SetParent(transform);
            _window = holder.AddComponent<DesktopWindow>();
        }

        private void SpawnPet()
        {
            _pet = new GameObject("DobeCat");
            _pet.transform.SetParent(transform);
            _pet.transform.position = _petSpawnPos;

            var view = _pet.AddComponent<PetView>();
            view.SpriteResourcePath = _petSpritePath;
            view.VisualScale = _petScale;

            var wander = _pet.AddComponent<PetWander>();
            wander.View = view;

            var dragger = _pet.AddComponent<PetDragger>();
            dragger.View = view;
            dragger.Wander = wander;

            var ctd = _pet.AddComponent<PetClickThroughDriver>();
            ctd.View = view;
            ctd.Dragger = dragger;

            // 联网同步：每节点广播本机桌宠位置，收到陌生 peer 自动生成幽灵跟随
            var sync = _pet.AddComponent<PetNetworkSync>();
            sync.LocalPet = _pet.transform;
            sync.GhostSpritePath = _petSpritePath;
            sync.GhostScale = _petScale;

            Debug.Log("[DobeCatGameManager] 占位桌宠已生成");
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
        }

        /// <summary>托底创建所需 Manager 单例（SingletonMono 会自动创建 GameObject）。</summary>
        private void EnsureFrameworkManagers()
        {
            // 访问 Instance 以触发 SingletonMono 自动创建
            _ = UIManager.Instance;
            _ = DanmuManager.Instance;
            _ = LiveStatusManager.Instance;
            _ = NetMgr.Instance; // NetworkManager 自动单例（首次访问会触发 Reset → 自动安装 Mirror）
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

        private void TryAutoConnectDanmu()
        {
            if (!_bilibiliAutoConnect) return;
            // 用 DobeCat Inspector 的字段构造 config，覆盖 DanmuManager 自身默认值
            var cfg = new DanmuConnectConfig
            {
                Mode = _danmuMode,
                RoomId = _danmuRoomId > 0 ? _danmuRoomId : _liveRoomId,
                PollIntervalSeconds = _pollIntervalSeconds,
                Sessdata = _sessdata,
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
