using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
using UnityEngine;
using UnityEngine.UI;

namespace EssSystem.Core.Presentation.UIManager.Entity.CommonEntity
{
    /// <summary>
    /// ScrollView 实体 — 对应 <see cref="UIScrollViewComponent"/>。
    /// <para>创建后通过 <see cref="ContentTransform"/> 向内容区动态添加子节点。</para>
    /// <para><see cref="SetText"/> 用于纯文本滚动区（日志、详情等），写入时自动滚到底部。</para>
    /// </summary>
    public class UIScrollViewEntity : UIEntity
    {
        private ScrollRect     _scrollRect;
        private RectTransform  _contentTransform;
        private Text           _contentText;

        /// <summary>内容区 RectTransform，可向其中 AddChild 创建动态行。</summary>
        public RectTransform ContentTransform => _contentTransform;

        protected override void Awake()
        {
            base.Awake();
            _scrollRect        = GetComponent<ScrollRect>();
            _contentTransform  = transform.Find("Viewport/Content") as RectTransform;
            Debug.Log($"[UIScrollViewEntity] Awake: _scrollRect={(_scrollRect != null ? "成功" : "null")}, _contentTransform={(_contentTransform != null ? "成功" : "null")}");
        }

        // ─── 公共 API ────────────────────────────────────────────────────────

        /// <summary>设置纯文本内容（自动滚到底部）。</summary>
        public void SetText(string text)
        {
            Debug.Log($"[UIScrollViewEntity] SetText: 输入文本长度={text?.Length ?? 0}");
            _contentText ??= EnsureContentText();
            Debug.Log($"[UIScrollViewEntity] SetText: _contentText={(_contentText != null ? "非null" : "null")}");
            if (_contentText != null) 
            { 
                _contentText.text = text;
                _contentText.SetAllDirty();  // 强制重新计算 Text 的尺寸
                Debug.Log($"[UIScrollViewEntity] SetText: 文本已设置，当前内容长度={_contentText.text?.Length ?? 0}");
                
                // 强制重新计算布局
                if (_contentTransform != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(_contentTransform);
                    Debug.Log($"[UIScrollViewEntity] SetText: 布局已重建，ContentTransform 大小={_contentTransform.rect.size}");
                }
            }
            ScrollToBottom();
        }

        /// <summary>清空内容区所有子节点。</summary>
        public void ClearContent()
        {
            if (_contentTransform == null) return;
            foreach (Transform child in _contentTransform)
                Destroy(child.gameObject);
            _contentText = null;
        }

        /// <summary>滚动到底部。</summary>
        public void ScrollToBottom()
        {
            if (_scrollRect != null) _scrollRect.verticalNormalizedPosition = 0f;
        }

        // ─── 内部辅助 ────────────────────────────────────────────────────────

        private Text EnsureContentText()
        {
            Debug.Log($"[UIScrollViewEntity] EnsureContentText: _contentTransform={(_contentTransform != null ? "非null" : "null")}");
            if (_contentTransform == null) 
            {
                Debug.LogError("[UIScrollViewEntity] EnsureContentText: _contentTransform 为 null，无法创建 ContentText");
                return null;
            }
            
            Debug.Log($"[UIScrollViewEntity] EnsureContentText: _contentTransform 大小={_contentTransform.rect.size}");
            
            var existing = _contentTransform.GetComponentInChildren<Text>();
            if (existing != null) 
            {
                Debug.Log("[UIScrollViewEntity] EnsureContentText: 找到现有的 Text 组件");
                return existing;
            }

            Debug.Log("[UIScrollViewEntity] EnsureContentText: 创建新的 ContentText");
            var go = new GameObject("ContentText");
            go.transform.SetParent(_contentTransform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(2, 0); rt.offsetMax = new Vector2(-2, 0);
            rt.localScale = Vector3.one;

            var t = go.AddComponent<Text>();
            t.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize           = 11;
            t.color              = Color.white;  // 改为白色，确保可见
            t.alignment          = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow   = VerticalWrapMode.Overflow;
            t.raycastTarget      = false;
            
            // 确保 CanvasRenderer 存在
            go.AddComponent<CanvasRenderer>();

            var le = go.AddComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.flexibleHeight = 1;  // 允许高度自动扩展
            le.preferredHeight = -1;  // -1 表示使用 Text 的 preferred height
            
            // 强制 Text 组件计算其 preferred height
            t.SetAllDirty();
            
            Debug.Log($"[UIScrollViewEntity] EnsureContentText: ContentText 创建完成，颜色={t.color}, preferredHeight={t.preferredHeight}");
            return t;
        }
    }
}
