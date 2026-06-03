using UnityEngine;
using System.Collections.Generic;

namespace Demo.Cubic.VFX
{
    /// <summary>
    /// Cubic VFX 特效管理器
    /// 管理所有视觉特效的播放
    /// </summary>
    public static class CubicVFXManager
    {
        /// <summary>
        /// 所有VFX特效定义
        /// </summary>
        public static readonly Dictionary<string, VFXInfo> VFXEffects = new Dictionary<string, VFXInfo>();

        /// <summary>
        /// VFX信息
        /// </summary>
        public class VFXInfo
        {
            public string Id;
            public string DisplayName;
            public Color Color;
            public float Duration;
            public string Description;
        }

        /// <summary>
        /// 屏幕闪光类型
        /// </summary>
        public enum ScreenFlashType
        {
            Damage,
            Crit,
            Heal,
            Freeze,
            Poison,
            Execute
        }

        /// <summary>
        /// 初始化VFX系统
        /// </summary>
        public static void Initialize()
        {
            RegisterVFEffects();
            Debug.Log($"[CubicVFXManager] VFX系统初始化完成，共 {VFXEffects.Count} 个特效");
        }

        /// <summary>
        /// 注册VFX特效
        /// </summary>
        private static void RegisterVFEffects()
        {
            RegisterVFX("warrior_slash", "横扫斩", Color.red, 0.3f, "战士挥舞武器");
            RegisterVFX("warrior_shout", "战吼", Color.yellow, 0.5f, "战士发出战吼");
            RegisterVFX("warrior_whirlwind", "旋风斩", Color.red, 1f, "战士旋转攻击");
            RegisterVFX("warrior_hit", "战士命中", Color.red, 0.2f, "命中特效");

            RegisterVFX("mage_fireball", "火球", new Color(1, 0.5f, 0), 0.8f, "火球飞行");
            RegisterVFX("mage_frost_nova", "冰霜新星", new Color(0.5f, 0.8f, 1), 0.5f, "冰霜爆发");
            RegisterVFX("mage_lightning", "闪电链", new Color(0.6f, 0.2f, 1), 0.4f, "链式闪电");
            RegisterVFX("mage_impact", "魔法命中", new Color(0.6f, 0.2f, 1), 0.2f, "魔法命中");

            RegisterVFX("archer_arrow", "箭矢", Color.green, 0.3f, "箭矢飞行");
            RegisterVFX("archer_pierce", "穿刺箭", Color.green, 0.4f, "穿透效果");
            RegisterVFX("archer_dash", "疾风步", Color.green, 0.2f, "快速位移");
            RegisterVFX("archer_hit", "弓箭命中", Color.green, 0.2f, "命中特效");

            RegisterVFX("paladin_holy", "圣光斩", Color.yellow, 0.4f, "圣光攻击");
            RegisterVFX("paladin_hammer", "正义之锤", Color.yellow, 0.5f, "锤击效果");
            RegisterVFX("paladin_devotion", "奉献光环", Color.yellow, 2f, "治疗光环");
            RegisterVFX("paladin_heal", "圣光治疗", Color.yellow, 0.5f, "治疗特效");

            RegisterVFX("hit_blood", "命中血液", Color.red, 0.3f, "血液飞溅");
            RegisterVFX("explosion", "爆炸", new Color(1, 0.5f, 0), 0.5f, "爆炸效果");
            RegisterVFX("shield", "护盾", new Color(0.5f, 0.8f, 1), 1f, "护盾效果");
            RegisterVFX("z_axis_light", "Z轴打光", new Color(1f, 0.85f, 0.45f), 1.35f, "从+Z方向投射的体积光束（URP菲涅尔光柱+Spot Light+Bloom增强）");
        }

        /// <summary>
        /// 注册单个VFX
        /// </summary>
        private static void RegisterVFX(string id, string displayName, Color color, float duration, string description)
        {
            var vfx = new VFXInfo
            {
                Id = id,
                DisplayName = displayName,
                Color = color,
                Duration = duration,
                Description = description
            };

            VFXEffects[id] = vfx;
        }

        /// <summary>
        /// 播放技能特效
        /// </summary>
        public static void PlaySkillVFX(string vfxId, Vector3 position)
        {
            if (VFXEffects.TryGetValue(vfxId, out var vfx))
            {
                // Z 轴打光特效走专门管理器（URP 体积光束 + Spot Light + Bloom）
                if (vfxId == "z_axis_light")
                {
                    CubicZAxisLightVFX.Play(position);
                    Debug.Log($"[CubicVFXManager] 播放VFX: {vfx.DisplayName} at {position}");
                    return;
                }

                CreateVFXEffect(vfx, position);
                Debug.Log($"[CubicVFXManager] 播放VFX: {vfx.DisplayName} at {position}");
            }
            else
            {
                Debug.LogWarning($"[CubicVFXManager] VFX不存在: {vfxId}");
            }
        }

        /// <summary>
        /// 创建VFX效果（简化版，后续可以扩展为粒子系统）
        /// </summary>
        private static void CreateVFXEffect(VFXInfo vfx, Vector3 position)
        {
            var vfxObj = new GameObject($"VFX_{vfx.DisplayName}");
            vfxObj.transform.position = position;

            var spriteRenderer = vfxObj.AddComponent<SpriteRenderer>();
            spriteRenderer.color = vfx.Color;
            spriteRenderer.sortingOrder = 100;

            int size = 32;
            var texture = new Texture2D(size, size);
            Color32[] colors = new Color32[size * size];
            for (int i = 0; i < size * size; i++)
            {
                int x = i % size;
                int y = i / size;
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(size / 2f, size / 2f));
                if (dist < size / 2f)
                {
                    colors[i] = Color.white;
                }
                else
                {
                    colors[i] = Color.clear;
                }
            }
            texture.SetPixels32(colors);
            texture.Apply();

            spriteRenderer.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                size
            );

            float scale = 2f;
            vfxObj.transform.localScale = new Vector3(scale, scale, 1);

            UnityEngine.Object.Destroy(vfxObj, vfx.Duration);
        }

        /// <summary>
        /// 播放屏幕闪光
        /// </summary>
        public static void PlayScreenFlash(ScreenFlashType flashType)
        {
            Color color;
            float duration;

            switch (flashType)
            {
                case ScreenFlashType.Damage:
                    color = new Color(1, 0, 0, 0.3f);
                    duration = 0.15f;
                    break;
                case ScreenFlashType.Crit:
                    color = new Color(1, 1, 0, 0.4f);
                    duration = 0.1f;
                    break;
                case ScreenFlashType.Heal:
                    color = new Color(0, 1, 0, 0.3f);
                    duration = 0.2f;
                    break;
                case ScreenFlashType.Freeze:
                    color = new Color(0, 0.5f, 1, 0.4f);
                    duration = 0.3f;
                    break;
                case ScreenFlashType.Poison:
                    color = new Color(0.5f, 0, 1, 0.3f);
                    duration = 0.5f;
                    break;
                case ScreenFlashType.Execute:
                    color = Color.white;
                    duration = 0.1f;
                    break;
                default:
                    color = Color.white;
                    duration = 0.1f;
                    break;
            }

            CreateScreenFlash(color, duration);
        }

        /// <summary>
        /// 创建屏幕闪光效果
        /// </summary>
        private static void CreateScreenFlash(Color color, float duration)
        {
            var flashObj = new GameObject("ScreenFlash");
            var canvas = flashObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;

            var image = flashObj.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            image.rectTransform.sizeDelta = new Vector2(Screen.width, Screen.height);

            UnityEngine.Object.Destroy(flashObj, duration);
        }
    }
}
