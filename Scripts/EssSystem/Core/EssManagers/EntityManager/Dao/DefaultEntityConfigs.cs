using UnityEngine;
using EssSystem.EssManager.EntityManager.Dao.Config;

namespace EssSystem.EssManager.EntityManager.Dao
{
    /// <summary>
    /// 内置示例 Entity 配置 —— 仅用于调试与快速跑通。
    /// 业务层可用相同 <c>ConfigId</c> 通过 <c>EntityService.RegisterConfig</c> 覆盖。
    /// </summary>
    public static class DefaultEntityConfigs
    {
        // ───── Tree 常量 ──────────────────────────────────────────
        // 跨模块协议：CharacterManager 会注册如下 ID 的 CharacterConfig。
        // 二者必须保持同步。本文件不依赖 CharacterManager 的任何类型。
        // 对应 CharacterManager.Dao.DefaultTreeCharacterConfigs 中的 prefix 与 variant 数量。
        private const string SmallTreeCharIdPrefix  = "SmallTreeChar_";
        private const string MediumTreeCharIdPrefix = "MediumTreeChar_";
        private const int    TreeVariantCount       = 4;

        public const string SmallTreeEntityId  = "SmallTreeEntity";
        public const string MediumTreeEntityId = "MediumTreeEntity";

        /// <summary>动态示例：用 CharacterManager 默认的 <c>Warrior</c>（字符串协议）作为显示。</summary>
        public static EntityConfig BuildWarriorEntity() =>
            new EntityConfig("WarriorEntity", "战士 Entity",
                characterConfigId: "Warrior",   // 与 CharacterManager DefaultCharacterConfigs.WarriorId 对齐
                kind: EntityKind.Dynamic);

        /// <summary>动态示例：用 CharacterManager 默认的 <c>Mage</c>（字符串协议）作为显示。</summary>
        public static EntityConfig BuildMageEntity() =>
            new EntityConfig("MageEntity", "法师 Entity",
                characterConfigId: "Mage",
                kind: EntityKind.Dynamic);

        /// <summary>
        /// 静态示例：小树 Entity（<c>Tree_small_1..4</c> 随机 + 1×1 格碰撞体）。
        /// </summary>
        public static EntityConfig BuildSmallTreeEntity()
        {
            return new EntityConfig(
                configId: SmallTreeEntityId,
                displayName: "小树",
                characterConfigId: null,
                kind: EntityKind.Static
            )
            {
                CharacterConfigVariants = BuildVariantIds(SmallTreeCharIdPrefix, TreeVariantCount),
                Collider = EntityColliderConfig.OneCellBox(), // 一格方块碰撞
                SpawnOffset = Vector3.up,                      // 上移一格
            };
        }

        /// <summary>
        /// 静态示例：中型树 Entity（<c>Tree_medium_1..4</c> 随机 + 1×1 格碰撞体）。
        /// </summary>
        public static EntityConfig BuildMediumTreeEntity()
        {
            return new EntityConfig(
                configId: MediumTreeEntityId,
                displayName: "中型树",
                characterConfigId: null,
                kind: EntityKind.Static
            )
            {
                CharacterConfigVariants = BuildVariantIds(MediumTreeCharIdPrefix, TreeVariantCount),
                Collider = EntityColliderConfig.OneCellBox(),
                SpawnOffset = Vector3.up,
            };
        }

        // ───── Helpers ──────────────────────────────────

        private static string[] BuildVariantIds(string prefix, int count)
        {
            var arr = new string[count];
            for (var i = 0; i < count; i++) arr[i] = prefix + (i + 1);
            return arr;
        }
    }
}
