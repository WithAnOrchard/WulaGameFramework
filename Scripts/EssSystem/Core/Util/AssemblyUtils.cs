using System.Reflection;

namespace EssSystem.Core.Util
{
    /// <summary>
    ///     程序集工具类 — 提供程序集相关的公用判断方法
    /// </summary>
    public static class AssemblyUtils
    {
        /// <summary>
        ///     判断是否为系统/引擎程序集（不包含用户代码）
        /// </summary>
        public static bool IsSystemAssembly(Assembly asm)
        {
            var name = asm.GetName().Name;
            if (string.IsNullOrEmpty(name)) return true;
            return name.StartsWith("System.", System.StringComparison.Ordinal)
                   || name.StartsWith("Microsoft.", System.StringComparison.Ordinal)
                   || name.StartsWith("Unity.", System.StringComparison.Ordinal)
                   || name.StartsWith("UnityEngine", System.StringComparison.Ordinal)
                   || name.StartsWith("UnityEditor", System.StringComparison.Ordinal)
                   || name.StartsWith("Mono.", System.StringComparison.Ordinal)
                   || name.StartsWith("nunit.", System.StringComparison.Ordinal)
                   || name == "mscorlib"
                   || name == "netstandard"
                   || name == "System";
        }
    }
}
