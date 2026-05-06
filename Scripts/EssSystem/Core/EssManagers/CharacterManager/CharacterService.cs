using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.EssManagers.Manager;
using EssSystem.EssManager.CharacterManager.Dao;
using EssSystem.EssManager.CharacterManager.Runtime;

namespace EssSystem.EssManager.CharacterManager
{
    /// <summary>
    /// Character 业务服务 —— 直接 C# API，无 Event 暴露。
    /// <list type="bullet">
    /// <item>持久化 <see cref="CharacterConfig"/>（<see cref="CAT_CONFIGS"/>），可由 JSON 修改</item>
    /// <item>运行时 <see cref="Character"/> 实例仅内存（<see cref="CAT_INSTANCES"/>，未写盘）</item>
    /// </list>
    /// </summary>
    public class CharacterService : Service<CharacterService>
    {
        #region 数据分类

        /// <summary>持久化：所有 <see cref="CharacterConfig"/></summary>
        public const string CAT_CONFIGS   = "Configs";

        /// <summary>仅内存：所有运行时 <see cref="Character"/> 实例（未写盘）</summary>
        public const string CAT_INSTANCES = "Characters";

        #endregion

        /// <summary>实例字典属于运行时态（持有 Unity View 引用），绝不持久化。
        /// 否则下次 Play 加载会得到 View 已被 Unity 销毁的"僵尸 Character"，
        /// 导致 <c>CreateCharacter</c> 命中重复并跳过 GameObject 重建。</summary>
        protected override bool IsTransientCategory(string category) => category == CAT_INSTANCES;

        #region Event 名常量（供 [EventListener] 订阅）

        /// <summary>
        /// 角色动画某帧触发的**广播**事件名。由 <see cref="CharacterPartView"/> 在播放到
        /// <see cref="Dao.CharacterActionConfig.FrameEvents"/> 登记的帧时发出。
        /// <para>参数：<c>[GameObject owner, string eventName, string actionName, int frameIndex]</c></para>
        /// <para>用 <c>[EventListener(CharacterService.EVT_FRAME_EVENT)]</c> 订阅。</para>
        /// </summary>
        public const string EVT_FRAME_EVENT = "CharacterFrameEvent";

        #endregion

        protected override void Initialize()
        {
            base.Initialize();
            Log("CharacterService 初始化完成", Color.green);
        }

        // ─────────────────────────────────────────────────────────────
        #region Config Management

        /// <summary>注册或覆盖配置（持久化）。</summary>
        public void RegisterConfig(CharacterConfig config)
        {
            if (config == null || string.IsNullOrEmpty(config.ConfigId))
            {
                LogWarning("忽略空配置或缺 ConfigId 的配置");
                return;
            }
            SetData(CAT_CONFIGS, config.ConfigId, config);
            Log($"注册角色配置: {config.ConfigId} ({config.DisplayName})", Color.blue);
        }

        public CharacterConfig GetConfig(string configId) => GetData<CharacterConfig>(CAT_CONFIGS, configId);

        public IEnumerable<CharacterConfig> GetAllConfigs()
        {
            if (!_dataStorage.TryGetValue(CAT_CONFIGS, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is CharacterConfig c) yield return c;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Instance Management

        /// <summary>取运行时实例。</summary>
        public Character GetCharacter(string instanceId) =>
            string.IsNullOrEmpty(instanceId) ? null : GetData<Character>(CAT_INSTANCES, instanceId);

        /// <summary>枚举所有运行时实例。</summary>
        public IEnumerable<Character> GetAllCharacters()
        {
            if (!_dataStorage.TryGetValue(CAT_INSTANCES, out var dict)) yield break;
            foreach (var kv in dict)
                if (kv.Value is Character c) yield return c;
        }

        /// <summary>
        /// 实例化 Character —— 必须在主线程调用（内部 <c>new GameObject</c>）。
        /// 重复 <paramref name="instanceId"/> 不会重建，返回已有实例。
        /// </summary>
        public Character CreateCharacter(string configId, string instanceId, Transform parent = null, Vector3? worldPosition = null)
        {
            if (string.IsNullOrEmpty(configId) || string.IsNullOrEmpty(instanceId))
            {
                LogWarning("CreateCharacter 参数无效");
                return null;
            }

            if (HasData(CAT_INSTANCES, instanceId))
            {
                LogWarning($"实例 {instanceId} 已存在，忽略重复创建");
                return GetCharacter(instanceId);
            }

            var config = GetConfig(configId);
            if (config == null)
            {
                LogWarning($"配置不存在: {configId}");
                return null;
            }

            var go = new GameObject($"Character_{instanceId}");
            if (parent != null) go.transform.SetParent(parent, false);
            if (worldPosition.HasValue) go.transform.position = worldPosition.Value;

            var view = go.AddComponent<CharacterView>();
            view.Build(instanceId, config);

            var character = new Character
            {
                InstanceId = instanceId,
                ConfigId   = configId,
                Config     = config,
                View       = view,
            };
            foreach (var kv in view.Parts)
                character.Parts[kv.Key] = kv.Value;

            // 仅内存（不调 SetData，避免触发存盘）
            if (!_dataStorage.ContainsKey(CAT_INSTANCES))
                _dataStorage[CAT_INSTANCES] = new Dictionary<string, object>();
            _dataStorage[CAT_INSTANCES][instanceId] = character;

            Log($"创建角色实例: {instanceId} (config={configId})", Color.green);
            return character;
        }

        /// <summary>销毁 Character 实例 + GameObject。</summary>
        public bool DestroyCharacter(string instanceId)
        {
            var c = GetCharacter(instanceId);
            if (c == null) return false;

            if (c.View != null && c.View.gameObject != null)
                Object.Destroy(c.View.gameObject);

            if (_dataStorage.TryGetValue(CAT_INSTANCES, out var dict))
                dict.Remove(instanceId);

            Log($"销毁角色实例: {instanceId}", Color.yellow);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Action Control

        /// <summary>播放动作；partId 为空则对所有 Dynamic 部件。</summary>
        public bool PlayAction(string instanceId, string actionName, string partId = null)
        {
            var c = GetCharacter(instanceId);
            if (c == null || c.View == null) return false;
            c.View.Play(actionName, partId);
            return true;
        }

        /// <summary>停止动作；partId 为空则停止所有部件。</summary>
        public bool StopAction(string instanceId, string partId = null)
        {
            var c = GetCharacter(instanceId);
            if (c == null || c.View == null) return false;
            c.View.Stop(partId);
            return true;
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Transform Control（缩放 / 移动）

        /// <summary>
        /// 设置 Character 根节点的 <c>localScale</c>（对整个 Character 统一缩放）。
        /// <para>配置级初值见 <see cref="CharacterConfig.RootScale"/>，本方法用于运行时动态调整（例如生长、放大特效）。</para>
        /// </summary>
        public bool SetScale(string instanceId, Vector3 scale)
        {
            var c = GetCharacter(instanceId);
            if (c == null || c.View == null) return false;
            c.View.transform.localScale = scale;
            return true;
        }

        /// <summary>设置 Character 世界坐标（绝对位置）。Entity 驱动下通常由 EntityService.Tick 同步，手动控制 Character 专用场景使用。</summary>
        public bool SetPosition(string instanceId, Vector3 worldPosition)
        {
            var c = GetCharacter(instanceId);
            if (c == null || c.View == null) return false;
            c.View.transform.position = worldPosition;
            return true;
        }

        /// <summary>相对当前位置平移 <paramref name="delta"/>。</summary>
        public bool Move(string instanceId, Vector3 delta)
        {
            var c = GetCharacter(instanceId);
            if (c == null || c.View == null) return false;
            c.View.transform.position += delta;
            return true;
        }

        #endregion
    }
}
