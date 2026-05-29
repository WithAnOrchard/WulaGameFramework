// 通用网络消息结构 —— 仅在 Mirror 已安装时编译。
// 业务方不直接接触本类型；NetworkManager 的 EVT_SEND_* 命令会自动包装。

#if MIRROR_INSTALLED
using Mirror;

namespace EssSystem.Core.Foundation.NetworkManager.Runtime
{
    /// <summary>通用文本消息：topic 用于业务路由，payload 是 JSON 字符串（由 NetworkService.EncodePayload 生成）。</summary>
    public struct WulaNetMessage : NetworkMessage
    {
        public string Topic;
        public string Payload;
    }
}
#endif
