using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.Dao.Skills;
using EssSystem.Core.Presentation.CharacterManager;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace Demo.Tribe.Player
{
    [DisallowMultipleComponent]
    public class TribePlayerSkills : MonoBehaviour
    {
        private const int SkillSlotCount = 4;

        private string _entityId;
        private bool _initialized;

        public bool IsInitialized => _initialized;

        public void Initialize(string entityId)
        {
            _entityId = entityId;
            EnsureDefaultSkills();
        }

        public void Tick()
        {
            if (!_initialized)
                EnsureDefaultSkills();
        }

        public bool EnsureDefaultSkills(string entityId = null)
        {
            if (!string.IsNullOrEmpty(entityId))
                _entityId = entityId;

            if (string.IsNullOrEmpty(_entityId) || !SkillService.HasInstance)
                return false;

            TribeSkillEffectCharacterConfigs.EnsureRegistered();

            var service = SkillService.Instance;
            service.RegisterDefinition(CommonSkills.BuildFireball());

            var slots = service.GetSlots(_entityId);
            if (slots == null || slots.Length != SkillSlotCount)
                service.InitSlots(_entityId, SkillSlotCount);

            var fireball = service.LearnSkill(_entityId, CommonSkills.SKILL_FIREBALL);
            if (fireball == null)
                return false;
            fireball.Definition = service.GetDefinition(CommonSkills.SKILL_FIREBALL);

            slots = service.GetSlots(_entityId);
            if (slots == null || slots.Length <= 0)
                return false;

            if (slots[0].Skill == null || slots[0].Skill.SkillId != CommonSkills.SKILL_FIREBALL)
                service.BindSlot(_entityId, 0, CommonSkills.SKILL_FIREBALL);

            _initialized = true;
            return true;
        }
    }

    internal static class TribeSkillEffectCharacterConfigs
    {
        public const string FireballImpactConfigId = "TribeFireballImpact";

        private static bool _registered;

        public static void EnsureRegistered()
        {
            if (_registered || CharacterService.Instance == null) return;
            CharacterService.Instance.RegisterConfig(BuildFireballImpactConfig());
            _registered = true;
        }

        private static CharacterConfig BuildFireballImpactConfig()
        {
            var impact = new CharacterActionConfig("Special")
                .WithSprites(
                    "Tribe/Common/Effects/MiniEffect2D/Effect13_0",
                    "Tribe/Common/Effects/MiniEffect2D/Effect13_1",
                    "Tribe/Common/Effects/MiniEffect2D/Effect13_2",
                    "Tribe/Common/Effects/MiniEffect2D/Effect13_3")
                .WithFrameRate(12f)
                .WithLoop(false);

            var body = new CharacterPartConfig("Body", CharacterPartType.Dynamic)
                .WithDynamic("Special", impact)
                .WithSortingOrder(280);

            return new CharacterConfig(FireballImpactConfigId, "Fireball Impact")
                .WithRootScale(Vector3.one)
                .WithRenderMode(CharacterRenderMode.Sprite2DAnimator)
                .WithPart(body);
        }
    }
}
