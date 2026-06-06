using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    public class DamageReflectEffect : ISkillEffect
    {
        public string BuffId = "damage_reflect";
        public float Duration = 5f;
        public float ReflectRatio = 0.5f;
        public bool ApplyToSelf = true;
        public string ReflectDamageType = "reflect";

        public DamageReflectEffect() { }
        public DamageReflectEffect(float reflectRatio, float duration, bool applyToSelf = true,
            string buffId = "damage_reflect", string reflectDamageType = "reflect")
        {
            ReflectRatio = reflectRatio;
            Duration = duration;
            ApplyToSelf = applyToSelf;
            BuffId = buffId;
            ReflectDamageType = reflectDamageType;
        }

        public void Apply(SkillEffectContext ctx)
        {
            var targetId = ApplyToSelf ? ctx?.CasterId : ctx?.TargetId;
            if (string.IsNullOrEmpty(targetId) || !SkillService.HasInstance) return;
            var ratio = ReflectRatio;
            var type = ReflectDamageType;
            var unsubscribe = SkillEntityProxy.RegisterDamagedCallback(targetId, (selfId, sourceId, dealt, _) =>
            {
                if (string.IsNullOrEmpty(sourceId) || sourceId == selfId || dealt <= 0f) return;
                SkillEntityProxy.Damage(sourceId, dealt * ratio, selfId, type);
            });
            if (unsubscribe == null) return;

            SkillService.Instance.ApplyBuff(targetId, new BuffInstance
            {
                BuffId = BuffId,
                SourceId = ctx.CasterId,
                Duration = Duration,
                OnExpire = _ => unsubscribe(),
            });
        }
    }
}
