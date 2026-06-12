using System.Collections.Generic;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Presentation.CharacterManager.Dao;
using UnityEngine;

namespace EssSystem.Core.Presentation.CharacterManager.Runtime
{
    /// <summary>
    /// 3D 单部件视图。通过 ResourceManager 事件加载 prefab，使用 Animator 播放动作状态。
    /// 非循环动作在 normalizedTime 到达末尾且不处于 transition 时触发完成事件。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPartView3D : CharacterPartView
    {
        public Transform PrefabInstance { get; private set; }
        public Animator Animator { get; private set; }

        private CharacterActionConfig _currentAction;
        private bool _playing;
        private bool _completeFired;
        private float _lastNormalizedTime;

        private Transform _rigRootBone;
        private Vector3 _rigRootInitialLocalPos;
        private Transform _prefabRootTransform;
        private Vector3 _prefabRootInitialLocalPos;
        private bool _baselineCaptured;

        public override bool CanPivotComplete => true;

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
            var layer = Mathf.Clamp(action.AnimatorLayer, 0, Mathf.Max(0, Animator.layerCount - 1));
            if (!Animator.HasState(layer, stateHash))
            {
                Debug.LogWarning(
                    $"[CharacterPartView3D] {Config.PartId}: Animator does not contain state '{stateName}' (layer {layer}).\n" +
                    $"  Controller='{Animator.runtimeAnimatorController?.name}', available states=[{ListControllerStates()}]");
                return false;
            }

            if (action.CrossFadeDuration > 0f)
                Animator.CrossFadeInFixedTime(stateHash, action.CrossFadeDuration, layer, 0f);
            else
                Animator.Play(stateHash, layer, 0f);

            Animator.speed = 1f;
            _currentAction = action;
            _playing = true;
            _completeFired = false;
            _lastNormalizedTime = 0f;
            return true;
        }

        public override void Stop()
        {
            if (Animator != null) Animator.speed = 0f;
            _playing = false;
        }

        private string ListControllerStates()
        {
            if (Animator == null || Animator.runtimeAnimatorController == null) return "<no controller>";
#if UNITY_EDITOR
            if (Animator.runtimeAnimatorController is UnityEditor.Animations.AnimatorController ac)
            {
                var names = new List<string>();
                foreach (var layer in ac.layers)
                    foreach (var st in layer.stateMachine.states)
                        names.Add(st.state.name);
                return string.Join(", ", names);
            }
#endif
            var clipNames = new List<string>();
            foreach (var clip in Animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null) clipNames.Add(clip.name);
            }

            return string.Join(", ", clipNames);
        }

        private void BuildPrefabInstance()
        {
            ClearPrefabInstance();
            if (string.IsNullOrEmpty(Config.PrefabPath)) return;

            var prefab = LoadPrefab(Config.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterPartView3D] {Config.PartId}: failed to load prefab {Config.PrefabPath}");
                return;
            }

            var go = Instantiate(prefab, transform);
            go.name = Config.PartId;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            PrefabInstance = go.transform;
            Animator = go.GetComponentInChildren<Animator>();
            if (Animator == null)
            {
                Animator = go.AddComponent<Animator>();
                Debug.Log($"[CharacterPartView3D] {Config.PartId}: prefab has no Animator, added one on the root.");
            }

            EnsureAnimatorController();
            Animator.applyRootMotion = false;

            _rigRootBone = FindRigRootBone(go.transform);
            _prefabRootTransform = go.transform;
            _baselineCaptured = false;

            if (_rigRootBone != null)
                Debug.Log($"[CharacterPartView3D] {Config.PartId}: rig root bone='{_rigRootBone.name}', baseline will be captured after first Animator sample.");
            else
                Debug.LogWarning($"[CharacterPartView3D] {Config.PartId}: rig root bone not found, walk animations may sink.");

#if UNITY_EDITOR
            DumpClipPositionCurves();
#endif
        }

        private void ClearPrefabInstance()
        {
            if (PrefabInstance == null) return;

            if (UnityEngine.Application.isPlaying) Destroy(PrefabInstance.gameObject);
            else DestroyImmediate(PrefabInstance.gameObject);

            PrefabInstance = null;
            Animator = null;
            _rigRootBone = null;
            _prefabRootTransform = null;
            _baselineCaptured = false;
        }

        private void EnsureAnimatorController()
        {
            if (Animator == null || Animator.runtimeAnimatorController != null) return;

            var ctrl = Resources.Load<RuntimeAnimatorController>(Config.PrefabPath);
#if UNITY_EDITOR
            if (ctrl == null)
            {
                var fileName = System.IO.Path.GetFileName(Config.PrefabPath);
                var guids = UnityEditor.AssetDatabase.FindAssets($"t:AnimatorController {fileName}");
                foreach (var guid in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    var noExt = System.IO.Path.ChangeExtension(path, null).Replace('\\', '/');
                    if (!noExt.EndsWith("/Resources/" + Config.PrefabPath, System.StringComparison.Ordinal) &&
                        !noExt.EndsWith("Resources/" + Config.PrefabPath, System.StringComparison.Ordinal))
                        continue;

                    ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                    if (ctrl != null) break;
                }
            }
#endif

            if (ctrl != null)
            {
                Animator.runtimeAnimatorController = ctrl;
                Debug.Log($"[CharacterPartView3D] {Config.PartId}: bound AnimatorController '{ctrl.name}'");
            }
            else
            {
                Debug.LogWarning(
                    $"[CharacterPartView3D] {Config.PartId}: can not find AnimatorController for Resources/{Config.PrefabPath}. " +
                    "Run menu item 'Tools/WulaSystem/Presentation/Character/3D/FBX/Build AnimatorController From Selected FBX' to generate it.");
            }
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
                foreach (var binding in bindings)
                {
                    if (!binding.propertyName.StartsWith("m_LocalPosition", System.StringComparison.Ordinal))
                        continue;

                    var key = $"{binding.path}|{binding.propertyName}";
                    if (seen.Add(key)) paths.Add($"{binding.path}.{binding.propertyName}");
                }

                if (paths.Count > 0)
                    Debug.Log($"[CharacterPartView3D] clip '{clip.name}' position curves:\n  " + string.Join("\n  ", paths));
            }
        }
#endif

        private static Transform FindRigRootBone(Transform prefabRoot)
        {
            if (prefabRoot == null) return null;

            var smr = prefabRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr != null && smr.rootBone != null) return smr.rootBone;

            string[] candidates = { "Hips", "Hip", "Armature", "Root", "RootBone", "rig", "root", "armature" };
            foreach (var name in candidates)
            {
                var found = FindDescendantByName(prefabRoot, name);
                if (found != null) return found;
            }

            return prefabRoot.childCount > 0 ? prefabRoot.GetChild(0) : null;
        }

        private static Transform FindDescendantByName(Transform root, string name)
        {
            if (root == null) return null;

            for (var i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (string.Equals(child.name, name, System.StringComparison.OrdinalIgnoreCase)) return child;

                var deeper = FindDescendantByName(child, name);
                if (deeper != null) return deeper;
            }

            return null;
        }

        private void LateUpdate()
        {
            if (!_baselineCaptured)
            {
                if (_rigRootBone != null) _rigRootInitialLocalPos = _rigRootBone.localPosition;
                if (_prefabRootTransform != null) _prefabRootInitialLocalPos = _prefabRootTransform.localPosition;

                _baselineCaptured = true;
                if (_rigRootBone != null)
                    Debug.Log($"[CharacterPartView3D] {Config.PartId}: captured rig baseline '{_rigRootBone.name}', localPos={_rigRootInitialLocalPos}");
                return;
            }

            if (_rigRootBone != null) _rigRootBone.localPosition = _rigRootInitialLocalPos;
            if (_prefabRootTransform != null) _prefabRootTransform.localPosition = _prefabRootInitialLocalPos;
        }

        private static GameObject LoadPrefab(string prefabId)
        {
            try
            {
                var result = EventProcessor.Instance.TriggerEventMethod("GetPrefab", new List<object> { prefabId });
                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                    return result[1] as GameObject;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView3D] failed to load prefab: {prefabId} -> {ex.Message}");
            }

            return null;
        }

        private void Update()
        {
            if (!_playing || _currentAction == null || Animator == null) return;

            var layer = Mathf.Clamp(_currentAction.AnimatorLayer, 0, Mathf.Max(0, Animator.layerCount - 1));
            var info = Animator.GetCurrentAnimatorStateInfo(layer);
            var normalizedTime = info.normalizedTime;

            BroadcastNormalizedTimeEvents(normalizedTime);
            _lastNormalizedTime = normalizedTime;

            if (!_currentAction.Loop && !_completeFired &&
                normalizedTime >= 1f && !Animator.IsInTransition(layer))
            {
                _completeFired = true;
                _playing = false;
                RaiseActionComplete(_currentAction.ActionName);
            }
            else if (_currentAction.Loop && normalizedTime >= 1f && !Animator.IsInTransition(layer))
            {
                RestartLoop(layer);
            }
        }

        private void BroadcastNormalizedTimeEvents(float normalizedTime)
        {
            if (_currentAction.NormalizedTimeEvents == null || _currentAction.NormalizedTimeEvents.Count == 0)
                return;

            var lastInCycle = _lastNormalizedTime % 1f;
            var nowInCycle = normalizedTime % 1f;
            var wrapped = nowInCycle < lastInCycle;

            foreach (var kv in _currentAction.NormalizedTimeEvents)
            {
                var threshold = kv.Key;
                var crossed = wrapped
                    ? threshold > lastInCycle || threshold <= nowInCycle
                    : threshold > lastInCycle && threshold <= nowInCycle;
                if (crossed) BroadcastFrameEvent(kv.Value, _currentAction.ActionName, -1);
            }
        }

        private void RestartLoop(int layer)
        {
            var stateName = _currentAction.ResolveAnimatorState();
            if (string.IsNullOrEmpty(stateName)) return;

            var hash = Animator.StringToHash(stateName);
            if (!Animator.HasState(layer, hash)) return;

            Animator.Play(hash, layer, 0f);
            _lastNormalizedTime = 0f;
        }
    }
}
