using UnityEngine;

namespace Demo.DobeCat.Pet
{
    /// <summary>
    /// M1 临时 wander 控制器（M2 替换为 EntityManager Brain）。
    /// <para>状态机：Idle ↔ Walk。每 N 秒选一个屏幕内随机目标点，朝目标移动；到达后切 Idle。</para>
    /// </summary>
    public class PetWander : MonoBehaviour
    {
        [Tooltip("移动速度（世界单位/秒）。")]
        public float MoveSpeed = 1.5f;

        [Tooltip("Idle 最短停留秒数。")]
        public float IdleMin = 1.5f;
        [Tooltip("Idle 最长停留秒数。")]
        public float IdleMax = 4f;

        [Tooltip("Walk 最长持续秒数（防止永远朝一个方向走）。")]
        public float WalkMax = 6f;

        [Tooltip("到达目标的距离阈值。")]
        public float ArriveDistance = 0.05f;

        public PetView View;

        private enum State { Idle, Walk }
        private State _state;
        private float _stateTimer;
        private Vector3 _target;

        public bool Paused { get; set; }

        private void Start()
        {
            EnterIdle();
        }

        private void Update()
        {
            if (Paused) return;
            _stateTimer -= Time.deltaTime;
            if (_state == State.Idle)
            {
                if (_stateTimer <= 0f) EnterWalk();
                return;
            }

            // Walk
            var pos = transform.position;
            var dir = _target - pos;
            dir.z = 0f;
            var dist = dir.magnitude;
            if (dist <= ArriveDistance || _stateTimer <= 0f)
            {
                EnterIdle();
                return;
            }
            var step = MoveSpeed * Time.deltaTime;
            transform.position = pos + (Vector3)(dir.normalized * Mathf.Min(step, dist));
            if (View != null) View.SetFacing(dir.x >= 0f ? 1 : -1);
        }

        private void EnterIdle()
        {
            _state = State.Idle;
            _stateTimer = Random.Range(IdleMin, IdleMax);
        }

        private void EnterWalk()
        {
            _state = State.Walk;
            _stateTimer = WalkMax;
            _target = PickRandomScreenTarget();
        }

        /// <summary>在主相机视口内挑一个随机点（保留 10% 边距）。</summary>
        private Vector3 PickRandomScreenTarget()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                return transform.position + new Vector3(Random.Range(-3f, 3f), Random.Range(-1f, 1f), 0f);
            }
            var vp = new Vector3(Random.Range(0.1f, 0.9f), Random.Range(0.1f, 0.9f), 0f);
            var z = Mathf.Abs(cam.transform.position.z);
            var world = cam.ViewportToWorldPoint(new Vector3(vp.x, vp.y, z));
            world.z = 0f;
            return world;
        }
    }
}
