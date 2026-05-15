using System.Collections.Generic;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;

namespace EssSystem.Core.EssManagers.Gameplay.SkillManager
{
    /// <summary>
    /// 鎶€鑳界鐞嗗櫒 鈥斺€?Manager 钖勫３锛岄┍鍔?<see cref="SkillService"/> 鐨?Tick锛?    /// 骞舵毚闇?bare-string 浜嬩欢鎺ュ彛渚涜法妯″潡璋冪敤銆?    /// <para><b>Priority 15</b>锛氭櫄浜?EntityManager(13) / BuildingManager(14)銆?/para>
    /// </summary>
    [Manager(15)]
    public class SkillManager : Manager<SkillManager>
    {
        // 鈹€鈹€鈹€ Event 鍚嶅父閲?鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        /// <summary>娉ㄥ唽鎶€鑳藉畾涔夛細data = [SkillDefinition]</summary>
        public const string EVT_REGISTER_SKILL = "RegisterSkill";

        /// <summary>瀛︿範鎶€鑳斤細data = [entityId, skillId]</summary>
        public const string EVT_LEARN_SKILL = "LearnSkill";

        /// <summary>閲婃斁鎶€鑳斤細data = [Entity caster, string skillId, Entity target?, Vector3 dir?, Vector3 pos?]</summary>
        public const string EVT_CAST_SKILL = "CastSkill";

        // 鈹€鈹€鈹€ 鐢熷懡鍛ㄦ湡 鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
        protected override void Initialize()
        {
            base.Initialize();
            // Service 鑷姩鍒涘缓锛圫ervice<T> 鍗曚緥锛?
            }

        protected override void Update()
        {
            base.Update();
            if (SkillService.HasInstance)
                SkillService.Instance.Tick(UnityEngine.Time.deltaTime);
        }

        // 鈹€鈹€鈹€ Event 澶勭悊锛坆are-string 搂4.1锛夆攢鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€鈹€
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
            var caster = data[0] as EntityManager.Dao.Entity;
            var skillId = data[1] as string;
            if (caster == null || skillId == null) return null;

            var target = data.Count > 2 ? data[2] as EntityManager.Dao.Entity : null;
            var dir = data.Count > 3 && data[3] is UnityEngine.Vector3 d ? d : UnityEngine.Vector3.zero;
            var pos = data.Count > 4 && data[4] is UnityEngine.Vector3 p ? p : UnityEngine.Vector3.zero;

            var success = SkillService.Instance.CastSkill(caster, skillId, target, dir, pos);
            return success ? new List<object> { ResultCode.OK } : new List<object> { ResultCode.Fail("技能释放失败") };
        }
    }
}
