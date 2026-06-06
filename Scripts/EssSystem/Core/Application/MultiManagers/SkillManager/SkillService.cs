using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Buffs;

namespace EssSystem.Core.Application.MultiManagers.SkillManager
{
    /// <summary>
    /// 技能服务 —— 技能注册、查询、释放、Buff 管理的业务核心。
    /// <para>
    /// 职责：
    /// <list type="bullet">
    /// <item>技能定义注册与查询</item>
    /// <item>实体技能实例管理（学习/遗忘/升级）</item>
    /// <item>技能释放流水线（消耗检查 → SkillExecutor）</item>
    /// <item>Buff 生命周期管理（挂载/Tick/过期清理）</item>
    /// <item>全局冷却 Tick</item>
    /// </list>
    /// </para>
    /// </summary>
    public class SkillService : Service<SkillService>
    {
        // ─── 技能定义注册表 ───────────────────────────────────────
        private readonly Dictionary<string, SkillDefinition> _definitions = new();

        // ─── 实体技能实例：entityInstanceId → (skillId → SkillInstance) ──
        private readonly Dictionary<string, Dictionary<string, SkillInstance>> _entitySkills = new();

        // ─── 实体技能槽位：entityInstanceId → SkillSlot[] ──────────
        private readonly Dictionary<string, SkillSlot[]> _entitySlots = new();

        // ─── Buff 管理：entityInstanceId → List<BuffInstance> ──────
        private readonly Dictionary<string, List<BuffInstance>> _entityBuffs = new();

        // ─── 活跃 Executor 列表（用于 Tick 推进前摇/后摇）──────────
        private readonly List<SkillExecutor> _activeExecutors = new();

        // ═══════════════════════════════════════════════════════════
        //  技能定义
        // ═══════════════════════════════════════════════════════════

        /// <summary>注册技能定义。</summary>
        public void RegisterDefinition(SkillDefinition def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return;
            _definitions[def.Id] = def;
        }

        /// <summary>查询技能定义。</summary>
        public SkillDefinition GetDefinition(string skillId)
            => _definitions.TryGetValue(skillId, out var def) ? def : null;

        // ═══════════════════════════════════════════════════════════
        //  实体技能实例
        // ═══════════════════════════════════════════════════════════

        /// <summary>给实体学习一个技能。</summary>
        public SkillInstance LearnSkill(string entityId, string skillId)
        {
            var def = GetDefinition(skillId);
            if (def == null)
            {
                Debug.LogWarning($"[SkillService] 未注册的技能: {skillId}");
                return null;
            }

            if (!_entitySkills.TryGetValue(entityId, out var skills))
            {
                skills = new Dictionary<string, SkillInstance>();
                _entitySkills[entityId] = skills;
            }

            if (skills.ContainsKey(skillId)) return skills[skillId]; // 已学

            var instance = new SkillInstance
            {
                SkillId = skillId,
                Definition = def,
                Level = 1,
                Unlocked = true,
            };
            skills[skillId] = instance;
            return instance;
        }

        /// <summary>遗忘技能。</summary>
        public bool ForgetSkill(string entityId, string skillId)
        {
            return _entitySkills.TryGetValue(entityId, out var skills) && skills.Remove(skillId);
        }

        /// <summary>获取实体的技能实例。</summary>
        public SkillInstance GetSkillInstance(string entityId, string skillId)
        {
            return _entitySkills.TryGetValue(entityId, out var skills) &&
                   skills.TryGetValue(skillId, out var inst)
                ? inst
                : null;
        }

        /// <summary>获取实体的所有技能。</summary>
        public IReadOnlyDictionary<string, SkillInstance> GetAllSkills(string entityId)
        {
            return _entitySkills.TryGetValue(entityId, out var skills) ? skills : null;
        }

        /// <summary>技能升级（+1 级）。</summary>
        public bool UpgradeSkill(string entityId, string skillId)
        {
            var inst = GetSkillInstance(entityId, skillId);
            if (inst?.Definition == null) return false;
            if (inst.Level >= inst.Definition.MaxLevel) return false;
            inst.Level++;
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  技能槽位
        // ═══════════════════════════════════════════════════════════

        /// <summary>初始化技能槽位（如 4 个快捷栏）。</summary>
        public void InitSlots(string entityId, int slotCount)
        {
            var slots = new SkillSlot[slotCount];
            for (var i = 0; i < slotCount; i++)
                slots[i] = new SkillSlot(i);
            _entitySlots[entityId] = slots;
        }

        /// <summary>获取技能槽位数组。</summary>
        public SkillSlot[] GetSlots(string entityId)
            => _entitySlots.TryGetValue(entityId, out var slots) ? slots : null;

        /// <summary>绑定技能到槽位。</summary>
        public bool BindSlot(string entityId, int slotIndex, string skillId)
        {
            var slots = GetSlots(entityId);
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length) return false;
            var inst = GetSkillInstance(entityId, skillId);
            if (inst == null) return false;
            slots[slotIndex].Bind(inst);
            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  技能释放
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// 释放技能 —— 检查冷却/消耗，创建 SkillExecutor 执行。
        /// </summary>
        /// <returns>true = 成功开始释放。</returns>
        public bool CastSkill(string casterId, string skillId, string targetId = null,
            UnityEngine.Vector3 direction = default, UnityEngine.Vector3 position = default)
        {
            if (string.IsNullOrEmpty(casterId)) return false;
            // Silence 短路：被沉默 / 眩晕的实体无法施法（眩晕也禁止主动技能）
            if (SkillEntityProxy.IsBlockedForCast(casterId)) return false;
            var inst = GetSkillInstance(casterId, skillId);
            if (inst == null || !inst.IsReady) return false;

            var def = inst.Definition;

            // TODO: 消耗检查（MP/HP），待接入 INeeds 或玩家状态
            // if (def.ManaCost > 0f) { ... }

            var ctx = new SkillEffectContext
            {
                CasterId = casterId,
                TargetId = targetId,
                Definition = def,
                Instance = inst,
                Direction = direction,
                Position = position,
            };

            var executor = new SkillExecutor();
            if (!executor.Begin(ctx)) return false;

            if (executor.IsActive)
                _activeExecutors.Add(executor);

            // 连招追踪：成功 Cast 后压入实体历史
            Runtime.ComboTracker.OnSkillCast(casterId, skillId);

            return true;
        }

        // ═══════════════════════════════════════════════════════════
        //  Buff 管理
        // ═══════════════════════════════════════════════════════════

        /// <summary>给实体施加 Buff。</summary>
        public void ApplyBuff(string targetId, BuffInstance buff)
        {
            if (string.IsNullOrEmpty(targetId) || buff == null) return;
            buff.TargetId = targetId;
            buff.Remaining = buff.Duration;
            buff.TickTimer = 0f;

            if (!_entityBuffs.TryGetValue(targetId, out var buffs))
            {
                buffs = new List<BuffInstance>();
                _entityBuffs[targetId] = buffs;
            }
            buffs.Add(buff);
        }

        /// <summary>查询实体上的所有 Buff。</summary>
        public List<BuffInstance> GetBuffs(string entityId)
            => _entityBuffs.TryGetValue(entityId, out var buffs) ? buffs : null;

        /// <summary>移除实体上指定 ID 的所有 Buff。</summary>
        public void RemoveBuff(string entityId, string buffId)
        {
            if (!_entityBuffs.TryGetValue(entityId, out var buffs)) return;
            for (var i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].BuffId == buffId)
                {
                    buffs[i].OnExpire?.Invoke(buffs[i]);
                    buffs.RemoveAt(i);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  Tick（由 SkillManager.Update 驱动）
        // ═══════════════════════════════════════════════════════════

        /// <summary>每帧推进：冷却、Executor、Buff。</summary>
        public void Tick(float deltaTime)
        {
            TickCooldowns(deltaTime);
            TickExecutors(deltaTime);
            TickBuffs(deltaTime);
        }

        private void TickCooldowns(float deltaTime)
        {
            foreach (var kv in _entitySkills)
                foreach (var skill in kv.Value.Values)
                    skill.TickCooldown(deltaTime);
        }

        private void TickExecutors(float deltaTime)
        {
            for (var i = _activeExecutors.Count - 1; i >= 0; i--)
            {
                _activeExecutors[i].Tick(deltaTime);
                if (!_activeExecutors[i].IsActive)
                    _activeExecutors.RemoveAt(i);
            }
        }

        private void TickBuffs(float deltaTime)
        {
            foreach (var kv in _entityBuffs)
            {
                var buffs = kv.Value;
                for (var i = buffs.Count - 1; i >= 0; i--)
                {
                    buffs[i].Tick(deltaTime);
                    if (buffs[i].IsExpired)
                    {
                        buffs[i].OnExpire?.Invoke(buffs[i]);
                        buffs.RemoveAt(i);
                    }
                }
            }
        }
    }

    internal static class SkillEntityProxy
    {
        public static bool CanUseEvents => EventProcessor.HasInstance;

        public static float Damage(string targetId, float amount, string sourceId = null,
            string damageType = null, Vector3? sourcePosition = null)
        {
            if (string.IsNullOrEmpty(targetId) || amount <= 0f || !CanUseEvents) return 0f;
            var args = new List<object> { targetId, amount, damageType, sourceId };
            if (sourcePosition.HasValue) args.Add(sourcePosition.Value);
            return ReadFloat(EventProcessor.Instance.TriggerEventMethod("DamageEntity", args), 0f);
        }

        public static float Heal(string targetId, float amount, string sourceId = null)
        {
            if (string.IsNullOrEmpty(targetId) || amount <= 0f || !CanUseEvents) return 0f;
            return ReadFloat(EventProcessor.Instance.TriggerEventMethod(
                "HealEntity", new List<object> { targetId, amount, sourceId }), 0f);
        }

        public static Vector3 Position(string entityId, Vector3 fallback = default)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return fallback;
            var result = EventProcessor.Instance.TriggerEventMethod("GetEntityPosition", new List<object> { entityId });
            return ResultCode.IsOk(result) && result.Count > 1 && result[1] is Vector3 v ? v : fallback;
        }

        public static void SetPosition(string entityId, Vector3 position)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return;
            EventProcessor.Instance.TriggerEventMethod("SetEntityPosition", new List<object> { entityId, position });
        }

        public static Transform Root(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return null;
            var result = EventProcessor.Instance.TriggerEventMethod("GetCharacterRoot", new List<object> { entityId });
            return ResultCode.IsOk(result) && result.Count > 1 ? result[1] as Transform : null;
        }

        public static string IdFrom(UnityEngine.Object obj)
        {
            if (obj == null || !CanUseEvents) return null;
            var result = EventProcessor.Instance.TriggerEventMethod("GetEntityIdFromObject", new List<object> { obj });
            return ResultCode.IsOk(result) && result.Count > 1 ? result[1] as string : null;
        }

        public static bool IsDead(string entityId, bool missingMeansDead = true)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return missingMeansDead;
            var result = EventProcessor.Instance.TriggerEventMethod("IsEntityDead", new List<object> { entityId });
            return ResultCode.IsOk(result) && result.Count > 1 && result[1] is bool b ? b : missingMeansDead;
        }

        public static bool IsBlockedForCast(string entityId)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return false;
            var result = EventProcessor.Instance.TriggerEventMethod("GetControlState", new List<object> { entityId });
            if (!ResultCode.IsOk(result) || result.Count < 3) return false;
            return (result[1] is bool stunned && stunned) || (result[2] is bool silenced && silenced);
        }

        public static bool PushControl(string entityId, string state) => ChangeControl("PushControlState", entityId, state);
        public static bool PopControl(string entityId, string state) => ChangeControl("PopControlState", entityId, state);

        private static bool ChangeControl(string eventName, string entityId, string state)
        {
            if (string.IsNullOrEmpty(entityId) || string.IsNullOrEmpty(state) || !CanUseEvents) return false;
            return ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(eventName, new List<object> { entityId, state }));
        }

        public static bool TryGetSpeedMultiplier(string entityId, out float value)
        {
            value = 1f;
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return false;
            var result = EventProcessor.Instance.TriggerEventMethod("GetSpeedMultiplier", new List<object> { entityId });
            if (!ResultCode.IsOk(result) || result.Count < 2) return false;
            value = System.Convert.ToSingle(result[1]);
            return true;
        }

        public static bool SetSpeedMultiplier(string entityId, float value)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return false;
            return ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(
                "SetSpeedMultiplier", new List<object> { entityId, Mathf.Max(0f, value) }));
        }

        public static bool TryGetDamageReduction(string entityId, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return false;
            var result = EventProcessor.Instance.TriggerEventMethod("GetDamageReduction", new List<object> { entityId });
            if (!ResultCode.IsOk(result) || result.Count < 2) return false;
            value = System.Convert.ToSingle(result[1]);
            return true;
        }

        public static bool SetDamageReduction(string entityId, float value)
        {
            if (string.IsNullOrEmpty(entityId) || !CanUseEvents) return false;
            return ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(
                "SetDamageReduction", new List<object> { entityId, Mathf.Clamp01(value) }));
        }

        public static System.Action RegisterDamagedCallback(string entityId, System.Action<string, string, float, string> callback)
        {
            if (string.IsNullOrEmpty(entityId) || callback == null || !CanUseEvents) return null;
            var result = EventProcessor.Instance.TriggerEventMethod("RegisterDamagedCallback", new List<object> { entityId, callback });
            return ResultCode.IsOk(result) && result.Count > 1 ? result[1] as System.Action : null;
        }

        private static float ReadFloat(List<object> result, float fallback)
        {
            return ResultCode.IsOk(result) && result.Count > 1 ? System.Convert.ToSingle(result[1]) : fallback;
        }
    }
}
