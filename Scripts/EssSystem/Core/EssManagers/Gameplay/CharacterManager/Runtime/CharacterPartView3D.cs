using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime
{
    /// <summary>
    /// 3D 单部件 View —— 通过 ResourceManager 加载 <see cref="CharacterPartConfig.PrefabPath"/>
    /// 指定的 Prefab 并实例化为子节点，使用 <see cref="UnityEngine.Animator"/> 切换动作状态。
    /// <para>每个动作 = 一个 Animator State：用 <see cref="CharacterActionConfig.ResolveAnimatorState"/> 解析名字，
    /// 通过 <see cref="UnityEngine.Animator.CrossFadeInFixedTime(int, float, int)"/> 切换。</para>
    /// <para>非循环动作完成检测：<c>StateInfo.normalizedTime ≥ 1 &amp;&amp; !inTransition</c> 时触发
    /// <see cref="CharacterPartView.OnActionComplete"/>。帧事件通过
    /// <see cref="CharacterActionConfig.NormalizedTimeEvents"/> 在 normalizedTime 跨越阈值时广播。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPartView3D : CharacterPartView
    {
        /// <summary>实例化出来的 Prefab 根 Transform（部件本体）。</summary>
        public Transform PrefabInstance { get; private set; }

        /// <summary>Prefab 上的 Animator（如果有）。</summary>
        public Animator Animator { get; private set; }

        // 当前动作运行态
        private CharacterActionConfig _currentAction;
        private bool  _playing;
        private bool  _completeFired;
        private float _lastNormalizedTime;

        // Rig 根骨骼锁定 —— 解决无 Avatar 时 clip 内 root/Hip Y 关键帧让模型下沉的问题
        // 基线值在 Animator 第一次采样之后捕获（取 idle pose 的 Y），不是 prefab 的 raw 默认
        private Transform _rigRootBone;
        private Vector3   _rigRootInitialLocalPos;
        private Transform _prefabRootTransform;     // go.transform —— 也要锁（path="" 曲线会写这里）
        private Vector3   _prefabRootInitialLocalPos;
        private bool      _baselineCaptured;

        // 3D 部件天然有 Animator —— 始终可作 pivot
        public override bool CanPivotComplete => true;

        #region Setup / Public API

        protected override void OnSetup()
        {
            BuildPrefabInstance();

            if (!string.IsNullOrEmpty(Config.DefaultActionName))
                Play(Config.DefaultActionName);
        }

        public override bool Play(string actionName)
        {
            if (Config == null || Animator == null) return false;

            var action = Config.GetAction(actionName);
            if (action == null) return false;

            var stateName = action.ResolveAnimatorState();
            if (string.IsNullOrEmpty(stateName)) return false;

            var stateHash = Animator.StringToHash(stateName);
            var layer     = Mathf.Clamp(action.AnimatorLayer, 0, Mathf.Max(0, Animator.layerCount - 1));
            if (!Animator.HasState(layer, stateHash))
            {
                var available = ListControllerStates();
                Debug.LogWarning(
                    $"[CharacterPartView3D] {Config.PartId}：Animator 不含 state '{stateName}' (layer {layer})。\n" +
                    $"  Controller='{Animator.runtimeAnimatorController?.name}'，可用 state：[{available}]");
                return false;
            }

            if (action.CrossFadeDuration > 0f)
                Animator.CrossFadeInFixedTime(stateHash, action.CrossFadeDuration, layer, 0f);
            else
                Animator.Play(stateHash, layer, 0f);

            Animator.speed         = 1f;
            _currentAction         = action;
            _playing               = true;
            _completeFired         = false;
            _lastNormalizedTime    = 0f;
            return true;
        }

        public override void Stop()
        {
            if (Animator != null) Animator.speed = 0f;
            _playing = false;
        }

        /// <summary>诊断辅助：列出当前 controller 内所有 state 名（逗号分隔）。</summary>
        private string ListControllerStates()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return "<no controller>";
#if UNITY_EDITOR
            if (Animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController ac)
            {
                var names = new List<string>();
                foreach (var layer in ac.layers)
                    foreach (var st in layer.stateMachine.states) names.Add(st.state.name);
                return string.Join(", ", names);
            }
#endif
            // 运行时退化：列 clip 名（Builder 生成的 controller state 名 = clip 名）
            var clipNames = new List<string>();
            foreach (var c in Animator.runtimeAnimatorController.animationClips)
                if (c != null) clipNames.Add(c.name);
            return string.Join(", ", clipNames);
        }

        #endregion

        #region Build prefab

        private void BuildPrefabInstance()
        {
            // 清理旧实例（重复 Setup 容错）
            if (PrefabInstance != null)
            {
                if (Application.isPlaying) Destroy(PrefabInstance.gameObject);
                else                       DestroyImmediate(PrefabInstance.gameObject);
                PrefabInstance = null;
                Animator = null;
            }

            if (string.IsNullOrEmpty(Config.PrefabPath)) return;

            var prefab = LoadPrefab(Config.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterPartView3D] {Config.PartId}：加载 Prefab 失败 → {Config.PrefabPath}");
                return;
            }

            var go = Instantiate(prefab, transform);
            go.name = Config.PartId;
            // Prefab 自身的本地变换由 Setup 应用在父节点（this），子节点保持单位
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            PrefabInstance = go.transform;
            Animator       = go.GetComponentInChildren<Animator>();

            // FBX Animation Type=None 或导入设置不带 Animator 时，自动补一个挂在根节点上。
            // Generic 动画通过骨骼 transform 路径作用，不强依赖 Avatar；clip 内 curve 会按 path 应用到子节点。
            if (Animator == null)
            {
                Animator = go.AddComponent<Animator>();
                Debug.Log($"[CharacterPartView3D] {Config.PartId}：FBX 上无 Animator，已自动添加到根节点");
            }

            {
                // 自动按 fbxPath 加载同名 .controller（CharacterConfigFactory 在 Editor 下会确保它存在）
                if (Animator.runtimeAnimatorController == null)
                {
                    var ctrl = Resources.Load<RuntimeAnimatorController>(Config.PrefabPath);
#if UNITY_EDITOR
                    // Editor fallback：刚生成的 .controller 可能未进 Resources 索引，直接用 AssetDatabase 找
                    if (ctrl == null)
                    {
                        var fileName = System.IO.Path.GetFileName(Config.PrefabPath);
                        var guids = UnityEditor.AssetDatabase.FindAssets(
                            $"t:AnimatorController {fileName}");
                        foreach (var g in guids)
                        {
                            var p = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                            var noExt = System.IO.Path.ChangeExtension(p, null).Replace('\\', '/');
                            if (noExt.EndsWith("/Resources/" + Config.PrefabPath, System.StringComparison.Ordinal) ||
                                noExt.EndsWith("Resources/" + Config.PrefabPath, System.StringComparison.Ordinal))
                            {
                                ctrl = UnityEditor.AssetDatabase
                                    .LoadAssetAtPath<RuntimeAnimatorController>(p);
                                if (ctrl != null) break;
                            }
                        }
                    }
#endif
                    if (ctrl != null)
                    {
                        Animator.runtimeAnimatorController = ctrl;
                        Debug.Log($"[CharacterPartView3D] {Config.PartId}：已绑定 Controller '{ctrl.name}'");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[CharacterPartView3D] {Config.PartId}：Resources/{Config.PrefabPath} 同名未找到 AnimatorController —— 动画不会播放。" +
                            $"运行 Tools/Character/Build AnimatorController From Selected FBX 后再试。");
                    }
                }
                // 关键：关 root motion —— 让 Animator 提取 clip 内 root translation/rotation curve，不写到骨骼。
                // 这样 walk 类动画的 Hip Y 关键帧就不会让模型陷地。
                Animator.applyRootMotion = false;
            }

            // 只找好引用；基线值（_rigRootInitialLocalPos / _prefabRootInitialLocalPos）
            // 推迟到第一次 LateUpdate 捕获 —— 那时 Animator 已经采样完默认 state（idle）
            // 直接拿 prefab 的 raw localPosition 是错的：idle clip 会把 body_1 抬到正确视觉高度，
            // raw 默认通常更低（rig 原点在腰部 → 静止显示需要靠 clip 抬）。
            _rigRootBone         = FindRigRootBone(go.transform);
            _prefabRootTransform = go.transform;
            _baselineCaptured    = false;
            if (_rigRootBone != null)
                Debug.Log($"[CharacterPartView3D] {Config.PartId}：rig root bone='{_rigRootBone.name}'（基线将在首次 Animator 采样后捕获）");
            else
                Debug.LogWarning($"[CharacterPartView3D] {Config.PartId}：未找到 rig 根骨骼，walk 可能下沉");

#if UNITY_EDITOR
            // 诊断：列出 controller 内所有 clip 中带 localPosition 曲线的 transform path
            DumpClipPositionCurves();
#endif
        }

#if UNITY_EDITOR
        private void DumpClipPositionCurves()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return;
            var seen = new HashSet<string>();
            foreach (var clip in Animator.runtimeAnimatorController.animationClips)
            {
                if (clip == null) continue;
                var bindings = UnityEditor.AnimationUtility.GetCurveBindings(clip);
                var paths = new List<string>();
                foreach (var b in bindings)
                {
                    if (b.propertyName.StartsWith("m_LocalPosition", System.StringComparison.Ordinal))
                    {
                        var key = $"{b.path}|{b.propertyName}";
                        if (seen.Add(key)) paths.Add($"{b.path}.{b.propertyName}");
                    }
                }
                if (paths.Count > 0)
                    Debug.Log($"[CharacterPartView3D] clip '{clip.name}' position curves:\n  " +
                              string.Join("\n  ", paths));
            }
        }
#endif

        /// <summary>
        /// 三段 fallback 找 rig 根：
        /// (1) <see cref="SkinnedMeshRenderer.rootBone"/>（蒙皮模型最可靠）
        /// (2) 常见名字递归搜索（Armature / Root / Hips / RootBone / rig 等，MC 风/Mixamo 等）
        /// (3) prefab 根的第一个子节点（最后兜底）
        /// </summary>
        private static Transform FindRigRootBone(Transform prefabRoot)
        {
            if (prefabRoot == null) return null;

            var smr = prefabRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.rootBone != null) return smr.rootBone;

            string[] candidates = { "Hips", "Hip", "Armature", "Root", "RootBone", "rig", "root", "armature" };
            foreach (var n in candidates)
            {
                var t = FindDescendantByName(prefabRoot, n);
                if (t != null) return t;
            }

            return prefabRoot.childCount > 0 ? prefabRoot.GetChild(0) : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (string.Equals(c.name, name, System.StringComparison.OrdinalIgnoreCase)) return c;
                var deeper = FindDescendantByName(c, name);
                if (deeper != null) return deeper;
            }
            return null;
        }

        // 在 Animator 采样后还原位置 —— Animator 在 Update 阶段 sample，LateUpdate 是覆盖时机
        private void LateUpdate()
        {
            // 第一次：捕获 idle pose 下 rig 根 / prefab 根的 localPosition 作为基线
            if (!_baselineCaptured)
            {
                if (_rigRootBone != null)         _rigRootInitialLocalPos    = _rigRootBone.localPosition;
                if (_prefabRootTransform != null) _prefabRootInitialLocalPos = _prefabRootTransform.localPosition;
                _baselineCaptured = true;
                if (_rigRootBone != null)
                    Debug.Log($"[CharacterPartView3D] {Config.PartId}：基线已捕获 rig='{_rigRootBone.name}' " +
                              $"localPos={_rigRootInitialLocalPos}");
                return;
            }

            // 后续帧：锁回 idle pose 基线，覆盖 walk 等 clip 的 root/Hip Y 曲线
            if (_rigRootBone != null)
                _rigRootBone.localPosition = _rigRootInitialLocalPos;
            if (_prefabRootTransform != null)
                _prefabRootTransform.localPosition = _prefabRootInitialLocalPos;
        }

        private static GameObject LoadPrefab(string prefabId)
        {
            try
            {
                var result = EventProcessor.Instance.TriggerEventMethod("GetResource",
                    new List<object> { prefabId, "Prefab", false });
                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                    return result[1] as GameObject;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView3D] 加载 Prefab 失败: {prefabId} → {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Loop

        private void Update()
        {
            if (!_playing || _currentAction == null || Animator == null) return;

            var layer = Mathf.Clamp(_currentAction.AnimatorLayer, 0, Mathf.Max(0, Animator.layerCount - 1));
            // Transition 期间的 next state info 也算当前；以 currentStateInfo 取归一化时间足够
            var info = Animator.GetCurrentAnimatorStateInfo(layer);
            var nt   = info.normalizedTime;

            // 帧事件（normalizedTime 跨越阈值）—— 仅按 0..1 周期内触发
            if (_currentAction.NormalizedTimeEvents != null && _currentAction.NormalizedTimeEvents.Count > 0)
            {
                var lastInCycle = _lastNormalizedTime % 1f;
                var nowInCycle  = nt % 1f;
                // 跨越 1.0 边界时，当作两个区间分别检查
                var wrapped = nowInCycle < lastInCycle;
                foreach (var kv in _currentAction.NormalizedTimeEvents)
                {
                    var threshold = kv.Key;
                    var crossed = wrapped
                        ? (threshold > lastInCycle || threshold <= nowInCycle)
                        : (threshold > lastInCycle && threshold <= nowInCycle);
                    if (crossed) BroadcastFrameEvent(kv.Value, _currentAction.ActionName, -1);
                }
            }
            _lastNormalizedTime = nt;

            // 完成检测：非循环 + normalizedTime ≥ 1 + 不在 transition
            if (!_currentAction.Loop && !_completeFired
                && nt >= 1f && !Animator.IsInTransition(layer))
            {
                _completeFired = true;
                _playing       = false;
                RaiseActionComplete(_currentAction.ActionName);
            }
        }

        #endregion
    }
}
