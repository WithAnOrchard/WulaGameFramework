using UnityEngine;

namespace Demo.Cubic.Map
{
    /// <summary>
    /// 地面标识符组件
    /// 附加到地面 GameObject 上用于识别
    /// </summary>
    public class GroundIdentifier : MonoBehaviour
    {
        /// <summary>
        /// 静态检查：是否是地面
        /// </summary>
        public static bool IsGround(GameObject obj)
        {
            if (obj == null) return false;
            return obj.GetComponent<GroundIdentifier>() != null;
        }

        /// <summary>
        /// 静态检查：是否是地面（通过 Collider2D）
        /// </summary>
        public static bool IsGround(Collider2D collider)
        {
            if (collider == null) return false;
            return collider.gameObject.GetComponent<GroundIdentifier>() != null;
        }
    }
}
