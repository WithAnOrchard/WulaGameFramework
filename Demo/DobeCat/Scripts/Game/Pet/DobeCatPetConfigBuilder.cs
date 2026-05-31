using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 将随机化变体转换为 CharacterConfig 的构建器。
    /// </summary>
    public static class DobeCatPetConfigBuilder
    {
        /// <summary>
        /// 动作元数据：名称 / 帧数 / FPS / 是否循环。
        /// 与 DefaultCharacterConfigs 保持一致。
        /// </summary>
        private struct ActionDef
        {
            public string Name;
            public int    FrameCount;
            public float  Fps;
            public bool   Loop;
        }

        /// <summary>
        /// 标准动作定义列表。
        /// </summary>
        private static readonly ActionDef[] StandardActions =
        {
            new ActionDef { Name = "Walk",    FrameCount = 6, Fps = 12f, Loop = true  },
            new ActionDef { Name = "Idle",    FrameCount = 4, Fps = 8f,  Loop = true  },
            new ActionDef { Name = "Jump",    FrameCount = 3, Fps = 10f, Loop = false },
            new ActionDef { Name = "Attack",  FrameCount = 4, Fps = 14f, Loop = false },
            new ActionDef { Name = "Defend",  FrameCount = 4, Fps = 10f, Loop = true  },
            new ActionDef { Name = "Damage",  FrameCount = 3, Fps = 12f, Loop = false },
            new ActionDef { Name = "Death",   FrameCount = 5, Fps = 8f,  Loop = false },
            new ActionDef { Name = "Special", FrameCount = 6, Fps = 12f, Loop = false },
        };

        private const string DefaultAction = "Idle";

        /// <summary>
        /// 根据选中的变体构建 CharacterConfig。
        /// </summary>
        public static CharacterConfig BuildConfig(string configId, Dictionary<string, PetPartVariant> selectedVariants)
        {
            if (string.IsNullOrEmpty(configId))
                configId = $"DobeCatRandom_{System.DateTime.Now.Ticks}";

            var config = new CharacterConfig(configId, "随机桌宠");
            config.WithRootScale(Vector3.one);
            config.WithRenderMode(CharacterRenderMode.Sprite2DAnimator);

            foreach (var kvp in selectedVariants)
            {
                var partId = kvp.Key;
                var variant = kvp.Value;

                var partConfig = CreatePartConfig(partId, variant);
                if (partConfig != null)
                    config.WithPart(partConfig);
            }

            return config;
        }

        /// <summary>
        /// 为单个部件创建 CharacterPartConfig。
        /// </summary>
        private static CharacterPartConfig CreatePartConfig(string partId, PetPartVariant variant)
        {
            if (string.IsNullOrEmpty(variant?.SheetPrefix))
            {
                Debug.LogWarning($"[DobeCatPetConfigBuilder] 变体 SheetPrefix 为空: PartId={partId}");
                return null;
            }

            var part = new CharacterPartConfig(partId, CharacterPartType.Dynamic);

            // 获取该部件的 SortingOrder
            var partRandomConfig = DobeCatPetRandomizer.PartConfigs
                .FirstOrDefault(p => p.PartId == partId);
            int sortingOrder = partRandomConfig?.SortingOrder ?? 0;
            part.WithSortingOrder(sortingOrder);

            // 根据 SheetPrefix 生成所有标准动作的 SpriteIds
            var actionConfigs = new List<CharacterActionConfig>();

            foreach (var actionDef in StandardActions)
            {
                var spriteIds = new List<string>();
                for (int f = 0; f < actionDef.FrameCount; f++)
                {
                    // Sprite ID 格式: {SheetPrefix}_{ActionName}_{frameIndex}
                    // 例如: Skin_warrior_1_Idle_0
                    var spriteId = $"{variant.SheetPrefix}_{actionDef.Name}_{f}";
                    spriteIds.Add(spriteId);
                }

                var actionConfig = new CharacterActionConfig(actionDef.Name);
                actionConfig.SpriteIds = spriteIds;
                actionConfig.FrameRate = actionDef.Fps;
                actionConfig.Loop = actionDef.Loop;
                
                actionConfigs.Add(actionConfig);
            }

            // 设置默认动作
            part.DefaultActionName = DefaultAction;
            part.Animations = actionConfigs;

            // 应用颜色偏移
            if (variant.ColorShift != Vector3.zero)
            {
                float h = variant.ColorShift.x / 360f;
                float s = variant.ColorShift.y;
                float v = variant.ColorShift.z;

                Color.RGBToHSV(Color.white, out float baseH, out float baseS, out float baseV);
                var shiftColor = Color.HSVToRGB(
                    Mathf.Repeat(h + baseH, 1f),
                    Mathf.Clamp01(baseS + s),
                    Mathf.Clamp01(baseV + v)
                );
                part.WithColor(shiftColor);
            }

            return part;
        }

        /// <summary>
        /// 注册随机生成的配置到 CharacterService。
        /// </summary>
        public static string RegisterRandomConfig(CharacterService service, Dictionary<string, PetPartVariant> selectedVariants)
        {
            if (service == null)
            {
                Debug.LogError("[DobeCatPetConfigBuilder] CharacterService 为空");
                return null;
            }

            // 生成唯一的 ConfigId
            string configId = $"DobeCatRandom_{System.DateTime.Now.Ticks}";

            var config = BuildConfig(configId, selectedVariants);
            service.RegisterConfig(config);

            Debug.Log($"[DobeCatPetConfigBuilder] 已注册随机配置: {configId}");
            return configId;
        }
    }
}
