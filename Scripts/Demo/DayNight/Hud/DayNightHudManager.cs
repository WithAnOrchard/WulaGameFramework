using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Event;
using EssSystem.Core.UI.Dao.CommonComponents;
using Demo.DayNight.BaseDefense;
using Demo.DayNight.WaveSpawn;

namespace Demo.DayNight.Hud
{
    /// <summary>
    /// 昼夜模式 HUD —— 监听昼夜 / 据点 HP / 波次广播，把状态写入 UIManager DAO 树。
    /// <para>遵守"业务模块禁止直创 Canvas / Text"的硬规则：通过 <c>UIManager.EVT_REGISTER_ENTITY</c>
    /// + <see cref="UIPanelComponent"/> / <see cref="UITextComponent"/> 完成 UI 构建。</para>
    /// <para>本 Manager 没有专属 Service —— UI 状态全部存在 DAO 树中。</para>
    /// </summary>
    [Manager(23)]
    public class DayNightHudManager : Manager<DayNightHudManager>
    {
        // ─── UI DAO id ──────────────────────────────────────────
        private const string PANEL_ID    = "DayNightHud";
        private const string TXT_PHASE   = "DayNightHud_Phase";
        private const string TXT_ROUND   = "DayNightHud_Round";
        private const string TXT_BASE_HP = "DayNightHud_BaseHp";
        private const string TXT_WAVE    = "DayNightHud_Wave";

        // 跨模块事件名常量（避免 using UIManager 模块）
        private const string EXT_REGISTER_ENTITY    = "RegisterUIEntity";
        private const string EXT_UNREGISTER_ENTITY  = "UnregisterUIEntity";
        private const string EXT_DAO_PROP_CHANGED   = "UIDaoPropertyChanged";

        [Header("HUD 布局")]
        [Tooltip("HUD 面板锚点位置（屏幕坐标，以 Canvas 中心为原点）")]
        [SerializeField] private Vector2 _panelPosition = new(-700f, 400f);
        [SerializeField] private Vector2 _panelSize     = new(280f, 160f);
        [SerializeField] private int     _fontSize      = 18;

        // 直接持有 DAO 句柄用于 SetText —— DAO 内部会触发 EVT_DAO_PROPERTY_CHANGED 同步给 UIEntity
        private UITextComponent _txtPhase, _txtRound, _txtBaseHp, _txtWave;
        private bool _registered;

        protected override void Initialize()
        {
            base.Initialize();
            Log("DayNightHudManager 初始化完成", Color.green);
        }

        protected virtual void Start() => BuildHud();

        protected override void OnDestroy()
        {
            if (_registered)
            {
                if (EventProcessor.HasInstance)
                    EventProcessor.Instance.TriggerEventMethod(EXT_UNREGISTER_ENTITY,
                        new List<object> { PANEL_ID });
                _registered = false;
            }
            base.OnDestroy();
        }

        // ─── 构建 UI（只做一次，后续走 SetText 同步）──────────────
        private void BuildHud()
        {
            if (_registered) return;

            var panel = new UIPanelComponent(PANEL_ID, "DayNightHud")
                .SetPosition(_panelPosition.x, _panelPosition.y)
                .SetSize(_panelSize.x, _panelSize.y)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0.55f))
                .SetVisible(true);

            _txtPhase  = MakeLine(TXT_PHASE,    "白天 / 第 1 轮",  yOffset:  50f);
            _txtRound  = MakeLine(TXT_ROUND,    "下一阶段：—",      yOffset:  20f);
            _txtBaseHp = MakeLine(TXT_BASE_HP,  "据点 HP: 1000/1000", yOffset: -10f);
            _txtWave   = MakeLine(TXT_WAVE,     "波次：—",          yOffset: -40f);

            panel.AddChild(_txtPhase);
            panel.AddChild(_txtRound);
            panel.AddChild(_txtBaseHp);
            panel.AddChild(_txtWave);

            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(EXT_REGISTER_ENTITY,
                new List<object> { PANEL_ID, panel });
            _registered = true;
        }

        private UITextComponent MakeLine(string daoId, string initial, float yOffset)
        {
            var t = new UITextComponent(daoId, daoId)
                .SetPosition(0f, yOffset)
                .SetSize(_panelSize.x - 16f, 24f)
                .SetFontSize(_fontSize)
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleCenter)
                .SetText(initial);
            t.SetVisible(true);
            return t;
        }

        // ─── 订阅广播 ───────────────────────────────────────────
        [EventListener(DayNightGameManager.EVT_PHASE_CHANGED)]
        public List<object> OnPhase(string e, List<object> data)
        {
            if (data == null || data.Count < 3) return ResultCode.Ok();
            var isNight = data[0] is bool b && b;
            var round = data[1] is int r ? r : 1;
            var isBoss = data[2] is bool bb && bb;
            _txtPhase?.SetText($"{(isNight ? (isBoss ? "BOSS 夜" : "夜晚") : "白天")} / 第 {round} 轮");
            _txtRound?.SetText(isNight ? "防御据点！" : "搜资源 / 修工事");
            return ResultCode.Ok();
        }

        [EventListener(BaseDefenseService.EVT_HP_CHANGED)]
        public List<object> OnBaseHp(string e, List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Ok();
            var hp = data[0] is int h ? h : 0;
            var max = data[1] is int m ? m : 0;
            _txtBaseHp?.SetText($"据点 HP: {hp}/{max}");
            return ResultCode.Ok();
        }

        [EventListener(BaseDefenseService.EVT_DESTROYED)]
        public List<object> OnBaseDestroyed(string e, List<object> data)
        {
            _txtBaseHp?.SetText("<color=red>据点已击毁</color>");
            return ResultCode.Ok();
        }

        [EventListener(WaveSpawnService.EVT_WAVE_STARTED)]
        public List<object> OnWaveStarted(string e, List<object> data)
        {
            if (data == null || data.Count < 3) return ResultCode.Ok();
            var round = data[0] is int r ? r : 0;
            var wave = data[1] is int w ? w : 0;
            var total = data[2] is int t ? t : 0;
            _txtWave?.SetText($"波次：第 {round} 轮 - {wave + 1} 波（敌 {total}）");
            return ResultCode.Ok();
        }

        [EventListener(WaveSpawnService.EVT_WAVE_CLEARED)]
        public List<object> OnWaveCleared(string e, List<object> data)
        {
            _txtWave?.SetText("波次：清完");
            return ResultCode.Ok();
        }
    }
}
