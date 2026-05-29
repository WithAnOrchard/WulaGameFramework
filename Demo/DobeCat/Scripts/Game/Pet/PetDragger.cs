using UnityEngine;
using EssSystem.Core.Platform.Windows;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 用鼠标按住桌宠拖拽。
    /// <para>窗口策略采用 §M1.6（全屏窗口 + 桌宠在窗口内移动）→ 拖拽改的是 Transform.position，不是 HWND 位置。</para>
    /// </summary>
    public class PetDragger : MonoBehaviour
    {
        public PetView View;
        public PetAiController Ai;

        [Tooltip("按下到识别为拖拽的最小屏幕像素阈值。")]
        public float DragThresholdPixels = 4f;

        public bool IsDragging { get; private set; }

        private Vector3 _grabOffset;
        private Vector2 _downScreen;

        private void Update()
        {
            HandleScrollZoom();

            if (Input.GetMouseButtonDown(0))
            {
                if (HitTestSelf(Input.mousePosition))
                {
                    _downScreen = Input.mousePosition;
                    _grabOffset = transform.position - ScreenToWorld(Input.mousePosition);
                    IsDragging = false; // 待越过阈值才进入正式拖拽
                }
            }
            else if (Input.GetMouseButton(0))
            {
                if (_grabOffset != Vector3.zero || _downScreen != Vector2.zero)
                {
                    if (!IsDragging && Vector2.Distance(Input.mousePosition, _downScreen) >= DragThresholdPixels)
                    {
                        IsDragging = true;
                        if (Ai != null) Ai.SetPaused(true);
                    }
                    if (IsDragging)
                    {
                        var w = ScreenToWorld(Input.mousePosition) + _grabOffset;
                        w.z = 0f;
                        w = ClampToScreen(w);
                        transform.position = w;
                        // EntityService.Tick 会把 CharacterRoot.position 赋为 entity.WorldPosition，
                        // 拖拽期间同步写回，避免下一帧被带回原位置。
                        if (Ai != null && Ai.Entity != null) Ai.Entity.WorldPosition = w;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (IsDragging && Ai != null) Ai.SetPaused(false);
                IsDragging = false;
                _grabOffset = Vector3.zero;
                _downScreen = Vector2.zero;
            }
        }

        /// <summary>桌宠是否覆盖此屏幕坐标（M1 简化为 sprite bounds，M2 升级为 alpha 像素测试）。</summary>
        public bool HitTestSelf(Vector2 screenPos)
        {
            if (View == null) return false;
            var world = ScreenToWorld(screenPos);
            return View.WorldBounds.Contains(new Vector3(world.x, world.y, View.WorldBounds.center.z));
        }

        private static Vector3 ScreenToWorld(Vector2 screenPos)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            var z = Mathf.Abs(cam.transform.position.z);
            return cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, z));
        }

        /// <summary>
        /// 鼠标悬停在宠物上时，滚轮缩放大小。
        /// 每格滚轮 ±10%，范围 50%–300%，缩放后气泡提示当前百分比。
        /// </summary>
        private void HandleScrollZoom()
        {
            var scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            var sc = PetScaleController.Instance;
            if (sc == null) return;

            // 只有光标在宠物上才响应滚轮（避免与其他 UI 滚动冲突）
            if (!HitTestSelf(Input.mousePosition)) return;

            float step    = 0.10f * Mathf.Sign(scroll);   // 每格 ±10%
            float newFact = Mathf.Clamp(
                sc.ScaleFactor + step,
                PetScaleController.ScaleMin,
                PetScaleController.ScaleMax);

            sc.SetScale(newFact);

            int pct = Mathf.RoundToInt(newFact * 100f);
            PetSpeechBubble.Instance?.Show($"大小 {pct}%", 1.2f);
        }

        /// <summary>把世界坐标夹紧到摄像机可见范围，留约半个身位（0.6 世界单位）边距。</summary>
        private static Vector3 ClampToScreen(Vector3 pos)
        {
            var cam = Camera.main;
            if (cam == null) return pos;
            var z  = Mathf.Abs(cam.transform.position.z);
            var bl = cam.ScreenToWorldPoint(new Vector3(0f,           0f,            z));
            var tr = cam.ScreenToWorldPoint(new Vector3(Screen.width, Screen.height, z));
            const float m = 0.6f;
            return new Vector3(
                Mathf.Clamp(pos.x, bl.x + m, tr.x - m),
                Mathf.Clamp(pos.y, bl.y + m, tr.y - m),
                pos.z);
        }
    }
}
