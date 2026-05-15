using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime
{
    /// <summary>
    /// 一个 Character 实例的根 MonoBehaviour ——
    /// 由 <see cref="CharacterManager.CharacterService.CreateCharacter"/> 创建，
    /// 持有所有部件 View 的引用，并对外暴露便捷的批量动作切换接口。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterView : MonoBehaviour
    {
        /// <summary>实例 ID（与 <see cref="Dao.Character.InstanceId"/> 同步）。</summary>
        public string InstanceId { get; private set; }

        /// <summary>当前 Character 的配置 ID。</summary>
        public string ConfigId { get; private set; }

        private readonly Dictionary<string, CharacterPartView> _parts = new Dictionary<string, CharacterPartView>();
        /// <summary>partId → 上一次实际底层 Play 的 actionName —— 用于减少重复 Play 导致的重置。</summary>
        private readonly Dictionary<string, string> _lastPartActions = new Dictionary<string, string>();
        /// <summary>攻击锁定截止时间（Time.time）—— 期间 Attack 角色部件保持 Attack 动作。</summary>
        private float _attackLockUntil;

        /// <summary>所有部件 View（partId → view）。</summary>
        public IReadOnlyDictionary<string, CharacterPartView> Parts => _parts;

        /// <summary>是否处于攻击锁定窗口中。</summary>
        public bool IsAttacking => Time.time < _attackLockUntil;

        /// <summary>
        /// 非循环动作在所有部件播完时触发一次（聚合事件）。参数：actionName。
        /// <para>内部监听任意一个部件的 <c>OnActionComplete</c> —— 所有部件同步播放，
        /// 用第一帧完成的那个代表整组的完成时刻即可。</para>
        /// </summary>
        public event System.Action<string> OnActionComplete;

        // 用来聚合任一部件完成的 pivot part（第一个被遍历到的 Dynamic 部件）
        private CharacterPartView _completePivot;

        #region Build

        /// <summary>
        /// 根据配置构建所有部件 GameObject —— 由 Service 在 <c>CreateCharacter</c> 中调用。
        /// </summary>
        public void Build(string instanceId, CharacterConfig config)
        {
            InstanceId = instanceId;
            ConfigId   = config?.ConfigId ?? string.Empty;
            transform.localScale = config?.RootScale ?? Vector3.one;

            if (config?.Parts == null) return;

            foreach (var partCfg in config.Parts)
            {
                if (partCfg == null || string.IsNullOrEmpty(partCfg.PartId)) continue;
                if (_parts.ContainsKey(partCfg.PartId))
                {
                    Debug.LogWarning($"[CharacterView] 重复的 PartId：{partCfg.PartId}（已忽略）");
                    continue;
                }

                var go = new GameObject(partCfg.PartId);
                go.transform.SetParent(transform, false);

                // 按整 Character 的 RenderMode 分派 PartView 组件
                CharacterPartView view = config.RenderMode switch
                {
                    CharacterRenderMode.Prefab3DClips    => go.AddComponent<CharacterPartView3DClips>(),
                    CharacterRenderMode.Prefab3D         => go.AddComponent<CharacterPartView3D>(),
                    CharacterRenderMode.Sprite2DAnimator => go.AddComponent<CharacterPartView2DAnimator>(),
                    _                                    => go.AddComponent<CharacterPartView2D>(),
                };
                // Sprite2DAnimator 需要 OwnerConfigId 才能定位 controller 资产，必须在 Setup 前注入
                if (view is CharacterPartView2DAnimator anim) anim.OwnerConfigId = ConfigId;
                view.Setup(partCfg);

                _parts[partCfg.PartId] = view;

                // 记第一个可作 pivot 的部件作为完成事件源 —— 所有部件同步，只订一次就够
                if (_completePivot == null && view.CanPivotComplete)
                {
                    _completePivot = view;
                    _completePivot.OnActionComplete += HandlePivotComplete;
                }
            }
        }

        private void HandlePivotComplete(string actionName)
        {
            OnActionComplete?.Invoke(actionName);
        }

        private void OnDestroy()
        {
            if (_completePivot != null) _completePivot.OnActionComplete -= HandlePivotComplete;
        }

        #endregion

        #region Convenience API

        /// <summary>给指定部件播放动作；partId 为空则对所有 Dynamic 部件尝试播放同名动作。
        /// <para>内部去重：同一部件连续送同名动作不会重置帧计数。</para></summary>
        public void Play(string actionName, string partId = null)
        {
            if (string.IsNullOrEmpty(partId))
            {
                foreach (var kv in _parts) PlayPart(kv.Key, kv.Value, actionName);
                return;
            }

            if (_parts.TryGetValue(partId, out var view))
                PlayPart(partId, view, actionName);
        }

        private void PlayPart(string partId, CharacterPartView view, string actionName)
        {
            if (view == null || string.IsNullOrEmpty(actionName)) return;
            if (_lastPartActions.TryGetValue(partId, out var current) && current == actionName) return;
            _lastPartActions[partId] = actionName;
            view.Play(actionName);
        }

        /// <summary>停止指定部件；partId 为空则停止全部。</summary>
        public void Stop(string partId = null)
        {
            if (string.IsNullOrEmpty(partId))
            {
                foreach (var kv in _parts) kv.Value?.Stop();
                return;
            }

            if (_parts.TryGetValue(partId, out var view))
                view?.Stop();
        }

        /// <summary>
        /// 播放非循环动作一次（要求该动作 <see cref="CharacterActionConfig.Loop"/> = false）。
        /// 不自动返回；需要返回请用 <see cref="PlayThenReturn"/>。
        /// </summary>
        public void PlayOnce(string actionName) => Play(actionName);

        /// <summary>
        /// 播放一次 <paramref name="oneShotAction"/>，完成后自动切回 <paramref name="returnToAction"/>（通常 Idle / Walk）。
        /// <para>典型用途：受伤 / 短暂特殊动作结束后回到基础动作。内部通过 <see cref="OnActionComplete"/> 一次性订阅实现。</para>
        /// <para>若 <paramref name="oneShotAction"/> 实际是循环动作，则不会触发 <c>OnActionComplete</c>，
        /// 也就不会回弹 —— 需要确保其 <see cref="CharacterActionConfig.Loop"/> = false。</para>
        /// </summary>
        public void PlayThenReturn(string oneShotAction, string returnToAction)
        {
            if (string.IsNullOrEmpty(oneShotAction)) return;

            System.Action<string> handler = null;
            handler = name =>
            {
                if (name != oneShotAction) return;
                OnActionComplete -= handler;
                if (!string.IsNullOrEmpty(returnToAction)) Play(returnToAction);
            };
            OnActionComplete += handler;
            Play(oneShotAction);
        }

        /// <summary>显示/隐藏指定部件；partId 为空则操作整个 Character。</summary>
        public void SetVisible(bool visible, string partId = null)
        {
            if (string.IsNullOrEmpty(partId))
            {
                gameObject.SetActive(visible);
                return;
            }

            if (_parts.TryGetValue(partId, out var view))
                view?.SetVisible(visible);
        }

        #endregion

        #region Locomotion / Combat State Machine

        /// <summary>根据部件 <see cref="CharacterPartConfig.LocomotionRole"/> 分发一次运动状态动作。
        /// <list type="bullet">
        /// <item><b>Movement</b>：Walk / Idle</item>
        /// <item><b>Body</b>：未落地时 Jump，否则 Walk / Idle</item>
        /// <item><b>Attack</b>：<see cref="IsAttacking"/> 期间保持 Attack，否则 Walk / Idle</item>
        /// </list></summary>
        public void PlayLocomotion(bool moving, bool grounded = true,
            string moveAction = "Walk", string idleAction = "Idle",
            string jumpAction = "Jump", string attackAction = "Attack")
        {
            var move = moving ? moveAction : idleAction;
            var body = grounded ? move : jumpAction;
            var attacking = IsAttacking;
            foreach (var kv in _parts)
            {
                var role = kv.Value?.Config?.LocomotionRole ?? CharacterLocomotionRole.Movement;
                var action = role switch
                {
                    CharacterLocomotionRole.Body => body,
                    CharacterLocomotionRole.Attack => attacking ? attackAction : move,
                    _ => move,
                };
                PlayPart(kv.Key, kv.Value, action);
            }
        }

        /// <summary>触发一次攻击：锁定 <paramref name="duration"/> 秒，期间 Attack 角色部件播放 <paramref name="attackAction"/>。
        /// <para>同一锁定窗口内多次调用会动作重起。</para></summary>
        public void TriggerAttack(float duration, string attackAction = "Attack")
        {
            _attackLockUntil = Time.time + Mathf.Max(0.01f, duration);
            foreach (var kv in _parts)
            {
                if ((kv.Value?.Config?.LocomotionRole ?? CharacterLocomotionRole.Movement) != CharacterLocomotionRole.Attack)
                    continue;
                _lastPartActions.Remove(kv.Key);
                PlayPart(kv.Key, kv.Value, attackAction);
            }
        }

        /// <summary>设置面朝：通过翻转 localScale.x 实现。</summary>
        public void SetFacingRight(bool right)
        {
            var s = transform.localScale;
            var abs = Mathf.Abs(s.x);
            var target = right ? abs : -abs;
            if (!Mathf.Approximately(s.x, target))
                transform.localScale = new Vector3(target, s.y, s.z);
        }

        #endregion
    }
}
