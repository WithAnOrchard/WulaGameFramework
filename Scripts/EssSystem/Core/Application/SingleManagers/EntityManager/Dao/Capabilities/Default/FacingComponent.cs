namespace EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Capabilities.Default
{
    /// <summary>
    /// <see cref="IFacing"/> 默认实现 —— 把面朝变化通过 <see cref="CharacterViewBridge"/> 转发到 CharacterManager，
    /// 由 Character 视图翻转 localScale.x。
    /// <para>如果没有关联 Character（<paramref name="characterInstanceId"/> 为空），仅维护内部状态。</para>
    /// </summary>
    public class FacingComponent : IFacing
    {
        public bool FacingRight { get; private set; }

        private readonly string _characterInstanceId;
        private Entity _owner;

        public FacingComponent(string characterInstanceId, bool initialRight = true)
        {
            _characterInstanceId = characterInstanceId ?? string.Empty;
            FacingRight = initialRight;
        }

        public void OnAttach(Entity owner) { _owner = owner; Dispatch(); }
        public void OnDetach(Entity owner) { _owner = null; }

        public void SetFacingRight(bool right)
        {
            if (FacingRight == right) return;
            FacingRight = right;
            Dispatch();
        }

        private void Dispatch()
        {
            if (string.IsNullOrEmpty(_characterInstanceId)) return;
            CharacterViewBridge.SetFacing(_characterInstanceId, FacingRight);
        }
    }
}
