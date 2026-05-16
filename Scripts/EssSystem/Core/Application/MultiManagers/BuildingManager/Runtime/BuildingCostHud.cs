using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao.Config;
using EssSystem.Core.Presentation.UIManager.Dao.CommonComponents;
// 本文件构造 UIComponent DAO（共享 DAO 层），但不 using UIManager 本体；
// UI 注册 / 查询 / 销毁全部走 §4.1 bare-string 协议事件。

namespace EssSystem.Core.Application.MultiManagers.BuildingManager.Runtime
{
    /// <summary>
    /// 显示建筑建造所需材料的 HUD —— 跟随建筑 character 屏幕上方一段距离。
    /// <para>渲染方案选 TODO.md item 47-59 的 <b>Screen-Space + 投影跟随</b>：</para>
    /// <list type="number">
    /// <item>通过 UIManager DAO API 构造 UIPanel + 每条材料一个 UIText 行</item>
    /// <item>注册成功后查 panel GameObject，缓存 <see cref="RectTransform"/> 一次</item>
    /// <item><see cref="LateUpdate"/> 中 <c>Camera.WorldToScreenPoint</c> + <c>ScreenPointToLocalPointInRectangle</c>
    /// 直接写 <c>anchoredPosition</c>，绕过 DAO 事件回路（高频写入路径）</item>
    /// </list>
    /// <para>建筑销毁 / 完成时 <c>OnDestroy</c> 会自动 EVT_UNREGISTER_ENTITY 清理 UI。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class BuildingCostHud : MonoBehaviour
    {
        [Tooltip("HUD 相对建筑 character 位置的世界空间偏移（屏幕投影前）。")]
        public Vector3 WorldOffset = new Vector3(0f, 1.5f, 0f);

        [Tooltip("每条材料行的高度（像素）。")]
        public float RowHeight = 28f;

        [Tooltip("HUD 宽度（像素）。")]
        public float Width = 140f;

        private string _rootDaoId;
        private UIPanelComponent _rootPanel;
        private readonly Dictionary<string, UITextComponent> _rowTexts = new Dictionary<string, UITextComponent>();

        private RectTransform _rootRect;
        private RectTransform _canvasRect;
        private Camera _camera;
        private Building _building;
        private bool _disposed;

        // ─── 公共 API ──────────────────────────────────────────────

        /// <summary>由 BuildingService 在创建 HUD 后调用一次。</summary>
        public void Bind(Building building)
        {
            _building = building;
            _camera = Camera.main;
            BuildUI();
            RefreshTexts();
        }

        /// <summary>外部更新材料剩余值时调（已自动改 dao.Text）。</summary>
        public void RefreshTexts()
        {
            if (_building == null || _rowTexts.Count == 0) return;
            foreach (var cost in _building.Config.Costs)
            {
                if (!_rowTexts.TryGetValue(cost.ItemId, out var txt)) continue;
                _building.Remaining.TryGetValue(cost.ItemId, out var rem);
                txt.SetText($"{cost.Display}  {cost.Amount - rem}/{cost.Amount}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // ApplicationLifecycle.IsQuitting 信号已集成到 HasInstance + EventProcessor 内部，
            // teardown 期事件分发会 silent-return，无需 try/catch 兜底。
            if (!string.IsNullOrEmpty(_rootDaoId) && EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod(
                    "UnregisterUIEntity", new List<object> { _rootDaoId });
            _rowTexts.Clear();
            _rootPanel = null;
            _rootRect = null;
            _canvasRect = null;
        }

        // ─── Unity Lifecycle ──────────────────────────────────────

        private void LateUpdate()
        {
            if (_disposed || _rootRect == null) return;
            if (_camera == null) _camera = Camera.main;

            UIWorldFollower.UpdatePositionOverlay(_camera, _canvasRect, _rootRect, transform.position + WorldOffset);
        }

        private void OnDestroy() => Dispose();

        // ─── 内部 ─────────────────────────────────────────────────

        private void BuildUI()
        {
            if (_building == null) return;
            var costs = _building.Config.Costs;
            if (costs == null || costs.Count == 0) return;

            _rootDaoId = $"bld_cost_{_building.InstanceId}";
            var totalHeight = RowHeight * costs.Count + 8f;
            _rootPanel = new UIPanelComponent(_rootDaoId, $"BuildingCostHud({_building.InstanceId})")
                .SetPosition(0, 0)
                .SetSize(Width, totalHeight)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0.55f))
                .SetVisible(true);

            for (var i = 0; i < costs.Count; i++)
            {
                var c = costs[i];
                var rowId = $"{_rootDaoId}_row_{c.ItemId}";
                var row = new UITextComponent(rowId, $"Row({c.ItemId})", "")
                    .SetPosition(0, -(i * RowHeight) + (totalHeight * 0.5f) - RowHeight * 0.5f - 4f)
                    .SetSize(Width - 8f, RowHeight)
                    .SetFontSize(14)
                    .SetColor(Color.white)
                    .SetAlignment(TextAnchor.MiddleCenter);
                _rootPanel.AddChild(row);
                _rowTexts[c.ItemId] = row;
            }

            // 注册到 UIManager
            if (!EventProcessor.HasInstance) return;
            EventProcessor.Instance.TriggerEventMethod(
                "RegisterUIEntity",          // = UIManager.EVT_REGISTER_ENTITY
                new List<object> { _rootDaoId, _rootPanel });

            // 查 root GameObject，缓存 RectTransform，避开 DAO 高频事件回路
            var go = QueryUIGameObject(_rootDaoId);
            if (go != null) _rootRect = go.transform as RectTransform;

            // 找 Canvas RectTransform（投影坐标转换需要）
            var canvasResult = EventProcessor.Instance.TriggerEventMethod(
                "GetUICanvasTransform",       // = UIManager.EVT_GET_CANVAS_TRANSFORM
                null);
            if (ResultCode.IsOk(canvasResult) && canvasResult.Count >= 2 && canvasResult[1] is Transform tr)
                _canvasRect = tr as RectTransform;
        }

        private static GameObject QueryUIGameObject(string daoId)
        {
            if (!EventProcessor.HasInstance) return null;
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject",           // = UIManager.EVT_GET_UI_GAMEOBJECT
                new List<object> { daoId });
            return ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as GameObject : null;
        }
    }
}
