using System.Collections.Generic;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;

namespace EssSystem.Core.Application.MultiManagers.SkillManager
{
    /// <summary>
    /// 技能管理器 —— Manager 薄壳，驱动 <see cref="SkillService"/> 的 Tick，
    /// 并暴露 bare-string 事件接口供跨模块调用。
    /// <para><b>Priority 15</b>：晚于 EntityManager(13) / BuildingManager(14)。</para>
    /// </summary>
    [Manager(15)]
    public class SkillManager : Manager<SkillManager>
    {
        // ─── Event 名常量 ─────────────────────────────────────────────
        /// <summary>注册技能定义：data = [SkillDefinition]</summary>
        public const string EVT_REGISTER_SKILL = "RegisterSkill";

        /// <summary>学习技能：data = [entityId, skillId]</summary>
        public const string EVT_LEARN_SKILL = "LearnSkill";

        /// <summary>释放技能：data = [Entity caster, string skillId, Entity target?, Vector3 dir?, Vector3 pos?]</summary>
        public const string EVT_CAST_SKILL = "CastSkill";

        // ─── 生命周期 ────────────────────────────────────────────────
        protected override void Initialize()
        {
            base.Initialize();
            // Service 自动创建（Service<T> 单例）
        }

        protected override void Update()
        {
            base.Update();
            if (SkillService.HasInstance)
                SkillService.Instance.Tick(UnityEngine.Time.deltaTime);
        }

        // ─── Event 处理（bare-string §4.1）──────────────────────────
        [Event(EVT_REGISTER_SKILL)]
        private List<object> OnRegisterSkill(string eventName, List<object> data)
        {
            if (data == null || data.Count < 1) return null;
            var def = data[0] as Dao.SkillDefinition;
            if (def != null) SkillService.Instance.RegisterDefinition(def);
            return null;
        }

        [Event(EVT_LEARN_SKILL)]
        private List<object> OnLearnSkill(string eventName, List<object> data)
        {
            if (data == null || data.Count < 2) return null;
            var entityId = data[0] as string;
            var skillId = data[1] as string;
            if (entityId != null && skillId != null)
                SkillService.Instance.LearnSkill(entityId, skillId);
            return null;
        }

        [Event(EVT_CAST_SKILL)]
        private List<object> OnCastSkill(string eventName, List<object> data)
        {
            if (data == null || data.Count < 2) return null;
            var caster = data[0] as Entity;
            var skillId = data[1] as string;
            if (caster == null || skillId == null) return null;

            var target = data.Count > 2 ? data[2] as Entity : null;
            var dir = data.Count > 3 && data[3] is UnityEngine.Vector3 d ? d : UnityEngine.Vector3.zero;
            var pos = data.Count > 4 && data[4] is UnityEngine.Vector3 p ? p : UnityEngine.Vector3.zero;

            var success = SkillService.Instance.CastSkill(caster, skillId, target, dir, pos);
            return success ? new List<object> { ResultCode.OK } : new List<object> { ResultCode.Fail("技能释放失败") };
        }
    }
}
