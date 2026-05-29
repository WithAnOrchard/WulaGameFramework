// Wula 网络桥接层 —— 继承 Mirror.NetworkManager，把 Mirror 回调转成 NetworkService 广播。
// 仅在 MIRROR_INSTALLED 编译宏存在时启用；缺失时本文件为空，NetworkManager 走占位模式。

#if MIRROR_INSTALLED
using Mirror;
using UnityEngine;

namespace EssSystem.Core.Foundation.NetworkManager.Runtime
{
    /// <summary>
    /// 框架对 <see cref="Mirror.NetworkManager"/> 的桥接：
    /// <list type="bullet">
    /// <item>统一入口：<see cref="HostStart"/> / <see cref="ServerStart"/> / <see cref="ClientConnect"/> / <see cref="StopAny"/>。</item>
    /// <item>统一消息：注册 <see cref="WulaNetMessage"/> 处理器，把 topic+payload 转给 <see cref="NetworkService"/> 广播 EVT_NET_MESSAGE。</item>
    /// <item>会自动确保挂载一个 Transport（默认 KCP），避免空 Transport 报错。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class WulaNetworkManagerBehaviour : Mirror.NetworkManager
    {
        private NetworkManager _facade;
        private NetworkService Svc => NetworkService.Instance;

        internal void Bind(NetworkManager facade) => _facade = facade;

        public override void Awake()
        {
            EnsureTransport();
            // 本框架走纯消息广播（EVT_BROADCAST + 自定义 Spawn），不需要 Mirror 的 PlayerPrefab 自动生成机制。
            // 不关掉 autoCreatePlayer 时，客户端连入会触发 OnServerAddPlayer → "PlayerPrefab is empty" 报错。
            autoCreatePlayer = false;
            base.Awake();
        }

        /// <summary>覆盖默认实现：不生成 PlayerPrefab。业务层用 EVT_BROADCAST 自己同步位置 / 创建幽灵。</summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 故意为空：本框架不强制 PlayerPrefab。
        }

        /// <summary>确保挂载了一个非抽象的 Transport。优先复用现有；否则全程序集扫一个 concrete subclass 加上。</summary>
        private void EnsureTransport()
        {
            if (transport != null) { Transport.active = transport; return; }

            var existing = GetComponent<Transport>();
            if (existing != null) { transport = existing; Transport.active = existing; return; }

            // 扫描所有已加载程序集，找 Transport 的非抽象子类（KcpTransport / SimpleWebTransport / TelepathyTransport ...）
            System.Type chosen = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                System.Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;
                    if (!typeof(Transport).IsAssignableFrom(t)) continue;
                    // 优先 KcpTransport（Mirror 默认）
                    if (t.Name == "KcpTransport") { chosen = t; break; }
                    chosen ??= t;
                }
                if (chosen != null && chosen.Name == "KcpTransport") break;
            }

            if (chosen == null)
            {
                Debug.LogError("[WulaNetworkManagerBehaviour] 找不到任何非抽象 Mirror.Transport 子类。" +
                               "请确认 Mirror 完整安装（KcpTransport 通常随包附带）。");
                return;
            }
            existing = (Transport)gameObject.AddComponent(chosen);
            transport = existing;
            Transport.active = existing;
            Debug.Log($"[WulaNetworkManagerBehaviour] 自动添加 Transport: {chosen.FullName}");
        }

        // ─── 入口 ───────────────────────────────────────────────
        public void HostStart(ushort port)
        {
            ApplyAddress(null, port);
            StopAny();
            StartHost();
        }

        public void ServerStart(ushort port)
        {
            ApplyAddress(null, port);
            StopAny();
            StartServer();
        }

        public void ClientConnect(string address, ushort port)
        {
            ApplyAddress(address, port);
            StopAny();
            StartClient();
        }

        public void StopAny()
        {
            if (NetworkServer.active && NetworkClient.isConnected) StopHost();
            else if (NetworkServer.active) StopServer();
            else if (NetworkClient.isConnected || NetworkClient.active) StopClient();
        }

        private void ApplyAddress(string address, ushort port)
        {
            if (!string.IsNullOrEmpty(address)) networkAddress = address;
            // KcpTransport / TelepathyTransport 都通过反射设置 Port，避免硬引用 Transport 子类
            if (transport != null)
            {
                var portField = transport.GetType().GetField("port",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (portField != null && portField.FieldType == typeof(ushort))
                    portField.SetValue(transport, port);
            }
        }

        // ─── 消息发送 ───────────────────────────────────────────
        public void SendToServer(string topic, string payload)
        {
            if (!NetworkClient.isConnected) { Svc?.NotifyError("SendToServer", "客户端未连接"); return; }
            NetworkClient.Send(new WulaNetMessage { Topic = topic, Payload = payload });
        }

        public void SendToAll(string topic, string payload)
        {
            if (!NetworkServer.active) { Svc?.NotifyError("SendToAll", "服务器未启动"); return; }
            NetworkServer.SendToReady(new WulaNetMessage { Topic = topic, Payload = payload });
        }

        // 广播专用 topic 前缀：server 收到后会自动 SendToAll（含本机），并把原始 topic 还原给业务侧。
        private const string BroadcastPrefix = "__bc__:";

        /// <summary>对等广播：任何节点调用，最终所有节点都会收到一次 EVT_NET_MESSAGE。</summary>
        public void Broadcast(string topic, string payload)
        {
            if (string.IsNullOrEmpty(topic)) return;
            if (NetworkServer.active)
            {
                // 在服务器/主机：直接全播 + 本机自我通知（保证 Host 自己也收到）
                NetworkServer.SendToReady(new WulaNetMessage { Topic = topic, Payload = payload });
                Svc?.NotifyMessage(0, topic, payload);
            }
            else if (NetworkClient.isConnected)
            {
                // 纯客户端：发到服务器，加前缀让服务器知道要转发；同时本机也立即收一次
                NetworkClient.Send(new WulaNetMessage { Topic = BroadcastPrefix + topic, Payload = payload });
                Svc?.NotifyMessage(0, topic, payload);
            }
            else
            {
                Svc?.NotifyError("Broadcast", "未连接，无法广播");
            }
        }

        public void SendToPeer(int connectionId, string topic, string payload)
        {
            if (!NetworkServer.active) { Svc?.NotifyError("SendToPeer", "服务器未启动"); return; }
            if (!NetworkServer.connections.TryGetValue(connectionId, out var conn))
            { Svc?.NotifyError("SendToPeer", $"connectionId={connectionId} 不存在"); return; }
            conn.Send(new WulaNetMessage { Topic = topic, Payload = payload });
        }

        // ─── Mirror 回调 ───────────────────────────────────────
        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.RegisterHandler<WulaNetMessage>(OnServerMessage);
            BroadcastStatus();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            BroadcastStatus();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<WulaNetMessage>(OnClientMessage);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            BroadcastStatus();
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();
            BroadcastStatus();
        }

        // ─── 状态变化兜底：Update 轮询连接转换 ─────────────────
        // 部分 Mirror 版本 OnClientConnect 签名不一致，回调可能不命中我们的 override；
        // 这里用边沿检测保证连接 / 断开都会广播一次状态。
        private NetworkRole _lastBroadcastRole = NetworkRole.None;
        private bool _lastBroadcastConnected;

        public override void Update()
        {
            base.Update();
            var role = InferRole();
            var connected = role switch
            {
                NetworkRole.Host => NetworkClient.isConnected && NetworkServer.active,
                NetworkRole.ServerOnly => NetworkServer.active,
                NetworkRole.Client => NetworkClient.isConnected,
                _ => false,
            };
            if (role != _lastBroadcastRole || connected != _lastBroadcastConnected)
            {
                _lastBroadcastRole = role;
                _lastBroadcastConnected = connected;
                BroadcastStatus();
            }
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Svc?.NotifyPeerJoined(conn.connectionId);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            Svc?.NotifyPeerLeft(conn.connectionId);
            base.OnServerDisconnect(conn);
        }

        // ─── 消息回调 ──────────────────────────────────────────
        private void OnServerMessage(NetworkConnectionToClient conn, WulaNetMessage msg)
        {
            // 广播前缀：server 自动 fan-out 给所有客户端（含原发送方做 echo），并把还原 topic 通知本机业务
            if (!string.IsNullOrEmpty(msg.Topic) && msg.Topic.StartsWith(BroadcastPrefix))
            {
                var realTopic = msg.Topic.Substring(BroadcastPrefix.Length);
                NetworkServer.SendToReady(new WulaNetMessage { Topic = realTopic, Payload = msg.Payload });
                Svc?.NotifyMessage(conn.connectionId, realTopic, msg.Payload);
                return;
            }
            Svc?.NotifyMessage(conn.connectionId, msg.Topic, msg.Payload);
        }

        private void OnClientMessage(WulaNetMessage msg)
            => Svc?.NotifyMessage(0, msg.Topic, msg.Payload);

        // ─── 状态推断与广播 ────────────────────────────────────
        private void BroadcastStatus()
        {
            var role = InferRole();
            var connected = role switch
            {
                NetworkRole.Host => NetworkClient.isConnected && NetworkServer.active,
                NetworkRole.ServerOnly => NetworkServer.active,
                NetworkRole.Client => NetworkClient.isConnected,
                _ => false
            };
            ushort port = 0;
            if (transport != null)
            {
                var pf = transport.GetType().GetField("port",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (pf != null && pf.FieldType == typeof(ushort)) port = (ushort)pf.GetValue(transport);
            }
            Svc?.NotifyStatus(role, connected, networkAddress ?? "localhost", port);
        }

        private static NetworkRole InferRole()
        {
            if (NetworkServer.active && NetworkClient.active) return NetworkRole.Host;
            if (NetworkServer.active) return NetworkRole.ServerOnly;
            if (NetworkClient.active || NetworkClient.isConnected) return NetworkRole.Client;
            return NetworkRole.None;
        }
    }
}
#endif
