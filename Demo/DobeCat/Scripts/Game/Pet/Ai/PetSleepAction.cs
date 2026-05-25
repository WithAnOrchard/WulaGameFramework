using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace Demo.DobeCat.Game.Pet.Ai
{
    /// <summary>
    /// Sleep action — pet stops moving and slowly recovers Energy (INeeds).
    /// Exits when Energy reaches WakeThreshold.
    /// </summary>
    public class PetSleepAction : IBrainAction
    {
        public float EnergyRecoveryPerSec = 0.001f;
        public float WakeThreshold        = 0.9f;

        private bool _bubbleShown;

        public void OnEnter(BrainContext ctx)
        {
            _bubbleShown  = false;
            ctx.IsMoving  = false;
        }

        public BrainStatus Tick(BrainContext ctx, float dt)
        {
            ctx.IsMoving = false;

            if (!_bubbleShown && PetSpeechBubble.Instance != null)
            {
                PetSpeechBubble.Instance.Show("zzz...", 5f);
                _bubbleShown = true;
            }

            var needs = ctx.Self?.Get<INeeds>();
            if (needs == null) return BrainStatus.Success;

            needs.Add("Energy", EnergyRecoveryPerSec * dt);

            if (needs.Get("Energy") >= WakeThreshold)
                return BrainStatus.Success;

            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            ctx.IsMoving = false;
        }
    }
}
