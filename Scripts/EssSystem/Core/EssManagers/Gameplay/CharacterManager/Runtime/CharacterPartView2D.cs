using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core;
using EssSystem.Core.Event;
using EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao;
// §4.1 跨模块走 bare-string 协议，不 using ResourceManager

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Runtime
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
            if (action == null || action.SpriteIds == null || action.SpriteIds.Count == 0)
            {
                _playing = false;
                _currentAction = null;
                return false;
            }

            _currentAction = action;
            _frameIndex = 0;
            _frameTimer = 0f;
            _playing = true;

            // 立即贴第一帧
            ApplyFrame();
            return true;
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
            if (_currentAction.SpriteIds == null || _currentAction.SpriteIds.Count == 0) return;

            _frameTimer += Time.deltaTime;
            var frameDuration = 1f / Mathf.Max(0.01f, _currentAction.FrameRate);
            if (_frameTimer < frameDuration) return;

            _frameTimer -= frameDuration;
            _frameIndex++;

            var completed = false;
            if (_frameIndex >= _currentAction.SpriteIds.Count)
            {
                if (_currentAction.Loop)
                    _frameIndex = 0;
                else
                {
                    _frameIndex = _currentAction.SpriteIds.Count - 1;
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

        private void ApplyFrame()
        {
            if (_currentAction == null || Renderer == null) return;
            if (_frameIndex < 0 || _frameIndex >= _currentAction.SpriteIds.Count) return;

            LoadSprite(_currentAction.SpriteIds[_frameIndex], sp =>
            {
                if (Renderer != null) Renderer.sprite = sp;
            });

            // 帧事件：某帧触发业务层监听的自定义事件（伤害判定 / 音效 / 粒子 等）
            if (_currentAction.FrameEvents != null
                && _currentAction.FrameEvents.TryGetValue(_frameIndex, out var evtName))
            {
                BroadcastFrameEvent(evtName, _currentAction.ActionName, _frameIndex);
            }
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
