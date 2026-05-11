using UnityEngine;

namespace Demo.Tribe.Background
{
    /// <summary>
    /// 视差背景单层 —— 按相机 X 位置驱动 <c>localX = -Repeat(camX * factor, wrapWidth)</c>，
    /// 让本 GO 下挂的几份图片副本（首/中/尾）组成横向无限循环。
    /// <para>
    /// 自身应作为 <see cref="UnityEngine.Camera"/> 子节点存在；这样 Y 自动跟随镜头，
    /// 仅 X 由本脚本接管做"视差 + 循环"。
    /// </para>
    /// <para>
    /// <b>parallaxFactor</b> 语义（基于 camera.x → 屏幕可见偏移）：
    /// <list type="bullet">
    /// <item>0 = 静止贴屏（远天）</item>
    /// <item>1 = 完全跟世界（近景，相机走多快它就退多快）</item>
    /// <item>0~1 中间值 = 视差感</item>
    /// </list>
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("用于读取 worldX 的相机；通常 = Main Camera。空值时自动取 Camera.main。")]
        [SerializeField] private Camera _camera;

        [Tooltip("视差倍率：0=不动；1=完全跟随玩家移动方向反向滚动。")]
        [SerializeField, Range(0f, 2f)] private float _parallaxFactor = 0.5f;

        [Tooltip("一份图片的世界宽度（已乘缩放）。layerLocalX 会按此值取模循环。")]
        [SerializeField, Min(0.01f)] private float _wrapWidth = 10f;

        [Tooltip("Y 偏移（相对相机 Y）；默认 0 = 层中心居屏幕中心。")]
        [SerializeField] private float _localY = 0f;

        [Tooltip("Z 世界坐标；仅位置用，叠序仍由 sortingOrder 决定。")]
        [SerializeField] private float _localZ = 10f;

        public void Configure(Camera camera, float parallaxFactor, float wrapWidth, float localY, float localZ)
        {
            _camera = camera;
            _parallaxFactor = parallaxFactor;
            _wrapWidth = wrapWidth;
            _localY = localY;
            _localZ = localZ;
        }

        private void LateUpdate()
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null || _wrapWidth <= 0f) return;
            var camPos = _camera.transform.position;
            // 视差 + 循环（世界坐标）：
            //   屏幕相对位 = layer.worldX - camX = -Repeat(camX*p, W) ∈ [-W, 0]
            //   p=0 → 完全跟相机；p=1 → 各副本在世界中固定位置。
            var wx = camPos.x - Mathf.Repeat(camPos.x * _parallaxFactor, _wrapWidth);
            // Y：层中心居镜头中心（加 _localY 偏移）。
            var wy = camPos.y + _localY;
            transform.position = new Vector3(wx, wy, _localZ);
        }
    }
}
