namespace BiliBiliLive
{
    /// <summary>
    /// B 站直播间公开信息（来自 <c>api.live.bilibili.com/room/v1/Room/get_info</c>）。
    /// 只读快照；所有字段都是匿名 API 即可获取，不需要身份码。
    /// </summary>
    public sealed class LiveRoomInfo
    {
        public long RoomId;
        /// <summary>主播 UID。</summary>
        public long Uid;
        /// <summary>0 = 未开播 / 1 = 直播中 / 2 = 轮播中。</summary>
        public int LiveStatus;
        /// <summary>直播标题。</summary>
        public string Title = string.Empty;
        /// <summary>子分区名（例如 "原神"）。</summary>
        public string AreaName = string.Empty;
        /// <summary>父分区名（例如 "网游"）。</summary>
        public string ParentAreaName = string.Empty;
        /// <summary>当前在线观众数（B 站会做模糊化处理）。</summary>
        public int Online;
        /// <summary>关注数。</summary>
        public int Attention;
        /// <summary>开播时间（格式 "yyyy-MM-dd HH:mm:ss"，未开播时为 "0000-00-00 00:00:00"）。</summary>
        public string LiveTime = string.Empty;
        /// <summary>用户标签（逗号分隔字符串）。</summary>
        public string Tags = string.Empty;
        /// <summary>主播简介。</summary>
        public string Description = string.Empty;
        /// <summary>直播间封面 URL。</summary>
        public string UserCover = string.Empty;
        /// <summary>关键帧封面 URL（直播中时段的最近截图）。</summary>
        public string Keyframe = string.Empty;
        /// <summary>背景图 URL。</summary>
        public string Background = string.Empty;

        public override string ToString()
        {
            return $"LiveRoomInfo[Room={RoomId}, Status={LiveStatus}, Title={Title}, Area={ParentAreaName}/{AreaName}, Online={Online}]";
        }
    }
}
