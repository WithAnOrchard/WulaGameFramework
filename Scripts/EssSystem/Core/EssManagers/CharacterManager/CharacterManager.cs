using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.EssManager.CharacterManager.Dao;

namespace EssSystem.EssManager.CharacterManager
{
    /// <summary>
    /// 角色门面 —— 挂在场景里的单例 MonoBehaviour，负责生命周期和默认配置注册。
    /// <para>业务层直接调用 <see cref="CharacterService"/>（通过 <see cref="Service"/> 或 <see cref="CharacterService.Instance"/>）。
    /// 无独立 Event 暴露 —— 内部模块无需通过 EventProcessor 就能使用。</para>
    /// </summary>
    [Manager(11)]
    public class CharacterManager : Manager<CharacterManager>
    {
        #region Inspector

        [Header("Default Templates")]
        [Tooltip("是否启动时注册内置示例配置（Warrior / Mage）；业务侧可用同 ConfigId 覆盖")]
        [SerializeField] private bool _registerDebugTemplates = true;

        #endregion

        public CharacterService Service => CharacterService.Instance;

        #region Lifecycle

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDebugTemplates)
            {
                // 内置默认以代码为准 —— 覆盖写入持久化，避免旧版本（如 Static 部件）遗留
                Service.RegisterConfig(DefaultCharacterConfigs.BuildWarrior());
                Service.RegisterConfig(DefaultCharacterConfigs.BuildMage());
            }

            Log("CharacterManager 初始化完成", Color.green);
        }

        protected override void UpdateServiceInspectorInfo()
        {
            if (Service == null) return;
            Service.UpdateInspectorInfo();
            _serviceInspectorInfo = Service.InspectorInfo;
        }

        protected override void SyncServiceLoggingSettings()
        {
            if (Service != null) Service.EnableLogging = _serviceEnableLogging;
        }

        #endregion
    }
}
