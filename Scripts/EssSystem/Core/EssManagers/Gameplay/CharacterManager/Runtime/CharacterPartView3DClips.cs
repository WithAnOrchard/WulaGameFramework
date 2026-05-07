using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;
using EssSystem.Core.EssManagers.Foundation.ResourceManager;   // C4: 走 façade，避免魔法字符串

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime
{
    /// <summary>
    /// 3D 单部件 View（Playables Clip 模式）—— 加载 FBX/Prefab 实例化后，
    /// 用 <see cref="PlayableGraph"/> + <see cref="AnimationMixerPlayable"/>
    /// 直接播 FBX 内的 <see cref="AnimationClip"/>，<b>不需要任何 AnimatorController 资产</b>。
    /// <para>每个动作 = 一个 Clip。<see cref="CharacterActionConfig.ResolveAnimatorState"/>
    /// 返回的字符串当作 clip 名（即 FBX 内 take 名）。</para>
    /// <para>CrossFade：通过 mixer 两个输入的权重渐变实现，时长 = <see cref="CharacterActionConfig.CrossFadeDuration"/>。</para>
    /// <para>完成检测 / 帧事件：基于当前 active clip playable 的 time / clip.length 做归一化时间跟踪。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPartView3DClips : CharacterPartView
    {
        /// <summary>实例化出来的 Prefab/FBX 根 Transform（部件本体）。</summary>
        public Transform PrefabInstance { get; private set; }

        /// <summary>Prefab/FBX 上的 Animator（必要时由本组件自动添加）。</summary>
        public Animator Animator { get; private set; }

        // Playables 状态
        private PlayableGraph             _graph;
        private AnimationMixerPlayable    _mixer;
        private AnimationClipPlayable     _activePlayable;     // 当前主输入（input 1）
        private AnimationClipPlayable     _fadingPlayable;     // 上一动作（input 0），渐隐
        private bool                      _activeValid;
        private bool                      _fadingValid;

        // CrossFade 状态
        private float _fadeDuration;
        private float _fadeElapsed;

        // 当前动作运行态
        private CharacterActionConfig _currentAction;
        private AnimationClip         _currentClip;

        /// <summary>本 FBX 内含的 clip 字典 —— Setup 时从 <c>EVT_GET_MODEL_CLIPS</c> 预取。
        /// Play 时优先在这里查，避免两个 FBX 同名 clip 的全局缓存碍撞。</summary>
        private readonly Dictionary<string, AnimationClip> _localClips = new Dictionary<string, AnimationClip>();
        private bool                  _playing;
        private bool                  _completeFired;
        private float                 _lastNormalizedTime;

        // 3D 部件 ── 可作 pivot
        public override bool CanPivotComplete => true;

        #region Setup / Public API

        protected override void OnSetup()
        {
            BuildPrefabInstance();

            if (Animator == null)
            {
                Debug.LogWarning($"[CharacterPartView3DClips] {Config?.PartId}：FBX/Prefab 上无 Animator，已自动添加");
                if (PrefabInstance != null)
                    Animator = PrefabInstance.gameObject.AddComponent<Animator>();
                else
                    Animator = gameObject.AddComponent<Animator>();
            }
            // Playables 模式下不需要 RuntimeAnimatorController
            Animator.runtimeAnimatorController = null;

            BuildPlayableGraph();

            // 预取本 FBX 的所有 clip 到本地字典，隔离全局同名碍撞
            CacheLocalClipsFromModel();

            if (!string.IsNullOrEmpty(Config.DefaultActionName))
                Play(Config.DefaultActionName);
        }

        private void CacheLocalClipsFromModel()
        {
            _localClips.Clear();
            if (string.IsNullOrEmpty(Config?.PrefabPath)) return;
            try
            {
                // C4: façade 调用 ResourceManager.GetModelClips。
                var r = EventProcessor.Instance.TriggerEventMethod(
                    ResourceManager.EVT_GET_MODEL_CLIPS,
                    new List<object> { Config.PrefabPath });
                if (r != null && r.Count >= 2 && ResultCode.IsOk(r) && r[1] is List<AnimationClip> clips)
                {
                    foreach (var c in clips)
                        if (c != null && !_localClips.ContainsKey(c.name))
                            _localClips[c.name] = c;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView3DClips] {Config?.PartId}：预取本 FBX clip 列表失败 → {ex.Message}");
            }
        }

        public override bool Play(string actionName)
        {
            if (Config == null || Animator == null) return false;
            if (!_graph.IsValid()) return false;

            var action = Config.GetAction(actionName);
            if (action == null) return false;

            var clipName = action.ResolveAnimatorState();
            if (string.IsNullOrEmpty(clipName))
            {
                Debug.LogWarning($"[CharacterPartView3DClips] {Config.PartId}：动作 {actionName} 未指定 Clip 名");
                return false;
            }

            // 优先本 FBX 内查（跨 FBX 同名 clip 这里不会撞）；未命中才走全局
            AnimationClip clip;
            if (!_localClips.TryGetValue(clipName, out clip))
                clip = LoadClip(clipName);
            if (clip == null)
            {
                Debug.LogWarning($"[CharacterPartView3DClips] {Config.PartId}：未找到 AnimationClip '{clipName}'（本 FBX 未含且全局缓存也未命中）");
                return false;
            }

            SwitchToClip(clip, action);
            return true;
        }

        public override void Stop()
        {
            if (_activeValid && _activePlayable.IsValid()) _activePlayable.SetSpeed(0d);
            _playing = false;
        }

        #endregion

        #region Build prefab / graph

        private void BuildPrefabInstance()
        {
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
                Debug.LogWarning($"[CharacterPartView3DClips] {Config.PartId}：加载 Prefab/FBX 失败 → {Config.PrefabPath}");
                return;
            }

            var go = Instantiate(prefab, transform);
            go.name = Config.PartId;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            PrefabInstance = go.transform;
            Animator       = go.GetComponentInChildren<Animator>();
        }

        private void BuildPlayableGraph()
        {
            if (_graph.IsValid()) _graph.Destroy();
            _graph = PlayableGraph.Create($"CharacterPartView3DClips_{Config?.PartId}");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            _mixer = AnimationMixerPlayable.Create(_graph, 2);
            // input 0 = fading（渐隐 / 上一动作），input 1 = active（当前动作）
            _mixer.SetInputWeight(0, 0f);
            _mixer.SetInputWeight(1, 1f);

            var output = AnimationPlayableOutput.Create(_graph, "Animation", Animator);
            output.SetSourcePlayable(_mixer);

            _graph.Play();
        }

        private static GameObject LoadPrefab(string prefabId)
        {
            try
            {
                // C4: façade 调用 ResourceManager.GetPrefab。
                var result = EventProcessor.Instance.TriggerEventMethod(
                    ResourceManager.EVT_GET_PREFAB,
                    new List<object> { prefabId });
                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                    return result[1] as GameObject;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView3DClips] 加载 Prefab 失败: {prefabId} → {ex.Message}");
            }
            return null;
        }

        private static AnimationClip LoadClip(string clipName)
        {
            try
            {
                // C4: façade 调用 ResourceManager.GetAnimationClip。
                var result = EventProcessor.Instance.TriggerEventMethod(
                    ResourceManager.EVT_GET_ANIMATION_CLIP,
                    new List<object> { clipName });
                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                    return result[1] as AnimationClip;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView3DClips] 加载 AnimationClip 失败: {clipName} → {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Play / CrossFade

        private void SwitchToClip(AnimationClip clip, CharacterActionConfig action)
        {
            // 1) 把当前 active 移到 fading 槽
            if (_fadingValid && _fadingPlayable.IsValid())
            {
                _mixer.DisconnectInput(0);
                _fadingPlayable.Destroy();
                _fadingValid = false;
            }
            if (_activeValid && _activePlayable.IsValid())
            {
                _mixer.DisconnectInput(1);
                _fadingPlayable = _activePlayable;
                _fadingValid    = true;
                _mixer.ConnectInput(0, _fadingPlayable, 0);
            }

            // 2) 创建新的 active clip playable
            var clipPlayable = AnimationClipPlayable.Create(_graph, clip);
            clipPlayable.SetApplyFootIK(false);
            clipPlayable.SetTime(0d);
            clipPlayable.SetSpeed(1d);

            _mixer.ConnectInput(1, clipPlayable, 0);
            _activePlayable = clipPlayable;
            _activeValid    = true;

            // 3) CrossFade 状态机
            _fadeDuration = Mathf.Max(0f, action != null ? action.CrossFadeDuration : 0f);
            _fadeElapsed  = 0f;
            if (_fadeDuration <= 0f || !_fadingValid)
            {
                _mixer.SetInputWeight(0, 0f);
                _mixer.SetInputWeight(1, 1f);
                if (_fadingValid)
                {
                    _mixer.DisconnectInput(0);
                    _fadingPlayable.Destroy();
                    _fadingValid = false;
                }
            }
            else
            {
                _mixer.SetInputWeight(0, 1f);
                _mixer.SetInputWeight(1, 0f);
            }

            _currentAction      = action;
            _currentClip        = clip;
            _playing            = true;
            _completeFired      = false;
            _lastNormalizedTime = 0f;
        }

        #endregion

        #region Loop

        private void Update()
        {
            if (!_graph.IsValid()) return;
            if (!_playing || _currentAction == null || _currentClip == null) return;

            // 1) 推进 CrossFade
            if (_fadingValid)
            {
                _fadeElapsed += Time.deltaTime;
                var t = _fadeDuration > 0f ? Mathf.Clamp01(_fadeElapsed / _fadeDuration) : 1f;
                _mixer.SetInputWeight(0, 1f - t);
                _mixer.SetInputWeight(1, t);
                if (t >= 1f)
                {
                    _mixer.DisconnectInput(0);
                    _fadingPlayable.Destroy();
                    _fadingValid = false;
                }
            }

            // 2) 计算归一化时间
            if (!_activeValid || !_activePlayable.IsValid()) return;
            var clipLen = Mathf.Max(0.0001f, _currentClip.length);
            var time    = (float)_activePlayable.GetTime();
            // 循环动作 wrap，非循环 clamp
            if (_currentAction.Loop)
            {
                if (time >= clipLen)
                {
                    time = time % clipLen;
                    _activePlayable.SetTime(time);
                }
            }
            var nt = time / clipLen;

            // 3) 帧事件（normalizedTime 跨越阈值）
            if (_currentAction.NormalizedTimeEvents != null && _currentAction.NormalizedTimeEvents.Count > 0)
            {
                var lastInCycle = _lastNormalizedTime % 1f;
                var nowInCycle  = nt % 1f;
                var wrapped     = nowInCycle < lastInCycle;
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

            // 4) 完成检测：非循环 + 时间走到末尾
            if (!_currentAction.Loop && !_completeFired && time >= clipLen - 0.0001f)
            {
                _completeFired = true;
                _playing       = false;
                RaiseActionComplete(_currentAction.ActionName);
            }
        }

        #endregion

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
