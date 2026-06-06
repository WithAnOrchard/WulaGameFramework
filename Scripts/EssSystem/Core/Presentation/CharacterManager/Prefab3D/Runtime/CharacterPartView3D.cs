using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager.Dao;
// 搂4.1 璺ㄦā鍧楄蛋 bare-string 鍗忚锛屼笉 using ResourceManager

namespace EssSystem.Core.Presentation.CharacterManager.Runtime
{
    /// <summary>
    /// 3D 鍗曢儴浠?View 鈥斺€?閫氳繃 ResourceManager 鍔犺浇 <see cref="CharacterPartConfig.PrefabPath"/>
    /// 鎸囧畾鐨?Prefab 骞跺疄渚嬪寲涓哄瓙鑺傜偣锛屼娇鐢?<see cref="UnityEngine.Animator"/> 鍒囨崲鍔ㄤ綔鐘舵€併€?
    /// <para>姣忎釜鍔ㄤ綔 = 涓€涓?Animator State锛氱敤 <see cref="CharacterActionConfig.ResolveAnimatorState"/> 瑙ｆ瀽鍚嶅瓧锛?
    /// 閫氳繃 <see cref="UnityEngine.Animator.CrossFadeInFixedTime(int, float, int)"/> 鍒囨崲銆?/para>
    /// <para>闈炲惊鐜姩浣滃畬鎴愭娴嬶細<c>StateInfo.normalizedTime 鈮?1 &amp;&amp; !inTransition</c> 鏃惰Е鍙?
    /// <see cref="CharacterPartView.OnActionComplete"/>銆傚抚浜嬩欢閫氳繃
    /// <see cref="CharacterActionConfig.NormalizedTimeEvents"/> 鍦?normalizedTime 璺ㄨ秺闃堝€兼椂骞挎挱銆?/para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPartView3D : CharacterPartView
    {
        /// <summary>瀹炰緥鍖栧嚭鏉ョ殑 Prefab 鏍?Transform锛堥儴浠舵湰浣擄級銆?/summary>
        public Transform PrefabInstance { get; private set; }

        /// <summary>Prefab 涓婄殑 Animator锛堝鏋滄湁锛夈€?/summary>
        public Animator Animator { get; private set; }

        // 褰撳墠鍔ㄤ綔杩愯鎬?
        private CharacterActionConfig _currentAction;
        private bool  _playing;
        private bool  _completeFired;
        private float _lastNormalizedTime;

        // Rig 鏍归楠奸攣瀹?鈥斺€?瑙ｅ喅鏃?Avatar 鏃?clip 鍐?root/Hip Y 鍏抽敭甯ц妯″瀷涓嬫矇鐨勯棶棰?
        // 鍩虹嚎鍊煎湪 Animator 绗竴娆￠噰鏍蜂箣鍚庢崟鑾凤紙鍙?idle pose 鐨?Y锛夛紝涓嶆槸 prefab 鐨?raw 榛樿
        private Transform _rigRootBone;
        private Vector3   _rigRootInitialLocalPos;
        private Transform _prefabRootTransform;     // go.transform 鈥斺€?涔熻閿侊紙path="" 鏇茬嚎浼氬啓杩欓噷锛?
        private Vector3   _prefabRootInitialLocalPos;
        private bool      _baselineCaptured;

        // 3D 閮ㄤ欢澶╃劧鏈?Animator 鈥斺€?濮嬬粓鍙綔 pivot
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
                    $"[CharacterPartView3D] {Config.PartId}锛欰nimator 涓嶅惈 state '{stateName}' (layer {layer})銆俓n" +
                    $"  Controller='{Animator.runtimeAnimatorController?.name}'锛屽彲鐢?state锛歔{available}]");
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

        /// <summary>璇婃柇杈呭姪锛氬垪鍑哄綋鍓?controller 鍐呮墍鏈?state 鍚嶏紙閫楀彿鍒嗛殧锛夈€?/summary>
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
            // 杩愯鏃堕€€鍖栵細鍒?clip 鍚嶏紙Builder 鐢熸垚鐨?controller state 鍚?= clip 鍚嶏級
            var clipNames = new List<string>();
            foreach (var c in Animator.runtimeAnimatorController.animationClips)
                if (c != null) clipNames.Add(c.name);
            return string.Join(", ", clipNames);
        }

        #endregion

        #region Build prefab

        private void BuildPrefabInstance()
        {
            // 娓呯悊鏃у疄渚嬶紙閲嶅 Setup 瀹归敊锛?
            if (PrefabInstance != null)
            {
                if (UnityEngine.Application.isPlaying) Destroy(PrefabInstance.gameObject);
                else                       DestroyImmediate(PrefabInstance.gameObject);
                PrefabInstance = null;
                Animator = null;
            }

            if (string.IsNullOrEmpty(Config.PrefabPath)) return;

            var prefab = LoadPrefab(Config.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[CharacterPartView3D] {Config.PartId}锛氬姞杞?Prefab 澶辫触 鈫?{Config.PrefabPath}");
                return;
            }

            var go = Instantiate(prefab, transform);
            go.name = Config.PartId;
            // Prefab 鑷韩鐨勬湰鍦板彉鎹㈢敱 Setup 搴旂敤鍦ㄧ埗鑺傜偣锛坱his锛夛紝瀛愯妭鐐逛繚鎸佸崟浣?
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale    = Vector3.one;

            PrefabInstance = go.transform;
            Animator       = go.GetComponentInChildren<Animator>();

            // FBX Animation Type=None 鎴栧鍏ヨ缃笉甯?Animator 鏃讹紝鑷姩琛ヤ竴涓寕鍦ㄦ牴鑺傜偣涓娿€?
            // Generic 鍔ㄧ敾閫氳繃楠ㄩ transform 璺緞浣滅敤锛屼笉寮轰緷璧?Avatar锛沜lip 鍐?curve 浼氭寜 path 搴旂敤鍒板瓙鑺傜偣銆?
            if (Animator == null)
            {
                Animator = go.AddComponent<Animator>();
                Debug.Log($"[CharacterPartView3D] {Config.PartId}锛欶BX 涓婃棤 Animator锛屽凡鑷姩娣诲姞鍒版牴鑺傜偣");
            }

            {
                // 鑷姩鎸?fbxPath 鍔犺浇鍚屽悕 .controller锛圕haracterConfigFactory 鍦?Editor 涓嬩細纭繚瀹冨瓨鍦級
                if (Animator.runtimeAnimatorController == null)
                {
                    var ctrl = Resources.Load<RuntimeAnimatorController>(Config.PrefabPath);
#if UNITY_EDITOR
                    // Editor fallback锛氬垰鐢熸垚鐨?.controller 鍙兘鏈繘 Resources 绱㈠紩锛岀洿鎺ョ敤 AssetDatabase 鎵?
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
                        Debug.Log($"[CharacterPartView3D] {Config.PartId}锛氬凡缁戝畾 Controller '{ctrl.name}'");
                    }
                    else
                    {
                        Debug.LogWarning(
                            $"[CharacterPartView3D] {Config.PartId} can not find AnimatorController for Resources/{Config.PrefabPath}. " +
                            $"Please run menu item 'Tools/WulaSystem/Presentation/Character/3D/FBX/Build AnimatorController From Selected FBX' to generate it.");
                    }
                }
                // 鍏抽敭锛氬叧 root motion 鈥斺€?璁?Animator 鎻愬彇 clip 鍐?root translation/rotation curve锛屼笉鍐欏埌楠ㄩ銆?
                // 杩欐牱 walk 绫诲姩鐢荤殑 Hip Y 鍏抽敭甯у氨涓嶄細璁╂ā鍨嬮櫡鍦般€?
                Animator.applyRootMotion = false;
            }

            // 鍙壘濂藉紩鐢紱鍩虹嚎鍊硷紙_rigRootInitialLocalPos / _prefabRootInitialLocalPos锛?
            // 鎺ㄨ繜鍒扮涓€娆?LateUpdate 鎹曡幏 鈥斺€?閭ｆ椂 Animator 宸茬粡閲囨牱瀹岄粯璁?state锛坕dle锛?
            // 鐩存帴鎷?prefab 鐨?raw localPosition 鏄敊鐨勶細idle clip 浼氭妸 body_1 鎶埌姝ｇ‘瑙嗚楂樺害锛?
            // raw 榛樿閫氬父鏇翠綆锛坮ig 鍘熺偣鍦ㄨ叞閮?鈫?闈欐鏄剧ず闇€瑕侀潬 clip 鎶級銆?
            _rigRootBone         = FindRigRootBone(go.transform);
            _prefabRootTransform = go.transform;
            _baselineCaptured    = false;
            if (_rigRootBone != null)
                Debug.Log($"[CharacterPartView3D] {Config.PartId}锛歳ig root bone='{_rigRootBone.name}'锛堝熀绾垮皢鍦ㄩ娆?Animator 閲囨牱鍚庢崟鑾凤級");
            else
                Debug.LogWarning($"[CharacterPartView3D] {Config.PartId}锛氭湭鎵惧埌 rig 鏍归楠硷紝walk 鍙兘涓嬫矇");

#if UNITY_EDITOR
            // 璇婃柇锛氬垪鍑?controller 鍐呮墍鏈?clip 涓甫 localPosition 鏇茬嚎鐨?transform path
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
        /// 涓夋 fallback 鎵?rig 鏍癸細
        /// (1) <see cref="SkinnedMeshRenderer.rootBone"/>锛堣挋鐨ā鍨嬫渶鍙潬锛?
        /// (2) 甯歌鍚嶅瓧閫掑綊鎼滅储锛圓rmature / Root / Hips / RootBone / rig 绛夛紝MC 椋?Mixamo 绛夛級
        /// (3) prefab 鏍圭殑绗竴涓瓙鑺傜偣锛堟渶鍚庡厹搴曪級
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

        // 鍦?Animator 閲囨牱鍚庤繕鍘熶綅缃?鈥斺€?Animator 鍦?Update 闃舵 sample锛孡ateUpdate 鏄鐩栨椂鏈?
        private void LateUpdate()
        {
            // 绗竴娆★細鎹曡幏 idle pose 涓?rig 鏍?/ prefab 鏍圭殑 localPosition 浣滀负鍩虹嚎
            if (!_baselineCaptured)
            {
                if (_rigRootBone != null)         _rigRootInitialLocalPos    = _rigRootBone.localPosition;
                if (_prefabRootTransform != null) _prefabRootInitialLocalPos = _prefabRootTransform.localPosition;
                _baselineCaptured = true;
                if (_rigRootBone != null)
                    Debug.Log($"[CharacterPartView3D] {Config.PartId}锛氬熀绾垮凡鎹曡幏 rig='{_rigRootBone.name}' " +
                              $"localPos={_rigRootInitialLocalPos}");
                return;
            }

            // 鍚庣画甯э細閿佸洖 idle pose 鍩虹嚎锛岃鐩?walk 绛?clip 鐨?root/Hip Y 鏇茬嚎
            if (_rigRootBone != null)
                _rigRootBone.localPosition = _rigRootInitialLocalPos;
            if (_prefabRootTransform != null)
                _prefabRootTransform.localPosition = _prefabRootInitialLocalPos;
        }

        private static GameObject LoadPrefab(string prefabId)
        {
            try
            {
                // 搂4.1 璺ㄦā鍧?bare-string fa莽ade锛歊esourceManager.EVT_GET_PREFAB
                var result = EventProcessor.Instance.TriggerEventMethod(
                    "GetPrefab",
                    new List<object> { prefabId });
                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                    return result[1] as GameObject;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView3D] 鍔犺浇 Prefab 澶辫触: {prefabId} 鈫?{ex.Message}");
            }
            return null;
        }

        #endregion

        #region Loop

        private void Update()
        {
            if (!_playing || _currentAction == null || Animator == null) return;

            var layer = Mathf.Clamp(_currentAction.AnimatorLayer, 0, Mathf.Max(0, Animator.layerCount - 1));
            // Transition 鏈熼棿鐨?next state info 涔熺畻褰撳墠锛涗互 currentStateInfo 鍙栧綊涓€鍖栨椂闂磋冻澶?
            var info = Animator.GetCurrentAnimatorStateInfo(layer);
            var nt   = info.normalizedTime;

            // 甯т簨浠讹紙normalizedTime 璺ㄨ秺闃堝€硷級鈥斺€?浠呮寜 0..1 鍛ㄦ湡鍐呰Е鍙?
            if (_currentAction.NormalizedTimeEvents != null && _currentAction.NormalizedTimeEvents.Count > 0)
            {
                var lastInCycle = _lastNormalizedTime % 1f;
                var nowInCycle  = nt % 1f;
                // 璺ㄨ秺 1.0 杈圭晫鏃讹紝褰撲綔涓や釜鍖洪棿鍒嗗埆妫€鏌?
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

            // 瀹屾垚妫€娴嬶細闈炲惊鐜?+ normalizedTime 鈮?1 + 涓嶅湪 transition
            if (!_currentAction.Loop && !_completeFired
                && nt >= 1f && !Animator.IsInTransition(layer))
            {
                _completeFired = true;
                _playing       = false;
                RaiseActionComplete(_currentAction.ActionName);
            }
            // 寰幆鍔ㄤ綔鏈熬鍏滃簳閲嶆斁锛欶BX 瀵煎叆 loopTime=false 鏃?Animator 浼氬崱鍦ㄦ渶鍚庝竴甯э紝
            // 杩欓噷寮哄埗 Play(0f) 閲嶅ご寮€濮嬶紝璁?Walk / Run / Idle 绛夋棤瑙?FBX 瀵煎叆璁剧疆姝ｇ‘寰幆銆?
            else if (_currentAction.Loop && nt >= 1f && !Animator.IsInTransition(layer))
            {
                var stateName = _currentAction.ResolveAnimatorState();
                if (!string.IsNullOrEmpty(stateName))
                {
                    var hash = Animator.StringToHash(stateName);
                    if (Animator.HasState(layer, hash))
                    {
                        Animator.Play(hash, layer, 0f);
                        _lastNormalizedTime = 0f;
                    }
                }
            }
        }

        #endregion
    }
}

