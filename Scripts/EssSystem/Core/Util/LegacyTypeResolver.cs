using System;
using System.Collections.Generic;

namespace EssSystem.Core.Util
{
    /// <summary>
    ///     标记类型曾经使用的旧 namespace / 类型名，使老存档（按 <see cref="Type.AssemblyQualifiedName"/> 写入）
    ///     在重命名 / 移动类后仍能被反序列化命中。
    /// </summary>
    /// <remarks>
    ///     <para>支持多次标注（一个类经历多次重命名都列上）。</para>
    ///     <para>匹配规则：传入存档里的 type 名，先按当前 AQN 直接 <see cref="Type.GetType(string)"/>；
    ///     失败则截掉 ", AssemblyName" 部分，按 FullName 在 [FormerName] 注册表里查表；
    ///     再失败则用 Type.GetType(fullNameOnly) 兜底。</para>
    ///     <para>典型用法：</para>
    ///     <code>
    ///     // 以前在 EssSystem.EssManager.MapManager.Dao.PerlinMapConfig
    ///     // 现在搬到了 EssSystem.Core.EssManagers.Gameplay.MapManager.TopDown2D.Dao.Templates.TopDownRandom.Config.PerlinMapConfig
    ///     [FormerName("EssSystem.EssManager.MapManager.Dao.PerlinMapConfig")]
    ///     [Serializable]
    ///     public class PerlinMapConfig : MapConfig { ... }
    ///     </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
    public sealed class FormerNameAttribute : Attribute
    {
        /// <summary>类型曾用的 FullName（namespace.TypeName），不必带 ", AssemblyName" 部分。</summary>
        public string LegacyFullName { get; }

        public FormerNameAttribute(string legacyFullName) => LegacyFullName = legacyFullName;
    }

    /// <summary>
    ///     存档反序列化阶段的类型解析器：先尝试当前 AQN，失败回退到 [FormerName] 注册表。
    /// </summary>
    /// <remarks>
    ///     <para>由 <c>Service&lt;T&gt;.DeserializeValue</c> 使用。业务方 / 框架使用者只需在搬迁过的类上加 <see cref="FormerNameAttribute"/>。</para>
    ///     <para>注册表懒加载，第一次 <see cref="Resolve"/> 调用时扫描所有用户程序集（跳过系统/Unity 程序集）。</para>
    /// </remarks>
    public static class LegacyTypeResolver
    {
        private static Dictionary<string, Type> _legacyMap;

        /// <summary>把存档里写入的类型名解析为运行时 Type。失败返回 null。</summary>
        public static Type Resolve(string typeName)
        {
            if (string.IsNullOrEmpty(typeName) || typeName == "null") return null;

            // 1) 走当前 AQN — 命中直接返回
            var t = Type.GetType(typeName);
            if (t != null) return t;

            // 2) 截掉 ", AssemblyName" 后缀，按 FullName 查 [FormerName] 表
            var commaIdx = typeName.IndexOf(',');
            var fullName = commaIdx >= 0 ? typeName.Substring(0, commaIdx).Trim() : typeName;

            EnsureMap();
            if (_legacyMap.TryGetValue(fullName, out var mapped)) return mapped;

            // 3) 兜底：仅 FullName 无 Assembly 提示，让 CLR 在已加载程序集里自查
            return Type.GetType(fullName);
        }

        /// <summary>主动重建注册表（编辑器热重载或新增 Assembly 后可调用）。</summary>
        public static void Refresh()
        {
            _legacyMap = null;
            EnsureMap();
        }

        private static void EnsureMap()
        {
            if (_legacyMap != null) return;
            var map = new Dictionary<string, Type>(StringComparer.Ordinal);
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (AssemblyUtils.IsSystemAssembly(asm)) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (System.Reflection.ReflectionTypeLoadException ex) { types = ex.Types; }
                foreach (var t in types)
                {
                    if (t == null) continue;
                    var attrs = t.GetCustomAttributes(typeof(FormerNameAttribute), false);
                    if (attrs.Length == 0) continue;
                    foreach (FormerNameAttribute a in attrs)
                    {
                        if (string.IsNullOrEmpty(a.LegacyFullName)) continue;
                        map[a.LegacyFullName] = t;   // 后到覆盖前到（多次重命名取最新映射）
                    }
                }
            }
            _legacyMap = map;
        }
    }
}
