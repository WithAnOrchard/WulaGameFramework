using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// <see cref="IControllable"/> 的默认实现 —— 用引用计数管理叠加。
    /// <para>多个 Stun Buff 同时挂载时：每个 Buff Apply 调一次 <see cref="PushStun"/>，OnExpire 调一次 <see cref="PopStun"/>，
    /// 计数 &gt; 0 即 <see cref="Stunned"/>=true。这样某一个 Buff 提前被 Cleanse 也不会让其他 Stun 失效。</para>
    /// </summary>
    public class ControllableComponent : IControllable
    {
        private int _stunStack;
        private int _silenceStack;

        public bool Stunned => _stunStack > 0;
        public bool Silenced => _silenceStack > 0;

        public void OnAttach(Entity owner) { }
        public void OnDetach(Entity owner) { _stunStack = 0; _silenceStack = 0; }

        public void PushStun() => _stunStack++;
        public void PopStun() { if (_stunStack > 0) _stunStack--; }
        public void PushSilence() => _silenceStack++;
        public void PopSilence() { if (_silenceStack > 0) _silenceStack--; }
    }
}
