using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;

namespace Demo.DayNight.BaseDefense
{
    /// <summary>据点防御 Service —— 维护核心据点 HP，HP 归零判失败。</summary>
    public class BaseDefenseService : Service<BaseDefenseService>
    {
        // ─── 数据分类 ────────────────────────────────────────────
        public const string CAT_STATE = "State";
        private const string KEY_MAX_HP = "MaxHp";
        private const string KEY_CURRENT_HP = "CurrentHp";

        // ─── Event 名常量（广播）────────────────────────────────
        /// <summary>HP 变化 **广播**。参数 <c>[int currentHp, int maxHp, int delta]</c>。</summary>
        public const string EVT_HP_CHANGED = "OnBaseHpChanged";

        /// <summary>据点被击毁 **广播**（无参数）。</summary>
        public const string EVT_DESTROYED = "OnBaseDestroyed";

        // ─── 运行时状态 ─────────────────────────────────────────
        public int MaxHp { get; private set; } = 1000;
        public int CurrentHp { get; private set; } = 1000;
        public bool IsDestroyed => CurrentHp <= 0;

        protected override void Initialize()
        {
            base.Initialize();
            // 从持久化恢复（首次为默认值）
            MaxHp = GetData<int>(CAT_STATE, KEY_MAX_HP);
            if (MaxHp <= 0) MaxHp = 1000;
            CurrentHp = GetData<int>(CAT_STATE, KEY_CURRENT_HP);
            if (CurrentHp <= 0) CurrentHp = MaxHp;
            Log($"BaseDefenseService 初始化完成 (HP {CurrentHp}/{MaxHp})", Color.green);
        }

        // ─── Public API ──────────────────────────────────────────
        public void Configure(int maxHp)
        {
            if (maxHp < 1) maxHp = 1;
            MaxHp = maxHp;
            CurrentHp = Mathf.Min(CurrentHp, MaxHp);
            SetData(CAT_STATE, KEY_MAX_HP, MaxHp);
            SetData(CAT_STATE, KEY_CURRENT_HP, CurrentHp);
            Broadcast(0);
        }

        /// <summary>对据点造成伤害；amount 必须 &gt; 0。</summary>
        public void ApplyDamage(int amount)
        {
            if (amount <= 0 || IsDestroyed) return;
            var prev = CurrentHp;
            CurrentHp = Mathf.Max(0, CurrentHp - amount);
            SetData(CAT_STATE, KEY_CURRENT_HP, CurrentHp);
            Broadcast(prev - CurrentHp);
            if (IsDestroyed)
            {
                if (EventProcessor.HasInstance)
                    EventProcessor.Instance.TriggerEvent(EVT_DESTROYED, new List<object>());
                Log("据点被击毁", Color.red);
            }
        }

        /// <summary>治疗（不超过 MaxHp）；如果已 destroyed 不会自动复活。</summary>
        public void Heal(int amount)
        {
            if (amount <= 0 || IsDestroyed) return;
            var prev = CurrentHp;
            CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
            SetData(CAT_STATE, KEY_CURRENT_HP, CurrentHp);
            Broadcast(prev - CurrentHp);
        }

        /// <summary>把 HP 重置回 MaxHp（新局开始）。</summary>
        public void Reset()
        {
            CurrentHp = MaxHp;
            SetData(CAT_STATE, KEY_CURRENT_HP, CurrentHp);
            Broadcast(0);
        }

        private void Broadcast(int delta)
        {
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEvent(EVT_HP_CHANGED,
                new List<object> { CurrentHp, MaxHp, delta });
        }
    }
}
