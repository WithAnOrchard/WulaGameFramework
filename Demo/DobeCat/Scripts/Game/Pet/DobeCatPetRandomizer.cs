using System;
using System.Collections.Generic;
using EssSystem.Core.Presentation.CharacterManager.Dao;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 桌宠部件变体配置 —— 一个部位可以有多个变体，随机选取。
    /// </summary>
    [Serializable]
    public class PetPartVariant
    {
        [Tooltip("变体ID（仅用于调试标识）")]
        public string VariantId;

        [Tooltip("精灵表前缀（对应 Resources.LoadAll 的路径前缀）")]
        public string SheetPrefix;

        [Tooltip("选择权重（越大越容易被选中）")]
        public float Weight = 1f;

        [Tooltip("颜色色调偏移（hsv格式：[h, s, v]，为空则不调整）")]
        public Vector3 ColorShift;
    }

    /// <summary>
    /// 单个部件的随机化配置。
    /// </summary>
    [Serializable]
    public class PetPartRandomConfig
    {
        [Tooltip("部件ID（对应 CharacterPartConfig.PartId）")]
        public string PartId;

        [Tooltip("Sorting Order（渲染优先级，数字越大越靠前）")]
        public int SortingOrder;

        [Tooltip("可选变体列表（至少需要一个）")]
        public List<PetPartVariant> Variants = new List<PetPartVariant>();

        [Tooltip("该部件是否启用随机化（false 则使用 Variants[0]）")]
        public bool EnableRandomization = true;
    }

    /// <summary>
    /// DobeCat 桌宠随机化器 —— 运行时随机选择部件变体，仅加载选中的素材。
    /// </summary>
    public static class DobeCatPetRandomizer
    {
        /// <summary>
        /// 随机化配置数据（可在 Inspector 配置或代码定义）。
        /// </summary>
        public static List<PetPartRandomConfig> PartConfigs { get; private set; } = new List<PetPartRandomConfig>();

        /// <summary>
        /// 当前选中的变体结果（PartId → 选中的 VariantId）
        /// </summary>
        public static Dictionary<string, string> SelectedVariants { get; } = new Dictionary<string, string>();

        /// <summary>
        /// 注册默认的 DobeCat 变体配置（示例）。
        /// 业务方可在 Start 前调用 AddVariants 扩展。
        /// </summary>
        public static void RegisterDefaultConfigs()
        {
            PartConfigs.Clear();

            // 皮肤变体 - 关键部件，必须有有效的变体
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Skin",
                SortingOrder = 0,
                EnableRandomization = true,
                Variants = BuildVariants("Skin",
                    CharacterVariantPools.GetVariants("Warrior", "Skin"),
                    CharacterVariantPools.GetVariants("Mage", "Skin"))
            });

            // 衣服变体
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Cloth",
                SortingOrder = 1,
                EnableRandomization = true,
                Variants = BuildVariants("Cloth",
                    CharacterVariantPools.GetVariants("Warrior", "Cloth"),
                    CharacterVariantPools.GetVariants("Mage", "Cloth"))
            });

            // 眼睛变体 - 关键部件，必须有有效的变体
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Eyes",
                SortingOrder = 2,
                EnableRandomization = true,
                Variants = BuildVariants("Eyes", CharacterVariantPools.GetVariants("Warrior", "Eyes"))
            });

            // 头发变体
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Hair",
                SortingOrder = 3,
                EnableRandomization = true,
                Variants = BuildVariants("Hair", CharacterVariantPools.GetVariants("Warrior", "Hair"))
            });

            // 头部装备变体 - 可选装饰，可以不显示
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Head",
                SortingOrder = 4,
                EnableRandomization = true,
                Variants = BuildVariants("Head",
                    CharacterVariantPools.GetVariants("Warrior", "Head"),
                    CharacterVariantPools.GetVariants("Mage", "Head"))
            });

            // 武器变体 - 可选装饰，可以不显示
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Weapon",
                SortingOrder = 5,
                EnableRandomization = true,
                Variants = BuildVariants("Weapon",
                    CharacterVariantPools.GetVariants("Warrior", "Weapon"),
                    CharacterVariantPools.GetVariants("Mage", "Weapon"))
            });

            // 盾牌变体
            AddPartConfig(new PetPartRandomConfig
            {
                PartId = "Shield",
                SortingOrder = 6,
                EnableRandomization = false, // 盾牌默认关闭随机化
                Variants = BuildVariants("Shield", CharacterVariantPools.GetVariants("Warrior", "Shield"))
            });

            Debug.Log($"[DobeCatPetRandomizer] 已注册 {PartConfigs.Count} 个部件的随机化配置");
        }

        private static List<PetPartVariant> BuildVariants(string partId, params List<string>[] groups)
        {
            var variants = new List<PetPartVariant>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group == null) continue;
                    foreach (var prefix in group)
                    {
                        if (string.IsNullOrEmpty(prefix) || !seen.Add(prefix)) continue;
                        variants.Add(new PetPartVariant
                        {
                            VariantId = $"{partId}_{prefix}",
                            SheetPrefix = prefix,
                            Weight = 1f
                        });
                    }
                }
            }

            if (variants.Count == 0)
                Debug.LogWarning($"[DobeCatPetRandomizer] 部件 {partId} 没有从 Resources 枚举到任何素材变体");
            return variants;
        }

        /// <summary>
        /// 添加或更新部件配置。
        /// </summary>
        public static void AddPartConfig(PetPartRandomConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.PartId)) return;

            var existing = PartConfigs.FindIndex(c => c.PartId == config.PartId);
            if (existing >= 0)
                PartConfigs[existing] = config;
            else
                PartConfigs.Add(config);
        }

        /// <summary>
        /// 随机选择所有部件变体，返回选中的配置字典。
        /// </summary>
        public static Dictionary<string, PetPartVariant> Randomize()
        {
            SelectedVariants.Clear();
            var result = new Dictionary<string, PetPartVariant>();

            foreach (var partConfig in PartConfigs)
            {
                if (partConfig.Variants == null || partConfig.Variants.Count == 0)
                {
                    Debug.LogWarning($"[DobeCatPetRandomizer] 部件 {partConfig.PartId} 没有变体配置");
                    continue;
                }

                PetPartVariant selected;

                if (!partConfig.EnableRandomization || partConfig.Variants.Count == 1)
                {
                    // 不启用随机化或只有一个变体，直接选第一个
                    selected = partConfig.Variants[0];
                }
                else
                {
                    // 按权重随机选择
                    selected = PickWeightedRandom(partConfig.Variants);
                }

                if (selected != null)
                {
                    result[partConfig.PartId] = selected;
                    SelectedVariants[partConfig.PartId] = selected.VariantId;
                    Debug.Log($"[DobeCatPetRandomizer] 部件 {partConfig.PartId} 选中变体: {selected.VariantId} ({selected.SheetPrefix})");
                }
            }

            return result;
        }

        /// <summary>
        /// 根据权重随机选择。
        /// </summary>
        private static PetPartVariant PickWeightedRandom(List<PetPartVariant> variants)
        {
            if (variants == null || variants.Count == 0) return null;

            float totalWeight = 0f;
            foreach (var v in variants)
                totalWeight += Mathf.Max(0f, v.Weight);

            if (totalWeight <= 0f)
                return variants[UnityEngine.Random.Range(0, variants.Count)];

            float randomValue = UnityEngine.Random.Range(0f, totalWeight);
            float currentWeight = 0f;

            foreach (var v in variants)
            {
                currentWeight += v.Weight;
                if (randomValue <= currentWeight)
                    return v;
            }

            return variants[variants.Count - 1];
        }

        /// <summary>
        /// 获取当前选中的变体信息。
        /// </summary>
        public static string GetSelectedVariantInfo()
        {
            var lines = new List<string>();
            foreach (var kv in SelectedVariants)
                lines.Add($"  {kv.Key}: {kv.Value}");
            return string.Join("\n", lines);
        }
    }
}
