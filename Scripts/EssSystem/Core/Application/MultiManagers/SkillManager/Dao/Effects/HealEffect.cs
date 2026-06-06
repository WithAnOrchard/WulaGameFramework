using EssSystem.Core.Application.MultiManagers.SkillManager;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class HealEffect : ISkillEffect
    {
        public float BaseHeal;
        public float HealPerLevel;
        public bool HealSelf;

        public HealEffect(float baseHeal, float healPerLevel = 0f, bool healSelf = false)
        {
            BaseHeal = baseHeal;
            HealPerLevel = healPerLevel;
            HealSelf = healSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            if (ctx == null) return;
            var targetId = HealSelf ? ctx.CasterId : ctx.TargetId;
            if (string.IsNullOrEmpty(targetId) || SkillEntityProxy.IsDead(targetId)) return;
            SkillEntityProxy.Heal(targetId, BaseHeal + HealPerLevel * (ctx.Level - 1), ctx.CasterId);
        }
    }
}
