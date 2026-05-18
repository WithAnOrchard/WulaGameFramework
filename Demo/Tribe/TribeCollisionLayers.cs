using UnityEngine;

namespace Demo.Tribe
{
    /// <summary>
    /// 部落 Demo 物理 Layer 矩阵：
    /// <list type="bullet">
    /// <item>怪物 ↔ 怪物：忽略碰撞（避免互相挤压）</item>
    /// <item>怪物 ↔ 掉落物：忽略碰撞（避免怪物推走死亡掉落 / 采集掉落）</item>
    /// </list>
    /// <para>用 Unity 内置 Layer Collision Matrix（运行时 <see cref="Physics2D.IgnoreLayerCollision(int,int,bool)"/>），
    /// 比 <c>Physics2D.IgnoreCollision</c> 的两两配对省心 —— 全场景任意时刻新建的 Collider2D 只要落在对应 layer
    /// 上即自动遵守矩阵规则。</para>
    /// <para>使用约定：
    /// <list type="number">
    /// <item>怪物（<see cref="Demo.Tribe.Entities.TribeCreature"/>）创建后调 <see cref="MarkCreature"/></item>
    /// <item>掉落物（InventoryManager 生成的 PickableItem）落地后调 <see cref="MarkDrop"/></item>
    /// </list>
    /// 不依赖 TagManager.asset 的 Layer Name —— 直接用 user layer index（8-31 在任何 Unity 项目都存在）。
    /// </para>
    /// </summary>
    public static class TribeCollisionLayers
    {
        /// <summary>怪物 layer index（user layer）。</summary>
        public const int LAYER_CREATURE = 8;

        /// <summary>掉落物 layer index（user layer）。</summary>
        public const int LAYER_DROP = 9;

        private static bool _matrixApplied;

        /// <summary>幂等：首次调用时设置 layer 矩阵；后续调用 no-op。</summary>
        public static void EnsureMatrix()
        {
            if (_matrixApplied) return;
            _matrixApplied = true;

            // 怪物之间不互相碰撞（包括 contact damager 触发与 RB 推挤）
            Physics2D.IgnoreLayerCollision(LAYER_CREATURE, LAYER_CREATURE, true);
            // 怪物不推动掉落物
            Physics2D.IgnoreLayerCollision(LAYER_CREATURE, LAYER_DROP, true);
            // 掉落物之间也不互相挤压（避免叠堆抖动）
            Physics2D.IgnoreLayerCollision(LAYER_DROP, LAYER_DROP, true);
        }

        /// <summary>把 GameObject 标为 Creature 层。空对象安全。</summary>
        public static void MarkCreature(GameObject go)
        {
            if (go == null) return;
            EnsureMatrix();
            SetLayerRecursively(go, LAYER_CREATURE);
        }

        /// <summary>把 GameObject 标为 Drop 层。空对象安全。</summary>
        public static void MarkDrop(GameObject go)
        {
            if (go == null) return;
            EnsureMatrix();
            SetLayerRecursively(go, LAYER_DROP);
        }

        /// <summary>子节点挂了 Collider2D（如 PickupTrigger）也要落到同一 layer，否则矩阵规则不生效。</summary>
        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }
    }
}
