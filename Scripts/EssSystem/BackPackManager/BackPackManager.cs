using EssSystem.Core.AbstractClass;

namespace EssSystem.BackPackManager
{
    public class BackPackManager : ManagerBase
    {
        private bool _hasInit;

        public static BackPackManager Instance => InstanceWithInit<BackPackManager>(instance => { instance.Init(true); });

        public void Init(bool logMessage)
        {
            if (_hasInit) return;
            _hasInit = true;
        }
    }
}