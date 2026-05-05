using System;

namespace BiliBiliDanmu.Net
{
    /// <summary>
    /// B 站直播弹幕长连接的 16 字节包头协议。
    /// <para>
    /// 字段排布（Big-Endian）：PacketLength(4) | HeaderLength(2) | Version(2) | Action(4) | Parameter(4)。
    /// </para>
    /// </summary>
    public struct DanmakuProtocol
    {
        /// <summary>消息总长度（协议头 + 数据长度）。</summary>
        public int PacketLength;
        /// <summary>消息头长度（固定 16 = sizeof(DanmakuProtocol)）。</summary>
        public short HeaderLength;
        /// <summary>消息版本号。</summary>
        public short Version;
        /// <summary>消息类型（Action）。</summary>
        public int Action;
        /// <summary>参数，固定为 1。</summary>
        public int Parameter;

        public static DanmakuProtocol FromBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 16) throw new ArgumentException("DanmakuProtocol buffer 长度不足 16");
            return new DanmakuProtocol
            {
                PacketLength = Internal.EndianBitConverter.BigEndian.ToInt32(buffer, 0),
                HeaderLength = Internal.EndianBitConverter.BigEndian.ToInt16(buffer, 4),
                Version      = Internal.EndianBitConverter.BigEndian.ToInt16(buffer, 6),
                Action       = Internal.EndianBitConverter.BigEndian.ToInt32(buffer, 8),
                Parameter    = Internal.EndianBitConverter.BigEndian.ToInt32(buffer, 12)
            };
        }
    }
}
