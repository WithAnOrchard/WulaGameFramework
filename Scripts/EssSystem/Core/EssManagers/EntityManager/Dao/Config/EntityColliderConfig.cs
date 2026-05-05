using System;
using UnityEngine;

namespace EssSystem.EssManager.EntityManager.Dao.Config
{
    /// <summary>
    /// Entity 碰撞体配置 —— 在 Entity 创建时由 <see cref="EntityService"/>
    /// 挂到显示用 Character 的根 GameObject 上（若 Entity 无 Character 则不挂）。
    /// <para>
    /// Tilemap 每格 = 1 unit，所以 <c>Size = (1,1)</c> 即一格方块碰撞。
    /// </para>
    /// </summary>
    [Serializable]
    public class EntityColliderConfig
    {
        /// <summary>碰撞体形状；<see cref="EntityColliderShape.None"/> 表示不挂碰撞体。</summary>
        public EntityColliderShape Shape = EntityColliderShape.None;

        /// <summary>
        /// 尺寸：<c>Box</c> 取 (w,h)；<c>Circle</c> 只用 <c>Size.x</c> 当半径。
        /// 单位 = Tile 格数（1 unit = 1 格）。
        /// </summary>
        public Vector2 Size = Vector2.one;

        /// <summary>相对 Character 根节点的 local 偏移。</summary>
        public Vector2 Offset = Vector2.zero;

        /// <summary>是否为 Trigger（仅触发事件，不做物理阻挡）。</summary>
        public bool IsTrigger = false;

        public EntityColliderConfig() { }

        public EntityColliderConfig(EntityColliderShape shape, Vector2 size, Vector2 offset = default, bool isTrigger = false)
        {
            Shape = shape;
            Size = size;
            Offset = offset;
            IsTrigger = isTrigger;
        }

        /// <summary>快捷：一格方块（1×1）。</summary>
        public static EntityColliderConfig OneCellBox(bool isTrigger = false) =>
            new EntityColliderConfig(EntityColliderShape.Box, Vector2.one, Vector2.zero, isTrigger);
    }
}
