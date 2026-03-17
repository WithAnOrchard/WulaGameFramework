using System;
using BiliBiliDanmu.BilibiliDM_PluginFramework;

namespace BilibiliDM_PluginFramework
{
    public delegate void DisconnectEvt(object sender, DisconnectEvtArgs e);

    public delegate void ReceivedDanmakuEvt(object sender, ReceivedDanmakuArgs e);

    public delegate void ReceivedRoomCountEvt(object sender, ReceivedRoomCountArgs e);

    public delegate void ConnectedEvt(object sender, ConnectedEvtArgs e);

    public class ReceivedRoomCountArgs
    {
        public uint UserCount;
    }

    public class DisconnectEvtArgs
    {
        public Exception Error;
    }

    public class ReceivedDanmakuArgs
    {
        public DanmakuModel Danmaku;

        public ReceivedDanmakuArgs(DanmakuModel danmaku)
        {
            Danmaku = danmaku;
        }
    }

    public class ConnectedEvtArgs
    {
        public int roomid;
    }
}