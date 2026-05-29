using System;
using UnityEngine;

namespace EssSystem.Core.Presentation.UIManager.Theme
{
    /// <summary>
    /// 通用主题管理器泛型基类。
    /// <para>提供：预设数组、当前索引、PlayerPrefs 持久化、<see cref="OnThemeChanged"/> 广播。</para>
    /// 使用方式：
    /// <code>
    /// public static class MyTheme : ThemeManager&lt;MyThemeData&gt;
    /// {
    ///     protected override string PrefKey      => "MyApp.ThemeIndex";
    ///     protected override MyThemeData[] BuildPresets() => new[] { ... };
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="T">主题数据类型，需实现 <see cref="IThemeData"/>。</typeparam>
    public abstract class ThemeManager<T> where T : IThemeData
    {
        private static int    _index;
        private static T[]    _presets;

        /// <summary>主题变更广播（主线程）。</summary>
        public static event Action OnThemeChanged;

        /// <summary>PlayerPrefs 存档键，由子类提供。</summary>
        protected abstract string PrefKey { get; }

        /// <summary>预设主题数组，由子类提供，首次访问时构建并缓存。</summary>
        protected abstract T[] BuildPresets();

        /// <summary>所有预设。</summary>
        public T[] Presets => _presets ??= BuildPresets();

        /// <summary>当前主题。</summary>
        public T Current => Presets[_index];

        /// <summary>当前主题索引。</summary>
        public int CurrentIndex => _index;

        /// <summary>从 PlayerPrefs 恢复上次选择。应在早期启动时调用一次。</summary>
        public void LoadSaved()
        {
            _index = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Presets.Length - 1);
        }

        /// <summary>切换主题并通知所有监听者重建 UI。</summary>
        public void Apply(int index)
        {
            index = Mathf.Clamp(index, 0, Presets.Length - 1);
            if (_index == index) return;
            _index = index;
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();
            try { OnThemeChanged?.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }
}
