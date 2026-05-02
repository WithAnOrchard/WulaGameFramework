using UnityEngine;

namespace EssSystem.EssManager.CharacterManager.Dao
{
    /// <summary>
    /// 内置默认 <see cref="CharacterConfig"/> 构造器（Warrior / Mage）。
    /// <para>每个部件均为 <see cref="CharacterPartType.Dynamic"/>，含 8 个动作：
    /// Walk / Idle / Jump / Attack / Defend / Damage / Death / Special。</para>
    /// <para>SpriteIds 引用的是 <see cref="EssSystem.EssManager.CharacterManager"/> 的
    /// <c>Tools/Character/Slice Sprite Sheets (8x6)</c> 工具切出的子 Sprite，
    /// 命名规则 <c>{sheetPrefix}_{Action}_{frameIndex}</c>。
    /// 业务侧使用前**必须先跑过一次切片菜单**，否则 Sprite 加载失败。</para>
    /// </summary>
    public static class DefaultCharacterConfigs
    {
        public const string WarriorId = "Warrior";
        public const string MageId    = "Mage";

        #region Action Layout (与 CharacterSpriteSheetSlicer 保持一致)

        /// <summary>动作元数据：名称 / 帧数 / FPS / 是否循环。</summary>
        private struct ActionDef
        {
            public string Name;
            public int    FrameCount;
            public float  Fps;
            public bool   Loop;
        }

        private static readonly ActionDef[] Actions =
        {
            new ActionDef { Name = "Walk",    FrameCount = 6, Fps = 12f, Loop = true },
            new ActionDef { Name = "Idle",    FrameCount = 4, Fps = 8f,  Loop = true },
            new ActionDef { Name = "Jump",    FrameCount = 3, Fps = 10f, Loop = true },
            new ActionDef { Name = "Attack",  FrameCount = 4, Fps = 14f, Loop = true },
            new ActionDef { Name = "Defend",  FrameCount = 4, Fps = 10f, Loop = true },
            new ActionDef { Name = "Damage",  FrameCount = 3, Fps = 12f, Loop = true },
            new ActionDef { Name = "Death",   FrameCount = 5, Fps = 8f,  Loop = true },
            new ActionDef { Name = "Special", FrameCount = 6, Fps = 12f, Loop = true },
        };

        public const string DefaultAction = "Idle";

        #endregion

        #region Builders

        /// <summary>战士默认 Model：铠甲 + 头盔 + 剑 + 盾。</summary>
        public static CharacterConfig BuildWarrior() =>
            new CharacterConfig(WarriorId, "战士")
                .WithRootScale(Vector3.one)
                .WithPart(MakeAnimatedPart("Skin",   "Skin_warrior_1",            0))
                .WithPart(MakeAnimatedPart("Cloth",  "Cloth_warrior_red",         1))
                .WithPart(MakeAnimatedPart("Eyes",   "Eyes_blue",                 2))
                .WithPart(MakeAnimatedPart("Hair",   "Hair_1_1_brown",            3))
                .WithPart(MakeAnimatedPart("Head",   "Headgear_Helmet_Close_1",   4))
                .WithPart(MakeAnimatedPart("Weapon", "Weapon_Sword_1",            5))
                .WithPart(MakeAnimatedPart("Shield", "Equipment_Shield_01",       6));

        /// <summary>法师默认 Model：长袍 + 巫师帽 + 法杖。</summary>
        public static CharacterConfig BuildMage() =>
            new CharacterConfig(MageId, "法师")
                .WithRootScale(Vector3.one)
                .WithPart(MakeAnimatedPart("Skin",   "Skin_mage_1",                  0))
                .WithPart(MakeAnimatedPart("Cloth",  "Cloth_mage_purple",            1))
                .WithPart(MakeAnimatedPart("Eyes",   "Eyes_blue",                    2))
                .WithPart(MakeAnimatedPart("Hair",   "Hair_2_2_brown",               3))
                .WithPart(MakeAnimatedPart("Head",   "Headgear_WitchHat_1_purple",   4))
                .WithPart(MakeAnimatedPart("Weapon", "Weapon_Rod_1",                 5));

        #endregion

        #region Helpers

        /// <summary>
        /// 用给定的 sheet 前缀生成一个 Dynamic 部件，含 8 个标准动作。
        /// 暴露为 public 便于 <c>CharacterPreviewPanel</c> 在切换变体时重建。
        /// </summary>
        public static CharacterPartConfig MakeAnimatedPart(string partId, string sheetPrefix, int sortingOrder)
        {
            var actions = new CharacterActionConfig[Actions.Length];
            for (var i = 0; i < Actions.Length; i++)
            {
                var def = Actions[i];
                var ids = new string[def.FrameCount];
                for (var f = 0; f < def.FrameCount; f++)
                    ids[f] = $"{sheetPrefix}_{def.Name}_{f}";
                actions[i] = new CharacterActionConfig(def.Name)
                    .WithSprites(ids)
                    .WithFrameRate(def.Fps)
                    .WithLoop(def.Loop);
            }

            return new CharacterPartConfig(partId, CharacterPartType.Dynamic)
                .WithDynamic(DefaultAction, actions)
                .WithSortingOrder(sortingOrder);
        }

        /// <summary>枚举所有标准动作名称（与切片工具的行顺序一致）。</summary>
        public static string[] GetAllActionNames()
        {
            var arr = new string[Actions.Length];
            for (var i = 0; i < Actions.Length; i++) arr[i] = Actions[i].Name;
            return arr;
        }

        #endregion
    }
}
