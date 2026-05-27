using Demo.DobeCat.Game;
using Demo.DobeCat.Game.Pet;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain;
using EssSystem.Core.Application.SingleManagers.EntityManager.Brain.Actions;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;
using UnityEngine;

namespace Demo.DobeCat.Game.Pet.Ai
{
    /// <summary>
    /// Boredom > 0.6 时触发的玩耍行为：短距离快速冲刺 + 短暂停顿循环，模拟追逐虚空玩具。
    /// 每次激活时随机展示玩耍气泡，并持续消耗 Boredom 需求值。
    /// DESIGN.md §4.3 Play Consideration
    /// </summary>
    public class PetPlayAction : IBrainAction
    {
        private enum Phase { Dash, Pause }
        private Phase  _phase;
        private float  _timer;
        private Vector3 _target;
        private float  _speed;
        private bool   _shownBubble;

        public void OnEnter(BrainContext ctx)
        {
            _speed = (ctx.Self.Get<IMovable>()?.MoveSpeed ?? 2f) * 1.6f;
            _shownBubble = false;
            EnterDash(ctx);
        }

        public BrainStatus Tick(BrainContext ctx, float dt)
        {
            if (!_shownBubble)
            {
                var text = DobeCatDialogueContent.Pick(DobeCatDialogueContent.PLAY) ?? "喵～";
                PetSpeechBubble.Instance?.Show(text, 2.5f);
                _shownBubble = true;
            }

            // Drain Boredom while playing
            ctx.Self.Get<INeeds>()?.Add("Boredom", -dt * 0.0008f);

            if (_phase == Phase.Pause)
            {
                ctx.IsMoving = false;
                _timer -= dt;
                if (_timer <= 0f) EnterDash(ctx);
                return BrainStatus.Running;
            }

            // Dash phase — move toward target
            var pos  = ctx.Self.WorldPosition;
            var diff = _target - pos;
            diff.z = 0f;
            if (diff.magnitude <= 0.15f)
            {
                EnterPause();
                return BrainStatus.Running;
            }

            var dir = (Vector2)(diff / diff.magnitude);
            BrainMoveHelper.ApplyMove(ctx.Self, dir, _speed, dt);
            ctx.IsMoving = true;
            if (Mathf.Abs(diff.x) > 0.05f) ctx.FacingDirection = diff.x > 0f ? 1 : -1;
            return BrainStatus.Running;
        }

        public void OnExit(BrainContext ctx)
        {
            BrainMoveHelper.ApplyMove(ctx.Self, Vector2.zero, 0f, 0f);
            ctx.IsMoving = false;
        }

        private void EnterDash(BrainContext ctx)
        {
            _phase  = Phase.Dash;
            _target = PickNearbyTarget(ctx.Self.WorldPosition);
        }

        private void EnterPause()
        {
            _phase = Phase.Pause;
            _timer = Random.Range(0.2f, 0.7f);
        }

        private static Vector3 PickNearbyTarget(Vector3 from)
        {
            var cam = Camera.main;
            if (cam == null)
                return from + new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 1f), 0f);

            var z = Mathf.Abs(cam.transform.position.z);
            for (int i = 0; i < 8; i++)
            {
                var vp    = new Vector3(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), z);
                var world = cam.ViewportToWorldPoint(vp);
                world.z = 0f;
                if (Vector3.Distance(from, world) < 5f) return world;
            }
            var fallback = from + new Vector3(Random.Range(-3f, 3f), Random.Range(-1.5f, 1.5f), 0f);
            fallback.z = 0f;
            return fallback;
        }
    }
}
