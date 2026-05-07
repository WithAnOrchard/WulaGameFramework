using System.Collections.Generic;

namespace EssSystem.Core.Util
{
    /// <summary>
    ///     线程安全的 <see cref="List{T}"/> 对象池 —— 减少热路径上 List 的 alloc / GC。
    /// </summary>
    /// <remarks>
    ///     <para>用法：</para>
    ///     <code>
    ///     var list = ListPool&lt;int&gt;.Rent();
    ///     try { /* use list */ }
    ///     finally { ListPool&lt;int&gt;.Return(list); }
    ///     </code>
    ///     <para>归还前框架自动 <c>Clear()</c>，调用方无需关心残留数据。</para>
    /// </remarks>
    public static class ListPool<T>
    {
        private const int MaxPoolSize = 50;
        private const int MaxRetainCapacity = 256;

        private static readonly Stack<List<T>> _pool = new();
        private static readonly object _gate = new();

        /// <summary>租用一个空 List。</summary>
        public static List<T> Rent()
        {
            lock (_gate)
            {
                if (_pool.Count > 0)
                {
                    var list = _pool.Pop();
                    list.Clear();
                    return list;
                }
            }
            return new List<T>();
        }

        /// <summary>归还 List 到池中。容量过大或池已满则放弃归还。</summary>
        public static void Return(List<T> list)
        {
            if (list == null) return;
            list.Clear();
            lock (_gate)
            {
                if (_pool.Count < MaxPoolSize && list.Capacity <= MaxRetainCapacity)
                    _pool.Push(list);
            }
        }
    }
}
