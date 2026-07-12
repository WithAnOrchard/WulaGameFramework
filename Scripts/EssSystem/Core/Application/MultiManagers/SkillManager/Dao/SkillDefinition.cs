using System;
using System.Collections.Generic;

namespace EssSystem.Core.Application.MultiManagers.SkillManager.Dao
{
    /// <summary>
    /// 技能静态定义 —— 纯数据，描述一个技能的所有参数。
    /// <para>可由配置表 / ScriptableObject / 代码直接构造。</para>
    /// </summary>
    [Serializable]
    public class SkillDefinition
    {
        // ─── 标识 ─────────────────────────────────────────────
        /// <summary>唯一 ID（如 "fireball", "heal_wave"）。</summary>
        public string Id;

        /// <summary>显示名称。</summary>
        public string DisplayName;

        /// <summary>技能描述（UI 提示）。</summary>
        public string Description;

        /// <summary>图标路径（Resources 下）。</summary>
        public string IconPath;

        /// <summary>技能大类，用于 UI 过滤和模板库整理。</summary>
        public string Category;

        /// <summary>制作状态，例如 Complete / Unfinished / Draft。</summary>
        public string SkillStatus;

        // ─── 消耗 ─────────────────────────────────────────────
        /// <summary>MP 消耗。</summary>
        public float ManaCost;

        /// <summary>HP 消耗（部分技能以血量为代价）。</summary>
        public float HpCost;

        // ─── 时间 ─────────────────────────────────────────────
        /// <summary>冷却时间（秒）。</summary>
        public float Cooldown = 1f;

        /// <summary>前摇时间（秒）—— 施法动作播放，期间可被打断。</summary>
        public float CastTime;

        /// <summary>后摇时间（秒）—— 技能释放后的硬直。</summary>
        public float RecoveryTime;

        // ─── 引导施法（Channeling）──────────────────────────
        /// <summary>引导时间（秒）；&gt; 0 时 <see cref="SkillExecutor"/> 在 Execute 后进入 Channeling 阶段，
        /// 每 <see cref="ChannelTickInterval"/> 重新执行 <see cref="Effects"/>，直到时长耗尽或被 Interrupt。</summary>
        public float ChannelTime;

        /// <summary>引导期间效果重复触发的间隔（秒）。&lt;= 0 时退化为 Execute 一次后等满 <see cref="ChannelTime"/>。</summary>
        public float ChannelTickInterval = 0.5f;

        // ─── 目标 ─────────────────────────────────────────────
        /// <summary>目标模式。</summary>
        public SkillTargetMode TargetMode = SkillTargetMode.None;

        /// <summary>技能施放距离（用于 Targeted / Directional）。</summary>
        public float Range = 5f;

        // ─── 效果链 ───────────────────────────────────────────
        /// <summary>
        /// 技能效果列表 —— 按顺序执行。
        /// <para>运行时由 <see cref="SkillExecutor"/> 在命中阶段逐一 Apply。</para>
        /// </summary>
        public List<ISkillEffect> Effects = new();

        // ─── 等级 ─────────────────────────────────────────────
        /// <summary>最大等级（1 = 不可升级）。</summary>
        public int MaxLevel = 1;
    }

    /// <summary>技能目标模式。</summary>
    public enum SkillTargetMode
    {
        /// <summary>无目标（自身 buff / 立即生效）。</summary>
        None,

        /// <summary>指向性 —— 选定一个实体。</summary>
        Targeted,

        /// <summary>方向性 —— 朝面朝方向释放。</summary>
        Directional,

        /// <summary>范围指定 —— 选定一个位置（AOE 中心）。</summary>
        PointTarget,
    }
}
