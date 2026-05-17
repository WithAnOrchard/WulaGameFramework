using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Application.MultiManagers.CraftingManager
{
    /// <summary>
    /// 制作业务服务 —— 持久化配方表 / 工作台表 / 玩家已学配方；运行时维护 CraftingSession 列表。
    /// <para>
    /// <b>骨架阶段</b>：仅承载 Service 数据存储约定与日志通道；
    /// LearnBlueprint / StartCraft / CompleteCraft / 品质判定 / CraftSkill 经验
    /// 在 <c>Demo/Tribe/ToDo.md #5</c> 各里程碑（M1-M5）实施。
    /// </para>
    /// </summary>
    public class CraftingService : Service<CraftingService>
    {
        #region 数据分类

        /// <summary>已注册的 CraftingRecipe（按 Id）。</summary>
        public const string CAT_RECIPES        = "Recipes";

        /// <summary>已注册的 WorkstationDefinition（按 Id）。</summary>
        public const string CAT_WORKSTATIONS   = "Workstations";

        /// <summary>玩家已学会的配方（按 playerId → Set&lt;recipeId&gt;）。</summary>
        public const string CAT_KNOWN_RECIPES  = "KnownRecipes";

        /// <summary>正在进行的 CraftingSession（按 sessionId，运行时不写盘）。</summary>
        public const string CAT_SESSIONS       = "Sessions";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("CraftingService 初始化完成（骨架）", Color.green);
        }
    }
}
