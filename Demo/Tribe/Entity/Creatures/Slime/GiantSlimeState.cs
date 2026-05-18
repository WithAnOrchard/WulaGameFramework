using UnityEngine;
using EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities;

namespace Demo.Tribe.Entities
{
    /// <summary>
    /// 史莱姆"巨大化"运行时状态 —— 由 <see cref="Slime"/>.BuildGiantBuff 闭包
    /// 创建挂载（框架 <c>BuffEffect</c> 在 Apply 时调用工厂），
    /// 缓存所有被修改的字段以便 Buff 到期时精准还原。
    /// <list type="bullet">
    /// <item>视觉：Visual.localScale × <see cref="ScaleMultiplier"/>，并上移补偿避免视觉沉地。</item>
    /// <item>碰撞：CircleCollider2D.radius × <see cref="ScaleMultiplier"/>。</item>
    /// <item>生命：DamageableComponent.SetMaxHp(MaxHp × Hp 倍率, refill=true)。</item>
    /// <item>防御：DamageableComponent.DamageReduction = <see cref="DamageReduction"/>。</item>
    /// <item>跳跃：克隆 config，将 SmallHopH/V × HopMul、HopCooldown / HopMul，Reconfigure hop。</item>
    /// </list>
    /// </summary>
    [DisallowMultipleComponent]
    public class GiantSlimeState : MonoBehaviour
    {
        public float ScaleMultiplier = 2f;
        public float HpMultiplier = 3f;
        public float DamageReduction = 0.5f;
        public float HopMultiplier = 1.5f;

        // ─── 原值缓存 ─────────────────────────────────────
        private bool _applied;
        private Vector3 _origVisualScale;
        private Vector3 _origVisualLocalPos;
        private float _origColliderRadius;
        private float _origMaxHp;
        private float _origDamageReduction;
        private TribeCreatureConfig _origConfig;       // hop._config 的"前任"引用
        private Color _origRendererColor = Color.white;
        private bool _origColorCached;

        /// <summary>应用巨大化效果。重复 Apply 直接跳过（已生效）。</summary>
        public void Apply()
        {
            if (_applied) return;

            // ① 视觉 + 抬升（让放大后的 sprite 不沉到地面以下）
            //    新视觉走 CharacterManager —— 从 TribeCreature.CharacterRoot 拿到 Character 根
            var visual = GetVisualRoot();
            if (visual != null)
            {
                _origVisualScale = visual.localScale;
                _origVisualLocalPos = visual.localPosition;
                var sr = visual.GetComponentInChildren<SpriteRenderer>();
                // 用 sprite 真实世界高度计算下沉补偿（Pivot=Center 时底部下沉一半）
                var bumpY = 0f;
                if (sr != null && sr.sprite != null)
                {
                    var worldHeight = sr.bounds.size.y; // 缩放前的当前世界高度
                    bumpY = worldHeight * 0.5f * (ScaleMultiplier - 1f);
                }
                visual.localScale = _origVisualScale * ScaleMultiplier;
                visual.localPosition = _origVisualLocalPos + new Vector3(0f, bumpY, 0f);

                // 红色染色提示玩家"这只是 Boss"
                if (sr != null)
                {
                    _origRendererColor = sr.color;
                    _origColorCached = true;
                    sr.color = new Color(1f, 0.55f, 0.55f, _origRendererColor.a);
                }
            }

            // ② 碰撞半径
            var col = GetComponent<CircleCollider2D>();
            if (col != null)
            {
                _origColliderRadius = col.radius;
                col.radius = _origColliderRadius * ScaleMultiplier;
            }

            // ③ 跳跃强度：克隆 config + Reconfigure hop（绝不修改 preset 共享实例）
            var hop = GetComponent<TribeSlimeHopBehavior>();
            if (hop != null && hop.CurrentConfig != null)
            {
                _origConfig = hop.CurrentConfig;
                var giantCfg = CloneConfig(_origConfig);
                giantCfg.SmallHopHorizontal *= HopMultiplier;
                giantCfg.SmallHopVertical *= HopMultiplier;
                giantCfg.BigHopHorizontal *= HopMultiplier;
                giantCfg.BigHopVertical *= HopMultiplier;
                giantCfg.HopCooldown = Mathf.Max(0.2f, _origConfig.HopCooldown / HopMultiplier);
                giantCfg.ContactDamage = _origConfig.ContactDamage * HopMultiplier;
                hop.Reconfigure(giantCfg);
            }

            // ④ 生命 + 减伤（DamageableComponent 提供 SetMaxHp + DamageReduction）
            var handle = GetComponent<EssSystem.Core.Application.SingleManagers.EntityManager.Runtime.EntityHandle>();
            var dmg = handle?.Entity?.Get<IDamageable>() as DamageableComponent;
            if (dmg != null)
            {
                _origMaxHp = dmg.MaxHp;
                _origDamageReduction = dmg.DamageReduction;
                dmg.SetMaxHp(_origMaxHp * HpMultiplier, refill: true);
                dmg.DamageReduction = Mathf.Clamp01(DamageReduction);
            }

            _applied = true;
        }

        /// <summary>还原所有修改。Buff 到期或宿主销毁前调用，幂等安全。</summary>
        public void Revert()
        {
            if (!_applied) return;
            _applied = false;

            var visual = GetVisualRoot();
            if (visual != null)
            {
                visual.localScale = _origVisualScale;
                visual.localPosition = _origVisualLocalPos;
                if (_origColorCached)
                {
                    var sr = visual.GetComponentInChildren<SpriteRenderer>();
                    if (sr != null) sr.color = _origRendererColor;
                }
            }

            var col = GetComponent<CircleCollider2D>();
            if (col != null && _origColliderRadius > 0f) col.radius = _origColliderRadius;

            var hop = GetComponent<TribeSlimeHopBehavior>();
            if (hop != null && _origConfig != null) hop.Reconfigure(_origConfig);

            var handle = GetComponent<EssSystem.Core.Application.SingleManagers.EntityManager.Runtime.EntityHandle>();
            var dmg = handle?.Entity?.Get<IDamageable>() as DamageableComponent;
            if (dmg != null && _origMaxHp > 0f)
            {
                dmg.SetMaxHp(_origMaxHp, refill: false);
                dmg.DamageReduction = _origDamageReduction;
            }
        }

        private void OnDestroy() => Revert();

        /// <summary>视觉根 —— 从 <see cref="TribeCreature.CharacterRoot"/>（CharacterManager 创建的子节点）拿；
        /// 兼容地兜底 <c>transform.Find("Visual")</c> 以防 TribeCreature 不在或回归测试旧路径。</summary>
        private Transform GetVisualRoot()
        {
            var creature = GetComponent<TribeCreature>();
            if (creature != null && creature.CharacterRoot != null) return creature.CharacterRoot;
            return transform.Find("Visual"); // legacy fallback —— 应不再触发
        }

        // 浅克隆：TribeCreatureConfig 是普通字段类，MemberwiseClone 足够。
        private static TribeCreatureConfig CloneConfig(TribeCreatureConfig src)
        {
            // 没有 [Serializable] DeepCopy 工具时，手动复制每个字段会很丑。
            // 这里用反射兜底：值类型字段直接拷贝；引用类型（Color/string）共享即可。
            var dst = new TribeCreatureConfig();
            foreach (var f in typeof(TribeCreatureConfig).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                f.SetValue(dst, f.GetValue(src));
            }
            return dst;
        }
    }
}
