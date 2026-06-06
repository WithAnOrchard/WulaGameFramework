using UnityEngine;

namespace Demo.Cubic.Map
{
    /// <summary>
    /// 地面标识符组件（3D 版）。
    /// <para>
    /// 附加到地面 GameObject 上用于识别。3D 化后用 <see cref="Collider"/> 判定（旧版是 <see cref="Collider2D"/>）。
    /// PlayerController 的 <c>OnCollisionEnter</c> 回调里会调 <see cref="IsGround(Collider)"/> 判断落地。
    /// </para>
    /// </summary>
    public class GroundIdentifier : MonoBehaviour
    {
        /// <summary>静态检查：是否是地面（GameObject 上挂了 GroundIdentifier）。</summary>
        public static bool IsGround(GameObject obj)
        {
            if (obj == null) return false;
            return obj.GetComponent<GroundIdentifier>() != null;
        }

        /// <summary>静态检查：是否是地面（3D Collider）。</summary>
        public static bool IsGround(Collider collider)
        {
            if (collider == null) return false;
            return collider.gameObject.GetComponent<GroundIdentifier>() != null;
        }
    }
}
