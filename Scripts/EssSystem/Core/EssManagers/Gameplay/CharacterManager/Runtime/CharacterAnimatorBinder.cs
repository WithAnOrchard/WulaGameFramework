using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime
{
    /// <summary>
    /// 把 <see cref="CharacterManager"/> 创建的 Character GameObject 绑定到本节点子层的可复用组件。
    /// 负责：
    /// <list type="bullet">
    /// <item>在 <see cref="Spawn"/> 时通过 <see cref="EventProcessor"/> 触发
    /// <see cref="CharacterManager.EVT_CREATE_CHARACTER"/> 创建角色，并把根节点 SetParent 到自身</item>
    /// <item>每帧 LateUpdate 锁回 <see cref="VisualPositionOffset"/> / <see cref="VisualEulerOffset"/>
    /// （动画 root motion 已在 <see cref="CharacterPartView3D"/> 处理；本组件只稳定外层位姿）</item>
    /// <item>提供 <see cref="ResolveAction"/> 工具：按精确名 → 关键词子串 fallback 找 config 内的实际动作名</item>
    /// <item><see cref="Play"/> 缓存当前 action，仅在变化时触发 <see cref="CharacterManager.EVT_PLAY_ACTION"/></item>
    /// <item><see cref="SetModelVisible"/> 一键切所有 Renderer.enabled（第一人称隐藏自身模型场景常用）</item>
    /// </list>
    /// <para>
    /// 跨模块解耦：通过 <c>EventProcessor</c> 调 CharacterManager，不直接 <c>using</c> CharacterService。
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterAnimatorBinder : MonoBehaviour
    {
        // ─────────────────────────────────────────────────────────────
        #region Inspector

        [Header("Character")]
        [Tooltip("使用的 CharacterConfig ID（= FBX 文件名）。CharacterManager 启动时自动注册 Resources/ 下所有 FBX。")]
        [SerializeField] private string _configId = "";

        [Tooltip("Character 实例 ID（场景内唯一）。空则用 GameObject.name + GUID 短码。")]
        [SerializeField] private string _instanceId = "";

        [Tooltip("Character 视觉缩放（仅作用在子节点；不影响碰撞体大小）。")]
        [SerializeField, Min(0.1f)] private float _visualScale = 1f;

        [Tooltip("Character 节点的本地欧拉偏移 —— 大多 MC 风格 FBX 默认面朝 -Z，需 (0,180,0) 翻正。")]
        [SerializeField] private Vector3 _visualEulerOffset = new Vector3(0f, 180f, 0f);

        [Tooltip("Character 节点的本地位置偏移。\n通常保持 0；CharacterPartView3D 内部已锁定 rig 根骨骼防陷地。")]
        [SerializeField] private Vector3 _visualPositionOffset = Vector3.zero;

        [Tooltip("Awake 后自动 Spawn；关掉则需要外部代码手动调 Spawn()。")]
        [SerializeField] private bool _autoSpawnOnStart = true;

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Public API

        /// <summary>当前角色的根 Transform（绑定后可用），未绑定则返回本节点。</summary>
        public Transform CharacterRoot => _characterRoot != null ? _characterRoot : transform;

        /// <summary>当前实例 ID（Spawn 后稳定）。</summary>
        public string InstanceId => _instanceId;

        public string ConfigId => _configId;
        public Vector3 VisualEulerOffset    { get => _visualEulerOffset;    set => _visualEulerOffset = value; }
        public Vector3 VisualPositionOffset { get => _visualPositionOffset; set => _visualPositionOffset = value; }
        public float   VisualScale          { get => _visualScale;          set { _visualScale = value; if (_characterRoot != null) _characterRoot.localScale = Vector3.one * _visualScale; } }

        /// <summary>Inspector 默认覆写（在 Awake/Spawn 之前）。</summary>
        public void SetConfigId(string id)   { if (!string.IsNullOrEmpty(id)) _configId   = id; }
        public void SetInstanceId(string id) { if (!string.IsNullOrEmpty(id)) _instanceId = id; }

        /// <summary>
        /// 触发 EVT_CREATE_CHARACTER 创建角色并绑定到自己。已绑定则不重复创建。
        /// 返回是否成功。
        /// </summary>
        public bool Spawn()
        {
            if (_characterRoot != null) return true;
            if (string.IsNullOrEmpty(_configId))
            {
                Debug.LogWarning($"[CharacterAnimatorBinder] {name}：ConfigId 为空，跳过 Spawn");
                return false;
            }
            if (string.IsNullOrEmpty(_instanceId))
                _instanceId = $"{name}_{System.Guid.NewGuid().ToString("N").Substring(0, 6)}";

            if (!EventProcessor.HasInstance)
            {
                Debug.LogWarning($"[CharacterAnimatorBinder] {name}：EventProcessor 未就绪，跳过 Spawn");
                return false;
            }

            var result = EventProcessor.Instance.TriggerEventMethod(
                CharacterManager.EVT_CREATE_CHARACTER,
                new List<object>
                {
                    _configId,
                    _instanceId,
                    transform,            // parent
                    transform.position,   // 初始世界坐标 = 本节点
                });

            if (!ResultCode.IsOk(result) || result.Count < 2)
            {
                Debug.LogError($"[CharacterAnimatorBinder] {name}：创建 Character 失败 configId='{_configId}', instanceId='{_instanceId}'");
                return false;
            }

            // result[1] 兼容多种返回类型
            Transform root = null;
            switch (result[1])
            {
                case Transform t: root = t; break;
                case Character ch: root = ch.View?.transform; break;
                case GameObject go: root = go.transform; break;
            }
            if (root == null)
            {
                Debug.LogError($"[CharacterAnimatorBinder] {name}：EVT_CREATE_CHARACTER 返回类型不识别: {result[1]?.GetType()}");
                return false;
            }

            BindCharacterRoot(root);
            return true;
        }

        /// <summary>切换/启动一个动作。同名重复调用会被跳过（避免 spam EVT_PLAY_ACTION）。</summary>
        public void Play(string actionName)
        {
            if (string.IsNullOrEmpty(actionName) || actionName == _currentAction) return;
            _currentAction = actionName;
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(_instanceId)) return;
            EventProcessor.Instance.TriggerEventMethod(
                CharacterManager.EVT_PLAY_ACTION,
                new List<object> { _instanceId, actionName });
        }

        /// <summary>显示/隐藏所有子 Renderer（第一人称下隐藏自身模型常用）。</summary>
        public void SetModelVisible(bool visible)
        {
            if (_characterRoot == null) return;
            foreach (var r in _characterRoot.GetComponentsInChildren<Renderer>(true))
                r.enabled = visible;
        }

        /// <summary>
        /// 按"精确名 → 关键词子串"两段 fallback，在当前 config 的可用动作名里查 best match。
        /// 未注册或都不命中则返回 null。
        /// </summary>
        /// <param name="preferredName">优先精确名（不分大小写）。</param>
        /// <param name="keywords">退化关键词，按顺序找包含该子串（不分大小写）的第一个。</param>
        public string ResolveAction(string preferredName, params string[] keywords)
        {
            EnsureNamesCached();
            if (_cachedActionNames == null || _cachedActionNames.Count == 0) return null;

            if (!string.IsNullOrEmpty(preferredName))
                foreach (var n in _cachedActionNames)
                    if (string.Equals(n, preferredName, System.StringComparison.OrdinalIgnoreCase)) return n;

            if (keywords != null)
                foreach (var kw in keywords)
                    foreach (var n in _cachedActionNames)
                        if (n.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0) return n;
            return null;
        }

        /// <summary>当前 config 内全部 action 名（已去重；若 service 未就绪返回空 list）。</summary>
        public IReadOnlyList<string> AvailableActionNames
        {
            get
            {
                EnsureNamesCached();
                return _cachedActionNames ?? (IReadOnlyList<string>)System.Array.Empty<string>();
            }
        }

        #endregion

        // ─────────────────────────────────────────────────────────────
        #region Runtime

        private Transform _characterRoot;
        private string    _currentAction;
        private List<string> _cachedActionNames;

        private void Start()
        {
            if (_autoSpawnOnStart) Spawn();
        }

        private void LateUpdate()
        {
            if (_characterRoot == null) return;
            // 锁住外层 transform（根 motion 已在 CharacterPartView3D 内层锁定）
            _characterRoot.localPosition = _visualPositionOffset;
            _characterRoot.localRotation = Quaternion.Euler(_visualEulerOffset);
        }

        private void BindCharacterRoot(Transform root)
        {
            _characterRoot = root;
            _characterRoot.SetParent(transform, worldPositionStays: false);
            _characterRoot.localPosition = _visualPositionOffset;
            _characterRoot.localRotation = Quaternion.Euler(_visualEulerOffset);
            _characterRoot.localScale    = Vector3.one * _visualScale;
        }

        private void EnsureNamesCached()
        {
            if (_cachedActionNames != null) return;
            _cachedActionNames = new List<string>();

            var service = CharacterService.Instance;
            if (service == null) return;
            var cfg = service.GetConfig(_configId);
            if (cfg?.Parts == null) return;
            foreach (var part in cfg.Parts)
            {
                if (part?.Animations == null) continue;
                foreach (var a in part.Animations)
                    if (!string.IsNullOrEmpty(a?.ActionName) && !_cachedActionNames.Contains(a.ActionName))
                        _cachedActionNames.Add(a.ActionName);
            }
        }

        #endregion
    }
}
