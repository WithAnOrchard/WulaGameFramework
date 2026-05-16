using System.Collections.Generic;
using EssSystem.Core.Base.Manager;

namespace EssSystem.Core.Presentation.EffectsManager
{
    /// <summary>
    /// 视觉特效服务 —— 持久化 VFX id → prefab path 映射。
    /// <para>启动后 <see cref="EffectsManager"/> 读这份注册表，运行时按 vfxId 查 prefab 路径，
    /// 通过 <c>ResourceManager</c>（bare-string <c>"GetPrefab"</c>）懒加载 prefab。</para>
    /// </summary>
    public class EffectsService : Service<EffectsService>
    {
        // ─── 数据分类 ────────────────────────────────────────────────
        private const string CATEGORY_REGISTRATIONS = "Registrations";

        // ─── 读 / 写 ─────────────────────────────────────────────────

        /// <summary>取 vfxId 对应的 prefab 路径；不存在返 null。</summary>
        public string GetRegistration(string vfxId) =>
            string.IsNullOrEmpty(vfxId) ? null : GetData<string>(CATEGORY_REGISTRATIONS, vfxId);

        /// <summary>覆盖 vfxId → prefab path 映射；空 path 视为移除。</summary>
        public void SetRegistration(string vfxId, string prefabPath)
        {
            if (string.IsNullOrEmpty(vfxId)) return;
            if (string.IsNullOrEmpty(prefabPath)) { RemoveRegistration(vfxId); return; }
            SetData(CATEGORY_REGISTRATIONS, vfxId, prefabPath);
        }

        /// <summary>移除 vfxId 注册。</summary>
        public bool RemoveRegistration(string vfxId) =>
            !string.IsNullOrEmpty(vfxId) && RemoveData(CATEGORY_REGISTRATIONS, vfxId);

        /// <summary>枚举所有 (vfxId, prefabPath) 对。</summary>
        public IEnumerable<(string vfxId, string prefabPath)> GetAllRegistrations()
        {
            if (!_dataStorage.TryGetValue(CATEGORY_REGISTRATIONS, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is string path && !string.IsNullOrEmpty(path))
                    yield return (kv.Key, path);
        }

        /// <summary>清空所有 VFX 注册。</summary>
        public void ResetAllRegistrations()
        {
            if (!_dataStorage.TryGetValue(CATEGORY_REGISTRATIONS, out var dict)) return;
            var keys = new List<string>(dict.Keys);
            foreach (var k in keys) RemoveData(CATEGORY_REGISTRATIONS, k);
        }
    }
}
