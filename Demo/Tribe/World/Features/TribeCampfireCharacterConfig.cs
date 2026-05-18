using UnityEngine;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 营火 CharacterConfig —— 单 Body 部件 + Idle 循环动作 (8 帧 spritesheet)。
    /// <para>动画驱动权交给 <see cref="CharacterPartView2DAnimator"/>，业务侧不再自行实现 frame swap；
    /// 帧 sprite 通过 ResourceManager 子图名兜底（<c>campfire_0..7</c>）按需加载。</para>
    /// <para>素材：<c>Resources/Tribe/Objects/campfire.png</c>，16x32 px @ PPU 100，
    /// 8 帧水平横向切片，每帧 pivot=(0,0) 左下角。</para>
    /// </summary>
    public static class TribeCampfireCharacterConfig
    {
        /// <summary>对外 ConfigId（跨模块字符串协议）。</summary>
        public const string ConfigId = "TribeCampfire";

        /// <summary>Idle 动作的帧率（fps）—— 与旧 <c>TribeCampfire._frameRate</c> 默认 8 对齐。</summary>
        public const float IdleFrameRate = 8f;

        private static bool _registered;

        /// <summary>
        /// 幂等注册：首次调用写入 CharacterService，重复调用直接返回。
        /// 业务侧（如 <see cref="CampFeature"/>）在 Build 时调用即可。
        /// </summary>
        public static void EnsureRegistered()
        {
            if (_registered) return;
            if (CharacterService.Instance == null) return;
            CharacterService.Instance.RegisterConfig(BuildConfig());
            _registered = true;
        }

        /// <summary>构造 CharacterConfig：1 个 Body（Dynamic） + 1 个 Idle 动作（loop=true）。</summary>
        public static CharacterConfig BuildConfig()
        {
            var idle = new CharacterActionConfig("Idle")
                .WithSprites("campfire_0", "campfire_1", "campfire_2", "campfire_3",
                             "campfire_4", "campfire_5", "campfire_6", "campfire_7")
                .WithFrameRate(IdleFrameRate)
                .WithLoop(true);

            var body = new CharacterPartConfig("Body", CharacterPartType.Dynamic)
                .WithDynamic("Idle", idle)
                .WithSortingOrder(0);

            // RootScale 由调用方按 CampfireScale 在创建后通过 SetScale 调整 —— 这里保持 1
            return new CharacterConfig(ConfigId, "营火")
                .WithRootScale(Vector3.one)
                .WithRenderMode(CharacterRenderMode.Sprite2DAnimator)
                .WithPart(body);
        }
    }
}
