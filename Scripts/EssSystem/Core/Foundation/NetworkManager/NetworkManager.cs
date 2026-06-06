using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace EssSystem.Core.Foundation.NetworkManager
{
    /// <summary>
    /// 多人联机网络通讯门面 —— 基于 Mirror。
    /// <list type="bullet">
    /// <item>挂载即装：编辑器侧自动通过 OpenUPM 安装 Mirror（见 <c>Editor/MirrorInstaller.cs</c>）。</item>
    /// <item>事件驱动：业务方完全通过 EventProcessor 调用本类的 <c>[Event]</c> 命令，不依赖 Mirror 类型。</item>
    /// <item>状态广播：连接 / 断开 / 对等进出 / 收到消息均通过 <see cref="NetworkService"/> 的 EVT_* 广播。</item>
    /// </list>
    /// <para>命令约定（payload 由 <see cref="NetworkService.EncodePayload"/> 自动 JSON 编码）：</para>
    /// <code>
    /// EventProcessor.TriggerEvent(NetworkManager.EVT_HOST_START, new List&lt;object&gt;{ (ushort)7777 });
    /// EventProcessor.TriggerEvent(NetworkManager.EVT_CLIENT_CONNECT, new List&lt;object&gt;{ "192.168.0.5", (ushort)7777 });
    /// EventProcessor.TriggerEvent(NetworkManager.EVT_SEND_TO_ALL, new List&lt;object&gt;{ "ChatMsg", payloadObject });
    /// </code>
    /// </summary>
    [Manager(2)]
    public class NetworkManager : Manager<NetworkManager>
    {
        // ─── 命令事件常量（由业务方 TriggerEvent，本 Manager [Event] 处理） ──
        #region 命令事件

        /// <summary>启动主机（服务器 + 本地客户端）。参数：<c>[ushort? port]</c>。</summary>
        public const string EVT_HOST_START = "NetHostStart";

        /// <summary>仅启动服务器（无本地客户端）。参数：<c>[ushort? port]</c>。</summary>
        public const string EVT_SERVER_START = "NetServerStart";

        /// <summary>启动客户端，连接到指定服务器。参数：<c>[string address, ushort? port]</c>。</summary>
        public const string EVT_CLIENT_CONNECT = "NetClientConnect";

        /// <summary>停止当前角色（幂等）。参数：无。</summary>
        public const string EVT_DISCONNECT = "NetDisconnect";

        /// <summary>客户端 → 服务器 发送一条消息。参数：<c>[string topic, object payload]</c>。</summary>
        public const string EVT_SEND_TO_SERVER = "NetSendToServer";

        /// <summary>服务器 → 全部已就绪客户端 广播一条消息。参数：<c>[string topic, object payload]</c>。</summary>
        public const string EVT_SEND_TO_ALL = "NetSendToAll";

        /// <summary>服务器 → 指定 connectionId 客户端 单播。参数：<c>[int connectionId, string topic, object payload]</c>。</summary>
        public const string EVT_SEND_TO_PEER = "NetSendToPeer";

        /// <summary>对等广播：任何节点调用，效果是"所有连入网络的节点都会收到一条 EVT_NET_MESSAGE"。
        /// <para>在 Server/Host 上 = SendToAll；在 Client 上 = 先发到 Server，由 Server 自动 SendToAll。</para>
        /// <para>参数：<c>[string topic, object payload]</c>。</para></summary>
        public const string EVT_BROADCAST = "NetBroadcast";

        #endregion

        #region Inspector

        [Header("启动")]
        [Tooltip("Initialize 后是否按 _autoMode 自动启动")]
        [SerializeField] private bool _autoStart = false;

        [Tooltip("_autoStart 启用时使用的角色")]
        [SerializeField] private NetworkRole _autoMode = NetworkRole.None;

        [Header("连接")]
        [Tooltip("默认服务器监听端口 / 客户端目标端口")]
        [SerializeField] private ushort _port = 7777;

        [Tooltip("客户端目标服务器地址（IP 或域名）")]
        [SerializeField] private string _serverAddress = "localhost";

        [Header("Mirror 桥接")]
        [Tooltip("挂载 WulaNetworkManagerBehaviour 的子物体名称（Mirror.NetworkManager 必须挂在 MonoBehaviour 上）")]
        [SerializeField] private string _mirrorHostObjectName = "MirrorHost";

        #endregion

        public NetworkService Service => NetworkService.Instance;
        public ushort DefaultPort => _port;
        public string DefaultServerAddress => _serverAddress;
        public string MirrorHostObjectName => _mirrorHostObjectName;

#if MIRROR_INSTALLED
        private Runtime.WulaNetworkManagerBehaviour _bridge;
        public Runtime.WulaNetworkManagerBehaviour Bridge => _bridge ??= EnsureBridge();
#endif

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

#if MIRROR_INSTALLED
            _ = Bridge; // 触发桥接物体创建（懒加载）
            if (_autoStart) AutoStart();
#else
            Log("Mirror 未安装：NetworkManager 处于占位模式。点击菜单 Tools/WulaSystem/Foundation/Network/Mirror/Install Mirror Now。", Color.yellow);
#endif
            Log($"NetworkManager 初始化完成 (port={_port}, autoStart={_autoStart}, mode={_autoMode})", Color.green);
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

        protected override void OnDestroy() { StopAny(); base.OnDestroy(); }
        protected override void OnApplicationQuit() { StopAny(); base.OnApplicationQuit(); }

#if UNITY_EDITOR
        /// <summary>组件第一次被 AddComponent 时调用 —— 立即弹窗触发 Mirror 安装检查。</summary>
        private void Reset()
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                var t = System.Type.GetType("EssSystem.Manager.NetworkManager.EditorTools.MirrorInstaller, Assembly-CSharp-Editor")
                       ?? System.Type.GetType("EssSystem.Manager.NetworkManager.EditorTools.MirrorInstaller");
                if (t == null) return;
                var isInstalled = (bool)(t.GetMethod("IsMirrorInstalled").Invoke(null, null));
                if (!isInstalled)
                    t.GetMethod("InstallMirror").Invoke(null, null);
            };
        }
#endif

#if MIRROR_INSTALLED
        private Runtime.WulaNetworkManagerBehaviour EnsureBridge()
        {
            var found = transform.Find(_mirrorHostObjectName);
            GameObject host;
            if (found != null) host = found.gameObject;
            else
            {
                host = new GameObject(_mirrorHostObjectName);
                host.transform.SetParent(transform, false);
            }
            var b = host.GetComponent<Runtime.WulaNetworkManagerBehaviour>();
            if (b == null) b = host.AddComponent<Runtime.WulaNetworkManagerBehaviour>();
            b.Bind(this);
            return b;
        }

        private void AutoStart()
        {
            switch (_autoMode)
            {
                case NetworkRole.Host: Bridge.HostStart(_port); break;
                case NetworkRole.ServerOnly: Bridge.ServerStart(_port); break;
                case NetworkRole.Client: Bridge.ClientConnect(_serverAddress, _port); break;
            }
        }
#endif

        private void StopAny()
        {
#if MIRROR_INSTALLED
            if (_bridge != null) _bridge.StopAny();
#endif
        }

        // ─── Event 命令处理 ──────────────────────────────────────
        #region [Event] handlers

        [Event(EVT_HOST_START)]
        public List<object> OnHostStart(List<object> data)
        {
#if MIRROR_INSTALLED
            var port = (data != null && data.Count > 0 && data[0] is ushort p) ? p : _port;
            Bridge.HostStart(port);
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_SERVER_START)]
        public List<object> OnServerStart(List<object> data)
        {
#if MIRROR_INSTALLED
            var port = (data != null && data.Count > 0 && data[0] is ushort p) ? p : _port;
            Bridge.ServerStart(port);
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_CLIENT_CONNECT)]
        public List<object> OnClientConnect(List<object> data)
        {
#if MIRROR_INSTALLED
            var addr = (data != null && data.Count > 0 && data[0] is string s && !string.IsNullOrEmpty(s)) ? s : _serverAddress;
            var port = (data != null && data.Count > 1 && data[1] is ushort p) ? p : _port;
            Bridge.ClientConnect(addr, port);
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_DISCONNECT)]
        public List<object> OnDisconnect(List<object> data)
        {
#if MIRROR_INSTALLED
            Bridge.StopAny();
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_SEND_TO_SERVER)]
        public List<object> OnSendToServer(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string topic) || string.IsNullOrEmpty(topic))
                return ResultCode.Fail("参数 [string topic, object payload]");
            var payload = data.Count > 1 ? data[1] : null;
#if MIRROR_INSTALLED
            Bridge.SendToServer(topic, NetworkService.EncodePayload(payload));
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_SEND_TO_ALL)]
        public List<object> OnSendToAll(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string topic) || string.IsNullOrEmpty(topic))
                return ResultCode.Fail("参数 [string topic, object payload]");
            var payload = data.Count > 1 ? data[1] : null;
#if MIRROR_INSTALLED
            Bridge.SendToAll(topic, NetworkService.EncodePayload(payload));
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_BROADCAST)]
        public List<object> OnBroadcast(List<object> data)
        {
            if (data == null || data.Count < 1 || !(data[0] is string topic) || string.IsNullOrEmpty(topic))
                return ResultCode.Fail("参数 [string topic, object payload]");
            var payload = data.Count > 1 ? data[1] : null;
#if MIRROR_INSTALLED
            Bridge.Broadcast(topic, NetworkService.EncodePayload(payload));
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        [Event(EVT_SEND_TO_PEER)]
        public List<object> OnSendToPeer(List<object> data)
        {
            if (data == null || data.Count < 3 || !(data[0] is int connId) || !(data[1] is string topic) || string.IsNullOrEmpty(topic))
                return ResultCode.Fail("参数 [int connectionId, string topic, object payload]");
            var payload = data[2];
#if MIRROR_INSTALLED
            Bridge.SendToPeer(connId, topic, NetworkService.EncodePayload(payload));
            return ResultCode.Ok();
#else
            return MirrorNotInstalled();
#endif
        }

        #endregion

        private static List<object> MirrorNotInstalled()
            => ResultCode.Fail("Mirror 未安装：菜单 Tools/WulaSystem/Foundation/Network/Mirror/Install Mirror Now");
    }
}
