using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Effects
{
    /// <summary>
    /// 净化效果 —— 从目标（或自身）身上移除指定 BuffId 的所有 Buff。
    /// <list type="bullet">
    /// <item><see cref="BuffIds"/>=null / 空：移除目标 **所有** Buff（"完全净化"）。</item>
    /// <item>否则只移除列出的 ID（典型 ["burn","slow","poison"]）。</item>
    /// <item>调用 <see cref="SkillService.RemoveBuff"/>，会触发被移除 Buff 的 OnExpire 还原回调。</item>
    /// </list>
    /// 典型用法：牧师"驱散"、武僧"涤罪"、解控药水。
    /// </summary>
    public class CleanseEffect : ISkillEffect
    {
        /// <summary>要移除的 BuffId 列表。null / 空 = 移除所有 Buff。</summary>
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
            var target = ApplyToSelf ? ctx.Caster : ctx.Target;
            if (target == null || string.IsNullOrEmpty(target.InstanceId)) return;
            if (!SkillService.HasInstance) return;

            var buffs = SkillService.Instance.GetBuffs(target.InstanceId);
            if (buffs == null || buffs.Count == 0) return;

            if (BuffIds == null || BuffIds.Count == 0)
            {
                // 收集所有 unique BuffId（防止迭代过程中修改集合）
                var allIds = new HashSet<string>();
                for (var i = 0; i < buffs.Count; i++)
                    if (!string.IsNullOrEmpty(buffs[i].BuffId)) allIds.Add(buffs[i].BuffId);
                foreach (var id in allIds)
                    SkillService.Instance.RemoveBuff(target.InstanceId, id);
            }
            else
            {
                for (var i = 0; i < BuffIds.Count; i++)
                    SkillService.Instance.RemoveBuff(target.InstanceId, BuffIds[i]);
            }
        }
    }
}
