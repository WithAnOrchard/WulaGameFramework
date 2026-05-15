namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities
{
    /// <summary>
    /// 受伤闪烁效果能力 —— Entity 受伤时触发视觉闪烁反馈。
    /// </summary>
    public interface IFlashEffect : IEntityCapability
    {
        /// <summary>触发受伤闪烁</summary>
        void OnFlash();
    }
}
