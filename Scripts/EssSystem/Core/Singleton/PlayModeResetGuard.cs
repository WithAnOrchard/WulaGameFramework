using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace EssSystem.Core.Singleton
{
    /// <summary>
    /// **Editor "Reload Domain" 关闭场景下的关键守卫**：
    /// 进入 Play 模式时（包括第一次和后续重启），强制把所有 <see cref="SingletonNormal{T}"/> 派生
    /// 单例的 <c>_instance</c> 重置为 null，使得新 Play session 拿到的是全新实例。
    /// <para>
    /// 不这样做会出现：上次 Play 残留的 <c>_dataStorage</c> / <c>_spawnQueue</c> /
    /// <c>EntityService</c> instance 字典 等状态在新 Play 里继续生效，
    /// 表现为"重新开始游戏后实体不再生 / 创建实体提示已存在"等诡异 bug。
    /// </para>
    /// <para>
    /// Domain Reload 开启时，C# 静态字段会自动清空，本守卫为 no-op。
    /// </para>
    /// </summary>
    internal static class PlayModeResetGuard
    {
#if UNITY_EDITOR
        // 仅在 Editor 起作用 —— Build 场景每次进程启动都是全新静态状态，不需要扫描重置。
        // 使用 SubsystemRegistration —— Unity 在每次 Play 进入时最早期触发，早于任何 Awake。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
#endif
        private static void ResetAllSingletons()
        {
            try
            {
                ResetSingletonNormalInstances();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayModeResetGuard] Singleton 重置异常: {ex.Message}");
            }

            try
            {
                ResetStaticRegistries();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayModeResetGuard] 静态注册表重置异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 扫描所有已加载程序集，对每个继承自 <c>SingletonNormal&lt;T&gt;</c> 的具体类型调用其静态
        /// <c>DestroyInstance()</c>（基类已在 <see cref="SingletonNormal{T}"/> 提供）。
        /// </summary>
        private static void ResetSingletonNormalInstances()
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var ai = 0; ai < assemblies.Length; ai++)
            {
                var asm = assemblies[ai];
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                catch { continue; }

                for (var ti = 0; ti < types.Length; ti++)
                {
                    var t = types[ti];
                    if (t == null || t.IsAbstract || t.IsGenericTypeDefinition) continue;
                    if (!IsSingletonNormalDerivative(t, out var closedBase)) continue;

                    var destroy = closedBase.GetMethod("DestroyInstance",
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    if (destroy != null)
                    {
                        try { destroy.Invoke(null, null); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[PlayModeResetGuard] {t.FullName}.DestroyInstance() 抛异常: {ex.InnerException?.Message ?? ex.Message}");
                        }
                    }
                    // 兜底：不论 Dispose 成败，都强制把基类 _instance 静态字段置 null。
                    // 防止 Service<T>.Dispose() 内的文件 IO/Save 抛异常时，旧实例继续被复用。
                    var instanceField = closedBase.GetField("_instance",
                        BindingFlags.NonPublic | BindingFlags.Static);
                    try { instanceField?.SetValue(null, null); }
                    catch { /* swallow */ }
                }
            }
        }

        /// <summary>
        /// 清空已知的进程级静态注册表。新增 Registry 时按需补充到这里。
        /// </summary>
        private static void ResetStaticRegistries()
        {
            // MapTemplateRegistry: 内部 _templates 字典需要清空，否则 MapManager.Initialize
            // 走 Contains() 判断会跳过重新注册，新 Play session 仍然引用上次的实例（无害但不一致）。
            var regType = Type.GetType(
                "EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Templates.MapTemplateRegistry, " +
                FindAssemblyNameOf("EssSystem.Core.EssManagers.Gameplay.MapManager.Dao.Templates.MapTemplateRegistry"));
            if (regType != null)
            {
                var dictField = regType.GetField("_templates", BindingFlags.NonPublic | BindingFlags.Static);
                if (dictField?.GetValue(null) is System.Collections.IDictionary d) d.Clear();
            }
        }

        /// <summary>检查 <paramref name="t"/> 是否继承自 <c>SingletonNormal&lt;T&gt;</c>，并输出闭合后的基类型。</summary>
        private static bool IsSingletonNormalDerivative(Type t, out Type closedBase)
        {
            closedBase = null;
            var cur = t.BaseType;
            while (cur != null)
            {
                if (cur.IsGenericType && cur.GetGenericTypeDefinition() == typeof(SingletonNormal<>))
                {
                    closedBase = cur;
                    return true;
                }
                cur = cur.BaseType;
            }
            return false;
        }

        private static string FindAssemblyNameOf(string fullName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                if (assemblies[i].GetType(fullName) != null) return assemblies[i].GetName().Name;
            }
            return string.Empty;
        }
    }
}
