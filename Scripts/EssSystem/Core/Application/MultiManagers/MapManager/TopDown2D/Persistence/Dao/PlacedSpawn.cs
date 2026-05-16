using System;

namespace EssSystem.Core.Application.MultiManagers.MapManager.TopDown2D.Persistence.Dao
{
    /// <summary>
    /// 玩家手动在区块内放置的实体记录（v2 预留字段，v1 不写也不读，但保留 JSON schema 兼容）。
    /// <para>
    /// 与 spawn 装饰器自动生成的实体不同，这类实体不由规则确定性派生 ——
    /// 必须在存档里完整描述，以便重新加载该区块时复现。
    /// </para>
    /// </summary>
    [Serializable]
    public struct PlacedSpawn
    {
        /// <summary>区块内本地坐标 X。</summary>
        public int LocalX;
        /// <summary>区块内本地坐标 Y。</summary>
        public int LocalY;
        /// <summary>EntityManager 中已注册的 EntityConfigId。</summary>
        public string EntityConfigId;
        /// <summary>玩家给的额外标签（用于 instanceId 生成、自定义元数据等；可空）。</summary>
        public string ExtraInstanceTag;
    }
}
