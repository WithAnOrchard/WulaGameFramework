using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using Demo.Tribe;

namespace Demo.Tribe.World.Features
{
    /// <summary>
    /// 临时小营地 Feature —— 营火 + 帐篷占位 + NPC（用 CharacterManager 创建，
    /// 与玩家同套身体部位系统，仅 sprite sheet 不同）。
    /// <para>
    /// 设计契合 ToDo #4 NPC 落地前的过渡：先用现有 CharacterManager 默认 config（如 Mage）
    /// 作为 NPC 视觉，后续 NpcManager 上线后改用 NpcConfig 驱动；视觉系统不变。
    /// </para>
    /// </summary>
    public class CampFeature : TribeFeatureSpec
    {
        // ─── 整体 ─────────────────────────────────────────
        /// <summary>营地名（GameObject 节点用）。</summary>
        public string CampName = "TempCamp";

        // ─── 营火 ─────────────────────────────────────────
        /// <summary>营火 sprite 资源路径（Resources.LoadAll&lt;Sprite&gt;，多帧动画）。</summary>
        public string CampfireSpritePath = "Tribe/Objects/campfire";

        /// <summary>营火音效路径（走 ResourceManager bare-string EVT_GET_AUDIO_CLIP）。</summary>
        public string CampfireAudioPath = "Sound/feuer";

        /// <summary>营火世界缩放（campfire.png 单帧 16x32 @ PPU=100 → 0.16x0.32 单位；
        /// 缩放 8 → 1.28x2.56 单位，比原先 (2,2) 大 4 倍线性尺寸）。</summary>
        public Vector2 CampfireScale = new Vector2(8f, 8f);

        /// <summary>营火相对 ctx.GroundY 的 Y 偏移。
        /// campfire sprite pivot = (0,0) 左下角，故 0 即底边贴地。</summary>
        public float CampfireYOffset = 0f;

        // ─── NPC ──────────────────────────────────────────
        /// <summary>NPC 视觉用的 CharacterConfig Id（与玩家 "Warrior" 同模式，"Mage" 即不同素材）。</summary>
        public string NpcCharacterConfigId = "Mage";

        /// <summary>NPC 实例 Id（场景内唯一）。</summary>
        public string NpcInstanceId = "TribeNpc_Alice";

        /// <summary>NPC 头顶名牌。</summary>
        public string NpcDisplayName = "向导艾丽丝";

        /// <summary>NPC 相对营火中心的 X 偏移。</summary>
        public float NpcOffsetX = 1.5f;

        /// <summary>NPC Character 视觉缩放（与 TribePlayer.character_visualScale 同款）。</summary>
        public float NpcVisualScale = 10f;

        /// <summary>NPC Character 视觉 Y 偏移（让脚踩在碰撞体上）。</summary>
        public float NpcVisualYOffset = 0.45f;

        /// <summary>NPC 各部件 SpriteRenderer 的 sortingOrder 基准。
        /// <para>默认走 <c>ctx.BaseSortingOrder</c> —— 与 <see cref="GatherableFeature"/> 同层，
        /// 这样 NPC 武器 / 盾牌不会"穿透"渲染到玩家身上（玩家默认 0~6 在前）。</para></summary>
        public bool NpcUseGatherableSortingLayer = true;

        // ─── 帐篷占位 ──────────────────────────────────────
        /// <summary>帐篷颜色块色调。</summary>
        public Color TentColor = new Color(0.65f, 0.45f, 0.30f);

        /// <summary>帐篷尺寸（世界单位）。</summary>
        public Vector2 TentSize = new Vector2(2f, 2f);

        /// <summary>左 / 右帐篷相对营火中心的 X 偏移。</summary>
        public float TentLeftOffsetX = -2.5f;
        public float TentRightOffsetX = 4f;

        public CampFeature() { }

        /// <summary>常用构造：指定营地中心 X 与 NPC 资料。</summary>
        public CampFeature(float worldX, string npcInstanceId, string npcDisplayName,
            string npcCharacterConfigId = "Mage")
        {
            WorldX = worldX;
            YOffset = 0f;
            NpcInstanceId = npcInstanceId;
            NpcDisplayName = npcDisplayName;
            NpcCharacterConfigId = npcCharacterConfigId;
        }

        public override void Build(TribeBiomeContext ctx)
        {
            var basePos = ComputeWorldPosition(ctx);
            var root = new GameObject($"Camp_{CampName}");
            if (ctx.WorldRoot != null) root.transform.SetParent(ctx.WorldRoot, false);
            root.transform.position = basePos;

            BuildTent(root.transform, ctx, TentLeftOffsetX, "🏕️帐篷A");
            BuildTent(root.transform, ctx, TentRightOffsetX, "🏕️帐篷B");
            BuildCampfire(root.transform, ctx);
            BuildNpc(root.transform, ctx);
        }

        // ─── 营火 ─────────────────────────────────────────
        private void BuildCampfire(Transform parent, TribeBiomeContext ctx)
        {
            var go = new GameObject("Campfire");
            go.transform.SetParent(parent, false);
            // campfire sprite pivot=(0,0) 左下角 → 把节点向左移半个 sprite 宽度（16px @ PPU=100 = 0.16 单位）
            // 让营火视觉中心对齐营地 X，底边对齐 ctx.GroundY
            const float spritePixelWidth = 16f;
            const float ppu = 100f;
            var halfWorldWidth = (spritePixelWidth / ppu) * 0.5f * CampfireScale.x;
            go.transform.localPosition = new Vector3(-halfWorldWidth, CampfireYOffset, 0f);
            go.transform.localScale = new Vector3(CampfireScale.x, CampfireScale.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = ctx.BaseSortingOrder + 1;

            // 走 ResourceManager bare-string 协议 + LoadAll 取多帧
            var frames = LoadSpriteFrames(CampfireSpritePath);
            var clip   = LoadAudioClipViaEvent(CampfireAudioPath);

            var fire = go.AddComponent<TribeCampfire>();
            fire.Initialize(frames, clip);
        }

        // ─── 帐篷占位（色块 + 标签）────────────────────────
        private void BuildTent(Transform parent, TribeBiomeContext ctx, float offsetX, string label)
        {
            var go = new GameObject($"Tent_{(offsetX < 0f ? "L" : "R")}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(offsetX, TentSize.y * 0.5f, 0f);
            go.transform.localScale = new Vector3(TentSize.x, TentSize.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteCache.GetWhitePixel();
            sr.color = TentColor;
            sr.sortingOrder = ctx.BaseSortingOrder;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, TentSize.x),
                                                       1f / Mathf.Max(0.01f, TentSize.y), 1f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = label;
            tm.characterSize = 0.12f;
            tm.fontSize = 64;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var tmr = labelGo.GetComponent<MeshRenderer>();
            if (tmr != null) tmr.sortingOrder = ctx.BaseSortingOrder + 1;
        }

        // ─── NPC（走 CharacterManager 与玩家同款）──────────
        private void BuildNpc(Transform parent, TribeBiomeContext ctx)
        {
            var go = new GameObject($"Npc_{NpcInstanceId}");
            go.transform.SetParent(parent, false);
            // NPC 根贴在营火地面同 Y；Character 子节点再做视觉抬升
            go.transform.localPosition = new Vector3(NpcOffsetX, 0f, 0f);

            // 走 CharacterViewBridge.CreateCharacter（bare-string Event 协议），
            // 与 TribePlayer.SpawnCharacter 同模式 —— 自动复用 CharacterManager 已注册的
            // Mage / Warrior / 自定义 NpcConfig 视觉表现。
            var characterRoot = CharacterViewBridge.CreateCharacter(
                NpcCharacterConfigId, NpcInstanceId,
                parent: go.transform, worldPosition: go.transform.position);

            if (characterRoot != null)
            {
                characterRoot.localPosition = new Vector3(0f, NpcVisualYOffset, 0f);
                characterRoot.localScale = Vector3.one * NpcVisualScale;
                CharacterViewBridge.PlayLocomotion(NpcInstanceId, moving: false, grounded: true);

                // 把 NPC 各部件 SpriteRenderer 整体落到 ctx.BaseSortingOrder 基准（采集物 / 装饰同层），
                // 保留 DefaultCharacterConfigs 内部 0~6 相对顺序（Skin..Shield 仍正确叠放）。
                // 玩家未额外设 sortingOrder（默认 0~6），ctx.BaseSortingOrder 通常 < 0（背景层基准），
                // 因此 NPC 整体会渲染在玩家之下，避免武器 / 盾牌透过玩家。
                if (NpcUseGatherableSortingLayer)
                {
                    var renderers = characterRoot.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var r in renderers) r.sortingOrder += ctx.BaseSortingOrder;
                }
            }
            else
            {
                Debug.LogWarning($"[CampFeature] 创建 NPC Character 失败：configId={NpcCharacterConfigId}（"
                    + "请确认 CharacterManager 已注册该 ConfigId）");
            }

            // 头顶中文名牌
            var labelGo = new GameObject("Name");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            var tm = labelGo.AddComponent<TextMesh>();
            tm.text = NpcDisplayName;
            tm.characterSize = 0.1f;
            tm.fontSize = 48;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.85f, 0.40f);
            var tmr = labelGo.GetComponent<MeshRenderer>();
            if (tmr != null) tmr.sortingOrder = ctx.BaseSortingOrder + 10;
        }

        // ─── 资源加载工具 ─────────────────────────────────
        private static Sprite[] LoadSpriteFrames(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            // ResourceManager.EVT_GET_SPRITE 仅返单 Sprite；多帧 sprite sheet 用 LoadAll 直接拿。
            return Resources.LoadAll<Sprite>(path);
        }

        private static AudioClip LoadAudioClipViaEvent(string path)
        {
            if (string.IsNullOrEmpty(path) || !EventProcessor.HasInstance) return null;
            // bare-string §4.1：跨模块走 "GetAudioClip"，避免 using ResourceManager 命名空间
            var r = EventProcessor.Instance.TriggerEventMethod(
                "GetAudioClip", new List<object> { path });
            return ResultCode.IsOk(r) && r.Count >= 2 ? r[1] as AudioClip : null;
        }
    }
}
