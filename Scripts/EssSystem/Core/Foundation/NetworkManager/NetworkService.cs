using System;
using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Util;
using UnityEngine;

namespace EssSystem.Core.Foundation.NetworkManager
{
    /// <summary>本机网络角色。</summary>
    public enum NetworkRole
    {
        /// <summary>未启动 / 已断开。</summary>
        None,
        /// <summary>纯服务器（不带客户端）。</summary>
        ServerOnly,
        /// <summary>主机（服务器 + 本地客户端）。</summary>
        Host,
        /// <summary>客户端（连接到远端服务器）。</summary>
        Client,
    }

    /// <summary>
    /// 网络通讯 Service —— 基于 Mirror，负责状态广播与消息转发。
    /// <list type="bullet">
    /// <item>所有 Mirror 类型隔离在 <c>Runtime/</c> 文件夹 + <c>#if MIRROR_INSTALLED</c>，本类对 Mirror 零编译时依赖。</item>
    /// <item>业务方仅通过 EventProcessor 与本 Service 交互：命令 EVT_* 在 <see cref="NetworkManager"/>；广播 EVT_* 在本类。</item>
    /// <item>Payload 走 <see cref="MiniJson"/> 序列化为字符串，类型限定为 string/long/double/bool/List/Dictionary。</item>
    /// </list>
    /// </summary>
    public class NetworkService : Service<NetworkService>
    {
        // ─── 广播事件常量（由 Service.TriggerEvent） ──────────────
        #region 广播事件

        /// <summary>网络角色 / 连接状态变更。参数：<c>[NetworkRole role, bool connected]</c>。</summary>
        public const string EVT_NET_STATUS_CHANGED = "OnNetworkStatusChanged";

        /// <summary>服务器侧：有客户端加入。参数：<c>[int connectionId]</c>。</summary>
        public const string EVT_PEER_JOINED = "OnNetworkPeerJoined";

        /// <summary>服务器侧：客户端离开。参数：<c>[int connectionId]</c>。</summary>
        public const string EVT_PEER_LEFT = "OnNetworkPeerLeft";

        /// <summary>收到一条对等消息。参数：<c>[int senderConnectionId, string topic, string payloadJson]</c>。
        /// <para>本地是 Server 时 senderConnectionId 为发送方 connId；本地是 Client 时 senderConnectionId 固定为 0（来自 Server）。</para></summary>
        public const string EVT_NET_MESSAGE = "OnNetworkMessage";

        /// <summary>错误。参数：<c>[string source, string message]</c>。</summary>
        public const string EVT_NET_ERROR = "OnNetworkError";

        #endregion

        // ─── 运行时状态 ───────────────────────────────────────────
        public NetworkRole Role { get; private set; } = NetworkRole.None;
        public bool IsConnected { get; private set; }
        public bool IsServer => Role == NetworkRole.Host || Role == NetworkRole.ServerOnly;
        public bool IsClient => Role == NetworkRole.Host || Role == NetworkRole.Client;
        public bool IsMirrorReady
        {
            get
            {
#if MIRROR_INSTALLED
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>当前连接到的服务器地址（Client/Host 模式下有效）。</summary>
        public string ServerAddress { get; private set; } = "localhost";
        /// <summary>当前监听 / 连接的端口。</summary>
        public ushort Port { get; private set; } = 7777;

        protected override void Initialize()
        {
            base.Initialize();
            Log($"NetworkService 初始化完成 (MirrorReady={IsMirrorReady})", IsMirrorReady ? Color.green : Color.yellow);
            if (!IsMirrorReady)
                LogWarning("Mirror 尚未安装：所有网络命令将被静默丢弃。请通过菜单 Tools/WulaFramework/Network/Install Mirror Now 安装。");

            // 高频事件加入静默集，避免每秒数十条 "触发事件 / 没监听器" 日志刷屏
            if (EventProcessor.HasInstance)
            {
                EventProcessor.Instance.SilenceEvent(EVT_NET_MESSAGE);
                EventProcessor.Instance.SilenceEvent("NetBroadcast");      // NetworkManager.EVT_BROADCAST
                EventProcessor.Instance.SilenceEvent("NetSendToServer");   // NetworkManager.EVT_SEND_TO_SERVER
                EventProcessor.Instance.SilenceEvent("NetSendToAll");      // NetworkManager.EVT_SEND_TO_ALL
                EventProcessor.Instance.SilenceEvent("NetSendToPeer");     // NetworkManager.EVT_SEND_TO_PEER
            }
        }

        // ─── 供 Runtime 桥接层（WulaNetworkManagerBehaviour）回调 ──
        #region Bridge → Service（由 Mirror 回调线程切回主线程后调用）

        internal void NotifyStatus(NetworkRole role, bool connected, string address, ushort port)
        {
            Role = role; IsConnected = connected; ServerAddress = address; Port = port;
            Log($"网络状态: role={role} connected={connected} addr={address}:{port}",
                connected ? Color.cyan : Color.gray);
            EventProcessor.Instance?.TriggerEvent(EVT_NET_STATUS_CHANGED,
                new List<object> { role, connected });
        }

        internal void NotifyPeerJoined(int connectionId)
        {
            Log($"对等加入: connId={connectionId}", Color.green);
            EventProcessor.Instance?.TriggerEvent(EVT_PEER_JOINED, new List<object> { connectionId });
        }

        internal void NotifyPeerLeft(int connectionId)
        {
            Log($"对等离开: connId={connectionId}", Color.yellow);
            EventProcessor.Instance?.TriggerEvent(EVT_PEER_LEFT, new List<object> { connectionId });
        }

        internal void NotifyMessage(int senderConnectionId, string topic, string payloadJson)
        {
            EventProcessor.Instance?.TriggerEvent(EVT_NET_MESSAGE,
                new List<object> { senderConnectionId, topic ?? string.Empty, payloadJson ?? string.Empty });
        }

        internal void NotifyError(string source, string message)
        {
            LogWarning($"[{source}] {message}");
            EventProcessor.Instance?.TriggerEvent(EVT_NET_ERROR, new List<object> { source ?? "", message ?? "" });
        }

        #endregion

        // ─── Payload 序列化 ──────────────────────────────────────
        /// <summary>把任意业务 payload 编码为 JSON 字符串。null / 已为 string 时原样。</summary>
        public static string EncodePayload(object payload)
        {
            if (payload == null) return string.Empty;
            if (payload is string s) return s;
            try { return MiniJson.Serialize(payload); }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkService] EncodePayload 失败: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>把订阅端收到的 JSON 还原为 <see cref="object"/>（Dict/List/原始类型）；不是 JSON 时返回原串。</summary>
        public static object DecodePayload(string payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson)) return null;
            try { return MiniJson.Deserialize(payloadJson) ?? (object)payloadJson; }
            catch { return payloadJson; }
        }
    }
}
