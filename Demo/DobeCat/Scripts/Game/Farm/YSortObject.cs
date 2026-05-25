using UnityEngine;

namespace Demo.DobeCat.Game.Farm
{
    /// <summary>
    /// 挂在任意带 SpriteRenderer 的 GameObject 上，每帧根据 Y 坐标动态更新
    /// sortingOrder，实现与农场植物一致的 Y 轴排序（Y 越低 = 越靠前）。
    /// 公式与 FarmWorldController 植物精灵保持一致：5000 - round(worldY * 100)
    /// </summary>
    public class YSortObject : MonoBehaviour
    {
        [Tooltip("在 Y 排序基础上的偏移，正值靠前，负值靠后")]
        public int SortOffset = 0;

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
        }

        private void LateUpdate()
        {
            if (_sr != null)
                _sr.sortingOrder = 5000 - Mathf.RoundToInt(transform.position.y * 100) + SortOffset;
        }
    }
}
