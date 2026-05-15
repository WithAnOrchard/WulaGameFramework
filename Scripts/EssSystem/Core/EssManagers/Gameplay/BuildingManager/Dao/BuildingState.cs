namespace EssSystem.Core.EssManagers.Gameplay.BuildingManager.Dao
{
    /// <summary>建筑运行时状态。Constructing → Completed 是单向的；销毁不进入此枚举（直接移出实例字典）。</summary>
    public enum BuildingState
    {
        /// <summary>正在建造（等待材料）。</summary>
        Constructing,
        /// <summary>已建成；能力链已注入到 Entity。</summary>
        Completed
    }
}
