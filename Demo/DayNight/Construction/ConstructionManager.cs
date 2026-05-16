using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;

namespace Demo.DayNight.Construction
{
    /// <summary>建造系统 Manager —— 把 Service 的 Place/Remove 暴露成事件。
    /// <para>白天才允许放置；监听 <see cref="DayNightGameManager.EVT_PHASE_CHANGED"/> 控制开关。</para></summary>
    [Manager(22)]
    public class ConstructionManager : Manager<ConstructionManager>
    {
        // ─── Event 名常量（命令）─────────────────────────────────
        /// <summary>放置工事（命令）。参数 <c>[string typeId, Vector3 position, float rotation?]</c>，返回 <c>Ok(string instanceId)</c>。</summary>
        public const string EVT_PLACE = "PlaceConstruction";

        /// <summary>移除工事（命令）。参数 <c>[string instanceId]</c>，返回 <see cref="ResultCode"/>。</summary>
        public const string EVT_REMOVE = "RemoveConstruction";

        [Header("玩法限制")]
        [Tooltip("是否仅在白天允许放置")]
        [SerializeField] private bool _onlyAllowAtDay = true;

        public ConstructionService Service => ConstructionService.Instance;

        /// <summary>当前是否允许放置（受昼夜阶段影响）。</summary>
        public bool CanPlace { get; private set; } = true;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;
            Log("ConstructionManager 初始化完成", Color.green);
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
        [Event(EVT_PLACE)]
        public List<object> Place(List<object> data)
        {
            if (data == null || data.Count < 2)
                return ResultCode.Fail("参数无效：需要 [string typeId, Vector3 position, float rotation?]");
            var typeId = data[0] as string;
            if (data[1] is not Vector3 pos) return ResultCode.Fail("参数无效：position 必须是 Vector3");
            var rot = data.Count >= 3 && data[2] is float f ? f : 0f;

            if (_onlyAllowAtDay && !CanPlace)
                return ResultCode.Fail("夜晚不允许建造");

            var id = Service?.Place(typeId, pos, rot);
            return string.IsNullOrEmpty(id) ? ResultCode.Fail("放置失败") : ResultCode.Ok(id);
        }

        [Event(EVT_REMOVE)]
        public List<object> Remove(List<object> data)
        {
            if (data == null || data.Count < 1)
                return ResultCode.Fail("参数无效：需要 [string instanceId]");
            var id = data[0] as string;
            return (Service != null && Service.Remove(id)) ? ResultCode.Ok() : ResultCode.Fail("未找到或移除失败");
        }

        // ─── 订阅昼夜切换 ───────────────────────────────────────
        [EventListener(DayNightGameManager.EVT_PHASE_CHANGED)]
        public List<object> OnPhaseChanged(string eventName, List<object> data)
        {
            if (data == null || data.Count < 1) return ResultCode.Ok();
            var isNight = data[0] is bool b && b;
            CanPlace = !_onlyAllowAtDay || !isNight;
            return ResultCode.Ok();
        }
    }
}
