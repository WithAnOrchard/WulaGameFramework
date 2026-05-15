using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Brain
{
    /// <summary>
    /// Brain 决策上下文（黑板）—— 每次 Tick 传递给 Score 函数和 Action。
    /// <para>
    /// 感知数据由 <see cref="ISensor"/> 周期性写入；需求参数从 <see cref="INeeds"/> 读取。
    /// 通用键值对存 <see cref="Memory"/>，供 Action 之间传递临时数据。
    /// </para>
    /// </summary>
    public class BrainContext
    {
        /// <summary>本实体引用。</summary>
        public Entity Self;

        // ─── 感知结果（由 Sensor 写入） ────────────────────────────
        /// <summary>感知范围内的其他实体（每次 Sensor 刷新时重写）。</summary>
        public readonly List<Entity> NearbyEntities = new();

        /// <summary>最近对我造成伤害的实体（由 Brain 监听 Damaged 事件写入）。</summary>
        public Entity ThreatSource;

        /// <summary>到威胁源的距离（感知刷新时计算）。</summary>
        public float DistanceToThreat;

        // ─── 运动状态（由 Action 写入，供 MonoBehaviour 动画层读取） ──
        /// <summary>当前朝向：+1 = 右，-1 = 左。由移动类 Action 每帧写入。</summary>
        public int FacingDirection = 1;

        /// <summary>当前是否在移动中。由移动类 Action 每帧写入。</summary>
        public bool IsMoving;

        /// <summary>当前是否处于奔跑状态（逃跑/冲刺等高速移动）。由移动类 Action 写入。</summary>
        public bool IsRunning;

        // ─── 通用黑板 ─────────────────────────────────────────────
        /// <summary>通用键值存储 —— Action 可以在此传递临时数据。</summary>
        public readonly Dictionary<string, object> Memory = new();

        // ─── 便捷读取 ─────────────────────────────────────────────

        /// <summary>从 <see cref="INeeds"/> 读取需求值（0~1），无 INeeds 返回 0。</summary>
        public float GetNeed(string needId)
        {
            var needs = Self?.Get<INeeds>();
            return needs?.Get(needId) ?? 0f;
        }

        /// <summary>HP 比例（0~1），无 <see cref="IDamageable"/> 返回 1。</summary>
        public float HpRatio
        {
            get
            {
                var dmg = Self?.Get<IDamageable>();
                if (dmg == null || dmg.MaxHp <= 0f) return 1f;
                return Mathf.Clamp01(dmg.CurrentHp / dmg.MaxHp);
            }
        }

        /// <summary>清空感知缓存（Sensor 刷新前调用）。</summary>
        public void ClearPerception()
        {
            NearbyEntities.Clear();
            DistanceToThreat = float.MaxValue;
        }
    }
}
