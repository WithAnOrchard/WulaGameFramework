using System;
using EssSystem.EssManager.MapManager.Dao.Generator;

namespace EssSystem.EssManager.MapManager.Dao.Config
{
    /// <summary>
    /// 地图配置抽象基类 —— 所有地图模板（Perlin / 手绘 / WFC / ...）的共同字段 + 生成器工厂入口。
    /// <para>
    /// **持久化兼容**：派生类必须使用 <c>[Serializable]</c> + 公开字段 + 默认构造函数，
    /// 走 <see cref="UnityEngine.JsonUtility"/>（Service 内部）。
    /// </para>
    /// <para>
    /// **多态读取**：Service 持久化时记录 <c>AssemblyQualifiedName</c>，反序列化能正确还原派生类型，
    /// 因此可以直接以 <c>MapConfig</c> 基类持有/查询，运行时按需向下转型或调用
    /// <see cref="CreateGenerator"/>。
    /// </para>
    /// </summary>
    [Serializable]
    public abstract class MapConfig
    {
        /// <summary>配置唯一 ID（Service 持久化主键）。</summary>
        public string ConfigId;

        /// <summary>显示名（调试 / UI 用，非主键）。</summary>
        public string DisplayName;

        /// <summary>区块边长（Tile 数）。默认 16。各模板可改但建议 2 的幂以方便除法/位运算。</summary>
        public int ChunkSize = 16;

        protected MapConfig() { }

        protected MapConfig(string configId, string displayName)
        {
            ConfigId = configId;
            DisplayName = displayName;
        }

        /// <summary>
        /// 根据本配置的当前字段构造一个生成器实例（无副作用，可重复调用）。
        /// 派生类必须实现。
        /// </summary>
        public abstract IMapGenerator CreateGenerator();
    }
}
