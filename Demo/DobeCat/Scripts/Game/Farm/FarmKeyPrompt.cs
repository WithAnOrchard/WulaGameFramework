using UnityEngine;
using Demo.DobeCat.Sys.Platform.Windows;

namespace Demo.DobeCat.Game.Farm
{
    /// <summary>
    /// 农场按键提示 —— 在玩家靠近农田且手持种子（有空格子）时显示空格键图标。
    /// <list type="bullet">
    /// <item>精灵从 Resources/UI/Keys/Space-Key.png 运行时对半切分（左=普通，右=按下）。</item>
    /// <item>由 FarmWorldController 在 SpawnTiles 后创建并每帧调用 Refresh。</item>
    /// </list>
    /// </summary>
    public class FarmKeyPrompt : MonoBehaviour
    {
        [Tooltip("提示图标相对玩家位置的世界偏移（默认居中在头顶上方）。")]
        public Vector3 Offset = new Vector3(0f, 0.5f, -0.1f);

        [Tooltip("图标世界尺寸（单位=米）。")]
        [Min(0.01f)] public float DisplaySize = 0.24f;

        private GameObject   _go;
        private SpriteRenderer _sr;
        private Sprite _sprNormal;
        private Sprite _sprPressed;

        // ── 初始化 ────────────────────────────────────────────────

        private void Awake()
        {
            _go = new GameObject("FarmKeyPrompt_Space");
            _go.transform.SetParent(transform, worldPositionStays: false);
            _sr = _go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 20;
            _go.SetActive(false);

            LoadSprites();
            if (_sprNormal != null) _sr.sprite = _sprNormal;
        }

        private void LoadSprites()
        {
            var tex = Resources.Load<Texture2D>("UI/Keys/Space-Key");
            if (tex == null)
            {
                Debug.LogWarning("[FarmKeyPrompt] 找不到 Resources/UI/Keys/Space-Key.png");
                return;
            }
            int hw = tex.width / 2;
            int h  = tex.height;
            // 左半 = 普通（未按下），右半 = 按下
            _sprNormal  = Sprite.Create(tex, new Rect(0,  0, hw, h), new Vector2(0.5f, 0.5f), 100f);
            _sprPressed = Sprite.Create(tex, new Rect(hw, 0, hw, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // ── 每帧更新 ──────────────────────────────────────────────

        /// <summary>
        /// 由 FarmWorldController.Update 每帧调用。
        /// </summary>
        /// <param name="farmCenter">农场中心世界坐标。</param>
        /// <param name="show">是否满足提示条件（靠近 + 手持种子 + 有空格子）。</param>
        public void Refresh(Vector3 farmCenter, bool show)
        {
            if (_go == null) return;

            if (_go.activeSelf != show)
                _go.SetActive(show);

            if (!show) return;

            // 位置：农场上方偏移
            _go.transform.position = farmCenter + Offset;

            // 根据精灵高度自适应缩放
            if (_sprNormal != null)
            {
                float sprH = _sprNormal.bounds.size.y;
                float s    = sprH > 0.001f ? DisplaySize / sprH : 1f;
                _go.transform.localScale = Vector3.one * s;
            }

            // 切换按下 / 普通状态
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            bool spaceDown = (Win32Native.GetAsyncKeyState(Win32Native.VK_SPACE) & 0x8000) != 0;
#else
            bool spaceDown = Input.GetKey(KeyCode.Space);
#endif
            if (_sr != null)
                _sr.sprite = (spaceDown && _sprPressed != null) ? _sprPressed : _sprNormal;
        }
    }
}
