using System;

namespace EssSystem.Core.Application.SingleManagers.NpcManager.Dao
{
    /// <summary>
    /// NPC 配置 —— 定义"这个 NPC 是谁、长什么样、能做什么"的不变数据。
    /// 运行时实例化为 <see cref="NpcInstance"/>。
    /// </summary>
    [Serializable]
    public class NpcConfig
    {
        /// <summary>唯一 Id（如 "tribe_merchant_alice"）。</summary>
        public string Id;

        /// <summary>显示名（玩家头顶 / 对白条 / 商店标题用）。</summary>
        public string DisplayName;

        /// <summary>视觉表现的 CharacterConfig Id（接 CharacterManager；空 = 用占位 Sprite）。</summary>
        public string CharacterConfigId;

        /// <summary>主要角色定位。</summary>
        public NpcRole Role = NpcRole.Generic;

        /// <summary>对白 Id（接 DialogueManager；当 Interactions 含 <see cref="NpcInteractionFlags.Talk"/> 时使用）。</summary>
        public string DialogueId;

        /// <summary>商店 Id（Role=Merchant 时必填，接 ShopManager）。</summary>
        public string ShopId;

        /// <summary>任意标签（"tribe", "town", "friendly", ...）。</summary>
        public string[] Tags;

        /// <summary>互动菜单标志位（自动生成 InteractionPanel 项）。</summary>
        public NpcInteractionFlags Interactions = NpcInteractionFlags.Talk;
    }
}
