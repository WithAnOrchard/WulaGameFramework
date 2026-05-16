using System;
using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager.Dao;

namespace EssSystem.Core.Presentation.CharacterManager.Runtime
{
    /// <summary>
    /// 2D Sprite + <b>AnimatorOverrideController</b> 部件 View。
    /// <para>架构：每个 Part = <see cref="SpriteRenderer"/> + <see cref="UnityEngine.Animator"/>，
    /// 加载 base <c>RuntimeAnimatorController</c>（一次性 Editor 生成），运行时用
    /// <see cref="AnimatorOverrideController"/> 把每个 state 的 placeholder clip 替换成
    /// 代码 <c>new AnimationClip()</c>（仅设 <c>frameRate / length / wrapMode</c>，**不写 sprite 关键帧**）。</para>
    /// <para>Sprite 切换：<see cref="Update"/> 读 <see cref="Animator"/> 当前 state 的 normalizedTime →
    /// 计算帧索引 → 直接写 <see cref="SpriteRenderer.sprite"/>。</para>
    /// <para><b>Loop=false</b>：clip wrapMode = <c>ClampForever</c> + 监听 normalizedTime ≥ 1
    /// 手动触发 <see cref="CharacterPartView.OnActionComplete"/>。</para>
    /// <para><b>前提</b>：在 Editor 跑过一次 <c>Tools/Character/Build Sprite Animator Base Controller</c>
    /// 生成 <c>Resources/Generated/CharacterAnimBase.controller</c>。</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPartView2DAnimator : CharacterPartView
    {
        // base controller 路径（与 Editor 工具常量同步）
        private const string BaseControllerResourcePath = "Generated/CharacterAnimBase";

        // 必须与 base controller 的 state 名（顺序无关）一致 —— Walk/Idle/Jump/Attack/Defend/Damage/Death/Special
        private static readonly HashSet<string> StandardStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Walk", "Idle", "Jump", "Attack", "Defend", "Damage", "Death", "Special" };

        public SpriteRenderer Renderer { get; private set; }
        public Animator       Animator { get; private set; }

        /// <summary>所属 CharacterConfig 的 ConfigId（保留，便于扩展）。</summary>
        public string OwnerConfigId { get; set; }

        public override bool CanPivotComplete => true;

        private AnimatorOverrideController _override;
        private readonly Dictionary<string, Sprite[]>             _frameCache  = new Dictionary<string, Sprite[]>();
        private readonly Dictionary<string, Sprite>               _spriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, CharacterActionConfig> _actionByName = new Dictionary<string, CharacterActionConfig>(StringComparer.OrdinalIgnoreCase);

        // 当前播放状态
        private CharacterActionConfig _currentAction;
        private string _currentStateName;
        private int    _currentStateHash;
        private bool   _completeFired;
        private bool   _initialized;

        #region Setup

        protected override void OnSetup()
        {
            // 1) 基础组件
            Renderer = GetComponent<SpriteRenderer>();
            if (Renderer == null) Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.color = Config.Color;
            Renderer.sortingOrder = Config.SortingOrder;

            Animator = GetComponent<Animator>();
            if (Animator == null) Animator = gameObject.AddComponent<Animator>();
            Animator.applyRootMotion = false;
            // sprite-swap 不依赖 Renderer 在屏内（默认 culling 会停 Animator → 帧不前进）
            Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // 2) 加载 base controller，包成 override
            var baseCtrl = Resources.Load<RuntimeAnimatorController>(BaseControllerResourcePath);
            if (baseCtrl == null)
            {
                Debug.LogWarning($"[CharacterPartView2DAnimator] 未找到 base AnimatorController: " +
                                 $"Resources/{BaseControllerResourcePath}.controller —— " +
                                 $"请先跑 Tools/Character/Build Sprite Animator Base Controller");
                return;
            }
            _override = new AnimatorOverrideController(baseCtrl);
            Animator.runtimeAnimatorController = _override;

            // 3) 索引 config 内的所有 actions
            if (Config.Animations != null)
            {
                foreach (var a in Config.Animations)
                    if (a != null && !string.IsNullOrEmpty(a.ActionName))
                        _actionByName[a.ActionName] = a;
            }

            // 4) 为每个 action 在 override 里替换 state 对应 clip（用代码 new AnimationClip）
            BuildAllOverrideClips();

            _initialized = true;

            // 5) 静态部件：直接贴 sprite，不走 Animator
            if (Config.PartType == CharacterPartType.Static)
            {
                LoadSpriteAsync(Config.StaticSpriteId, sp => { if (Renderer != null) Renderer.sprite = sp; });
                return;
            }

            // 6) 默认动作
            if (!string.IsNullOrEmpty(Config.DefaultActionName))
                Play(Config.DefaultActionName);
        }

        private void OnDestroy()
        {
            if (_override != null)
            {
                // override 自身是 ScriptableObject —— 由 GC 回收，不需要 Destroy（注意：base ctrl 是 Resource asset，绝不能 Destroy）
                _override = null;
            }
        }

        #endregion

        #region Override controller 构建

        /// <summary>遍历 config 的所有 action，替换 base controller 中对应 state 的 placeholder clip。</summary>
        private void BuildAllOverrideClips()
        {
            if (_override == null) return;

            // 拷贝当前所有 override pair（key = base 中的 placeholder clip，value = override 后的 clip）
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            _override.GetOverrides(pairs);

            // pair.Key.name 就是 state 名（CharacterAnimatorBaseControllerBuilder 把 placeholder 命名为 state 名）
            for (int i = 0; i < pairs.Count; i++)
            {
                var key = pairs[i].Key;
                if (key == null) continue;
                var stateName = key.name;
                if (!_actionByName.TryGetValue(stateName, out var action) || action == null) continue;
                var newClip = CreateRuntimeClip(action);
                pairs[i] = new KeyValuePair<AnimationClip, AnimationClip>(key, newClip);
            }
            _override.ApplyOverrides(pairs);
        }

        /// <summary>纯运行时创建 AnimationClip：仅设 frameRate / length / wrapMode。无 sprite 关键帧（sprite 由 Update 手动 swap）。</summary>
        private static AnimationClip CreateRuntimeClip(CharacterActionConfig action)
        {
            var fps = Mathf.Max(0.01f, action.FrameRate);
            var count = Mathf.Max(1, action.SpriteIds?.Count ?? 1);
            var length = count / fps;

            var clip = new AnimationClip
            {
                name = action.ActionName,
                frameRate = fps,
                wrapMode = action.Loop ? WrapMode.Loop : WrapMode.ClampForever,
                legacy = false,
            };

            // 用一条 dummy float curve 把 length 顶起来（ AnimationClip.length 来自 curves 的最大时间）。
            // path 指向不存在的子节点 → 不影响任何实际属性。
            clip.SetCurve("__length_stub__", typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0f, 0f, length, 0f));

            return clip;
        }

        #endregion

        #region Public API

        public override bool Play(string actionName)
        {
            if (!_initialized || Animator == null) return false;
            if (string.IsNullOrEmpty(actionName)) return false;
            if (!_actionByName.TryGetValue(actionName, out var action) || action == null) return false;
            if (!StandardStates.Contains(actionName))
            {
                Debug.LogWarning($"[CharacterPartView2DAnimator] action '{actionName}' 不在 base controller 标准 state 列表 —— 已忽略");
                return false;
            }

            // 准备 sprite 数组（按 actionName 缓存）
            EnsureSpriteCache(action);

            _currentAction = action;
            _currentStateName = actionName;
            _completeFired = false;

            // 强制从头播；layer 0
            Animator.Play(actionName, 0, 0f);
            Animator.Update(0f); // 立即 evaluate 一次，避免第一帧 sprite 还是上次
            _currentStateHash = Animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

            return true;
        }

        public override void Stop()
        {
            if (Animator != null) Animator.speed = 0f;
            _completeFired = false;
        }

        #endregion

        #region 每帧 sprite swap

        private void Update()
        {
            if (!_initialized || _currentAction == null || Animator == null || Renderer == null) return;
            if (!_frameCache.TryGetValue(_currentStateName, out var frames) || frames == null || frames.Length == 0) return;

            var info = Animator.GetCurrentAnimatorStateInfo(0);
            // 状态被外部切换（不是我们的当前 state）→ 停止干预
            if (info.shortNameHash != _currentStateHash) return;

            var nt = info.normalizedTime;
            var loop = _currentAction.Loop;
            int idx;

            if (loop)
            {
                var f = nt - Mathf.Floor(nt); // [0,1)
                idx = Mathf.FloorToInt(f * frames.Length);
                if (idx >= frames.Length) idx = frames.Length - 1;
                if (idx < 0) idx = 0;
            }
            else
            {
                idx = Mathf.FloorToInt(nt * frames.Length);
                if (idx >= frames.Length - 1)
                {
                    idx = frames.Length - 1;
                    if (!_completeFired)
                    {
                        _completeFired = true;
                        RaiseActionComplete(_currentStateName);
                    }
                }
                else if (idx < 0) idx = 0;
            }

            var sp = frames[idx];
            if (sp != null) Renderer.sprite = sp;
        }

        #endregion

        #region Sprite 加载

        private void EnsureSpriteCache(CharacterActionConfig action)
        {
            if (_frameCache.ContainsKey(action.ActionName)) return;
            var arr = new Sprite[action.SpriteIds?.Count ?? 0];
            _frameCache[action.ActionName] = arr;
            if (action.SpriteIds == null) return;
            for (int i = 0; i < action.SpriteIds.Count; i++)
            {
                var idx = i;
                LoadSpriteAsync(action.SpriteIds[i], sp => { arr[idx] = sp; });
            }
        }

        private void LoadSpriteAsync(string spriteId, Action<Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(spriteId) || onLoaded == null) return;
            if (_spriteCache.TryGetValue(spriteId, out var cached) && cached != null)
            {
                onLoaded(cached);
                return;
            }
            try
            {
                // §4.1 跨模块 bare-string façade：ResourceManager.EVT_GET_SPRITE
                var result = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite", new List<object> { spriteId });
                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                {
                    var sp = result[1] as Sprite;
                    if (sp != null)
                    {
                        _spriteCache[spriteId] = sp;
                        onLoaded(sp);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterPartView2DAnimator] 加载 Sprite 失败: {spriteId} → {ex.Message}");
            }
        }

        #endregion
    }
}
