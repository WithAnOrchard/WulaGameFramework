using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.EssManager.EntityManager.Dao.Capabilities;
using EssSystem.EssManager.EntityManager.Dao.Config;

namespace EssSystem.EssManager.EntityManager.Dao
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
    }
}
