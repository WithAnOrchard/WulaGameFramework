using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IStats"/> 的默认实现 —— 在内存里维护 <see cref="AttributeSet"/>，
    /// 读 Primary / Derived 时按 <see cref="StatFormulas"/> 公式 + Modifier 合成。
    /// <para>
    /// <b>骨架阶段</b>：未做事件广播 / 持续时间 Tick / 持久化；
    /// 这些将在 #2.M1 里程碑实施时补齐（参见 <c>Demo/Tribe/ToDo.md</c>）。
    /// </para>
    /// </summary>
    public class StatsComponent : IStats
    {
        private Entity _owner;
        private readonly AttributeSet _attributes;

        public AttributeSet Attributes => _attributes;

        public StatsComponent() { _attributes = new AttributeSet(); }

        public StatsComponent(AttributeSet preset) { _attributes = preset ?? new AttributeSet(); }

        public StatsComponent(int str, int dex, int con, int intl, int wis, int cha)
        {
            _attributes = new AttributeSet(str, dex, con, intl, wis, cha);
        }

        public void OnAttach(Entity owner) { _owner = owner; }
        public void OnDetach(Entity owner) { _owner = null; }

        public int GetPrimary(PrimaryStat stat)
        {
            float raw = _attributes.GetPrimaryRaw(stat);
            float flat = 0f, percentAdd = 0f, percentMul = 1f;
            for (int i = 0; i < _attributes.Modifiers.Count; i++)
            {
                var m = _attributes.Modifiers[i];
                if (!m.TargetIsPrimary || m.TargetPrimary != stat) continue;
                switch (m.Op)
                {
                    case StatModifierOp.Flat:        flat += m.Value; break;
                    case StatModifierOp.PercentAdd:  percentAdd += m.Value; break;
                    case StatModifierOp.PercentMul:  percentMul *= (1f + m.Value); break;
                }
            }
            float final = (raw + flat) * (1f + percentAdd) * percentMul;
            return UnityEngine.Mathf.RoundToInt(final);
        }

        public float GetDerived(DerivedStat stat)
        {
            float baseValue = StatFormulas.ComputeDerived(stat, _attributes);
            float flat = 0f, percentAdd = 0f, percentMul = 1f;
            for (int i = 0; i < _attributes.Modifiers.Count; i++)
            {
                var m = _attributes.Modifiers[i];
                if (m.TargetIsPrimary || m.TargetDerived != stat) continue;
                switch (m.Op)
                {
                    case StatModifierOp.Flat:        flat += m.Value; break;
                    case StatModifierOp.PercentAdd:  percentAdd += m.Value; break;
                    case StatModifierOp.PercentMul:  percentMul *= (1f + m.Value); break;
                }
            }
            return (baseValue + flat) * (1f + percentAdd) * percentMul;
        }

        public void AddModifier(StatModifier modifier)
        {
            if (modifier == null) return;
            // 同 SourceId 的旧条目先全清，避免重复叠加（穿戴同件装备两次只生效一次）。
            if (!string.IsNullOrEmpty(modifier.SourceId))
                RemoveBySource(modifier.SourceId);
            _attributes.Modifiers.Add(modifier);
        }

        public void RemoveBySource(string sourceId)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            _attributes.Modifiers.RemoveAll(m => m.SourceId == sourceId);
        }

        public void SetPrimary(PrimaryStat stat, int value)
        {
            _attributes.SetPrimaryRaw(stat, value);
        }
    }
}
