using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;
using EssSystem.Core.Application.MultiManagers.SkillManager.UI;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class LifeStealBuffEffect : ISkillEffect
    {
        public string BuffId = "bloodthirst";
        public float Duration = 6f;
        public float HealRatio = 0.35f;
        public string DamageTypeFilter = "physical";
        public string IconPath;
        public string DisplayName = "Bloodthirst";
        public string Description;

        public LifeStealBuffEffect() { }

        public LifeStealBuffEffect(string buffId, float duration, float healRatio,
            string damageTypeFilter, string iconPath, string displayName, string description)
        {
            BuffId = string.IsNullOrEmpty(buffId) ? "bloodthirst" : buffId;
            Duration = duration;
            HealRatio = healRatio;
            DamageTypeFilter = damageTypeFilter;
            IconPath = iconPath;
            DisplayName = displayName;
            Description = description;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var casterId = ctx?.CasterId;
            if (string.IsNullOrEmpty(casterId) || !SkillService.HasInstance) return;

            SkillService.Instance.RemoveBuff(casterId, BuffId);

            var ratio = Mathf.Max(0f, HealRatio);
            var typeFilter = DamageTypeFilter;
            var unsubscribe = SkillEntityProxy.RegisterDealtDamageCallback(casterId,
                (sourceId, targetId, dealt, damageType) =>
                {
                    if (sourceId != casterId || dealt <= 0f) return;
                    if (!MatchesDamageType(damageType, typeFilter)) return;
                    var heal = Mathf.Max(1f, dealt * ratio);
                    SkillEntityProxy.Heal(casterId, heal, casterId);
                });

            if (unsubscribe == null) return;

            SkillBuffStatusUI.ShowForPlayer(casterId, BuffId, IconPath, DisplayName,
                BuildDescription(), Duration);
            var visual = SkillBuffVisual.Attach(casterId, SkillBuffVisualProfile.Bloodthirst(Duration));

            SkillService.Instance.ApplyBuff(casterId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = casterId,
                Duration = Duration,
                OnExpire = _ =>
                {
                    unsubscribe();
                    visual?.StopAndDestroy();
                    SkillBuffStatusUI.Hide(casterId, BuffId);
                },
            });
        }

        private string BuildDescription()
        {
            if (!string.IsNullOrEmpty(Description)) return Description;
            var percent = Mathf.RoundToInt(Mathf.Max(0f, HealRatio) * 100f);
            return $"{Duration:0.#}s: physical damage restores {percent}% HP.";
        }

        private static bool MatchesDamageType(string damageType, string filter)
        {
            if (string.IsNullOrEmpty(filter) || filter == "*") return true;
            return string.Equals(damageType ?? string.Empty, filter, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
