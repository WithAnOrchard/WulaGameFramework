using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
namespace EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities
{
    /// <summary>
    /// 可转向能力 —— Entity 能在 X 轴方向左右翻面。
    /// <para>实现负责把状态同步给显示层（典型：通过 <c>CharacterManager.EVT_SET_FACING</c> 翻转 Character localScale.x）。</para>
    /// </summary>
    public interface IFacing : IEntityCapability
    {
        /// <summary>当前是否面朝右。</summary>
        bool FacingRight { get; }

        /// <summary>设置面朝；若与当前相同则忽略。</summary>
        void SetFacingRight(bool right);
    }
}
