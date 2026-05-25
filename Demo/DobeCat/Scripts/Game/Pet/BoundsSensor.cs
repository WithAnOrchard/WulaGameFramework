using UnityEngine;

namespace Demo.DobeCat.Game.Pet
{
    /// <summary>
    /// 每帧将桌宠根节点钳制在摄像机视口内，防止宠物游荡到屏幕边缘外。
    /// 在 LateUpdate 运行确保覆盖所有移动计算。
    /// DESIGN.md §4.5 BoundsSensor
    /// </summary>
    public class BoundsSensor : MonoBehaviour
    {
        [Tooltip("距视口边缘的世界单位 margin（正值 = 留边距；负值 = 允许略微出框）。")]
        [SerializeField] private float _margin = 0.3f;

        [Tooltip("是否在越界时平滑推回（false = 瞬间钳制）。")]
        [SerializeField] private bool _smooth = true;

        [SerializeField, Min(1f)] private float _pushBackSpeed = 8f;

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || !cam.orthographic) return;

            var z  = Mathf.Abs(cam.transform.position.z);
            var bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, z));
            var tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, z));

            var minX = bl.x + _margin;
            var maxX = tr.x - _margin;
            var minY = bl.y + _margin;
            var maxY = tr.y - _margin;

            var pos = transform.position;
            var cx  = Mathf.Clamp(pos.x, minX, maxX);
            var cy  = Mathf.Clamp(pos.y, minY, maxY);

            if (_smooth && (cx != pos.x || cy != pos.y))
            {
                pos.x = Mathf.MoveTowards(pos.x, cx, _pushBackSpeed * Time.unscaledDeltaTime);
                pos.y = Mathf.MoveTowards(pos.y, cy, _pushBackSpeed * Time.unscaledDeltaTime);
            }
            else
            {
                pos.x = cx;
                pos.y = cy;
            }
            transform.position = pos;
        }
    }
}
