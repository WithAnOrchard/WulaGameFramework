using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;

namespace Demo.DayNight.BaseDefense
{
    /// <summary>据点防御 Manager —— 仅做事件薄门面。</summary>
    [Manager(21)]
    public class BaseDefenseManager : Manager<BaseDefenseManager>
    {
        // ─── Event 名常量（命令）─────────────────────────────────
        /// <summary>对据点造成伤害（命令）。参数 <c>[int amount]</c>，返回 <see cref="ResultCode"/>。</summary>
        public const string EVT_DAMAGE_BASE = "DamageBase";

        /// <summary>重置据点 HP 到 MaxHp（命令）。无参，返回 <see cref="ResultCode"/>。</summary>
        public const string EVT_RESET_BASE = "ResetBase";

        [Header("据点配置")]
        [Tooltip("据点最大 HP；Initialize 时会写入 Service")]
        [SerializeField, Min(1)] private int _maxHp = 1000;

        [Tooltip("启动时是否重置 HP（否则保留上局存档）")]
        [SerializeField] private bool _resetOnStart = true;

        public BaseDefenseService Service => BaseDefenseService.Instance;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null)
            {
                _serviceEnableLogging = Service.EnableLogging;
                Service.Configure(_maxHp);
                if (_resetOnStart) Service.Reset();
            }
            Log("BaseDefenseManager 初始化完成", Color.green);
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

        // ─── 事件处理（命令）────────────────────────────────────
        [Event(EVT_DAMAGE_BASE)]
        public List<object> DamageBase(List<object> data)
        {
            if (data == null || data.Count < 1 || data[0] is not int amount)
                return ResultCode.Fail("参数无效：需要 [int amount]");
            Service?.ApplyDamage(amount);
            return ResultCode.Ok();
        }

        [Event(EVT_RESET_BASE)]
        public List<object> ResetBase(List<object> data)
        {
            Service?.Reset();
            return ResultCode.Ok();
        }
    }
}
