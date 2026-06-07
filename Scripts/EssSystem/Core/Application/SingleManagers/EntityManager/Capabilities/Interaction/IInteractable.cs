using System;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 可交互能力 —— Entity 暴露"靠近 + 按键"互动入口。
    /// <list type="bullet">
    /// <item><b>典型用例</b>：NPC 对话、工作台制作、宝箱开启、门 / 传送门触发。</item>
    /// <item><b>实现方</b>：<see cref="InteractableComponent"/>（默认）；或业务方实现自定义版本（自定义提示 UI、复杂条件等）。</item>
    /// <item><b>触发条件</b>：玩家进入 <see cref="Radius"/> 范围 + 按下 <see cref="InteractKey"/>。</item>
    /// </list>
    /// </summary>
    public interface IInteractable : IEntityCapability
    {
        /// <summary>互动半径（世界单位）。</summary>
        float Radius { get; set; }

        /// <summary>互动键（默认 F）。</summary>
        string InteractAction { get; set; }

        /// <summary>头顶提示文字（如 "[F] 对话" / "[F] 制作"）。</summary>
        string PromptLabel { get; set; }

        /// <summary>是否启用 —— 关闭后不再显示提示，也不响应按键。</summary>
        bool Enabled { get; set; }

        /// <summary>玩家是否正处在交互范围内（运行时只读，业务层可参考做边缘视效）。</summary>
        bool PlayerInRange { get; }

        /// <summary>互动触发回调 —— 玩家按下 <see cref="InteractKey"/> 时调用。</summary>
        Action OnInteract { get; set; }
    }
}
