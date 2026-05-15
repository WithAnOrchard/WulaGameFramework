using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Base.Event;
using EssSystem.Core.EssManagers.Presentation.UIManager.Dao.CommonComponents;

namespace Demo.Tribe.Player
{
    /// <summary>
    /// <see cref="TribePlayer"/> 的 HUD 视图 —— 独立 MonoBehaviour 组件，
    /// 负责构建 / 更新 / 销毁顶部血/蓝/经验条、头像、金币显示。
    /// <para>外部职责（由 Player 持有）：游戏数值的权威状态。本组件只读快照、写显示。</para>
    /// <para>调用顺序：
    /// <list type="number">
    /// <item><see cref="Build"/>（Player.Start 时一次）</item>
    /// <item><see cref="AttachHeadSprite"/>（Character 创建好后，由 Player 触发；可重试）</item>
    /// <item><see cref="SetStats"/>（每帧或数值变化时）</item>
    /// </list></para>
    /// </summary>
    [DisallowMultipleComponent]
    public class TribePlayerHud : MonoBehaviour
    {
        // ─── 引用 ────────────────────────────────────────────────
        private string _hudId;
        private Transform _characterRoot;
        private UIBarComponent _hpBar, _mpBar, _expBar;
        private UITextComponent _hpText, _mpText, _expText, _coinsText;
        private UIPanelComponent _headSprite;
        private bool _headSpriteReady;
        private bool _registered;

        // ─── 缓存（避免每帧重写 UIText）────────────────────────────
        private float _lastHp = -1f, _lastMaxHp = -1f, _lastMp = -1f, _lastMaxMp = -1f;
        private int _lastExp = -1, _lastMaxExp = -1, _lastCoins = -1;

        /// <summary>是否已成功向 UIManager 注册。</summary>
        public bool IsBuilt => _registered;

        /// <summary>HUD 在 UIManager 中的根实体 Id（= "{instanceId}_Hud"）。</summary>
        public string HudId => _hudId;

        /// <summary>构建 HUD 实体并向 UIManager 注册。<paramref name="instanceId"/> 用于派生唯一 Id。</summary>
        public bool Build(string instanceId, Transform characterRoot)
        {
            if (_registered) return true;
            if (!EventProcessor.HasInstance) return false;

            _hudId = $"{instanceId}_Hud";
            _characterRoot = characterRoot;

            var hud = new UIPanelComponent(_hudId, "TribePlayerHud")
                .SetPosition(16f, -16f).SetSize(560f, 132f)
                .SetBackgroundColor(new Color(0f, 0f, 0f, 0f)).SetVisible(true);

            var headFrame = new UIPanelComponent($"{_hudId}_head", "Head")
                .SetPosition(66f, -62f).SetSize(112f, 112f)
                .SetBackgroundSpriteId("Head").SetBackgroundColor(Color.white).SetVisible(true);
            _headSprite = new UIPanelComponent($"{_hudId}_head_sprite", "HeadSprite")
                .SetPosition(56f, 56f).SetSize(140f, 140f)
                .SetBackgroundColor(Color.white).SetVisible(true);

            _hpBar  = MakeBar("hp",  264f, -18f, 280f, 28f, "Bar_1", "RedBar",   new Color(1f, 0.25f, 0.25f));
            _mpBar  = MakeBar("mp",  239f, -44f, 230f, 24f, "Bar_2", "BlueBar",  new Color(0.25f, 0.55f, 1f));
            _expBar = MakeBar("exp", 239f, -68f, 230f, 24f, "Bar_2", "BrownBar", new Color(0.55f, 0.32f, 0.15f));

            var coinContainer = new UIPanelComponent($"{_hudId}_coins_bg", "CoinContainer")
                .SetPosition(174f, -92.5f).SetSize(100f, 25f)
                .SetBackgroundSpriteId("CoinContainer").SetBackgroundColor(Color.white).SetVisible(true);

            _hpText    = MakeValueText("hp_value",  280f, 28f);
            _mpText    = MakeValueText("mp_value",  230f, 24f);
            _expText   = MakeValueText("exp_value", 230f, 24f);
            _coinsText = MakeValueText("coins",     100f, 25f);

            _hpBar.AddChild(_hpText);
            _mpBar.AddChild(_mpText);
            _expBar.AddChild(_expText);
            coinContainer.AddChild(_coinsText);
            headFrame.AddChild(_headSprite);
            hud.AddChild(headFrame);
            hud.AddChild(_hpBar);
            hud.AddChild(_mpBar);
            hud.AddChild(_expBar);
            hud.AddChild(coinContainer);

            if (!ResultCode.IsOk(EventProcessor.Instance.TriggerEventMethod(
                    "RegisterUIEntity", new List<object> { _hudId, hud })))
            {
                Debug.LogWarning("[TribePlayerHud] 注册 HUD 失败");
                return false;
            }

            _registered = true;
            AnchorTopLeft();
            AttachHeadSprite();
            return true;
        }

        /// <summary>销毁 HUD 实体（由 Player.OnDestroy 调用）。</summary>
        public void Dispose()
        {
            if (!_registered) return;
            _registered = false;
            // ApplicationLifecycle.IsQuitting 信号已集成到 HasInstance + EventProcessor 内部，
            // teardown 期事件分发会 silent-return，无需 try/catch 兜底。
            if (EventProcessor.HasInstance)
                EventProcessor.Instance.TriggerEventMethod("UnregisterUIEntity", new List<object> { _hudId });
        }

        /// <summary>把 Character 头部 SpriteRenderer 的 sprite 同步到 HUD 头像格内。可重复调用直到成功。</summary>
        public void AttachHeadSprite()
        {
            if (!_registered || _headSpriteReady || _headSprite == null) return;
            if (_characterRoot == null) return;
            var head = _characterRoot.Find("Head");
            var renderer = head != null ? head.GetComponent<SpriteRenderer>() : null;
            if (renderer == null || renderer.sprite == null) return;

            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { _headSprite.Id });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject headGo) return;
            var image = headGo.GetComponent<UnityEngine.UI.Image>();
            if (image == null) return;
            image.sprite = renderer.sprite;
            image.color = Color.white;
            image.preserveAspect = true;
            _headSpriteReady = true;
        }

        /// <summary>提交一帧数值快照；内部 diff 缓存避免重复写 UI。<paramref name="force"/> = true 强刷。</summary>
        public void SetStats(float hp, float maxHp, float mp, float maxMp, int exp, int maxExp, int coins, bool force = false)
        {
            if (!_registered) return;
            if (!_headSpriteReady) AttachHeadSprite();

            if (force || !Mathf.Approximately(hp, _lastHp) || !Mathf.Approximately(maxHp, _lastMaxHp))
            {
                _lastHp = hp; _lastMaxHp = maxHp;
                _hpBar.SetValue(hp, maxHp);
                _hpText.SetText($"{Mathf.CeilToInt(hp)}/{Mathf.CeilToInt(maxHp)}");
            }
            if (force || !Mathf.Approximately(mp, _lastMp) || !Mathf.Approximately(maxMp, _lastMaxMp))
            {
                _lastMp = mp; _lastMaxMp = maxMp;
                _mpBar.SetValue(mp, maxMp);
                _mpText.SetText($"{Mathf.CeilToInt(mp)}/{Mathf.CeilToInt(maxMp)}");
            }
            if (force || exp != _lastExp || maxExp != _lastMaxExp)
            {
                _lastExp = exp; _lastMaxExp = maxExp;
                _expBar.SetValue(exp, maxExp);
                _expText.SetText($"{exp}/{maxExp}");
            }
            if (force || coins != _lastCoins)
            {
                _lastCoins = coins;
                _coinsText.SetText(coins.ToString());
            }
        }

        // ─── 内部 ────────────────────────────────────────────────
        private void AnchorTopLeft()
        {
            var result = EventProcessor.Instance.TriggerEventMethod(
                "GetUIGameObject", new List<object> { _hudId });
            if (!ResultCode.IsOk(result) || result.Count < 2 || result[1] is not GameObject hudGo) return;
            if (!hudGo.TryGetComponent<RectTransform>(out var rect)) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(16f, -16f);
        }

        private UIBarComponent MakeBar(string suffix, float x, float y, float w, float h,
            string bgSprite, string fillSprite, Color fillColor) =>
            new UIBarComponent($"{_hudId}_{suffix}_bar", suffix)
                .SetPosition(x, y).SetSize(w, h).SetRange(0f, 1f).SetValue(1f)
                .SetBackgroundSpriteId(bgSprite).SetFillSpriteId(fillSprite)
                .SetFillPadding(10f, 6f).SetBackgroundColor(Color.white).SetFillColor(fillColor).SetVisible(true);

        /// <summary>HUD 数值文本：4x 超采样消除缩放走样。</summary>
        private UITextComponent MakeValueText(string suffix, float w, float h)
        {
            const float s = 4f;
            return new UITextComponent($"{_hudId}_{suffix}", suffix)
                .SetPosition(0f, 0f).SetSize(w * s, h * s).SetScale(1f / s, 1f / s)
                .SetFontSize(Mathf.RoundToInt(18f * s)).SetColor(Color.white)
                .SetAlignment(TextAnchor.MiddleCenter).SetText(string.Empty).SetVisible(true);
        }
    }
}
