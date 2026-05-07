using System;
using UnityEngine;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config
{
    /// <summary>
    /// Entity 持久化配置 —— 描述一类 Entity 的静态属性（模板）。
    /// <para>
    /// <b>显示</b>：通过 <see cref="CharacterConfigId"/> 指向一份 <c>CharacterConfig</c>，
    /// <see cref="EntityService"/> 创建 Entity 时会调用 <c>CharacterService.CreateCharacter</c>
    /// 来生成实际显示用的 <c>Character</c>。
    /// </para>
    /// <para>
    /// <b>扩展</b>：后续各类玩法字段（速度 / 血量 / AI 类型 / 掉落表 …）直接在此类加字段 + 注释即可，
    /// 不需要改 Service/Manager 架构。
    /// </para>
    /// </summary>
    [Serializable]
    public class EntityConfig
    {
        /// <summary>模板唯一 ID（持久化主键）。</summary>
        public string ConfigId;

        /// <summary>可读名称（UI 展示、日志用）。</summary>
        public string DisplayName;

        /// <summary>
        /// 静态（植物 / 矿石 / 建筑）还是动态（动物 / 怪物 / NPC）。
        /// 决定是否参与移动 / AI Tick 等子系统。默认 <see cref="EntityKind.Dynamic"/>。
        /// </summary>
        public EntityKind Kind = EntityKind.Dynamic;

        /// <summary>
        /// 对应的 <c>CharacterConfig.ConfigId</c> —— 创建 Entity 时用它拉一个 Character 作为显示。
        /// 留空则 Entity 无可视部分（纯逻辑实体）。
        /// <para>若同时设置了 <see cref="CharacterConfigVariants"/> 并且非空，则会优先从 variants 里**随机挑一个**，
        /// 本字段仅作为 fallback。</para>
        /// </summary>
        public string CharacterConfigId;

        /// <summary>
        /// 可选：随机变体池。非空时 <see cref="EntityService.CreateEntity"/> 会从中随机取一个 <c>CharacterConfig.ConfigId</c>，
        /// 实现同一 EntityConfig 生成多种外观（例如"一棵树"随机四种贴图）。
        /// </summary>
        public string[] CharacterConfigVariants;

        /// <summary>
        /// 碰撞体配置 —— Shape = None 时不挂。
        /// 由 <see cref="EntityService.CreateEntity"/> 在 Character 根 GameObject 上 AddComponent。
        /// </summary>
        public EntityColliderConfig Collider = new EntityColliderConfig();

        /// <summary>
        /// 创建实体时对 Character 世界坐标施加的额外偏移（与 <c>CharacterConfig.RootScale</c> 无关的<b>世界空间</b>偏移）。
        /// 用于"视觉对齐 Tile 格子"等场景，例如树默认 <c>(0,1,0)</c> 让树根落在 Entity 的 <c>WorldPosition</c> 那一格上。
        /// </summary>
        public Vector3 SpawnOffset = Vector3.zero;

        // ─── 预留的常见玩法字段（后续补充用；现在仅占位，均可安全忽略）────────
        // public float MoveSpeed;
        // public float MaxHp;
        // public string AiProfileId;
        // public string[] LootTableIds;

        public EntityConfig() { }

        public EntityConfig(string configId, string displayName, string characterConfigId = null, EntityKind kind = EntityKind.Dynamic)
        {
            ConfigId = configId;
            DisplayName = displayName;
            CharacterConfigId = characterConfigId;
            Kind = kind;
        }
    }
}
