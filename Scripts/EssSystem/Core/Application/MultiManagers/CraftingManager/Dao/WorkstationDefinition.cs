using System;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager.Dao
{
    /// <summary>
    /// 工作台定义 —— "什么样的工作台可以做什么类别的 Tier ≤ N 的配方"。
    /// 地图上的物理工作台是业务侧 <c>WorkstationFeature</c>，引用本结构的 Id。
    /// </summary>
    [Serializable]
    public class WorkstationDefinition
    {
        /// <summary>唯一 Id（"workbench_basic" / "anvil_iron" / "furnace" / "alchemy_table"）。</summary>
        public string Id;

        /// <summary>显示名。</summary>
        public string DisplayName;

        /// <summary>Tier 等级（决定可制作配方 Tier 上限）。</summary>
        public int Tier = 1;

        /// <summary>仅支持的 Recipe.CategoryId 列表（空 = 全部支持）。</summary>
        public string[] Categories;
    }
}
