using EssSystem.Core.Manager;

namespace EssSystem.EssManager.UIManager
{
    /// <summary>
    ///     UI管理器 - Unity MonoBehaviour单例，用于UI管理
    /// </summary>
    public class UIManager : Manager<UIManager>
    {
        private UIService _uiService;

        protected override void Initialize()
        {
            base.Initialize();
            _uiService = UIService.Instance;
        }
    }
}