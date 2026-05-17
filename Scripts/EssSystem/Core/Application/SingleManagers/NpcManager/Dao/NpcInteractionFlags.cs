using System;

namespace EssSystem.Core.Application.SingleManagers.NpcManager.Dao
{
    /// <summary>
    /// NPC 互动菜单标志位 —— 决定 InteractionPanel 自动生成的菜单项。
    /// <para>
    /// 单一标志（如仅 <see cref="Talk"/>）时按 E 直接进入对应行为；
    /// 多标志时弹出菜单让玩家选择。
    /// </para>
    /// </summary>
    [Flags]
    public enum NpcInteractionFlags
    {
        None  = 0,
        /// <summary>对话 —— 走 DialogueManager 打开 NpcConfig.DialogueId 对白。</summary>
        Talk  = 1 << 0,
        /// <summary>交易 —— 走 ShopManager 打开 NpcConfig.ShopId 商店。</summary>
        Trade = 1 << 1,
        /// <summary>任务 —— 走未来 QuestManager。</summary>
        Quest = 1 << 2,
        /// <summary>训练 —— 走未来 TrainingManager。</summary>
        Train = 1 << 3,
        /// <summary>寄存 —— 走仓库 / 银行 UI。</summary>
        Bank  = 1 << 4,
    }
}
