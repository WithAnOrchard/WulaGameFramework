using System;
using System.Collections.Generic;
using UnityEngine;

namespace EssSystem.Core.Application.MultiManagers.BuildingManager.Dao.Config
{
    public enum BuildingColliderShape
    {
        None = 0,
        Box = 1,
        Circle = 2,
        Box3D = 3,
        Sphere3D = 4,
        Capsule3D = 5,
    }

    [Serializable]
    public class BuildingColliderConfig
    {
        public BuildingColliderShape Shape = BuildingColliderShape.None;
        public Vector2 Size = Vector2.one;
        public Vector2 Offset = Vector2.zero;
        public bool IsTrigger = false;

        public BuildingColliderConfig() { }

        public BuildingColliderConfig(BuildingColliderShape shape, Vector2 size, Vector2 offset = default, bool isTrigger = false)
        {
            Shape = shape;
            Size = size;
            Offset = offset;
            IsTrigger = isTrigger;
        }
    }

    /// <summary>
    /// 建筑模板配置。建筑运行时通过 EntityManager 事件创建为静态实体，
    /// 增加了：材料清单（建造期）+ HP（可被破坏）+ 一段在"建造完成"瞬间施加的链式能力工厂。
    ///
    /// <para>由于 <see cref="ApplyCapabilities"/> 是 <see cref="Action{T}"/>（不可序列化），
    /// 本配置**不进入持久化**，需要在 GameManager 启动时通过代码注册一次（参 <c>DefaultBuildingConfigs</c>）。</para>
    /// </summary>
    public class BuildingConfig
    {
        /// <summary>模板唯一 id。</summary>
        public string ConfigId;

        /// <summary>UI 显示名（建造 HUD / 提示框用）。</summary>
        public string DisplayName;

        /// <summary>完成态外观 —— 走 CharacterManager 的 <c>CharacterConfig.ConfigId</c>。</summary>
        public string CharacterConfigId;

        /// <summary>可选：建造中的外观（一般是半透明骨架）。留空则建造中沿用 <see cref="CharacterConfigId"/>。</summary>
        public string PendingCharacterConfigId;

        /// <summary>碰撞体配置 —— 阻挡型建筑（墙）填 <c>IsTrigger = false</c>，光环 / 接触伤害用 trigger。</summary>
        public BuildingColliderConfig Collider = new BuildingColliderConfig();

        /// <summary>世界空间偏移（与 <c>EntityConfig.SpawnOffset</c> 同义）。</summary>
        public Vector3 SpawnOffset = Vector3.zero;

        /// <summary>最大 HP；&lt;= 0 表示不可破坏（永久建筑）。</summary>
        public float MaxHp = 50f;

        /// <summary>建造材料清单。空表示直接完成态（无需建造阶段）。</summary>
        public List<BuildingCost> Costs = new List<BuildingCost>();

        /// <summary>
        /// 完成时对底层实体 ID 施加能力的回调。跨模块能力注入应走 EntityManager 事件。
        /// </summary>
        public Action<string> ApplyCapabilities;

        public BuildingConfig() { }

        public BuildingConfig(string configId, string displayName, string characterConfigId)
        {
            ConfigId = configId;
            DisplayName = displayName;
            CharacterConfigId = characterConfigId;
        }

        // ─── 流式 Builder（与 Entity 的链式风格统一）──────────────────

        public BuildingConfig WithVisual(string characterConfigId, string pendingCharacterConfigId = null)
        {
            CharacterConfigId = characterConfigId;
            PendingCharacterConfigId = pendingCharacterConfigId;
            return this;
        }

        public BuildingConfig WithCollider(BuildingColliderConfig collider)
        {
            Collider = collider ?? new BuildingColliderConfig();
            return this;
        }

        public BuildingConfig WithMaxHp(float maxHp) { MaxHp = maxHp; return this; }

        public BuildingConfig WithSpawnOffset(Vector3 offset) { SpawnOffset = offset; return this; }

        public BuildingConfig WithCost(string itemId, int amount, string displayName = null)
        {
            Costs.Add(new BuildingCost(itemId, amount, displayName));
            return this;
        }

        /// <summary>
        /// 设置完成时施加的能力链。lambda 参数为底层 entityId。
        /// </summary>
        public BuildingConfig OnComplete(Action<string> applyCapabilities)
        {
            ApplyCapabilities = applyCapabilities;
            return this;
        }
    }
}
