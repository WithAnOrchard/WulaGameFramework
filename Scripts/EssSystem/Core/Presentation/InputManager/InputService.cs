using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Presentation.InputManager
{
    /// <summary>
    /// 输入服务 —— 持久化玩家自定义键位映射（actionName → KeyCodes）。
    /// <para>默认绑定不写入 Service；只在玩家自定义后保存覆盖项，重启自动恢复。</para>
    /// </summary>
    public class InputService : Service<InputService>
    {
        // ─── 数据分类 ────────────────────────────────────────────────
        private const string CATEGORY_BINDINGS = "Bindings";

        // ─── 持久化数据形态 ───────────────────────────────────────────
        // 存 string[]（KeyCode 名）而非 KeyCode[] —— 简化 JSON 序列化兼容
        // 取出时通过 System.Enum.TryParse 还原。

        protected override void Initialize()
        {
            base.Initialize();
        }

        // ─── 读 / 写 ─────────────────────────────────────────────────

        /// <summary>取某个 Action 的玩家自定义绑定。null = 无覆盖（用默认）。</summary>
        public KeyCode[] GetBinding(string action)
        {
            if (string.IsNullOrEmpty(action)) return null;
            var raw = GetData<string[]>(CATEGORY_BINDINGS, action);
            return ParseKeys(raw);
        }

        /// <summary>覆盖玩家自定义绑定；null/空数组 = 移除覆盖。</summary>
        public void SetBinding(string action, KeyCode[] keys)
        {
            if (string.IsNullOrEmpty(action)) return;
            if (keys == null || keys.Length == 0) { RemoveBinding(action); return; }
            var names = new string[keys.Length];
            for (int i = 0; i < keys.Length; i++) names[i] = keys[i].ToString();
            SetData(CATEGORY_BINDINGS, action, names);
        }

        public bool RemoveBinding(string action) =>
            !string.IsNullOrEmpty(action) && RemoveData(CATEGORY_BINDINGS, action);

        /// <summary>枚举所有玩家自定义绑定（不包含默认）。</summary>
        public IEnumerable<(string action, KeyCode[] keys)> GetAllBindings()
        {
            if (!_dataStorage.TryGetValue(CATEGORY_BINDINGS, out var dict)) yield break;
            foreach (var kv in dict)
            {
                var keys = ParseKeys(kv.Value as string[]);
                if (keys != null && keys.Length > 0) yield return (kv.Key, keys);
            }
        }

        /// <summary>清空所有玩家自定义键位（恢复默认）。</summary>
        public void ResetAllBindings()
        {
            if (_dataStorage.TryGetValue(CATEGORY_BINDINGS, out var dict))
            {
                var keys = new List<string>(dict.Keys);
                foreach (var k in keys) RemoveData(CATEGORY_BINDINGS, k);
            }
            Log("玩家自定义键位已清空（恢复默认）", Color.yellow);
        }

        // ─── 内部 ────────────────────────────────────────────────────
        private static KeyCode[] ParseKeys(string[] raw)
        {
            if (raw == null || raw.Length == 0) return null;
            var list = new List<KeyCode>(raw.Length);
            foreach (var s in raw)
                if (!string.IsNullOrEmpty(s) && System.Enum.TryParse<KeyCode>(s, out var k)) list.Add(k);
            return list.Count == 0 ? null : list.ToArray();
        }
    }
}
