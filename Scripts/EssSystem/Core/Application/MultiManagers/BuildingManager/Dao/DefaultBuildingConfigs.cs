using UnityEngine;
using EssSystem.Core.Application.MultiManagers.BuildingManager.Dao.Config;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao.Config;

namespace EssSystem.Core.Application.MultiManagers.BuildingManager.Dao
{
    /// <summary>
    /// 内置 4 个示范建筑模板。业务侧可直接覆盖：用同 ConfigId 再 RegisterConfig 即可。
    /// 这 4 个用于演示 <c>BuildingManager</c> 4 大类用法：
    ///   1. BarbedWire     —— 接触伤害（<c>IContactDamage</c>）
    ///   2. HealingTower   —— 范围治疗（<c>IAura</c>）
    ///   3. Wall           —— 纯阻挡（无 capability，IsTrigger = false）
    ///   4. Harvester      —— 周期产出（<c>IHarvester</c>）
    ///
    /// <para>注：CharacterConfigId 留空时 <c>EntityService</c> 不会调用 CharacterManager 创建外观；
    /// 业务侧需要让建筑有视觉时，提供对应的 CharacterConfig 并填进 <see cref="BuildingConfig.CharacterConfigId"/>。
    /// 这里用占位 id 让"框架可跑"，渲染由 Demo 层补。</para>
    /// </summary>
    public static class DefaultBuildingConfigs
    {
        // 建议外部模块用同 id 重新 Register 来覆盖外观 —— 这里只是占位
        public const string ID_BARBED_WIRE   = "BarbedWire";
        public const string ID_HEALING_TOWER = "HealingTower";
        public const string ID_WALL          = "Wall";
        public const string ID_HARVESTER     = "Harvester";

        public static BuildingConfig BuildBarbedWire() => new BuildingConfig(ID_BARBED_WIRE, "铁丝网", characterConfigId: null)
            .WithCollider(new EntityColliderConfig(EntityColliderShape.Box, Vector2.one, Vector2.zero, isTrigger: true))
            .WithMaxHp(30f)
            .WithCost("wood",  2, "木材")
            .WithCost("iron",  1, "铁块")
            // 完成后每秒对 1.0 半径内造成 5 伤
            .OnComplete(e => e.CanDamageOnContact(damagePerTick: 5f, radius: 1.0f, tickInterval: 1f, damageType: "BarbedWire"));

        public static BuildingConfig BuildHealingTower() => new BuildingConfig(ID_HEALING_TOWER, "治疗塔", characterConfigId: null)
            .WithCollider(new EntityColliderConfig(EntityColliderShape.Box, Vector2.one, Vector2.zero, isTrigger: true))
            .WithMaxHp(120f)
            .WithCost("wood",   5, "木材")
            .WithCost("iron",   3, "铁块")
            .WithCost("crystal", 1, "水晶")
            // 完成后每秒治疗 3.5 半径内所有 IDamageable 5 点（除自身）
            .OnComplete(e => e.EmitAura(healPerTick: 5f, radius: 3.5f, tickInterval: 1f, includeSelf: false));

        public static BuildingConfig BuildWall() => new BuildingConfig(ID_WALL, "石墙", characterConfigId: null)
            .WithCollider(new EntityColliderConfig(EntityColliderShape.Box, Vector2.one, Vector2.zero, isTrigger: false))
            .WithMaxHp(200f)
            .WithCost("stone", 3, "石材");
            // OnComplete 不挂能力 —— 墙的"功能"就是不可穿透的碰撞体本身

        public static BuildingConfig BuildHarvester() => new BuildingConfig(ID_HARVESTER, "采集器", characterConfigId: null)
            .WithCollider(new EntityColliderConfig(EntityColliderShape.Box, Vector2.one, Vector2.zero, isTrigger: true))
            .WithMaxHp(50f)
            .WithCost("wood", 4, "木材")
            .WithCost("iron", 2, "铁块")
            // 完成后每 5 秒往 player 容器丢 1 个 wood
            .OnComplete(e => e.Harvest(itemId: "wood", amount: 1, interval: 5f, targetInventoryId: "player"));
    }
}
