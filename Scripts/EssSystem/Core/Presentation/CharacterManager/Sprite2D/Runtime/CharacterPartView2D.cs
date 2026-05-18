using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Presentation.CharacterManager.Dao;
// §4.1 跨模块走 bare-string 协议，不 using ResourceManager

namespace EssSystem.Core.Presentation.CharacterManager.Runtime
{
    /// <summary>
    /// 2D 单部件 View —— 通过 <see cref="SpriteRenderer"/> 渲染；
    /// 根据 <see cref="CharacterPartConfig.PartType"/> 决定行为：
    /// <list type="bullet">
    /// <item><b>Static</b>：加载一次 <see cref="CharacterPartConfig.StaticSpriteId"/>，永不切换。</item>
    /// <item><b>Dynamic</b>：按 <see cref="CharacterActionConfig.SpriteIds"/> 序列循环切帧。</item>
    /// </list>
    /// 通过 ResourceManager 的 <c>GetResource</c> Event 异步获取 Sprite。
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPartView2D : CharacterPartView
    {
        /// <summary>Sprite 渲染器（由 <see cref="OnSetup"/> 自动添加）。</summary>
        public SpriteRenderer Renderer { get; private set; }

        // ─── Dynamic 状态
        private CharacterActionConfig _currentAction;
        private int   _frameIndex;
        private float _frameTimer;
        private bool  _playing;

        // 缓存已加载的 Sprite（避免重复触发 GetResource Event）
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // ─── Sheet 模式专用 ─────────────────────────────────────────
        /// <summary>当前朝向（-1 / 0 / +1）；<see cref="CharacterActionConfig.DirectionalFrameIndices"/>
        /// 命中时按此挑选帧索引序列。</summary>
        public int Direction { get; private set; } = 1;

        // sheet path → 排序后的 sub-sprites（按 sprite.name 末尾数字升序）
        private static readonly Dictionary<string, Sprite[]> _sheetCache = new Dictionary<string, Sprite[]>();
        private Sprite[] _currentSheet;          // 当前 action 引用的 sheet sub-sprites（已排序）
        private int[]    _currentFrameIndices;   // 选定的帧索引序列（按 Direction 派遣）

        public override bool CanPivotComplete =>
            Config != null && Config.PartType == CharacterPartType.Dynamic;

        #region Setup / Public API

        protected override void OnSetup()
        {
            Renderer = GetComponent<SpriteRenderer>();
            if (Renderer == null) Renderer = gameObject.AddComponent<SpriteRenderer>();
            Renderer.color = Config.Color;
            Renderer.sortingOrder = Config.SortingOrder;

            switch (Config.PartType)
            {
                case CharacterPartType.Static:
                    LoadSprite(Config.StaticSpriteId, sp => { if (Renderer != null) Renderer.sprite = sp; });
                    break;

                case CharacterPartType.Dynamic:
                    if (!string.IsNullOrEmpty(Config.DefaultActionName))
                        Play(Config.DefaultActionName);
                    break;
            }
        }

        /// <summary>开始播放指定动作（仅 Dynamic 部件有效）；动作不存在则停止当前播放。</summary>
        public override bool Play(string actionName)
        {
            if (Config == null || Config.PartType != CharacterPartType.Dynamic) return false;

            var action = Config.GetAction(actionName);
            if (action == null) { StopInternal(); return false; }

            // 两种数据源：① Sheet 模式（SheetResourcePath 非空） ② SpriteIds 模式
            if (!string.IsNullOrEmpty(action.SheetResourcePath))
            {
                var sheet = LoadSheet(action.SheetResourcePath);
                if (sheet == null || sheet.Length == 0) { StopInternal(); return false; }
                _currentSheet = sheet;
                _currentFrameIndices = ResolveFrameIndices(action, sheet.Length);
                if (_currentFrameIndices == null || _currentFrameIndices.Length == 0) { StopInternal(); return false; }
            }
            else
            {
                if (action.SpriteIds == null || action.SpriteIds.Count == 0) { StopInternal(); return false; }
                _currentSheet = null;
                _currentFrameIndices = null;
            }

            _currentAction = action;
            _frameIndex = 0;
            _frameTimer = 0f;
            _playing = true;

            ApplyFrame();
            return true;
        }

        /// <summary>设置朝向 sign（-1 / 0 / +1）。命中当前 action 的 <see cref="CharacterActionConfig.DirectionalFrameIndices"/>
        /// 时即时切换到对应帧序列；不命中则保持当前序列。</summary>
        public void SetDirection(int direction)
        {
            var d = direction == 0 ? 0 : (direction > 0 ? 1 : -1);
            if (Direction == d) return;
            Direction = d;
            // 只在 Sheet 模式 + 当前 action 含方向变体时刷新
            if (_currentAction == null || string.IsNullOrEmpty(_currentAction.SheetResourcePath)) return;
            if (_currentAction.DirectionalFrameIndices == null) return;
            if (_currentSheet == null) return;

            var newIndices = ResolveFrameIndices(_currentAction, _currentSheet.Length);
            if (newIndices == null || newIndices.Length == 0) return;
            _currentFrameIndices = newIndices;
            _frameIndex = Mathf.Clamp(_frameIndex, 0, newIndices.Length - 1);
            ApplyFrame();
        }

        private void StopInternal()
        {
            _playing = false;
            _currentAction = null;
            _currentSheet = null;
            _currentFrameIndices = null;
        }

        /// <summary>停止当前动作，停在最后一次贴上去的 Sprite 上。</summary>
        public override void Stop()
        {
            _playing = false;
        }

        /// <summary>
        /// 运行时切换静态 Sprite —— 仅 Static 部件有效；同步更新 <see cref="CharacterPartConfig.StaticSpriteId"/>。
        /// 若 spriteId 为空则清空 Sprite。
        /// </summary>
        public void SetStaticSprite(string spriteId)
        {
            if (Config == null || Config.PartType != CharacterPartType.Static) return;
            Config.StaticSpriteId = spriteId ?? string.Empty;
            if (string.IsNullOrEmpty(spriteId))
            {
                if (Renderer != null) Renderer.sprite = null;
                return;
            }
            LoadSprite(spriteId, sp => { if (Renderer != null) Renderer.sprite = sp; });
        }

        #endregion

        #region Loop

        private void Update()
        {
            if (!_playing || _currentAction == null) return;
            var frameCount = GetCurrentFrameCount();
            if (frameCount <= 0) return;

            _frameTimer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(0.01f, _currentAction.FrameRate);
            if (_frameTimer < frameDuration) return;

            _frameTimer -= frameDuration;
            _frameIndex++;

            var completed = false;
            if (_frameIndex >= frameCount)
            {
                if (_currentAction.Loop)
                    _frameIndex = 0;
                else
                {
                    _frameIndex = frameCount - 1;
                    _playing = false;
                    completed = true;
                }
            }

            ApplyFrame();

            // 非循环动作播到末帧后触发完成回调 —— 放在 ApplyFrame 之后，
            // 确保监听者读到的是已贴上最后一帧的稳态
            if (completed)
            {
                RaiseActionComplete(_currentAction.ActionName);
            }
        }

        /// <summary>当前动作总帧数（Sheet 模式取 _currentFrameIndices.Length；SpriteIds 模式取 list.Count）。</summary>
        private int GetCurrentFrameCount()
        {
            if (_currentAction == null) return 0;
            if (_currentFrameIndices != null) return _currentFrameIndices.Length;
            return _currentAction.SpriteIds?.Count ?? 0;
        }

        private void ApplyFrame()
        {
            if (_currentAction == null || Renderer == null) return;

            // Sheet 模式：直接索引 sub-sprites
            if (_currentSheet != null && _currentFrameIndices != null)
            {
                if (_frameIndex < 0 || _frameIndex >= _currentFrameIndices.Length) return;
                var subIdx = _currentFrameIndices[_frameIndex];
                if (subIdx < 0 || subIdx >= _currentSheet.Length) return;
                Renderer.sprite = _currentSheet[subIdx];
            }
            else
            {
                // SpriteIds 模式：通过 GetSprite 异步加载
                if (_frameIndex < 0 || _frameIndex >= _currentAction.SpriteIds.Count) return;
                LoadSprite(_currentAction.SpriteIds[_frameIndex], sp =>
                {
                    if (Renderer != null) Renderer.sprite = sp;
                });
            }

            // 帧事件：某帧触发业务层监听的自定义事件（伤害判定 / 音效 / 粒子 等）
            if (_currentAction.FrameEvents != null
                && _currentAction.FrameEvents.TryGetValue(_frameIndex, out var evtName))
            {
                BroadcastFrameEvent(evtName, _currentAction.ActionName, _frameIndex);
            }
        }

        // ─── Sheet 加载 + 帧索引解析 ─────────────────────────────────
        /// <summary>从 Resources 加载 sheet（按 sprite.name 末尾数字升序排序）；命中静态缓存则同步返回。</summary>
        private static Sprite[] LoadSheet(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath)) return null;
            if (_sheetCache.TryGetValue(resourcePath, out var cached)) return cached;
            var sprites = Resources.LoadAll<Sprite>(resourcePath);
            if (sprites == null || sprites.Length == 0)
            {
                Debug.LogWarning($"[CharacterPartView2D] Sheet 加载失败：{resourcePath}");
                _sheetCache[resourcePath] = null;
                return null;
            }
            System.Array.Sort(sprites, (a, b) => GetSpriteTrailingIndex(a.name).CompareTo(GetSpriteTrailingIndex(b.name)));
            _sheetCache[resourcePath] = sprites;
            return sprites;
        }

        /// <summary>根据当前 Direction 解析 action 的帧索引序列，回退到 SheetFrameIndices；
        /// 都为空时返回 [0..sheetLen-1]（顺序播放全部 sub-sprite）。</summary>
        private int[] ResolveFrameIndices(CharacterActionConfig action, int sheetLen)
        {
            if (action.DirectionalFrameIndices != null
                && action.DirectionalFrameIndices.TryGetValue(Direction, out var dirIndices)
                && dirIndices != null && dirIndices.Length > 0)
            {
                return dirIndices;
            }
            if (action.SheetFrameIndices != null && action.SheetFrameIndices.Length > 0)
                return action.SheetFrameIndices;
            // 默认：顺序播全部
            var all = new int[sheetLen];
            for (var i = 0; i < sheetLen; i++) all[i] = i;
            return all;
        }

        /// <summary>从 sprite.name 末尾 "_N" 提取索引数字（与 Tribe 旧 animator 排序兼容）。</summary>
        private static int GetSpriteTrailingIndex(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return 0;
            var underscore = spriteName.LastIndexOf('_');
            if (underscore < 0 || underscore >= spriteName.Length - 1) return 0;
            return int.TryParse(spriteName.Substring(underscore + 1), out var index) ? index : 0;
        }

        #endregion

        #region Sprite Loading

        /// <summary>
        /// 通过 <c>GetResource</c> Event 加载 Sprite（命中缓存则同步回调）。
        /// 加载失败时回调不被调用，渲染器保留上一帧。
        /// </summary>
        private void LoadSprite(string spriteId, System.Action<Sprite> onLoaded)
        {
            if (string.IsNullOrEmpty(spriteId) || onLoaded == null) return;

            if (_spriteCache.TryGetValue(spriteId, out var cached))
            {
                onLoaded(cached);
                return;
            }

            try
            {
                // §4.1 跨模块 bare-string façade 调用 ResourceManager.EVT_GET_SPRITE；
                //      ResourceManager.GetSprite 内部加 type tag 转发 EVT_GET_RESOURCE。
                var result = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite",
                    new List<object> { spriteId });

                if (result != null && result.Count >= 2 && ResultCode.IsOk(result))
                {
                    var sprite = result[1] as Sprite;
                    if (sprite != null)
                    {
                        _spriteCache[spriteId] = sprite;
                        onLoaded(sprite);
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[CharacterPartView2D] 加载 Sprite 失败: {spriteId} → {ex.Message}");
            }
        }

        #endregion
    }
}
