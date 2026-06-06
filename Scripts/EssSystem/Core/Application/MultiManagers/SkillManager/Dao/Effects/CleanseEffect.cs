using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class CleanseEffect : ISkillEffect
    {
        public List<string> BuffIds;
        public bool ApplyToSelf = true;

        public CleanseEffect() { }
        public CleanseEffect(List<string> buffIds, bool applyToSelf = true)
        {
            BuffIds = buffIds;
            ApplyToSelf = applyToSelf;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            var buffs = SkillService.Instance.GetBuffs(targetId);
            if (buffs == null || buffs.Count == 0) return;

            if (BuffIds == null || BuffIds.Count == 0)
            {
                var allIds = new HashSet<string>();
                for (var i = 0; i < buffs.Count; i++)
                    if (!string.IsNullOrEmpty(buffs[i].BuffId)) allIds.Add(buffs[i].BuffId);
                foreach (var id in allIds)
                    SkillService.Instance.RemoveBuff(targetId, id);
            }
            else
            {
                for (var i = 0; i < BuffIds.Count; i++)
                    SkillService.Instance.RemoveBuff(targetId, BuffIds[i]);
            }
        }
    }
}
