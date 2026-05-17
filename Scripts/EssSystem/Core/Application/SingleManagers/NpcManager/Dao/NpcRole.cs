namespace EssSystem.Core.Application.SingleManagers.NpcManager.Dao
{
    /// <summary>
    /// NPC 主要角色定位 —— 决定默认互动菜单的项目集合与业务侧的初始挂接。
    /// </summary>
    public enum NpcRole
    {
        /// <summary>普通 NPC（仅可对话 / 装饰）。</summary>
        Generic = 0,
        /// <summary>商人 —— 触发 ShopManager 的 OpenShop（NpcConfig.ShopId 必填）。</summary>
        Merchant = 1,
        /// <summary>任务发布者 —— 接未来 QuestManager。</summary>
        Quester = 2,
        /// <summary>导师 / 训练员 —— 提升技能等级。</summary>
        Trainer = 3,
        /// <summary>故事讲述者 —— 长篇剧情 / 回忆录。</summary>
        Storyteller = 4,
        /// <summary>守卫 —— 巡逻 / 进入限制。</summary>
        Guard = 5,
        /// <summary>银行家 / 仓库管理员 —— 寄存物品。</summary>
        Banker = 6,
    }
}
