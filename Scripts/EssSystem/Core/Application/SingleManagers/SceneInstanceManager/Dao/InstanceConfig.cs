using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.SingleManagers.SceneInstanceManager.Dao
{
    /// <summary>
    /// 子场景配置 —— 描述一个 Instance 的 ID / 主题 / 空间偏移 / 入口 / 出口 / 规则。
    /// <para>
    /// 多人架构（设计 #3）：所有 Instance 与 OverWorld 共存于同一 Unity Scene，
    /// 通过 <see cref="OriginOffset"/> 的巨大坐标偏移彼此隔离。玩家"进入"= 瞬移到
    /// <c>OriginOffset + EntryPosition</c>。
    /// </para>
    /// </summary>
    [Serializable]
    public class InstanceConfig
    {
        /// <summary>唯一 Id。</summary>
        public string Id;

        /// <summary>显示名（UI 提示用）。</summary>
        public string DisplayName;

        /// <summary>主题分类。</summary>
        public InstanceTheme Theme;

        /// <summary>
        /// Instance 在世界中的偏移基点（相对世界原点）。
        /// 推荐 Instance 之间 ≥ 20000 单位间距，避免相机/物理穿越。
        /// </summary>
        public Vector2 OriginOffset;

        /// <summary>玩家进入后落点（相对 <see cref="OriginOffset"/>）。</summary>
        public Vector2 EntryPosition;

        /// <summary>规则（禁敌人 / HP 回复 / 时间锁定 / 休眠阈值）。</summary>
        public InstanceRules Rules = new InstanceRules();

        /// <summary>出口列表（一般 1 个回 OverWorld，复杂副本可多门通向不同地点）。</summary>
        public List<PortalSpec> ExitPortals = new List<PortalSpec>();

        /// <summary>环境音 Id（接 AudioManager）。</summary>
        public string AmbientSoundId;

        /// <summary>用哪个 Builder 构造场景内容（业务侧约定的标识）。</summary>
        public string InstanceBuilderId;
    }
}
