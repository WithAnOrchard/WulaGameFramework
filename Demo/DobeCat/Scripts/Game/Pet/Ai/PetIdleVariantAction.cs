using Demo.DobeCat.Game;
using Demo.DobeCat.Game.Pet;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet.Ai
{
    /// <summary>
    /// Idle-variant action — shows a random phrase in the speech bubble then
    /// immediately returns Success so Brain can reassign.
    /// Also reduces Boredom slightly on activation.
    /// </summary>
    public class PetIdleVariantAction : IBrainAction
    {
        public float BoredomReduction = 0.1f;

        public void OnEnter(BrainContext ctx)
        {
            var text = DobeCatDialogueContent.Pick(DobeCatDialogueContent.IDLE) ?? "喵～";
            PetSpeechBubble.Instance?.Show(text, 3f);
            ctx.Self?.Get<INeeds>()?.Add("Boredom", -BoredomReduction);
        }

        public BrainStatus Tick(BrainContext ctx, float dt) => BrainStatus.Success;

        public void OnExit(BrainContext ctx) { }
    }
}
