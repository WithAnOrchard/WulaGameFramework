using UnityEngine;
using EssSystem.Core.Application.MultiManagers.SkillManager;
using EssSystem.Core.Application.MultiManagers.SkillManager.UI;
using EssSystem.Core.Presentation.InputManager;

namespace Demo.Tribe.Player
{
    [DisallowMultipleComponent]
    public class TribePlayerSkills : MonoBehaviour
    {
        private const int SkillSlotCount = 4;
        private static readonly string[] DefaultSkillIds =
        {
            "common_fireball",
            "common_ice_shard",
            "common_thunder_spear",
            "common_arcane_bomb",
        };

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

            HandleSkillSelectionPanelInput();
            SkillSelectionPanel.Tick();
        }

        public bool EnsureDefaultSkills(string entityId = null)
        {
            if (!string.IsNullOrEmpty(entityId))
                _entityId = entityId;

            if (string.IsNullOrEmpty(_entityId) || !SkillService.HasInstance)
                return false;

            var service = SkillService.Instance;

            var slots = service.GetSlots(_entityId);
            if (slots == null || slots.Length != SkillSlotCount)
                service.InitSlots(_entityId, SkillSlotCount);

            for (var i = 0; i < DefaultSkillIds.Length; i++)
            {
                var skillId = DefaultSkillIds[i];
                var instance = service.LearnSkill(_entityId, skillId);
                if (instance == null)
                    return false;

                instance.Definition ??= service.GetDefinition(skillId);
                if (!service.BindSlot(_entityId, i, skillId))
                    return false;
            }

            _initialized = true;
            return true;
        }

        private void OnDestroy()
        {
            if (SkillSelectionPanel.IsOpen())
                SkillSelectionPanel.Close();
        }

        private void HandleSkillSelectionPanelInput()
        {
            var input = InputManager.TryGetInstance();
            if (input == null) return;

            if (input.IsDown(InputManager.ACTION_SKILL_SELECT_TOGGLE))
                SkillSelectionPanel.Toggle(_entityId);
        }
    }
}
