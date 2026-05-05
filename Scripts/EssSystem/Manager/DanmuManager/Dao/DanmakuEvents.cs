using System;

namespace BiliBiliDanmu.Dao
{
    /// <summary>
    /// 弹幕底层长连接向外发出的事件委托 + 参数类型。
    /// <para>
    /// 本模块仅供 <c>Net.OpenDanmakuLoader</c> 使用；业务层<b>不应直接订阅</b>这些委托，
    /// 而是通过 <c>DanmuService.EVT_*</c> 事件（已切主线程 + 经过 EventProcessor 调度）。
    /// </para>
    /// </summary>

    public delegate void DisconnectEvt(object sender, DisconnectEvtArgs e);
    public delegate void ReceivedDanmakuEvt(object sender, ReceivedDanmakuArgs e);
    public delegate void ReceivedRoomCountEvt(object sender, ReceivedRoomCountArgs e);
    public delegate void ConnectedEvt(object sender, ConnectedEvtArgs e);

    /// <summary>人数更新事件参数。</summary>
    public class ReceivedRoomCountArgs
    {
        public uint UserCount;
    }

    /// <summary>断开事件参数。<see cref="Error"/> 为 null 表示主动断开。</summary>
    public class DisconnectEvtArgs
    {
        public Exception Error;
    }

    /// <summary>收到弹幕事件参数（包装 <see cref="DanmakuModel"/>）。</summary>
    public class ReceivedDanmakuArgs
    {
        public DanmakuModel Danmaku;

        public ReceivedDanmakuArgs(DanmakuModel danmaku)
        {
            Danmaku = danmaku;
        }
    }

    /// <summary>连接成功事件参数（仅老版 DMPlugin 用，保留以兼容三方）。</summary>
    public class ConnectedEvtArgs
    {
        public int roomid;
    }
}
