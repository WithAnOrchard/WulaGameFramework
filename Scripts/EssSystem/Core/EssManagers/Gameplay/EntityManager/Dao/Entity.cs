using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Capabilities.Default;
using EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao.Config;

namespace EssSystem.Core.EssManagers.Gameplay.EntityManager.Dao
{
    /// <summary>
    /// Entity 运行时实例（<b>非持久化</b>）—— 逻辑状态 + 显示 Character 引用 + 可插拔能力字典。
    /// <para>
    /// 由 <see cref="EntityManager.EntityService"/> 在 <c>CreateEntity</c> 时构建；
    /// 销毁时会级联销毁关联的 Character GameObject 并 <see cref="DetachAllCapabilities"/>。
    /// </para>
    /// <para>
    /// <b>能力系统</b>：用 <c>Add&lt;IDamageable&gt;(new DamageableComponent(100))</c> 挂能力；
    /// 用 <c>Get&lt;IDamageable&gt;()</c> 取；用 <c>Has&lt;IDamageable&gt;()</c> 判定。
    /// 主键是<b>接口类型</b>，不是具体类。
    /// </para>
    /// </summary>
    public class Entity
    {
        /// <summary>实例唯一 ID（场景内唯一，由调用方指定）。</summary>
        public string InstanceId;

        /// <summary>对应的 <see cref="EntityConfig.ConfigId"/>。</summary>
        public string ConfigId;

        /// <summary>原始配置引用（避免后续再查表）。</summary>
        public EntityConfig Config;

        /// <summary>静态 / 动态类别 —— 在 <c>CreateEntity</c> 时从 <see cref="EntityConfig.Kind"/> 拷入。</summary>
        public EntityKind Kind;

        /// <summary>
        /// 关联的 Character 实例 ID —— 纯字符串协议，不依赖 CharacterManager 类型。
        /// 需要控制 Character 时调 <c>EVT_*_CHARACTER</c> 事件（例如 EVT_MOVE_CHARACTER）并传入本 ID。
        /// </summary>
        public string CharacterInstanceId;

        /// <summary>
        /// 缓存的 Character 根 <see cref="Transform"/>（Unity 原生类型，非跨模块耦合）。
        /// 由 EntityService 在 CreateEntity 时从 <c>EVT_CREATE_CHARACTER</c> 返回值中拿到；
        /// 供每帧同步位置、挂载碰撞体等使用。无显示部分时为 null。
        /// </summary>
        public Transform CharacterRoot;

        /// <summary>当前世界坐标（权威位置，供 AI / 物理 / 存档用；Character.View 负责跟随）。</summary>
        public Vector3 WorldPosition;

        // ─── 能力字典 ───────────────────────────────────────────────
        private readonly Dictionary<Type, IEntityCapability> _capabilities = new();

        /// <summary>只读视图：本实体所有挂载的能力。</summary>
        public IReadOnlyDictionary<Type, IEntityCapability> Capabilities => _capabilities;

        /// <summary>
        /// 挂一个能力到本实体。<typeparamref name="T"/> 必须是**接口类型**（<c>IDamageable</c> 等），
        /// 用它作字典主键 —— 同一接口重复挂会覆盖并触发旧实例 <c>OnDetach</c>。
        /// </summary>
        public T Add<T>(T capability) where T : class, IEntityCapability
        {
            if (capability == null) return null;
            var key = typeof(T);
            if (_capabilities.TryGetValue(key, out var old) && !ReferenceEquals(old, capability))
            {
                try { old.OnDetach(this); } catch { /* 业务异常不影响挂载流程 */ }
            }
            _capabilities[key] = capability;
            capability.OnAttach(this);
            return capability;
        }

        /// <summary>取能力（未挂则返回 null）。按**接口类型**查找。</summary>
        public T Get<T>() where T : class, IEntityCapability
            => _capabilities.TryGetValue(typeof(T), out var v) ? v as T : null;

        /// <summary>是否挂了某能力。</summary>
        public bool Has<T>() where T : class, IEntityCapability
            => _capabilities.ContainsKey(typeof(T));

        /// <summary>移除能力，返回是否命中；命中时触发 <c>OnDetach</c>。</summary>
        public bool Remove<T>() where T : class, IEntityCapability
        {
            var key = typeof(T);
            if (!_capabilities.TryGetValue(key, out var cap)) return false;
            _capabilities.Remove(key);
            try { cap.OnDetach(this); } catch { /* swallow */ }
            return true;
        }

        /// <summary>Entity 销毁前由 Service 调用；按挂载反序触发 <c>OnDetach</c>。</summary>
        public void DetachAllCapabilities()
        {
            if (_capabilities.Count == 0) return;
            // 先拷贝避免枚举中修改
            var caps = new List<IEntityCapability>(_capabilities.Values);
            _capabilities.Clear();
            for (var i = caps.Count - 1; i >= 0; i--)
            {
                try { caps[i].OnDetach(this); } catch { /* swallow */ }
            }
        }

        // ─── 链式 Fluent API ─────────────────────────────────────────
        //
        // 设计目标：把"装能力"从 `entity.Add<IDamageable>(new DamageableComponent(100))`
        // 这种两段式调用，简化为像自然语言一样的链：
        //
        //   entity.CanMove(3f).CanAttack(10, 1.5f).CanBeAttacked(100).CanFlash(root);
        //
        // 规则：
        //   - 所有 Can* / Cannot* 方法返回 `this`，可无限链下去
        //   - 主键仍是接口类型，重复调用同名 Can* 会覆盖（参 Add<T> 语义）
        //   - 不会做"GetOrAdd" —— 每次都创建新组件实例并替换
        //   - 想自定义实现：仍可走 `entity.With<T>(myCustomComponent)`
        //   - 想运行时摘掉：`entity.Without<T>()`
        //
        // 这是纯客户端便利封装，运行时行为与 Add<T> 一致。

        /// <summary>通用链式挂载 —— 等价于 <see cref="Add{T}"/>，但返回 <c>this</c> 以便链调。</summary>
        public Entity With<T>(T capability) where T : class, IEntityCapability
        {
            Add(capability);
            return this;
        }

        /// <summary>通用链式卸载 —— 等价于 <see cref="Remove{T}"/>，但返回 <c>this</c>。</summary>
        public Entity Without<T>() where T : class, IEntityCapability
        {
            Remove<T>();
            return this;
        }

        /// <summary>赋予移动能力（<see cref="IMovable"/> + <see cref="MovableComponent"/>）。</summary>
        public Entity CanMove(float moveSpeed)
        {
            Add<IMovable>(new MovableComponent(moveSpeed));
            return this;
        }

        /// <summary>赋予攻击能力（<see cref="IAttacker"/> + <see cref="AttackerComponent"/>）。</summary>
        public Entity CanAttack(float attackPower, float attackRange = 1.5f, float attackCooldown = 0.6f)
        {
            Add<IAttacker>(new AttackerComponent(attackPower, attackRange, attackCooldown));
            return this;
        }

        /// <summary>赋予可被伤害能力（<see cref="IDamageable"/> + <see cref="DamageableComponent"/>）。</summary>
        public Entity CanBeAttacked(float maxHp)
        {
            Add<IDamageable>(new DamageableComponent(maxHp));
            return this;
        }

        /// <summary>
        /// 监听死亡事件 —— 必须在 <see cref="CanBeAttacked"/> 之后链。无 <see cref="IDamageable"/> 时静默忽略。
        /// 等价于 <c>entity.Get&lt;IDamageable&gt;().Died += handler</c>。
        /// </summary>
        public Entity OnDied(Action<Entity, Entity> handler)
        {
            if (handler == null) return this;
            var dmg = Get<IDamageable>();
            if (dmg != null) dmg.Died += handler;
            return this;
        }

        /// <summary>
        /// 监听受伤事件 —— 必须在 <see cref="CanBeAttacked"/> 之后链。无 <see cref="IDamageable"/> 时静默忽略。
        /// </summary>
        public Entity OnDamaged(Action<Entity, Entity, float, string> handler)
        {
            if (handler == null) return this;
            var dmg = Get<IDamageable>();
            if (dmg != null) dmg.Damaged += handler;
            return this;
        }

        /// <summary>挂"暂时不可被攻击"标记（<see cref="IInvulnerable"/>）。配合 <see cref="Without{T}"/> 解除。</summary>
        public Entity CannotBeAttacked(string reason = "Default")
        {
            Add<IInvulnerable>(new InvulnerableComponent(reason, true));
            return this;
        }

        /// <summary>受伤变白闪烁效果 —— root 自动搜索其下所有 SpriteRenderer。</summary>
        public Entity CanFlash(Transform root, float flashDuration = 0.15f, Color? flashColor = null)
        {
            Add<IFlashEffect>(new FlashEffectComponent(root, flashDuration, flashColor));
            return this;
        }

        /// <summary>受伤击退效果 —— 需要 Rigidbody2D 参与方向计算。</summary>
        public Entity CanKnockback(Rigidbody2D rb, float force = 5f, float duration = 0.2f)
        {
            Add<IKnockbackEffect>(new KnockbackEffectComponent(rb, force, duration));
            return this;
        }

        /// <summary>接触伤害（铁丝网类）—— 周期 <see cref="Physics2D.OverlapCircle"/> 扫描造成伤害。</summary>
        public Entity CanDamageOnContact(float damagePerTick, float radius,
            float tickInterval = 1f, string damageType = "ContactDamage", LayerMask layerMask = default)
        {
            Add<IContactDamage>(new ContactDamageComponent(damagePerTick, radius, tickInterval, damageType, layerMask));
            return this;
        }

        /// <summary>光环（治疗塔 / 毒气云）—— 周期 OverlapCircle，对范围内 <see cref="IDamageable"/> 加血或扣血。</summary>
        public Entity EmitAura(float healPerTick, float radius,
            float tickInterval = 1f, LayerMask layerMask = default, bool includeSelf = false)
        {
            Add<IAura>(new AuraComponent(healPerTick, radius, tickInterval, layerMask, includeSelf));
            return this;
        }

        /// <summary>周期采集 —— 定时往 <paramref name="targetInventoryId"/> 容器丢一份物品。</summary>
        public Entity Harvest(string itemId, int amount, float interval, string targetInventoryId = "player")
        {
            Add<IHarvester>(new HarvesterComponent(itemId, amount, interval, targetInventoryId));
            return this;
        }

        /// <summary>
        /// 启用 Utility AI（<see cref="IBrain"/> + <see cref="BrainComponent"/>）。
        /// <para><paramref name="setup"/> 用于添加 Sensor / Consideration / Patrol 等。</para>
        /// <para>互斥规则：自动移除已挂的 <see cref="IPatrol"/>（Brain 接管移动决策）。</para>
        /// </summary>
        public Entity CanThink(Action<BrainComponent> setup = null)
        {
            Remove<IPatrol>();
            var brain = new BrainComponent();
            setup?.Invoke(brain);
            Add<IBrain>(brain);
            return this;
        }
    }
}
