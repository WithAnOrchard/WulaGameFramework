using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Runtime
{
    /// <summary>
    /// 连招追踪器 —— 按实体维护"最近 N 个技能 ID + 时间戳"的滚动缓冲；
    /// 当尾部序列匹配已注册的 <see cref="ComboDefinition.Sequence"/>（且全部命中均在 <see cref="ComboDefinition.WindowSeconds"/> 内），
    /// 自动触发 finisher 技能并清空该实体的缓冲。
    /// <para>
    /// 钩点：<c>SkillService.CastSkill</c> 在 Begin 成功后会调用 <see cref="OnSkillCast"/>。
    /// </para>
    /// <para>
    /// 全局静态：跨场景持久；如需热重载或测试隔离，调用 <see cref="Clear"/>。
    /// </para>
    /// </summary>
    public static class ComboTracker
    {
        /// <summary>一条连招定义。Sequence 顺序为发动顺序（最早 → 最新）。</summary>
        public class ComboDefinition
        {
            public string FinisherSkillId;
            public List<string> Sequence;
            /// <summary>整个连招完成的最大耗时（自第一个技能起算）。0 = 无限。</summary>
            public float WindowSeconds = 3f;
            /// <summary>触发后冷却（finisher 上的 CD 之外的"连招本身"冷却，秒）。</summary>
            public float ComboCooldown;
        }

        private static readonly List<ComboDefinition> _combos = new();

        // 每个实体一份滚动历史：item = (skillId, time)
        private static readonly Dictionary<string, List<(string id, float t)>> _history = new();
        private static readonly Dictionary<string, Dictionary<string, float>> _comboReady = new(); // entity → comboKey → 下次可触发时间

        // 缓冲上限：避免长跑游戏中无限增长
        private const int MaxHistory = 16;

        /// <summary>注册一个连招定义。重复注册不去重 —— 调用方自管理。</summary>
        public static void RegisterCombo(ComboDefinition combo)
        {
            if (combo == null || combo.Sequence == null || combo.Sequence.Count == 0) return;
            if (string.IsNullOrEmpty(combo.FinisherSkillId)) return;
            _combos.Add(combo);
        }

        /// <summary>清空所有注册的连招 + 所有实体历史（测试 / 场景切换用）。</summary>
        public static void Clear()
        {
            _combos.Clear();
            _history.Clear();
            _comboReady.Clear();
        }

        /// <summary>SkillService 钩点：每次成功 Cast 后调用，用于扫描连招匹配。</summary>
        public static void OnSkillCast(string casterId, string skillId)
        {
            if (string.IsNullOrEmpty(casterId) || string.IsNullOrEmpty(skillId)) return;
            if (_combos.Count == 0) return;

            var key = casterId;
            if (!_history.TryGetValue(key, out var hist))
            {
                hist = new List<(string, float)>();
                _history[key] = hist;
            }
            var now = Time.time;
            hist.Add((skillId, now));
            if (hist.Count > MaxHistory) hist.RemoveAt(0);

            // 检查所有连招
            for (var i = 0; i < _combos.Count; i++)
            {
                var combo = _combos[i];
                if (combo.Sequence.Count > hist.Count) continue;

                if (!MatchesTail(hist, combo.Sequence)) continue;

                // 时间窗校验：尾段 N 项的首尾时间差 ≤ WindowSeconds
                if (combo.WindowSeconds > 0f)
                {
                    var firstIdx = hist.Count - combo.Sequence.Count;
                    if (now - hist[firstIdx].t > combo.WindowSeconds) continue;
                }

                // 连招冷却校验
                if (!_comboReady.TryGetValue(key, out var perEntityCd))
                {
                    perEntityCd = new Dictionary<string, float>();
                    _comboReady[key] = perEntityCd;
                }
                var comboKey = combo.FinisherSkillId;
                if (perEntityCd.TryGetValue(comboKey, out var readyAt) && now < readyAt) continue;

                // 触发！
                hist.Clear();
                if (combo.ComboCooldown > 0f) perEntityCd[comboKey] = now + combo.ComboCooldown;

                if (SkillService.HasInstance)
                    SkillService.Instance.CastSkill(casterId, combo.FinisherSkillId);
                return; // 一次只触发一个连招
            }
        }

        // 判断 hist 末尾 seq.Count 项是否依次 == seq
        private static bool MatchesTail(List<(string id, float t)> hist, List<string> seq)
        {
            var offset = hist.Count - seq.Count;
            for (var i = 0; i < seq.Count; i++)
            {
                if (hist[offset + i].id != seq[i]) return false;
            }
            return true;
        }
    }
}
