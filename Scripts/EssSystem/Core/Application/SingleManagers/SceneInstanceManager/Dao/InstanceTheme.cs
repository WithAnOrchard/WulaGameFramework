namespace EssSystem.Core.Application.SingleManagers.SceneInstanceManager.Dao
{
    /// <summary>
    /// 子场景（Instance）主题 —— 决定 InstanceRules 的预设倾向与玩家进入时的世界状态。
    /// </summary>
    public enum InstanceTheme
    {
        /// <summary>安全采集 / 休整区（禁敌人 + HP 缓回）。</summary>
        Safe = 0,
        /// <summary>一次性触发剧情 / 神秘洞窟。</summary>
        Event = 1,
        /// <summary>战斗副本 / Boss 房（敌人密度高 + 掉落加成）。</summary>
        Combat = 2,
        /// <summary>解谜房 / 古代神殿（触发器驱动）。</summary>
        Puzzle = 3,
        /// <summary>高密度 NPC 社交区 / 王城（强制和平）。</summary>
        Social = 4,
    }
}
