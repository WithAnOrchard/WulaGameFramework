using System.Collections.Generic;
using Demo.DobeCat.Window;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager;
using UnityEngine;

namespace Demo.DobeCat.Pet
{
    /// <summary>
    /// 桌宠 WASD 移动控制器。并存模式：
    /// <list type="bullet">
    /// <item><b>WASD 按下</b> → 接管移动，暂停 <see cref="PetWander"/>。</item>
    /// <item><b>WASD 松开</b> → 恢复 wander，角色自动随机游荡。</item>
    /// <item><b>ControlEnabled = false</b> → 完全禁用（托盘菜单切换用，不走这里）。</item>
    /// </list>
    /// </summary>
    public class PetWasdController : MonoBehaviour
    {
        [Tooltip("移动速度（世界单位/秒）。")]
        public float MoveSpeed = 4f;

        [Tooltip("启用 / 禁用 WASD 控制（由托盘菜单切换）。")]
        public bool ControlEnabled = false;

        public PetView View;
        public PetWander Wander;

        /// <summary>关联的 CharacterManager 实例 ID（留空 = 不联动 Locomotion 动作）。</summary>
        public string CharacterInstanceId = "";

        private bool _wasMoving;
        private int _lastFacing;

        public void SetEnabled(bool enabled)
        {
            ControlEnabled = enabled;
            if (!enabled && Wander != null) Wander.Paused = false; // 禁用后强制恢复 wander
        }

        private void Update()
        {
            if (!ControlEnabled) return;

            var win = DesktopWindow.Instance;
            var axis = win != null ? win.GetGlobalWasdAxis() : Vector2.zero;
            if (axis.sqrMagnitude > 1f) axis.Normalize();

            var moving = axis.sqrMagnitude > 1e-3f;

            // 并存调度：WASD 按下才暂停 wander；松开后让 wander 接管
            if (Wander != null && Wander.Paused != moving) Wander.Paused = moving;

            if (moving)
            {
                var delta = (Vector3)(axis * MoveSpeed * Time.deltaTime);
                transform.position += delta;
                if (Mathf.Abs(axis.x) > 0.01f)
                {
                    var face = axis.x > 0f ? 1 : -1;
                    if (face != _lastFacing) NotifyFacing(face);
                    _lastFacing = face;
                }
            }
            // locomotion 动画只在 WASD 接管期间主动驱动；松开时交由 wander 发 EVT_PLAY_LOCOMOTION(false)
            if (moving && !_wasMoving) NotifyLocomotion(true);
            else if (!moving && _wasMoving) NotifyLocomotion(false);
            _wasMoving = moving;
        }

        private void NotifyLocomotion(bool moving)
        {
            if (string.IsNullOrEmpty(CharacterInstanceId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(CharacterManager.EVT_PLAY_LOCOMOTION,
                new List<object> { CharacterInstanceId, moving, true });
        }

        private void NotifyFacing(int face)
        {
            if (string.IsNullOrEmpty(CharacterInstanceId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(CharacterManager.EVT_SET_FACING,
                new List<object> { CharacterInstanceId, face > 0 });
        }
    }
}
