using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BiliBiliDanmu.Net
{
    /// <summary>
    /// 长连接收包用的 Stream 扩展。仅本模块使用。
    /// </summary>
    internal static class StreamExtensions
    {
        /// <summary>
        /// 保证从 <paramref name="stream"/> 读满 <paramref name="count"/> 字节到 <paramref name="buffer"/>，
        /// 读不满时循环直到读完；流结束会抛 <see cref="ObjectDisposedException"/>。
        /// </summary>
        public static async Task ReadBAsync(this Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
        {
            if (offset + count > buffer.Length)
                throw new ArgumentException("ReadBAsync: offset + count 超出 buffer 长度");
            var read = 0;
            while (read < count)
            {
                var available = await stream.ReadAsync(buffer, offset, count - read, ct);
                if (available == 0) throw new ObjectDisposedException(null);
                read += available;
                offset += available;
            }
        }
    }
}
