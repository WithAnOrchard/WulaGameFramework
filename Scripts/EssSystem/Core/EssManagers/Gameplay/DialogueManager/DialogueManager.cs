using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Gameplay.DialogueManager.Dao;
using EssSystem.Core.EssManagers.Gameplay.DialogueManager.Dao.UIConfig;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
// §4.1 跨模块调用走 bare-string 协议，不 using UIManager 命名空间。

namespace EssSystem.Core.EssManagers.Gameplay.DialogueManager
{
    /// <summary>
    /// 对话门面 — 挂到场景里的单例 MonoBehaviour。
    /// <para>
    /// 负责：(1) 默认 Dialogue / DialogueConfig 注册；(2) <c>EVT_OPEN_UI</c> / <c>EVT_CLOSE_UI</c> 命令；
    /// (3) 监听 <see cref="DialogueService"/> 广播原地刷新 UI。<br/>
    /// 业务/状态机在 <see cref="DialogueService"/>；UI 构建/绑定在 <see cref="DialogueUIBuilder"/>。
    /// </para>
    /// </summary>
    [Manager(15)]
    public class DialogueManager : Manager<DialogueManager>
    {
        // ─── 跨模块调用方使用的常量 ───
        public const string EVT_OPEN_UI  = "OpenDialogueUI";
        public const string EVT_CLOSE_UI = "CloseDialogueUI";

        /// <summary>UI 实体注册时使用的 daoId（全局唯一，单实例足够覆盖典型用法）。</summary>
        private const string DialogueUiId = "__dialogue_ui_root";

        #region Inspector

        [Header("Default Registration")]
        [Tooltip("是否在启动时注册一份默认 DialogueConfig（id=Default）")]
        [SerializeField] private bool _registerDefaultConfig = true;

        [Tooltip("是否在启动时注册一段调试用对话（id=DebugDialogue），便于开箱即用")]
        [SerializeField] private bool _registerDebugDialogue = true;

        public const string DefaultConfigId = "Default";
        public const string DebugDialogueId = "DebugDialogue";

        #endregion

        public DialogueService Service => DialogueService.Instance;

        /// <summary>已注册的对话框 DAO 根（缓存复用，关闭=隐藏，再次打开=显示）。</summary>
        private UIPanelComponent _rootPanel;
        private DialogueUIRefs _refs;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            if (_registerDefaultConfig) RegisterDefaultConfigIfMissing();
            if (_registerDebugDialogue) RegisterDebugDialogueIfMissing();

            Log("DialogueManager 初始化完成", Color.green);
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

        #region Defaults

        private void RegisterDefaultConfigIfMissing()
        {
            if (Service.HasData(DialogueService.CAT_CONFIGS, DefaultConfigId)) return;
            Service.RegisterConfig(new DialogueConfig(DefaultConfigId, "默认对话框配置"));
        }

        private void RegisterDebugDialogueIfMissing()
        {
            if (Service.HasData(DialogueService.CAT_DIALOGUES, DebugDialogueId)) return;

            var d = new Dialogue(DebugDialogueId, "调试对话")
                .WithConfig(DefaultConfigId)
                .AddLine(new DialogueLine("L1", "向导", "你好，旅行者。这是一段调试对话。"))
                .AddLine(new DialogueLine("L2", "向导", "点击右下角 ▶ 按钮可以继续。")
                    .WithNextLine("L3"))
                .AddLine(new DialogueLine("L3", "向导", "现在选择你的回应：")
                    .AddOption(new DialogueOption("继续探索").WithNextLine("L4"))
                    .AddOption(new DialogueOption("结束对话")));
            d.AddLine(new DialogueLine("L4", "向导", "祝你好运！"));

            Service.RegisterDialogue(d);
        }

        #endregion

        #region UI Commands

        /// <summary>打开对话 UI 并启动会话。data: [dialogueId, configId?]</summary>
        [Event(EVT_OPEN_UI)]
        public List<object> OpenDialogueUI(List<object> data)
        {
            if (data == null || data.Count < 1 || data[0] is not string dialogueId || string.IsNullOrEmpty(dialogueId))
                return ResultCode.Fail("参数错误：需要 [dialogueId]");

            var configId = data.Count >= 2 && data[1] is string s && !string.IsNullOrEmpty(s) ? s : null;

            // 启动会话（覆盖式）
            if (!Service.StartDialogue(dialogueId, configId))
                return ResultCode.Fail($"启动对话失败: {dialogueId}");

            var config = Service.GetConfig(Service.ActiveConfigId)
                         ?? Service.GetConfig(DefaultConfigId)
                         ?? new DialogueConfig(DefaultConfigId, "默认");

            // 已有缓存 UI → 仅刷新 + 显示
            if (_rootPanel != null && _refs != null && QueryUIGameObject(DialogueUiId) != null)
            {
                _rootPanel.Visible = true;
                RefreshCurrentLine();
                return ResultCode.Ok(dialogueId);
            }

            // 重建
            var (panel, refs) = DialogueUIBuilder.BuildPanelTree(DialogueUiId, config);
            var regResult = EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity",
                new List<object> { DialogueUiId, panel });
            if (!ResultCode.IsOk(regResult))
            {
                Service.EndDialogue();
                return ResultCode.Fail("注册对话 UI 实体失败");
            }

            _rootPanel = panel;
            _refs = refs;

            // 绑定按钮回调
            BindButtonCallbacks();

            RefreshCurrentLine();
            return ResultCode.Ok(dialogueId);
        }

        /// <summary>关闭对话 UI（隐藏，不销毁缓存）。</summary>
        [Event(EVT_CLOSE_UI)]
        public List<object> CloseDialogueUI(List<object> data)
        {
            Service.EndDialogue();
            HideUi();
            return ResultCode.Ok("closed");
        }

        private void HideUi()
        {
            if (_rootPanel != null) _rootPanel.Visible = false;
        }

        #endregion

        #region Service Broadcasts

        [EventListener(DialogueService.EVT_LINE_CHANGED)]
        public List<object> OnDialogueLineChanged(string evt, List<object> args)
        {
            if (_rootPanel == null) return null;
            RefreshCurrentLine();
            return null;
        }

        [EventListener(DialogueService.EVT_ENDED)]
        public List<object> OnDialogueEnded(string evt, List<object> args)
        {
            HideUi();
            return null;
        }

        #endregion

        #region UI Helpers

        private void BindButtonCallbacks()
        {
            if (_refs == null) return;

            if (_refs.NextButton != null)
            {
                _refs.NextButton.OnClick += _ => Service.Advance();
            }
            if (_refs.CloseButton != null)
            {
                _refs.CloseButton.OnClick += _ =>
                    EventProcessor.Instance.TriggerEventMethod(EVT_CLOSE_UI, new List<object>());
            }
            if (_refs.OptionButtons != null)
            {
                for (var i = 0; i < _refs.OptionButtons.Length; i++)
                {
                    var idx = i; // 闭包捕获
                    _refs.OptionButtons[i].OnClick += _ => Service.SelectOption(idx);
                }
            }
        }

        private void RefreshCurrentLine()
        {
            if (_rootPanel == null || _refs == null) return;
            if (!Service.IsActive) { HideUi(); return; }

            var dialogue = Service.GetDialogue(Service.ActiveDialogueId);
            var line = dialogue?.GetLine(Service.ActiveLineId);
            if (line == null) { HideUi(); return; }

            DialogueUIBuilder.ApplyLine(_refs, dialogue, line);
            _rootPanel.Visible = true;
        }

        private static GameObject QueryUIGameObject(string daoId)
        {
            if (!EventProcessor.HasInstance) return null;
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { daoId });
            return ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as GameObject : null;
        }

        #endregion

        #region Editor

        /// <summary>
        /// 强制覆盖磁盘上保存的 Default 配置 / DebugDialogue 为代码中最新默认值。
        /// <para>用途：升级 DialogueConfig 默认布局后，旧持久化值不会被自动覆盖
        /// （<see cref="RegisterDefaultConfigIfMissing"/> 仅在缺失时写）；调此可强制刷新。</para>
        /// </summary>
        [ContextMenu("强制重置默认配置（覆盖持久化）")]
        private void EditorForceResetDefaults()
        {
            if (Service == null) return;
            Service.RegisterConfig(new DialogueConfig(DefaultConfigId, "默认对话框配置"));
            // DebugDialogue 业务无关字段（线条/选项）几乎不会变；仅在 toggle 启用时刷新
            if (_registerDebugDialogue)
            {
                Service.RemoveData(DialogueService.CAT_DIALOGUES, DebugDialogueId);
                RegisterDebugDialogueIfMissing();
            }
            Log("已强制重置默认 DialogueConfig（下次打开对话时生效）", Color.yellow);
        }

        [ContextMenu("打开调试对话")]
        private void EditorOpenDebugDialogue()
        {
            EventProcessor.Instance.TriggerEventMethod(
                EVT_OPEN_UI, new List<object> { DebugDialogueId });
        }

        [ContextMenu("关闭对话")]
        private void EditorCloseDialogue()
        {
            EventProcessor.Instance.TriggerEventMethod(EVT_CLOSE_UI, new List<object>());
        }

        #endregion
    }
}
