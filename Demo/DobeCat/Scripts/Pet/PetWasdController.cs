using System.Collections.Generic;
using Demo.DobeCat.Window;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager;
using UnityEngine;

namespace Demo.DobeCat.Pet
{
    /// <summary>
    /// 桌宠 WASD 移动控制器（可由托盘菜单切换）。
    /// <list type="bullet">
    /// <item>开启时暂停 <see cref="PetWander"/>，根据 <see cref="DesktopWindow.GetGlobalWasdAxis"/> 直接移动 transform。</item>
    /// <item>关闭时恢复 wander。</item>
    /// <item>朝向变化通过 <see cref="PetView.SetFacing"/> + CharacterManager.EVT_SET_FACING 同步给角色。</item>
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
            if (Wander != null) Wander.Paused = enabled; // 开启 WASD 时暂停 wander
            if (!enabled) NotifyLocomotion(false);
        }

        private void Update()
        {
            if (!ControlEnabled) return;

            var win = DesktopWindow.Instance;
            var axis = win != null ? win.GetGlobalWasdAxis() : Vector2.zero;
            if (axis.sqrMagnitude > 1f) axis.Normalize();

            var moving = axis.sqrMagnitude > 1e-3f;
            if (moving)
            {
                var delta = (Vector3)(axis * MoveSpeed * Time.deltaTime);
                transform.position += delta;
                if (Mathf.Abs(axis.x) > 0.01f)
                {
                    var face = axis.x > 0f ? 1 : -1;
                    // 仅走 EVT_SET_FACING；不能再调 PetView.SetFacing（父子双翻会抵消）
                    if (face != _lastFacing) NotifyFacing(face);
                    _lastFacing = face;
                }
            }
            if (moving != _wasMoving)
            {
                NotifyLocomotion(moving);
                _wasMoving = moving;
            }
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
