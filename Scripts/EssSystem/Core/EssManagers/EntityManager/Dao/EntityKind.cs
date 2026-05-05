namespace EssSystem.EssManager.EntityManager.Dao
{
    /// <summary>
    /// Entity 大类。决定是否参与移动 / 物理 / AI Tick 等可选子系统。
    /// <list type="bullet">
    /// <item><b>Static</b> —— 不可移动（植物、矿石、场景道具、建筑等）。<c>EntityService.Tick</c>
    /// 默认会跳过它们的位置同步，业务也应避免对其加 AI / 移动能力。</item>
    /// <item><b>Dynamic</b> —— 可移动（动物、怪物、玩家、NPC 等）。位置由逻辑层驱动，
    /// Tick 会把 <c>Entity.WorldPosition</c> 同步到显示 Character。</item>
    /// </list>
    /// </summary>
    public enum EntityKind
    {
        Static = 0,
        Dynamic = 1,
    }
}
