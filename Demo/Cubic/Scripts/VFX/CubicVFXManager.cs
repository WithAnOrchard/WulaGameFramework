using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;

namespace Demo.Cubic.VFX
{
    /// <summary>
    /// Cubic VFX 桥接层 —— 走框架 <c>EffectsManager</c> 的 Event API（bare-string §4.1）。
    /// <para>
    /// 原 homegrown <c>CreateVFXEffect</c>（自己 new GameObject + AddComponent&lt;SpriteRenderer&gt; + 程序化贴图）、
    /// <c>CreateScreenFlash</c>（自己 new Canvas + Image）全部删除 —— 这些都属于 §5/§7 反模式。
    /// </para>
    /// <para>
    /// 调用方通过 <see cref="PlaySkillVFX(string, Vector3)"/> 触发特效，框架会按
    /// <c>EVT_PLAY_VFX</c> → <c>ResourceManager.GetPrefab</c> → 对象池取/还 实例。
    /// </para>
    /// </summary>
    public static class CubicVFXManager
    {
        /// <summary>屏幕闪光类型 —— 保留为业务公共 API，由 <see cref="PlayScreenFlash"/> 翻译为颜色+时长。</summary>
        public enum ScreenFlashType
        {
            Damage,
            Crit,
            Heal,
            Freeze,
            Poison,
            Execute,
        }

        private static bool _initialized;

        // ════════════════════════════════════════════════════════════
        //  vfxId → Resources 路径 占位映射
        //  资源实际 prefab 留给用户在 Unity Editor 里拖入；本文件只登记映射
        //  + 触发播放。具体路径与 VFX 视觉美术解耦。
        // ════════════════════════════════════════════════════════════
        private struct VfxRegistration
        {
            public string Id;
            public string PrefabPath;
            public float AutoDestroy;
        }

        private static readonly List<VfxRegistration> _registrations = new()
        {
            // ─── 战士 ────────────────────────────────────────────
            new() { Id = "warrior_slash",     PrefabPath = "VFX/Cubic/WarriorSlash",     AutoDestroy = 0.4f },
            new() { Id = "warrior_shout",     PrefabPath = "VFX/Cubic/WarriorShout",     AutoDestroy = 0.6f },
            new() { Id = "warrior_whirlwind", PrefabPath = "VFX/Cubic/WarriorWhirlwind", AutoDestroy = 1.2f },
            new() { Id = "warrior_hit",       PrefabPath = "VFX/Cubic/HitBlood",         AutoDestroy = 0.3f },

            // ─── 魔法师 ──────────────────────────────────────────
            new() { Id = "mage_fireball",  PrefabPath = "VFX/Cubic/MageFireball",  AutoDestroy = 0.8f },
            new() { Id = "mage_frost_nova",PrefabPath = "VFX/Cubic/MageFrostNova",AutoDestroy = 0.6f },
            new() { Id = "mage_lightning", PrefabPath = "VFX/Cubic/MageLightning", AutoDestroy = 0.5f },
            new() { Id = "mage_impact",    PrefabPath = "VFX/Cubic/MageImpact",    AutoDestroy = 0.3f },

            // ─── 弓箭手 ──────────────────────────────────────────
            new() { Id = "archer_arrow",  PrefabPath = "VFX/Cubic/ArcherArrow",  AutoDestroy = 0.3f },
            new() { Id = "archer_pierce",  PrefabPath = "VFX/Cubic/ArcherPierce", AutoDestroy = 0.5f },
            new() { Id = "archer_dash",    PrefabPath = "VFX/Cubic/ArcherDash",    AutoDestroy = 0.3f },
            new() { Id = "archer_hit",     PrefabPath = "VFX/Cubic/HitBlood",      AutoDestroy = 0.3f },

            // ─── 圣骑士 ──────────────────────────────────────────
            new() { Id = "paladin_holy",     PrefabPath = "VFX/Cubic/PaladinHoly",    AutoDestroy = 0.5f },
            new() { Id = "paladin_hammer",   PrefabPath = "VFX/Cubic/PaladinHammer",  AutoDestroy = 0.6f },
            new() { Id = "paladin_devotion", PrefabPath = "VFX/Cubic/PaladinDevotion",AutoDestroy = 1.5f },
            new() { Id = "paladin_heal",     PrefabPath = "VFX/Cubic/PaladinHeal",    AutoDestroy = 0.6f },

            // ─── 通用 ────────────────────────────────────────────
            new() { Id = "hit_blood",  PrefabPath = "VFX/Cubic/HitBlood",  AutoDestroy = 0.3f },
            new() { Id = "explosion",  PrefabPath = "VFX/Cubic/Explosion", AutoDestroy = 0.6f },
            new() { Id = "shield",     PrefabPath = "VFX/Cubic/Shield",    AutoDestroy = 1.0f },
        };

        /// <summary>
        /// 初始化 —— 把全部 vfxId → prefabPath 注册到框架 EffectsManager（bare-string）。
        /// 幂等，重复调用只生效一次。
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            if (!EventProcessor.HasInstance)
            {
                Debug.LogWarning("[CubicVFXManager] EventProcessor 未就绪，跳过 VFX 注册");
                return;
            }
            _initialized = true;

            foreach (var r in _registrations)
            {
                // §4.1 bare-string：消费方不 using EffectsManager 命名空间。
                EventProcessor.Instance.TriggerEventMethod(
                    "RegisterVFX",
                    new List<object> { r.Id, r.PrefabPath });
            }

            Debug.Log($"[CubicVFXManager] VFX 注册完成，共 {_registrations.Count} 个（实际 prefab 资源由用户在 Unity Editor 拖入）");
        }

        /// <summary>查询某 vfxId 的自动回收时长（无 → 0）。</summary>
        public static float GetAutoDestroy(string vfxId)
        {
            foreach (var r in _registrations)
                if (r.Id == vfxId) return r.AutoDestroy;
            return 0f;
        }

        /// <summary>
        /// 播放指定 VFX 特效（不关心效果时长，框架按各自注册时长自动回收）。
        /// </summary>
        public static void PlaySkillVFX(string vfxId, Vector3 position)
        {
            if (!EventProcessor.HasInstance) return;
            var autoDestroy = GetAutoDestroy(vfxId);
            EventProcessor.Instance.TriggerEventMethod(
                "PlayVFX",
                new List<object> { vfxId, position, null, autoDestroy });
        }

        /// <summary>播放 VFX（自定义旋转）。</summary>
        public static void PlaySkillVFX(string vfxId, Vector3 position, Quaternion rotation)
        {
            if (!EventProcessor.HasInstance) return;
            var autoDestroy = GetAutoDestroy(vfxId);
            EventProcessor.Instance.TriggerEventMethod(
                "PlayVFX",
                new List<object> { vfxId, position, rotation, autoDestroy });
        }

        /// <summary>屏幕闪光 —— 走框架 <c>EVT_SCREEN_FLASH</c>，由 EffectsManager 建独立 overlay Canvas。</summary>
        public static void PlayScreenFlash(ScreenFlashType flashType)
        {
            if (!EventProcessor.HasInstance) return;
            var (color, duration) = TranslateFlash(flashType);
            EventProcessor.Instance.TriggerEventMethod(
                "PlayScreenFlash",
                new List<object> { color, duration });
        }

        // ─── 内部：把业务 flashType 翻译为框架协议的颜色 + 时长 ───
        private static (Color color, float duration) TranslateFlash(ScreenFlashType t)
        {
            return t switch
            {
                ScreenFlashType.Damage  => (new Color(1f, 0f, 0f, 0.3f), 0.15f),
                ScreenFlashType.Crit    => (new Color(1f, 1f, 0f, 0.4f), 0.1f),
                ScreenFlashType.Heal    => (new Color(0f, 1f, 0f, 0.3f), 0.2f),
                ScreenFlashType.Freeze  => (new Color(0f, 0.5f, 1f, 0.4f), 0.3f),
                ScreenFlashType.Poison  => (new Color(0.5f, 0f, 1f, 0.3f), 0.5f),
                ScreenFlashType.Execute => (Color.white, 0.1f),
                _ => (Color.white, 0.1f),
            };
        }
    }
}
