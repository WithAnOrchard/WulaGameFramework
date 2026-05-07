using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;
// C4: 走 EventProcessor + 常量协议 引入 UIManager 仅为获取 EVT_X 常量不使用其运行时 API
using UIMgr = EssSystem.Core.EssManagers.Presentation.UIManager.UIManager;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime.Preview
{
    /// <summary>
    /// Character 预览面板 —— 全部 UI 走 EventProcessor 事件注册（遵循项目规则1）。
    /// <para>顶部 3 行：Model / Action / Scale 选择器；左侧每个 Dynamic Part 一行，
    /// 中间是真实的 <see cref="CharacterView"/> GameObject。</para>
    /// <para>变体切换通过 <c>DefaultCharacterConfigs.MakeAnimatedPart</c> 重建 PartConfig
    /// 并对 PartView 重新 Setup；Model 切换则销毁角色并整树重建 UI。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPreviewPanel : MonoBehaviour
    {
        #region Inspector

        [Header("Preview Settings")]
        [SerializeField] private Vector3 _previewPosition  = Vector3.zero;
        [SerializeField] private string  _previewInstanceId = "preview_character";

        [Header("Scale (作用于 CharacterView 根 GameObject)")]
        [SerializeField] private float _initialScale = 3f;
        [SerializeField] private float _scaleStep    = 1.25f;
        [SerializeField] private float _minScale     = 0.25f;
        [SerializeField] private float _maxScale     = 32f;

        #endregion

        #region Layout Constants (1920×1080 reference, 坐标 = 父 RectTransform 左下原点)

        private const string RootId = "cpp_root";

        // Canvas 逻辑尺寸（运行时从实际 Canvas RectTransform 读取；ScaleWithScreenSize 下 width/height 会随屏幕 aspect 变化）
        private float _canvasW = 1920f;
        private float _canvasH = 1080f;

        // 顶栏（3 行选择器）—— 行内宽度 600（symmetric），高 170 内分 3 行 44 高、间距 6、上下各 13 padding
        private const float TopBarH = 170f;
        private const float RowW    = 600f;
        private const float RowH    = 44f;

        // 左侧部件面板 —— 行内宽度 540（symmetric）
        private const float PartsPanelW = 560f;
        private const float PartRowW    = 540f;
        private const float PartRowH    = 36f;
        private const float PartRowSpacing = 6f;
        private const float PartsPanelPaddingTop = 14f;

        #endregion

        #region Runtime State

        private readonly List<CharacterConfig> _models = new List<CharacterConfig>();
        private int _modelIndex;
        private CharacterConfig CurrentModel => _models.Count > 0 ? _models[_modelIndex] : null;
        private string CurrentModelId => CurrentModel?.ConfigId ?? string.Empty;

        private string[] _allActions;
        private int _actionIndex;
        private string CurrentAction => (_allActions != null && _allActions.Length > 0) ? _allActions[_actionIndex] : string.Empty;

        private class PartState
        {
            public string PartId;
            public int    SortingOrder;
            public string CurrentPrefix;
        }
        private readonly List<PartState> _partStates = new List<PartState>();

        private float _scale;

        // 上次构建时的 Canvas 逻辑尺寸，用于检测窗口 resize 触发 UI 重建
        private Vector2 _lastBuiltCanvasSize;

        // UI DAO 引用（用于 in-place 改文本，避免重建）
        private UIPanelComponent _root;
        private UITextComponent  _modelText;
        private UITextComponent  _actionText;
        private UITextComponent  _scaleText;
        private readonly Dictionary<string, UITextComponent> _partTexts = new Dictionary<string, UITextComponent>();

        private static bool _isAppQuitting;

        #endregion

        #region Lifecycle

        private void OnApplicationQuit() { _isAppQuitting = true; }

        private void Start()
        {
            if (CharacterService.Instance == null)
            {
                Debug.LogError("[CharacterPreviewPanel] CharacterService 未初始化");
                return;
            }
            _allActions = DefaultCharacterConfigs.GetAllActionNames();
            _actionIndex = System.Array.IndexOf(_allActions, DefaultCharacterConfigs.DefaultAction);
            if (_actionIndex < 0) _actionIndex = 0;
            _scale = Mathf.Clamp(_initialScale, _minScale, _maxScale);

            RefreshModelList();
            if (_models.Count == 0)
            {
                Debug.LogWarning("[CharacterPreviewPanel] 无已注册 CharacterConfig，面板未构建");
                return;
            }

            SwitchModel(0);
        }

        private void Update()
        {
            // 监听 Canvas 逻辑尺寸变化（窗口 resize / Game View 切分辨率）→ 重建 UI
            if (_root == null) return;
            var canvasRT = QueryCanvasRectTransform();
            if (canvasRT == null) return;
            var size = canvasRT.rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            if (Mathf.Abs(size.x - _lastBuiltCanvasSize.x) < 0.5f && Mathf.Abs(size.y - _lastBuiltCanvasSize.y) < 0.5f) return;
            BuildAndRegisterUI();
        }

        private void OnEnable()
        {
            // 重新启用面板时，用当前真实 Canvas 尺寸重建 —— 避免关面板期间 Game View/窗口 resize 后位置过时
            if (_root != null && _models.Count > 0) BuildAndRegisterUI();
            else if (_root != null) _root.Visible = true;
            SetCharacterVisible(true);
        }

        private void OnDisable()
        {
            if (_root != null) _root.Visible = false;
            SetCharacterVisible(false);
        }

        private void OnDestroy()
        {
            if (_isAppQuitting) return;
            // 销毁 UI（走 UIManager 事件，仅用字符串协议）
            var ep = EventProcessor.Instance;
            if (ep != null)
                ep.TriggerEventMethod(UIMgr.EVT_UNREGISTER_ENTITY, new List<object> { RootId });   // C4

            if (CharacterService.Instance != null)
                CharacterService.Instance.DestroyCharacter(_previewInstanceId);
        }

        private void RefreshModelList()
        {
            _models.Clear();
            foreach (var c in CharacterService.Instance.GetAllConfigs())
                if (c != null) _models.Add(c);
            _models.Sort((a, b) => string.CompareOrdinal(a.ConfigId, b.ConfigId));
            _modelIndex = 0;
        }

        private void SetCharacterVisible(bool visible)
        {
            var c = CharacterService.Instance != null
                ? CharacterService.Instance.GetCharacter(_previewInstanceId)
                : null;
            if (c?.View != null) c.View.gameObject.SetActive(visible);
        }

        #endregion

        #region UI Build (UIManager DAO)

        /// <summary>整树重建 UI（在 Model 切换时调用 —— 因为部件数/ID 不同，无法 in-place 改）。</summary>
        private void BuildAndRegisterUI()
        {
            var ep = EventProcessor.Instance;
            if (ep == null) return;

            // 1) 卸载旧 UI —— 必须同步销毁。Object.Destroy 延后到帧末，
            // 在同一帧立即 Register 新树会与旧的同名 GameObject 共存，连续点击会累积重复。
            // 改用 DestroyImmediate 拆掉旧 GameObject，UIEntity.OnDestroy 会自动从 UIService 缓存移除。
            // 走事件拿旧根 GameObject —— 不引用 UIEntity 类
            // C4: 走事件拿旧根 GameObject —— 常量不引用 UIEntity 类
            var oldGoResult = ep.TriggerEventMethod(
                UIMgr.EVT_GET_UI_GAMEOBJECT,
                new List<object> { RootId });
            var oldGo = ResultCode.IsOk(oldGoResult) && oldGoResult.Count >= 2 ? oldGoResult[1] as GameObject : null;
            if (oldGo != null) Object.DestroyImmediate(oldGo);
            _partTexts.Clear();

            // 2) 读取真实 Canvas 逻辑尺寸（ScaleWithScreenSize 下随屏幕 aspect 变化，
            //    固定用 1920×1080 会在非 16:9 或 Game 窗口尺寸不同时让顶栏/面板被挤出屏幕）
            var canvasRT = QueryCanvasRectTransform();
            if (canvasRT != null)
            {
                var rect = canvasRT.rect;
                if (rect.width  > 0f) _canvasW = rect.width;
                if (rect.height > 0f) _canvasH = rect.height;
            }
            _lastBuiltCanvasSize = new Vector2(_canvasW, _canvasH);

            // 3) 构造新 DAO 树
            _root = new UIPanelComponent(RootId, "CharacterPreviewRoot")
                .SetPosition(_canvasW * 0.5f, _canvasH * 0.5f)
                .SetSize(_canvasW, _canvasH)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(true);

            BuildTopBar(_root);
            BuildPartsPanel(_root);

            // 3) 注册到 UIManager／C4 使用常量
            ep.TriggerEventMethod(UIMgr.EVT_REGISTER_ENTITY, new List<object> { RootId, _root });
        }

        /// <summary>走 EVT_GET_CANVAS_TRANSFORM 获取 UI Canvas 根 RectTransform，不引用 UIEntity 类。</summary>
        private static RectTransform QueryCanvasRectTransform()
        {
            if (!EventProcessor.HasInstance) return null;
            // C4: 使用常量
            var r = EventProcessor.Instance.TriggerEventMethod(UIMgr.EVT_GET_CANVAS_TRANSFORM, null);
            if (!ResultCode.IsOk(r) || r.Count < 2) return null;
            return r[1] as RectTransform;
        }

        private void BuildTopBar(UIPanelComponent parent)
        {
            // 顶栏 panel：贴在 canvas 顶端（使用运行时 Canvas 真实尺寸）
            var topBar = new UIPanelComponent("cpp_topbar", "TopBar")
                .SetPosition(_canvasW * 0.5f, _canvasH - TopBarH * 0.5f)
                .SetSize(_canvasW, TopBarH)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0.35f))
                .SetVisible(true);

            // 3 行选择器，y 从顶到底（上下对称：pad 13 + 3×44 + 2×gap6 = 13+132+12+13 = 170）
            var rowYs = new[] { 135f, 85f, 35f };
            var cx = _canvasW * 0.5f;

            _modelText  = AddSelectorRow(topBar, "cpp_row_model",  "Model:",  cx, rowYs[0],
                                         () => SwitchModel(_modelIndex - 1),
                                         () => SwitchModel(_modelIndex + 1));
            _actionText = AddSelectorRow(topBar, "cpp_row_action", "Action:", cx, rowYs[1],
                                         () => SwitchAction(_actionIndex - 1),
                                         () => SwitchAction(_actionIndex + 1));
            _scaleText  = AddSelectorRow(topBar, "cpp_row_scale",  "Scale:",  cx, rowYs[2],
                                         () => StepScale(1f / _scaleStep),
                                         () => StepScale(_scaleStep));

            _modelText.Text  = CurrentModelId;
            _actionText.Text = CurrentAction;
            _scaleText.Text  = FormatScale(_scale);

            parent.AddChild(topBar);
        }

        /// <summary>构建一行 [Label] [◀] [Value Text] [▶]。返回 value text 引用便于后续改字。</summary>
        private static UITextComponent AddSelectorRow(
            UIPanelComponent parent, string idPrefix, string label, float cx, float cy,
            System.Action onPrev, System.Action onNext)
        {
            var row = new UIPanelComponent(idPrefix, idPrefix)
                .SetPosition(cx, cy)
                .SetSize(RowW, RowH)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(true);

            // 同 PartRow 坐标系（行 panel 左下角为锚，内容可溢出 —— 实际用的是 anchoredPosition 原值）
            // 文字用超采样：FontSize ×2 + Size ×2 + Scale 0.5，視觉等效但以 2× 分辨率栅格化
            var lbl = new UITextComponent(idPrefix + "_lbl", "label")
                .SetPosition(-90f, 0f)
                .SetSize(220f, 64f)
                .SetScale(0.5f, 0.5f)
                .SetFontSize(40)
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleRight)
                .SetText(label);
            lbl.SetVisible(true);
            row.AddChild(lbl);

            var btnPrev = new UIButtonComponent(idPrefix + "_prev", "prev", string.Empty)
                .SetPosition(350f, 20f)
                .SetSize(40f, 40f)
                .SetButtonSpriteId("btn_left")
                .SetVisible(true)
                .SetInteractable(true);
            btnPrev.OnClick += _ => onPrev?.Invoke();
            row.AddChild(btnPrev);

            var valueText = new UITextComponent(idPrefix + "_val", "value")
                .SetPosition(145f, 0f)
                .SetSize(480f, 64f)
                .SetScale(0.5f, 0.5f)
                .SetFontSize(40)
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleCenter)
                .SetText("-");
            valueText.SetVisible(true);
            row.AddChild(valueText);

            var btnNext = new UIButtonComponent(idPrefix + "_next", "next", string.Empty)
                .SetPosition(530f, 20f)
                .SetSize(40f, 40f)
                .SetButtonSpriteId("btn_right")
                .SetVisible(true)
                .SetInteractable(true);
            btnNext.OnClick += _ => onNext?.Invoke();
            row.AddChild(btnNext);

            parent.AddChild(row);
            return valueText;
        }

        private void BuildPartsPanel(UIPanelComponent parent)
        {
            // 左侧面板：从顶栏底端往下铺到 canvas 底（使用运行时 Canvas 真实尺寸）
            var panelH = _canvasH - TopBarH;
            var panel = new UIPanelComponent("cpp_parts", "PartsPanel")
                .SetPosition(PartsPanelW * 0.5f, panelH * 0.5f)
                .SetSize(PartsPanelW, panelH)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0.35f))
                .SetVisible(true);

            // 行从顶向下排列
            var topY = panelH - PartsPanelPaddingTop;
            for (var i = 0; i < _partStates.Count; i++)
            {
                var ps = _partStates[i];
                var rowCy = topY - PartRowH * 0.5f - i * (PartRowH + PartRowSpacing);
                AddPartRow(panel, ps, rowCy);
            }

            parent.AddChild(panel);
        }

        private void AddPartRow(UIPanelComponent parent, PartState ps, float cy)
        {
            var idPrefix = "cpp_part_" + ps.PartId;
            var row = new UIPanelComponent(idPrefix, idPrefix)
                .SetPosition(PartsPanelW * 0.5f, cy)
                .SetSize(PartRowW, PartRowH)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f))
                .SetVisible(true);

            // 坐标用用户在 Inspector 里量出的 anchoredPosition 值，直接套用
            var partId = ps.PartId; // closure

            // 文字超采样：FontSize ×2 + Size ×2 + Scale 0.5
            var label = new UITextComponent(idPrefix + "_lbl", "label")
                .SetPosition(-90f, 0f)
                .SetSize(220f, 56f)
                .SetScale(0.5f, 0.5f)
                .SetFontSize(36)
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleLeft)
                .SetText(ps.PartId);
            label.SetVisible(true);
            row.AddChild(label);

            var btnPrev = new UIButtonComponent(idPrefix + "_prev", "prev", string.Empty)
                .SetPosition(290f, 20f)
                .SetSize(40f, 40f)
                .SetButtonSpriteId("btn_left")
                .SetVisible(true)
                .SetInteractable(true);
            btnPrev.OnClick += _ => CyclePartVariant(partId, -1);
            row.AddChild(btnPrev);

            var value = new UITextComponent(idPrefix + "_val", "value")
                .SetPosition(145f, 0f)
                .SetSize(480f, 56f)
                .SetScale(0.5f, 0.5f)
                .SetFontSize(36)
                .SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleCenter)
                .SetText(ShortName(ps.CurrentPrefix));
            value.SetVisible(true);
            row.AddChild(value);
            _partTexts[ps.PartId] = value;

            var btnNext = new UIButtonComponent(idPrefix + "_next", "next", string.Empty)
                .SetPosition(530f, 20f)
                .SetSize(40f, 40f)
                .SetButtonSpriteId("btn_right")
                .SetVisible(true)
                .SetInteractable(true);
            btnNext.OnClick += _ => CyclePartVariant(partId, +1);
            row.AddChild(btnNext);

            parent.AddChild(row);
        }

        #endregion

        #region Actions

        private void SwitchModel(int newIndex)
        {
            if (_models.Count == 0) return;
            _modelIndex = ((newIndex % _models.Count) + _models.Count) % _models.Count;

            // 重建角色实例
            CharacterService.Instance.DestroyCharacter(_previewInstanceId);
            CharacterService.Instance.CreateCharacter(CurrentModelId, _previewInstanceId, null, _previewPosition);

            // 重新提取 part 列表（按当前 Model 的 Dynamic 部件）
            _partStates.Clear();
            if (CurrentModel != null)
            {
                foreach (var part in CurrentModel.Parts)
                {
                    if (part == null || part.PartType != CharacterPartType.Dynamic) continue;
                    _partStates.Add(new PartState
                    {
                        PartId        = part.PartId,
                        SortingOrder  = part.SortingOrder,
                        CurrentPrefix = ExtractSheetPrefix(part),
                    });
                }
            }

            // Model 切换 → 部件列表通常变化 → 整树重建 UI
            BuildAndRegisterUI();

            ApplyScale();
            ApplyCurrentAction();
        }

        private void SwitchAction(int newIndex)
        {
            if (_allActions == null || _allActions.Length == 0) return;
            _actionIndex = ((newIndex % _allActions.Length) + _allActions.Length) % _allActions.Length;
            if (_actionText != null) _actionText.Text = CurrentAction;
            ApplyCurrentAction();
        }

        private void ApplyCurrentAction()
        {
            var character = CharacterService.Instance.GetCharacter(_previewInstanceId);
            if (character == null || character.View == null) return;
            character.View.Play(CurrentAction);
        }

        private void StepScale(float factor)
        {
            _scale = Mathf.Clamp(_scale * factor, _minScale, _maxScale);
            ApplyScale();
        }

        private void ApplyScale()
        {
            if (_scaleText != null) _scaleText.Text = FormatScale(_scale);
            var character = CharacterService.Instance != null
                ? CharacterService.Instance.GetCharacter(_previewInstanceId)
                : null;
            if (character == null || character.View == null) return;
            character.View.transform.localScale = Vector3.one * _scale;
        }

        private void CyclePartVariant(string partId, int dir)
        {
            var ps = _partStates.Find(p => p.PartId == partId);
            if (ps == null) return;

            var variants = CharacterVariantPools.GetVariants(CurrentModelId, partId);
            if (variants == null || variants.Count == 0) return;

            var next = dir >= 0
                ? CharacterVariantPools.Next(variants, ps.CurrentPrefix)
                : CharacterVariantPools.Prev(variants, ps.CurrentPrefix);
            if (next == ps.CurrentPrefix) return;

            ps.CurrentPrefix = next;
            if (_partTexts.TryGetValue(partId, out var text))
                text.Text = ShortName(next);

            // 用新前缀重建该部件的 PartConfig，让 PartView 重新 Setup
            var character = CharacterService.Instance.GetCharacter(_previewInstanceId);
            if (character == null) return;
            if (!character.Parts.TryGetValue(partId, out var partView) || partView == null) return;

            var newPartCfg = DefaultCharacterConfigs.MakeAnimatedPart(partId, next, ps.SortingOrder);
            partView.Setup(newPartCfg);
            // 对整个 Character 重新 Play(CurrentAction) —— 所有 Dynamic 部件归零帧强行对齐，
            // 避免刚换的部件从第 0 帧开始、其它部件还在中间帧的不同步状态
            character.View.Play(CurrentAction);
        }

        #endregion

        #region Helpers

        private static string FormatScale(float s) => s.ToString("0.##") + "x";

        /// <summary>从 PartConfig 首个 Action 反推 sheet 前缀（去掉 _{ActionName}_0 后缀）。</summary>
        private static string ExtractSheetPrefix(CharacterPartConfig part)
        {
            if (part == null || part.Animations == null || part.Animations.Count == 0) return string.Empty;
            var firstAction = part.Animations[0];
            if (firstAction == null || firstAction.SpriteIds == null || firstAction.SpriteIds.Count == 0) return string.Empty;
            var first = firstAction.SpriteIds[0];
            var suffix = "_" + firstAction.ActionName + "_0";
            return first.EndsWith(suffix, System.StringComparison.Ordinal)
                ? first.Substring(0, first.Length - suffix.Length)
                : first;
        }

        /// <summary>显示用：去掉前缀第一段（路径），保留主体。<c>Skin_warrior_1 → warrior_1</c>。</summary>
        private static string ShortName(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return "(empty)";
            var first = prefix.IndexOf('_');
            return first >= 0 && first < prefix.Length - 1 ? prefix.Substring(first + 1) : prefix;
        }

        #endregion
    }
}
