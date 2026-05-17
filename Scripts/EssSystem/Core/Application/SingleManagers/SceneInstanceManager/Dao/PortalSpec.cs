using System;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.SceneInstanceManager.Dao
{
    /// <summary>
    /// 出 / 入门描述 —— 子场景内部的传送门描述（出口）；
    /// 主世界侧的门由业务层（如 <c>Demo/Tribe/World/Features/PortalFeature</c>）放置。
    /// </summary>
    [Serializable]
    public class PortalSpec
    {
        /// <summary>门的位置（相对 Instance.OriginOffset 的局部坐标）。</summary>
        public Vector2 LocalPosition;

        /// <summary>目标 Instance Id（"" / null = 通往 OverWorld）。</summary>
        public string TargetInstanceId;

        /// <summary>从该门出去后玩家落在 OverWorld 的位置（仅 TargetInstanceId 为空时使用）。</summary>
        public Vector2 ReturnPosition;

        /// <summary>提示文本（玩家走近时 HUD 显示，默认 "按 E"）。</summary>
        public string PromptText = "按 E";

        /// <summary>门的 Sprite 资源 Id（走 ResourceManager 解析）。</summary>
        public string SpriteId;
    }
}
