using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager
{
    /// <summary>
    /// Entity → Character 视图桥 —— 提供类型安全的静态方法，封装所有 §4.1 bare-string 事件调用。
    /// <para>
    /// <b>动机</b>：Capability（如 <c>FacingComponent</c>）和业务代码（如 <c>TribePlayer</c>）
    /// 此前各自硬编码 <c>"SetCharacterFacing"</c>、<c>"PlayCharacterLocomotion"</c> 等字符串 +
    /// 手动拼 <c>List&lt;object&gt;</c>。本桥把这些散落的调用统一收口到一处：
    /// <list type="bullet">
    /// <item>调用方只需 <c>CharacterViewBridge.SetFacing(id, true)</c>，无需知道事件名或参数顺序。</item>
    /// <item>事件名改动只改本文件；调用方零修改。</item>
    /// <item>不引入跨模块编译依赖 —— 内部仍走 EventProcessor bare-string。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class CharacterViewBridge
    {
        // ─── 事件名常量（镜像 CharacterManager.EVT_*，避免跨模块 using）────
        private const string CREATE       = "CreateCharacter";
        private const string DESTROY      = "DestroyCharacter";
        private const string SET_FACING   = "SetCharacterFacing";
        private const string LOCOMOTION   = "PlayCharacterLocomotion";
        private const string ATTACK       = "TriggerCharacterAttack";
        private const string PLAY_ACTION  = "PlayCharacterAction";
        private const string STOP_ACTION  = "StopCharacterAction";
        private const string MOVE         = "MoveCharacter";
        private const string SET_POSITION = "SetCharacterPosition";
        private const string SET_SCALE    = "SetCharacterScale";

        // ─── 复用缓存（避免每帧 new List）──────────────────────────
        private static readonly List<object> _args = new List<object>(4);

        private static List<object> Args(params object[] a)
        {
            _args.Clear();
            for (var i = 0; i < a.Length; i++) _args.Add(a[i]);
            return _args;
        }

        // ─── 生命周期 ─────────────────────────────────────────────

        /// <summary>创建 Character 实例。返回 Character 根 Transform（失败为 null）。</summary>
        public static Transform CreateCharacter(string configId, string instanceId,
            Transform parent = null, Vector3? worldPosition = null)
        {
            if (!EventProcessor.HasInstance) return null;
            var result = EventProcessor.Instance.TriggerEventMethod(
                CREATE, new List<object> { configId, instanceId, parent, worldPosition ?? Vector3.zero });
            return ResultCode.IsOk(result) && result.Count >= 2 && result[1] is Transform root ? root : null;
        }

        /// <summary>销毁 Character 实例。</summary>
        public static bool DestroyCharacter(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !EventProcessor.HasInstance) return false;
            var r = EventProcessor.Instance.TriggerEventMethod(DESTROY, new List<object> { instanceId });
            return ResultCode.IsOk(r);
        }

        // ─── 视觉驱动 ────────────────────────────────────────────

        /// <summary>设置面朝方向（翻转 localScale.x）。</summary>
        public static void SetFacing(string instanceId, bool facingRight)
        {
            Dispatch(SET_FACING, instanceId, facingRight);
        }

        /// <summary>分发运动状态（idle / walk / airborne）。</summary>
        public static void PlayLocomotion(string instanceId, bool moving, bool grounded = true)
        {
            Dispatch(LOCOMOTION, instanceId, moving, grounded);
        }

        /// <summary>触发攻击锁定动画。</summary>
        public static void TriggerAttack(string instanceId, float duration)
        {
            Dispatch(ATTACK, instanceId, duration);
        }

        /// <summary>播放指定动作。</summary>
        public static void PlayAction(string instanceId, string actionName, string partId = null)
        {
            if (partId != null) Dispatch(PLAY_ACTION, instanceId, actionName, partId);
            else                Dispatch(PLAY_ACTION, instanceId, actionName);
        }

        /// <summary>停止动作。</summary>
        public static void StopAction(string instanceId, string partId = null)
        {
            if (partId != null) Dispatch(STOP_ACTION, instanceId, partId);
            else                Dispatch(STOP_ACTION, instanceId);
        }

        // ─── 空间变换 ────────────────────────────────────────────

        /// <summary>设置 Character 世界坐标。</summary>
        public static void SetPosition(string instanceId, Vector3 worldPosition)
        {
            Dispatch(SET_POSITION, instanceId, worldPosition);
        }

        /// <summary>平移 Character（delta）。</summary>
        public static void Move(string instanceId, Vector3 delta)
        {
            Dispatch(MOVE, instanceId, delta);
        }

        /// <summary>设置 Character 根缩放。</summary>
        public static void SetScale(string instanceId, Vector3 scale)
        {
            Dispatch(SET_SCALE, instanceId, scale);
        }

        // ─── 内部 ────────────────────────────────────────────────

        private static void Dispatch(string eventName, string instanceId, object arg1)
        {
            if (string.IsNullOrEmpty(instanceId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(eventName, Args(instanceId, arg1));
        }

        private static void Dispatch(string eventName, string instanceId, object arg1, object arg2)
        {
            if (string.IsNullOrEmpty(instanceId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(eventName, Args(instanceId, arg1, arg2));
        }

        private static void Dispatch(string eventName, string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || !EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(eventName, Args(instanceId));
        }
    }
}
