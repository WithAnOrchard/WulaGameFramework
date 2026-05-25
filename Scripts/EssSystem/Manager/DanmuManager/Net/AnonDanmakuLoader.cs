using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EssSystem.Core.Base.Util;

namespace BiliBiliDanmu.Net
{
    /// <summary>
    /// 匿名/Token 模式 WSS 长连接。
    /// <list type="bullet">
    /// <item>WebSocket 连接到 <c>wss://&lt;host&gt;/sub</c></item>
    /// <item>16 字节大端包头：<c>packet_len(4) header_len(2) protover(2) op(4) seq(4)</c></item>
    /// <item>op=7 认证 / op=2 心跳 (30s) / op=5 命令包（protover=2 zlib / protover=3 brotli）</item>
    /// <item>auth 包带 uid 时（Token 模式），服务端不过滤别人弹幕；uid=0 仅能见自己</item>
    /// </list>
    /// </summary>
    public sealed class AnonDanmakuLoader : IDisposable
    {
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;
        private readonly long _roomId;
        private readonly long _uid;
        private readonly string _token;
        private readonly string _host;
        private readonly int _wssPort;

        /// <summary>消息抵达（后台线程）。</summary>
        public event EventHandler<AnonDanmuMessage> ReceivedDanmaku;
        /// <summary>断开（后台线程）。Exception=null 表示正常断开。</summary>
        public event EventHandler<Exception> Disconnected;

        public bool Connected => _ws != null && _ws.State == WebSocketState.Open;

        public AnonDanmakuLoader(long roomId, string token, string host, int wssPort, long uid = 0)
        {
            _roomId = roomId;
            _uid = uid;
            _token = token;
            _host = host;
            _wssPort = wssPort > 0 ? wssPort : 443;
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _ws = new ClientWebSocket();
                var url = $"wss://{_host}:{_wssPort}/sub";
                await _ws.ConnectAsync(new Uri(url), CancellationToken.None);

                var authJson = $"{{\"uid\":{_uid},\"roomid\":{_roomId},\"protover\":2,\"platform\":\"web\",\"type\":2,\"key\":\"{_token}\"}}";
                var authBytes = Encoding.UTF8.GetBytes(authJson);
                var authPkt = BuildPacket(opcode: 7, protover: 1, body: authBytes);
                await _ws.SendAsync(new ArraySegment<byte>(authPkt), WebSocketMessageType.Binary, true, CancellationToken.None);

                _cts = new CancellationTokenSource();
                _ = ReceiveLoopAsync(_cts.Token);
                _ = HeartbeatLoopAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                Disconnected?.Invoke(this, ex);
                Dispose();
                return false;
            }
        }

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try
            {
                if (_ws?.State == WebSocketState.Open)
                    _ = _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch { }
        }

        public void Dispose()
        {
            try { _cts?.Cancel(); _cts?.Dispose(); } catch { }
            try { _ws?.Dispose(); } catch { }
            _cts = null;
            _ws = null;
        }

        // ─── 包构造 ─────────────────────────────────────────────
        private static byte[] BuildPacket(int opcode, int protover, byte[] body)
        {
            var len = 16 + body.Length;
            var buf = new byte[len];
            buf[0] = (byte)(len >> 24); buf[1] = (byte)(len >> 16); buf[2] = (byte)(len >> 8); buf[3] = (byte)len;
            buf[4] = 0; buf[5] = 16;
            buf[6] = (byte)(protover >> 8); buf[7] = (byte)protover;
            buf[8] = (byte)(opcode >> 24); buf[9] = (byte)(opcode >> 16); buf[10] = (byte)(opcode >> 8); buf[11] = (byte)opcode;
            buf[12] = 0; buf[13] = 0; buf[14] = 0; buf[15] = 1;
            Array.Copy(body, 0, buf, 16, body.Length);
            return buf;
        }

        private async Task HeartbeatLoopAsync(CancellationToken ct)
        {
            var hbBody = Encoding.UTF8.GetBytes("[object Object]");
            try
            {
                while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
                {
                    var hb = BuildPacket(opcode: 2, protover: 1, body: hbBody);
                    await _ws.SendAsync(new ArraySegment<byte>(hb), WebSocketMessageType.Binary, true, ct);
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Disconnected?.Invoke(this, ex); }
        }

        private async Task ReceiveLoopAsync(CancellationToken ct)
        {
            var buffer = new byte[1024 * 64];
            try
            {
                while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
                {
                    using var ms = new MemoryStream();
                    WebSocketReceiveResult r;
                    do
                    {
                        r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                        if (r.MessageType == WebSocketMessageType.Close)
                        {
                            Disconnected?.Invoke(this, null);
                            return;
                        }
                        ms.Write(buffer, 0, r.Count);
                    } while (!r.EndOfMessage);

                    ProcessPackets(ms.ToArray());
                }
            }
            catch (OperationCanceledException) { Disconnected?.Invoke(this, null); }
            catch (Exception ex) { Disconnected?.Invoke(this, ex); }
        }

        private void ProcessPackets(byte[] data)
        {
            var i = 0;
            while (i + 16 <= data.Length)
            {
                var packetLen = ReadInt32BE(data, i);
                var headerLen = ReadInt16BE(data, i + 4);
                var protover = ReadInt16BE(data, i + 6);
                var op = ReadInt32BE(data, i + 8);
                if (packetLen <= headerLen || i + packetLen > data.Length) break;

                var bodyOffset = i + headerLen;
                var bodyLen = packetLen - headerLen;
                try { HandlePacket(op, protover, data, bodyOffset, bodyLen); } catch { }
                i += packetLen;
            }
        }

        private void HandlePacket(int op, int protover, byte[] buf, int off, int len)
        {
            if (op != 5)
            {
                // 8 = auth ack, 3 = heartbeat ack（包体含人气值）, 其他忽略
                return;
            }
            if (protover == 2)
            {
                ProcessPackets(Inflate(buf, off, len));
            }
            else if (protover == 3)
            {
                try { ProcessPackets(BrotliInflate(buf, off, len)); }
                catch { /* 运行时不支持 brotli，丢弃 */ }
            }
            else if (protover == 0 || protover == 1)
            {
                DispatchCommand(Encoding.UTF8.GetString(buf, off, len));
            }
        }

        private static byte[] Inflate(byte[] data, int off, int len)
        {
            using var input = new MemoryStream(data, off + 2, len - 2);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] BrotliInflate(byte[] data, int off, int len)
        {
            using var input = new MemoryStream(data, off, len);
            using var br = new BrotliStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            br.CopyTo(output);
            return output.ToArray();
        }

        private void DispatchCommand(string json)
        {
            var node = MiniJson.Parse(json);
            var cmd = node["cmd"].ToString();
            if (string.IsNullOrEmpty(cmd)) return;

            switch (cmd)
            {
                case "DANMU_MSG":
                {
                    var info = node["info"];
                    var text = info[1].ToString();
                    var info2 = info[2];
                    var uid = info2[0].ToObject<long>();
                    var uname = info2[1].ToString();
                    ReceivedDanmaku?.Invoke(this, new AnonDanmuMessage
                    {
                        Type = AnonDanmuMsgType.Comment,
                        UserName = uname,
                        Text = text,
                        UserId = uid,
                    });
                    break;
                }
                case "SEND_GIFT":
                {
                    var d = node["data"];
                    ReceivedDanmaku?.Invoke(this, new AnonDanmuMessage
                    {
                        Type = AnonDanmuMsgType.Gift,
                        UserName = d["uname"].ToString(),
                        GiftName = d["giftName"].ToString(),
                        GiftCount = d.Value<int>("num"),
                        UserId = d.Value<long>("uid"),
                        GiftPrice    = d.Value<int>("price"),
                        GiftCoinType = d["coin_type"]?.ToString() ?? "silver",
                    });
                    break;
                }
                case "SUPER_CHAT_MESSAGE":
                {
                    var d = node["data"];
                    ReceivedDanmaku?.Invoke(this, new AnonDanmuMessage
                    {
                        Type = AnonDanmuMsgType.SuperChat,
                        UserName = d["user_info"]["uname"].ToString(),
                        Text = d["message"].ToString(),
                        GiftCount = d.Value<int>("price"),
                        UserId = d.Value<long>("uid"),
                    });
                    break;
                }
            }
        }

        private static int ReadInt32BE(byte[] b, int i)
            => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];
        private static int ReadInt16BE(byte[] b, int i)
            => (b[i] << 8) | b[i + 1];
    }

    public enum AnonDanmuMsgType { Comment, Gift, SuperChat }

    public sealed class AnonDanmuMessage
    {
        public AnonDanmuMsgType Type;
        public string UserName = string.Empty;
        public string Text = string.Empty;
        public string GiftName = string.Empty;
        public int GiftCount;
        public long UserId;
        /// <summary>Price per gift in 金瓜子 (100 = 1 battery = 0.1 RMB). 0 for silver gifts.</summary>
        public int GiftPrice;
        /// <summary>"gold" = paid battery gift, "silver" = free room-coin gift.</summary>
        public string GiftCoinType = "silver";
    }
}
