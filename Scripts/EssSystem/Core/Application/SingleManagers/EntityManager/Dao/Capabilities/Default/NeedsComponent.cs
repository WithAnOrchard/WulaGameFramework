using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="INeeds"/> 默认实现 —— 通用需求参数字典 + 可选自动增长。
    /// <para>
    /// 支持注册"增长速率"：如 <c>SetRate("Hunger", 0.01f)</c> → 每秒饥饿 +0.01。
    /// 增长在 <see cref="ITickableCapability.Tick"/> 中自动执行。
    /// </para>
    /// </summary>
    public class NeedsComponent : INeeds, ITickableCapability
    {
        private readonly Dictionary<string, float> _needs = new();
        private readonly Dictionary<string, float> _rates = new();
        private Entity _owner;

        public IReadOnlyDictionary<string, float> All => _needs;

        /// <summary>
        /// 构造并初始化需求。
        /// </summary>
        /// <param name="initialNeeds">初始需求名 → 初始值。</param>
        public NeedsComponent(params (string id, float initial, float ratePerSec)[] initialNeeds)
        {
            if (initialNeeds == null) return;
            foreach (var (id, initial, rate) in initialNeeds)
            {
                _needs[id] = Mathf.Clamp01(initial);
                if (rate != 0f) _rates[id] = rate;
            }
        }

        public float Get(string needId)
        {
            if (string.IsNullOrEmpty(needId)) return 0f;
            return _needs.TryGetValue(needId, out var v) ? v : 0f;
        }

        public void Set(string needId, float value)
        {
            if (string.IsNullOrEmpty(needId)) return;
            _needs[needId] = Mathf.Clamp01(value);
        }

        public void Add(string needId, float delta)
        {
            if (string.IsNullOrEmpty(needId)) return;
            _needs.TryGetValue(needId, out var current);
            _needs[needId] = Mathf.Clamp01(current + delta);
        }

        /// <summary>设置需求的自动增长速率（每秒）。0 = 停止自动增长。</summary>
        public void SetRate(string needId, float ratePerSecond)
        {
            if (string.IsNullOrEmpty(needId)) return;
            if (ratePerSecond == 0f)
                _rates.Remove(needId);
            else
                _rates[needId] = ratePerSecond;
        }

        // ─── IEntityCapability ────────────────────────────────────
        public void OnAttach(Entity owner) => _owner = owner;
        public void OnDetach(Entity owner) => _owner = null;

        // ─── ITickableCapability ──────────────────────────────────
        public void Tick(float deltaTime)
        {
            if (_rates.Count == 0) return;
            foreach (var kv in _rates)
            {
                _needs.TryGetValue(kv.Key, out var current);
                _needs[kv.Key] = Mathf.Clamp01(current + kv.Value * deltaTime);
            }
        }
    }
}
