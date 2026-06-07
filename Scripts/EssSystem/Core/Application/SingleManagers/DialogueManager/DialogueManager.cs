using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Manager;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao;
using EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.Specs;
using EssSystem.Core.Foundation.DataManager.RuntimeConfig;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
// §4.1 跨模块调用走 bare-string 协议，不 using UIManager 命名空间。

namespace EssSystem.Core.Application.SingleManagers.DialogueManager
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

        /// <summary>把一组运行时 <see cref="UnityEngine.Sprite"/> 按 z-order（背→前）层叠贴到头像位 ——
        /// 实现"角色多部件复合头像"（如 Skin + Eyes + Hair + Head 一起叠出完整脸）。
        /// 与 Player HUD 单层 <c>TribePlayerHud.AttachHeadSprite</c> 互补；绕过 spriteId 通道，
        /// 直接传 Sprite 引用，无需依赖 ResourceManager 是否已切片注册。
        /// <para>data: <c>[Sprite single]</c> 或 <c>[List&lt;Sprite&gt; layers]</c>。返回 Ok / Fail。
        /// 必须在 <see cref="EVT_OPEN_UI"/> 之后调。重复调用会先清空旧的覆盖层再贴新的。</para></summary>
        public const string EVT_SET_PORTRAIT_SPRITE = "SetDialoguePortraitSprite";

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
        private const string DEFAULT_CONFIG_PATH = "Framework/Dialogue/default_dialogue.json";

        #endregion

        public DialogueService Service => DialogueService.Instance;

        /// <summary>已注册的对话框 DAO 根（缓存复用，关闭=隐藏，再次打开=显示）。</summary>
        private UIPanelComponent _rootPanel;
        private DialogueUIRefs _refs;

        protected override void Initialize()
        {
            base.Initialize();
            if (Service != null) _serviceEnableLogging = Service.EnableLogging;

            RegisterDefaultsFromConfig();

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

        private void RegisterDefaultsFromConfig()
        {
            if (Service == null) return;
            if (!RuntimeConfigLoader.TryLoadJson<DialogueDefaultConfigFile>(
                    DEFAULT_CONFIG_PATH,
                    out var file,
                    msg => Log(msg, Color.gray)))
            {
                Log($"Dialogue default config not found: {DEFAULT_CONFIG_PATH}", Color.yellow);
                return;
            }

            if (_registerDefaultConfig && file?.Configs != null)
            {
                ClearCategory(DialogueService.CAT_CONFIGS);
                foreach (var config in file.Configs)
                    if (config != null && !string.IsNullOrEmpty(config.ConfigId))
                        Service.RegisterConfig(config);
            }

            if (_registerDebugDialogue && file?.Dialogues != null)
            {
                ClearCategory(DialogueService.CAT_DIALOGUES);
                foreach (var dialogue in file.Dialogues)
                    if (dialogue != null && !string.IsNullOrEmpty(dialogue.Id))
                        Service.RegisterDialogue(dialogue);
            }
        }

        private void ClearCategory(string category)
        {
            var keys = new List<string>(Service.GetKeys(category));
            foreach (var key in keys)
                Service.RemoveData(category, key);
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

            var config = !string.IsNullOrEmpty(configId)
                ? Service.GetConfig(Service.ActiveConfigId)
                : new DialogueConfig(DefaultConfigId, "默认");
            config ??= new DialogueConfig(DefaultConfigId, "默认");

            // 对话 UI 的布局迭代频繁，打开时按当前配置重建，避免旧尺寸/旧位置残留。
            RebuildUiRoot();

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

        /// <summary>把一组 Sprite 层叠贴到头像位（参 EVT_SET_PORTRAIT_SPRITE 文档）。
        /// 调用时机：必须在 EVT_OPEN_UI 之后；UI 已重建出 Portrait 子节点。
        /// 重复调用会先清掉旧覆盖层。</summary>
        [Event(EVT_SET_PORTRAIT_SPRITE)]
        public List<object> SetPortraitSprite(List<object> data)
        {
            if (data == null || data.Count < 1) return ResultCode.Fail("参数错误：需要 [Sprite | List<Sprite>]");

            // 兼容单 Sprite 与 List<Sprite>
            List<Sprite> layers = null;
            if (data[0] is Sprite single) layers = new List<Sprite> { single };
            else if (data[0] is List<Sprite> list) layers = list;
            else if (data[0] is IEnumerable<Sprite> enumerable) layers = new List<Sprite>(enumerable);
            if (layers == null) return ResultCode.Fail("参数错误：data[0] 必须是 Sprite 或 List<Sprite>");

            if (_refs?.Portrait == null) return ResultCode.Fail("Portrait 尚未构建（先 OpenDialogueUI）");
            var portraitGo = QueryUIGameObject(_refs.Portrait.Id);
            if (portraitGo == null) return ResultCode.Fail("Portrait UI GameObject 未找到");
            var baseImage = portraitGo.GetComponent<UnityEngine.UI.Image>();
            if (baseImage == null) return ResultCode.Fail("Portrait 节点缺少 Image");

            // 1) 清掉之前 SetPortraitSprite 创建的覆盖层（命名前缀 "_PortraitLayer_"）
            for (var i = portraitGo.transform.childCount - 1; i >= 0; i--)
            {
                var child = portraitGo.transform.GetChild(i);
                if (child != null && child.name.StartsWith("_PortraitLayer_"))
                    Destroy(child.gameObject);
            }

            // 2) 第 0 层 → 主 Image（复用 Portrait 自身），后续层 → 子 Image 叠在同 rect 上
            for (var i = 0; i < layers.Count; i++)
            {
                var sprite = layers[i];
                if (sprite == null) continue;
                if (i == 0)
                {
                    baseImage.sprite = sprite;
                    baseImage.color = Color.white;
                    baseImage.preserveAspect = true;
                }
                else
                {
                    var layerGo = new GameObject($"_PortraitLayer_{i}");
                    layerGo.transform.SetParent(portraitGo.transform, false);
                    var rt = layerGo.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var img = layerGo.AddComponent<UnityEngine.UI.Image>();
                    img.sprite = sprite;
                    img.color = Color.white;
                    img.preserveAspect = true;
                    img.raycastTarget = false;
                }
            }
            return ResultCode.Ok($"portrait set ({layers.Count} layers)");
        }

        private void HideUi()
        {
            if (_rootPanel != null) _rootPanel.Visible = false;
        }

        private void RebuildUiRoot()
        {
            if (!EventProcessor.HasInstance) return;
            if (_rootPanel == null && QueryUIGameObject(DialogueUiId) == null) return;

            EventProcessor.Instance.TriggerEventMethod(
                "UnregisterUIEntity", new List<object> { DialogueUiId });
            _rootPanel = null;
            _refs = null;
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
        /// 从 FrameworkResources/Config 重新读取 Default 配置 / DebugDialogue。
        /// <para>用途：调整 Dialogue 默认配置文件后，调此可强制刷新当前运行时注册表。</para>
        /// </summary>
        [ContextMenu("强制重置默认配置（覆盖持久化）")]
        private void EditorForceResetDefaults()
        {
            if (Service == null) return;
            RegisterDefaultsFromConfig();
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
