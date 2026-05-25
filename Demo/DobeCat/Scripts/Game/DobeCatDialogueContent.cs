using System;
using System.Collections.Generic;
using EssSystem.Core.Application.SingleManagers.DialogueManager;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao;
using UnityEngine;

namespace Demo.DobeCat.Game
{
    /// <summary>
    /// DobeCat 所有对话文本内容的集中注册处。
    /// 通过 <see cref="DialogueService"/> 存储，方便未来扩展（多语言、从配置文件加载等）。
    /// 提供 <see cref="Pick"/> 静态方法，供各 AI Action / 交互控制器随机取一条文本。
    /// DESIGN.md §8.4 对话库
    /// </summary>
    public static class DobeCatDialogueContent
    {
        // ─── 对话 ID 常量 ─────────────────────────────────────────────────────
        public const string IDLE       = "dobecat_idle";
        public const string CLICK      = "dobecat_click";
        public const string PET        = "dobecat_pet";
        public const string PLAY       = "dobecat_play";
        public const string SLEEP      = "dobecat_sleep";
        public const string FEED       = "dobecat_feed";
        public const string HUNGRY     = "dobecat_hungry";
        public const string MORNING    = "dobecat_morning";
        public const string AFTERNOON  = "dobecat_afternoon";
        public const string EVENING    = "dobecat_evening";
        public const string LATENIGHT  = "dobecat_latenight";
        public const string DANMAKU    = "dobecat_danmaku";
        public const string GIFT       = "dobecat_gift";

        private static bool _registered;

        // ─── 公共 API ─────────────────────────────────────────────────────────

        /// <summary>
        /// 随机取一条属于 <paramref name="dialogueId"/> 的文本。
        /// 若 DialogueService 未就绪或对话未注册，则返回 null。
        /// </summary>
        public static string Pick(string dialogueId)
        {
            EnsureRegistered();
            var svc = DialogueService.Instance;
            if (svc == null) return null;
            var dlg = svc.GetData<Dialogue>(DialogueService.CAT_DIALOGUES, dialogueId);
            if (dlg?.Lines == null || dlg.Lines.Count == 0) return null;
            return dlg.Lines[UnityEngine.Random.Range(0, dlg.Lines.Count)].Text;
        }

        /// <summary>
        /// 按当前时间返回问候语对话 ID（早晨 / 下午 / 傍晚 / 深夜）。
        /// </summary>
        public static string GreetingByTime()
        {
            var h = DateTime.Now.Hour;
            if (h >= 6  && h < 12) return MORNING;
            if (h >= 12 && h < 18) return AFTERNOON;
            if (h >= 18 && h < 23) return EVENING;
            return LATENIGHT;
        }

        // ─── 注册 ─────────────────────────────────────────────────────────────

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            var svc = DialogueService.Instance;
            if (svc == null)
            {
                Debug.LogWarning("[DobeCatDialogueContent] DialogueService 未就绪，对话内容未注册");
                return;
            }
            RegisterAll(svc);
        }

        private static void RegisterAll(DialogueService svc)
        {
            Register(svc, IDLE, "随机闲聊",
                "喵～", "又是平静的一天呢。", "*伸懒腰*", "今天天气怎么样呀？",
                "主人在忙什么呀～", "（眯起眼睛）", "嗯……", "嗯嗯嗯……",
                "*打了个大哈欠*", "呼噜呼噜……", "喵……（盯着你看）");

            Register(svc, CLICK, "点击反应",
                "喵！", "呀！", "嗯？", "别戳我啦～", "你好呀～",
                "*歪头*", "摸什么呢…", "喵喵喵！", "唔……");

            Register(svc, PET, "撸猫反应",
                "呼噜呼噜～", "嗯嗯嗯…好舒服～", "再摸一下嘛～",
                "呼～", "*眯眼*", "好喜欢这种感觉……");

            Register(svc, PLAY, "玩耍",
                "喵～在抓虚空！", "嗖嗖！追到了！", "毛球！毛球！",
                "哈！骗到你了！", "喵的我好无聊～", "嗖～！", "嘿嘿嘿～");

            Register(svc, SLEEP, "睡觉",
                "呼噜呼噜……（睡着了）", "困了……稍微睡一下……",
                "Zzz……", "*团成一团*", "嗯……不要打扰我……");

            Register(svc, FEED, "投喂感谢",
                "谢谢喂食！ >'ω'<", "好吃！！", "喵～ 最喜欢你了！",
                "嗷嗷嗷！！！", "果然还是猫粮好吃～");

            Register(svc, HUNGRY, "饥饿",
                "好饿……主人能给我吃点东西吗", "肚子在叫了……",
                "喵……饿……", "（可怜巴巴地看着你）");

            Register(svc, MORNING, "早晨问候",
                "早安～今天也要加油哦！", "早上好！新的一天开始啦～",
                "主人早！有没有睡好呀？", "新的一天，新的开始！喵～");

            Register(svc, AFTERNOON, "下午问候",
                "下午好～有没有好好休息呢？", "下午了，记得喝杯茶～",
                "主人，下午的工作加油！", "悠闲的下午，真不错……");

            Register(svc, EVENING, "傍晚问候",
                "天黑了，主人辛苦了！", "一天结束了，好好放松一下吧～",
                "傍晚好！今天过得怎么样？", "快去吃晚饭吧，别饿着了！");

            Register(svc, LATENIGHT, "深夜",
                "好晚了，主人早点休息哦～", "熬夜对身体不好呢……",
                "已经很晚了，去睡觉吧～", "喵……陪主人晚一点没问题……不过要早点睡哦");

            Register(svc, DANMAKU, "弹幕",
                "好多人说话！", "哇，这么热闹！", "主播真的很厉害呢～",
                "大家都在说什么呀？", "喵～ 好开心！");

            Register(svc, GIFT, "礼物感谢",
                "哇！谢谢礼物！>w<", "主播收到礼物啦！太棒了！",
                "感谢支持！主播一定会更努力的！",
                "喵～！开心！！", "收到礼物好开心～ 谢谢你！");
        }

        private static void Register(DialogueService svc, string id, string name, params string[] lines)
        {
            var dlg = new Dialogue(id, name);
            for (int i = 0; i < lines.Length; i++)
                dlg.AddLine(new DialogueLine($"{id}_{i}", "DobeCat", lines[i]));
            svc.RegisterDialogue(dlg);
        }
    }
}
