using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using Demo.DobeCat.Game.Pet;

namespace Demo.DobeCat.Game.Pet.Ai
{
    /// <summary>
    /// Pet looks toward the cursor when it is nearby (DESIGN.md §4.3 ReactToCursor).
    /// Faces the cursor direction and shows a curious bubble on enter.
    /// Exits when cursor moves out of reaction range.
    /// </summary>
    public class PetCursorReactAction : IBrainAction
    {
        private readonly float  _reactionRange;
        private readonly string _characterId;
        private float           _duration;

        private static readonly string[] CuriousPhrases =
        {
            "*歪头*", "嗯？", "喵～", "在看什么呀？",
        };

        public PetCursorReactAction(string characterId, float reactionRange = 2.5f)
        {
            _characterId   = characterId;
            _reactionRange = reactionRange;
        }

        public void OnEnter(BrainContext ctx)
        {
            _duration = 0f;
            if (Random.value < 0.4f)
                PetSpeechBubble.Instance?.Show(
                    CuriousPhrases[Random.Range(0, CuriousPhrases.Length)], 2f);
        }

        public BrainStatus Tick(BrainContext ctx, float deltaTime)
        {
            _duration += deltaTime;

            var cursorWorld = GetCursorWorld();
            var selfPos     = ctx.Self.WorldPosition;
            var dist        = Vector2.Distance(
                new Vector2(cursorWorld.x, cursorWorld.y),
                new Vector2(selfPos.x, selfPos.y));

            if (dist > _reactionRange * 1.3f || _duration > 5f)
                return BrainStatus.Success;

            // Face the cursor
            var right = cursorWorld.x >= selfPos.x;
            CharacterViewBridge.SetFacing(_characterId, right);

            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx) { }

        private static Vector3 GetCursorWorld()
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            Vector2 screenPos;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            screenPos = Demo.DobeCat.Sys.Platform.Windows.DesktopOverlay.GetGlobalCursorScreenPos();
#else
            screenPos = Input.mousePosition;
#endif
            var z = Mathf.Abs(cam.transform.position.z);
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        }
    }
}
