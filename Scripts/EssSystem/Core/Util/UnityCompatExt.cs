using UnityEngine;

namespace EssSystem.Core.Util
{
    /// <summary>
    /// Unity 兼容性扩展方法 —— 绕过 <c>??</c> / <c>?.</c> 对 <see cref="UnityEngine.Object"/> fake-null 的陷阱。
    /// <para>Unity 重载了 <c>== null</c> 运算符来识别已销毁但 C# 引用未清理的 fake-null 状态，
    /// 而 <c>??</c> / <c>is null</c> 走 C# 原生判空，会绕过 Unity 重载。</para>
    /// <para>本类提供安全的 Get-or-Add 模式，内部用 <c>== null</c>，团队无需手写重复的 Get + if + Add 三行套路。</para>
    /// </summary>
    public static class UnityCompatExt
    {
        /// <summary>
        /// 安全获取组件，不存在则添加。内部用 Unity <c>== null</c> 运算符，正确处理 fake-null。
        /// </summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="go">目标 GameObject。</param>
        /// <returns>已有或新添加的组件实例。</returns>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        /// <summary>
        /// 安全获取组件，不存在则添加。作用于 Component 的 gameObject。
        /// </summary>
        public static T GetOrAddComponent<T>(this Component comp) where T : Component
        {
            return comp.gameObject.GetOrAddComponent<T>();
        }

        /// <summary>
        /// Unity-safe null 检查 —— 对 <see cref="UnityEngine.Object"/> 派生类型使用 Unity 重载的 <c>== null</c>。
        /// <para>用于无法避免泛型场景下需要统一 null 判断的地方。</para>
        /// </summary>
        public static bool IsUnityNull(this UnityEngine.Object obj)
        {
            return obj == null;
        }

        /// <summary>
        /// Unity-safe 非空检查。
        /// </summary>
        public static bool IsUnityAlive(this UnityEngine.Object obj)
        {
            return obj != null;
        }
    }
}
