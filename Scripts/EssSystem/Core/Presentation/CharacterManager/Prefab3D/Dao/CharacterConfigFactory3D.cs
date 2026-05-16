// §4.1 跨模块走 bare-string 协议，不 using ResourceManager

namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    /// <summary>
    /// <see cref="CharacterConfigFactory"/> 的 **3D Prefab / FBX 分部** ——
    /// 提供 Prefab3D + AnimatorController / FBX 一键注册等工厂方法。
    /// 2D Sprite 工厂方法在 <c>CharacterConfigFactory2D.cs</c>。
    /// </summary>
    public static partial class CharacterConfigFactory
    {
        // ─────────────────────────────────────────────────────────────
        // 3D Prefab + Animator 模式
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 单部件 3D 角色：一个 Prefab + 一组 Animator state 名作为动作。
        /// <para>actionStateNames 中每一项 = 既是 ActionName 又是 Animator state 名。Loop 默认 true。</para>
        /// </summary>
        /// <param name="configId">注册到 CharacterService 的 ID。</param>
        /// <param name="prefabPath">通过 ResourceManager 以 <c>"Prefab"</c> 类型加载的 Prefab 资源 ID。</param>
        /// <param name="defaultAction">Setup 后自动播放的 Animator state 名（通常 "Idle"）。</param>
        /// <param name="actionStateNames">支持切换的 Animator state 名列表（通常 Idle/Walk/Attack/...）。</param>
        public static CharacterConfig MakeSimplePrefab3D(string configId, string prefabPath,
            string defaultAction, params string[] actionStateNames)
        {
            var cfg = new CharacterConfig(configId, configId)
                .WithRenderMode(CharacterRenderMode.Prefab3D);

            var actions = new System.Collections.Generic.List<CharacterActionConfig>();
            if (actionStateNames != null)
            {
                foreach (var name in actionStateNames)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    actions.Add(new CharacterActionConfig(name).WithAnimatorState(name));
                }
            }

            var part = new CharacterPartConfig("Body", CharacterPartType.Dynamic)
                .WithPrefab(prefabPath)
                .WithDynamic(defaultAction ?? string.Empty, actions.ToArray());

            cfg.WithPart(part);
            return cfg;
        }

        /// <summary>一键注册单部件 3D 角色到 <see cref="CharacterManager.CharacterService"/>。</summary>
        public static CharacterConfig RegisterSimplePrefab3D(string configId, string prefabPath,
            string defaultAction, params string[] actionStateNames)
        {
            var cfg = MakeSimplePrefab3D(configId, prefabPath, defaultAction, actionStateNames);
            if (CharacterService.Instance != null)
                CharacterService.Instance.RegisterConfig(cfg);
            return cfg;
        }

        // ─────────────────────────────────────────────────────────────
        // FBX 模型（Prefab3D 路径，自动生成同目录 .controller）
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 单部件 FBX 模型 —— 直接播 FBX 内的 AnimationClip。每个 clipName 既是 ActionName 又是 FBX 内 take 名。
        /// <para>FBX 须放在 <c>Resources/</c> 下（如 <c>Resources/Models/Characters3D/zombie.fbx</c>），
        /// 启动时会被 ResourceManager 自动索引。</para>
        /// <para>使用 <see cref="CharacterRenderMode.Prefab3D"/>（标准 Animator + AnimatorController），
        /// 比 Prefab3DClips 优势：Animator 在 applyRootMotion=false 时会正确提取 root translation 不写到骨骼，
        /// 避免 walk 类动画 Hip Y 关键帧让模型陷入地面。
        /// 配套：FBX 同目录需有同名 .controller（由 <c>FBXAnimatorControllerBuilder</c> 自动生成）。</para>
        /// </summary>
        /// <param name="configId">注册到 CharacterService 的 ID。</param>
        /// <param name="fbxPath">FBX 资源 ID（Resources 相对路径或裸文件名，如 <c>"zombie"</c>）。</param>
        /// <param name="defaultAction">Setup 后自动播的 clip 名（通常 "Idle"）。</param>
        /// <param name="loopActions">需循环的动作列表（例 Idle/Walk/Run）。</param>
        /// <param name="oneShotActions">非循环动作列表（例 Attack/Death）。</param>
        public static CharacterConfig MakeFBXModel(string configId, string fbxPath,
            string defaultAction, string[] loopActions = null, string[] oneShotActions = null)
        {
            var cfg = new CharacterConfig(configId, configId)
                .WithRenderMode(CharacterRenderMode.Prefab3D);

            var actions = new System.Collections.Generic.List<CharacterActionConfig>();
            if (loopActions != null)
            {
                foreach (var name in loopActions)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    actions.Add(new CharacterActionConfig(name).WithAnimatorState(name).WithLoop(true));
                }
            }
            if (oneShotActions != null)
            {
                foreach (var name in oneShotActions)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    actions.Add(new CharacterActionConfig(name).WithAnimatorState(name).WithLoop(false));
                }
            }

            var part = new CharacterPartConfig("Body", CharacterPartType.Dynamic)
                .WithPrefab(fbxPath)
                .WithDynamic(defaultAction ?? string.Empty, actions.ToArray());

            cfg.WithPart(part);
            return cfg;
        }

        /// <summary>一键注册 FBX 模型到 <see cref="CharacterManager.CharacterService"/>。</summary>
        public static CharacterConfig RegisterFBXModel(string configId, string fbxPath,
            string defaultAction, string[] loopActions = null, string[] oneShotActions = null)
        {
            var cfg = MakeFBXModel(configId, fbxPath, defaultAction, loopActions, oneShotActions);
            if (CharacterService.Instance != null)
                CharacterService.Instance.RegisterConfig(cfg);
            return cfg;
        }

        /// <summary>
        /// 极简一行 FBX 注册：自动 Editor 扫描 FBX 内 clip 名作为 actions（全 Loop=true）。
        /// 仅 Editor 下能扫描精确，Build 期请用显式 <see cref="RegisterFBXModel"/>。
        /// </summary>
        public static CharacterConfig RegisterFBXModelAuto(string configId, string fbxPath, string defaultAction = null)
        {
            var clipNames = new System.Collections.Generic.List<string>();
#if UNITY_EDITOR
            try
            {
                // §4.1 跨模块 bare-string：ResourceManager.EVT_GET_MODEL_CLIPS
                var r = EssSystem.Core.Base.Event.EventProcessor.Instance.TriggerEventMethod(
                    "GetModelClips",
                    new System.Collections.Generic.List<object> { fbxPath });
                if (EssSystem.Core.Base.Util.ResultCode.IsOk(r) && r.Count >= 2 &&
                    r[1] is System.Collections.Generic.List<UnityEngine.AnimationClip> clips)
                {
                    foreach (var c in clips)
                        if (c != null && !clipNames.Contains(c.name)) clipNames.Add(c.name);
                }
            }
            catch { /* ignore */ }

            // Editor 下：保证 FBX 同目录有同名 .controller（Prefab3D 运行时按 fbxPath 加载）
            EnsureControllerForFBX(fbxPath);
#endif
            // 无 clip → 跳过：通常是静态道具 FBX，没动作可注册
            if (clipNames.Count == 0)
            {
                UnityEngine.Debug.Log($"[CharacterConfigFactory] {configId} ({fbxPath}) 无 AnimationClip，跳过 FBX 注册");
                return null;
            }

            var def = defaultAction ?? clipNames[0];
            return RegisterFBXModel(configId, fbxPath, def, clipNames.ToArray(), null);
        }

        /// <summary>
        /// 扫描 <c>Resources/</c> 下所有 FBX/Model（Editor + Build 都支持）并逐个 <see cref="RegisterFBXModelAuto"/>。
        /// <para><b>configId = FBX 文件名（不含扩展名）</b>；<b>fbxPath = Resources 相对路径（无扩展名）</b>。</para>
        /// <para><b>defaultAction</b>：按 <paramref name="defaultActionPreferences"/> 顺序命中第一个；都不命中用 clip 列表第 0 个。</para>
        /// <para><b>无 clip 的 FBX 自动跳过</b>（静态道具）。</para>
        /// <para><b>Editor</b>：<c>ResourceService</c> 启动时已用 AssetDatabase 索引；
        /// <b>Build</b>：依赖 <c>Resources/CharacterFBXManifest.json</c>（由 <c>FBXManifestBuilder</c> 在 Editor 或 Build 预处理生成）。</para>
        /// <para>本函数靠 <c>EVT_GET_ALL_MODEL_PATHS</c> + <c>EVT_GET_MODEL_CLIPS</c>，<b>不直接依赖 AssetDatabase</b>，因此 Build 也能跑。</para>
        /// </summary>
        /// <param name="subFolder">仅注册该子目录（Resources 相对，如 <c>"Models/Characters3D"</c>）；为空则全部。</param>
        /// <param name="defaultActionPreferences">defaultAction 候选名（按顺序匹配第一个命中的 clip 名）。默认 ["Idle","Idle_01","idle"]。</param>
        /// <returns>成功注册的 FBX 数量（不含被跳过的）。</returns>
        public static int RegisterAllFBXInResources(string subFolder = null,
            string[] defaultActionPreferences = null)
        {
            defaultActionPreferences ??= new[] { "Idle", "Idle_01", "idle" };
            string subFilter = string.IsNullOrEmpty(subFolder)
                ? null
                : subFolder.Replace('\\', '/').Trim('/');

            // 1) 列出所有 FBX 路径
            System.Collections.Generic.List<string> allPaths = null;
            try
            {
                // §4.1 跨模块 bare-string：ResourceManager.EVT_GET_ALL_MODEL_PATHS
                var r = EssSystem.Core.Base.Event.EventProcessor.Instance.TriggerEventMethod(
                    "GetAllModelPaths",
                    new System.Collections.Generic.List<object>());
                if (EssSystem.Core.Base.Util.ResultCode.IsOk(r) && r.Count >= 2)
                    allPaths = r[1] as System.Collections.Generic.List<string>;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[CharacterConfigFactory] EVT_GET_ALL_MODEL_PATHS 失败：{ex.Message}");
            }
            if (allPaths == null || allPaths.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[CharacterConfigFactory] 未发现任何 FBX/Model（Build 期请确认 Resources/CharacterFBXManifest.json 已生成）");
                return 0;
            }

            int registered = 0, skipped = 0;
            foreach (var rel in allPaths)
            {
                if (string.IsNullOrEmpty(rel)) continue;
                var relNorm = rel.Replace('\\', '/');

                if (subFilter != null
                    && !relNorm.StartsWith(subFilter + "/", System.StringComparison.OrdinalIgnoreCase)
                    && !relNorm.Equals(subFilter, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                var fileName = System.IO.Path.GetFileNameWithoutExtension(relNorm);
                if (string.IsNullOrEmpty(fileName)) continue;

                // 选 defaultAction
                string defaultAction = null;
                try
                {
                    // §4.1 跨模块 bare-string：ResourceManager.EVT_GET_MODEL_CLIPS
                    var r = EssSystem.Core.Base.Event.EventProcessor.Instance.TriggerEventMethod(
                        "GetModelClips",
                        new System.Collections.Generic.List<object> { relNorm });
                    if (EssSystem.Core.Base.Util.ResultCode.IsOk(r) && r.Count >= 2 &&
                        r[1] is System.Collections.Generic.List<UnityEngine.AnimationClip> clips && clips.Count > 0)
                    {
                        foreach (var pref in defaultActionPreferences)
                        {
                            foreach (var c in clips)
                                if (c != null && string.Equals(c.name, pref, System.StringComparison.Ordinal))
                                { defaultAction = c.name; break; }
                            if (defaultAction != null) break;
                        }
                        if (defaultAction == null) defaultAction = clips[0].name;
                    }
                }
                catch { /* swallow — RegisterFBXModelAuto 会再尝试并按需跳过 */ }

                var cfg = RegisterFBXModelAuto(fileName, relNorm, defaultAction);
                if (cfg != null) registered++; else skipped++;
            }
            UnityEngine.Debug.Log($"[CharacterConfigFactory] RegisterAllFBXInResources：Resources/{subFilter ?? ""} → 注册 {registered}，跳过 {skipped}（无 clip）");
            return registered;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 下确保 <paramref name="fbxResourcesPath"/>（相对 Resources/，无扩展名）所对应 FBX 同目录有同名 .controller。
        /// 若已存在则跳过；否则用 <c>FBXAnimatorControllerBuilder</c> 生成。
        /// </summary>
        private static void EnsureControllerForFBX(string fbxResourcesPath)
        {
            if (string.IsNullOrEmpty(fbxResourcesPath)) return;
            try
            {
                // 1) Resources 路径 → 资产路径：在 Assets/**/Resources/<fbxResourcesPath>.<ext> 下查
                var fileName = System.IO.Path.GetFileName(fbxResourcesPath);
                var guids = UnityEditor.AssetDatabase.FindAssets($"t:Model {fileName}");
                string fbxAssetPath = null;
                foreach (var g in guids)
                {
                    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                    var noExt = System.IO.Path.ChangeExtension(path, null).Replace('\\', '/');
                    if (noExt.EndsWith("/Resources/" + fbxResourcesPath, System.StringComparison.Ordinal) ||
                        noExt.EndsWith("Resources/" + fbxResourcesPath, System.StringComparison.Ordinal))
                    { fbxAssetPath = path; break; }
                }
                if (fbxAssetPath == null) return; // 找不到精确匹配就放弃（不阻塞注册）

                // 2) 同目录 .controller 已存在 → 跳过
                var dir = System.IO.Path.GetDirectoryName(fbxAssetPath)?.Replace('\\', '/') ?? "";
                var name = System.IO.Path.GetFileNameWithoutExtension(fbxAssetPath);
                var ctrlPath = $"{dir}/{name}.controller";
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(ctrlPath) != null) return;

                // 3) 收集 FBX 内 clip → 创建 controller 添 state
                var subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(fbxAssetPath);
                var clipList = new System.Collections.Generic.List<UnityEngine.AnimationClip>();
                foreach (var a in subAssets)
                {
                    if (a is UnityEngine.AnimationClip ac &&
                        !ac.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                        clipList.Add(ac);
                }
                if (clipList.Count == 0) return;

                var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var sm = ctrl.layers[0].stateMachine;
                for (int i = sm.states.Length - 1; i >= 0; i--) sm.RemoveState(sm.states[i].state);
                for (int i = 0; i < clipList.Count; i++)
                {
                    var st = sm.AddState(clipList[i].name);
                    st.motion = clipList[i];
                    if (i == 0) sm.defaultState = st;
                }
                UnityEditor.EditorUtility.SetDirty(ctrl);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEngine.Debug.Log($"[CharacterConfigFactory] 自动生成 Controller: {ctrlPath}（{clipList.Count} 个 state）");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[CharacterConfigFactory] EnsureControllerForFBX 失败: {fbxResourcesPath} → {ex.Message}");
            }
        }
#endif
    }
}
