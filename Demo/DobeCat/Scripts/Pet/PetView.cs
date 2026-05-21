using UnityEngine;

namespace Demo.DobeCat.Pet
{
    /// <summary>
    /// 桌宠根视觉/命中代理。视觉本体走 <c>EssSystem.Core.Presentation.CharacterManager</c> 注册的角色（挂在子物体上），
    /// 本组件只负责：根级 localScale、朝向翻转、子 Renderer 并集包围盒。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PetView : MonoBehaviour
    {
        [Tooltip("根级视觉缩放（影响所有子 Renderer）。")]
        public float VisualScale = 1f;

        // 兼容旧 PetNetworkSync 写入的字段，保留 getter/setter 但不再使用（占位贴图已删除）
        [System.NonSerialized] public string SpriteResourcePath;
        [System.NonSerialized] public Color PlaceholderColor;

        // 始终关闭本机 SpriteRenderer，仅保留以满足 RequireComponent + 老旧引用
        public bool UseChildRenderers = true;

        private SpriteRenderer _renderer;
        private int _facing = 1;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.enabled = false;
            _renderer.sprite = null;
            transform.localScale = Vector3.one * Mathf.Max(0.01f, VisualScale);
        }

        /// <summary>设置朝向：+1 右，-1 左。仅翻转 localScale.x。</summary>
        public void SetFacing(int dir)
        {
            if (dir == 0 || dir == _facing) return;
            _facing = dir > 0 ? 1 : -1;
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * _facing;
            transform.localScale = s;
        }

        /// <summary>世界包围盒：所有子 Renderer 的并集（角色逐帧 sprite 大小变化时实时反映）。</summary>
        public Bounds WorldBounds
        {
            get
            {
                Bounds? acc = null;
                var rs = GetComponentsInChildren<Renderer>(false);
                for (var i = 0; i < rs.Length; i++)
                {
                    var r = rs[i];
                    if (r == _renderer) continue; // 跳过本机被禁用的 SpriteRenderer
                    if (!r.enabled) continue;
                    var b = r.bounds;
                    if (b.size.sqrMagnitude < 1e-6f) continue;
                    if (acc == null) acc = b; else { var bb = acc.Value; bb.Encapsulate(b); acc = bb; }
                }
                return acc ?? new Bounds(transform.position, Vector3.one * 0.5f);
            }
        }
    }
}
