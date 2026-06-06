using System.Collections.Generic;
using UnityEngine;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Application.SingleManagers.EntityManager;
using EssSystem.Core.Application.SingleManagers.EntityManager.Dao;
using Demo.Tribe;
using Demo.Tribe.Resource;

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
        /// <summary>营火视觉走 CharacterManager —— ConfigId 注册逻辑见
        /// <see cref="TribeCampfireCharacterConfig"/>。素材 spritesheet
        /// <c>Resources/Tribe/Objects/campfire.png</c> 切片名 campfire_0~7。</summary>
        /// <summary>营火音效路径（走 AudioManager bare-string EVT_PLAY_POSITIONAL_LOOP_SFX）。</summary>
        public string CampfireAudioPath = "Tribe/Common/Sound/feuer";

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

        /// <summary>NPC 互动时打开的对话 Id（参 DialogueManager 注册表，默认 DebugDialogue）。</summary>
        public string NpcDialogueId = "DebugDialogue";

        /// <summary>NPC 互动半径。</summary>
        public float NpcInteractRadius = 2.5f;

        // ─── 帐篷 ──────────────────────────────────────
        /// <summary>帐篷 sprite 资源路径（Resources.Load&lt;Sprite&gt;）。</summary>
        public string TentSpritePath = "Tribe/Common/Buildings/tent";

        /// <summary>帐篷世界缩放倍数（按 sprite 原始尺寸放大）。</summary>
        public float TentScale = 6f;

        /// <summary>素材缺失时的 fallback 色块色调。</summary>
        public Color TentColor = new Color(0.65f, 0.45f, 0.30f);

        /// <summary>fallback 色块尺寸（世界单位）。</summary>
        public Vector2 TentFallbackSize = new Vector2(2f, 2f);

        /// <summary>帐篷相对营火中心的 X 偏移。</summary>
        public float TentOffsetX = -3.5f;

        /// <summary>帐篷在底边贴地基础上的额外 Y 偏移（素材留白 / 视觉微调用，正值往上）。</summary>
        public float TentYOffset = -0.7f;

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

            BuildTent(root.transform, ctx, TentOffsetX, "🏕️帐篷");
            BuildCampfire(root.transform, ctx);
            BuildNpc(root.transform, ctx);

            // 部落世界边界：帐篷左侧 8 单位作为初始"地图极限"。
            // 留出 ~2 座农场（每座宽 3 单位）的预留空间，玩家不会一出生就撞墙。
            // 建造农场后由 TribeFarmCoordinator 监听 OnFarmSpawned 自动继续向左扩展。
            var boundary = Demo.Tribe.World.TribeWorldBoundary.EnsureInstance(ctx.WorldRoot);
            var tentX = basePos.x + TentOffsetX;
            boundary.SetLeftLimit(tentX - 8f);
            boundary.SetIndicatorBottomY(ctx.GroundY);

            // 农场协调器：注册默认 FarmConfig / CropConfig，监听 OnFarmSpawned 自动扩边界 + 渲染。
            // 开发期按 G 键可在边界处生成一座基础农场，验证完整链路。
            Demo.Tribe.World.TribeFarmCoordinator.EnsureInstance(ctx.WorldRoot);
        }

        // ─── 营火 ─────────────────────────────────────────
        private void BuildCampfire(Transform parent, TribeBiomeContext ctx)
        {
            // 视觉容器：负责定位 + 持有 TribeCampfire（音频锚点）
            var go = new GameObject("Campfire");
            go.transform.SetParent(parent, false);
            // campfire sprite pivot=(0,0) 左下角 → 把节点向左移半个 sprite 宽度（16px @ PPU=100 = 0.16 单位）
            // 让营火视觉中心对齐营地 X，底边对齐 ctx.GroundY
            const float spritePixelWidth = 16f;
            const float ppu = 100f;
            var halfWorldWidth = (spritePixelWidth / ppu) * 0.5f * CampfireScale.x;
            go.transform.localPosition = new Vector3(-halfWorldWidth, CampfireYOffset, 0f);

            // 视觉走 CharacterManager —— Idle 循环动作 + sprite_0..7
            TribeCampfireCharacterConfig.EnsureRegistered();
            var charInstanceId = $"TribeCampfire_{go.GetHashCode()}";
            var charRoot = CharacterViewBridge.CreateCharacter(
                TribeCampfireCharacterConfig.ConfigId, charInstanceId,
                parent: go.transform, worldPosition: go.transform.position);
            if (charRoot != null)
            {
                charRoot.localScale = new Vector3(CampfireScale.x, CampfireScale.y, 1f);
                // 部件 SortingOrder 写死在 config 里 (=0)；这里整体抬到 ctx.BaseSortingOrder+1
                foreach (var r in charRoot.GetComponentsInChildren<SpriteRenderer>(true))
                    r.sortingOrder = ctx.BaseSortingOrder + 1;
            }
            else
            {
                Debug.LogWarning("[CampFeature] 创建营火 Character 失败 —— 请确认已运行 " +
                    "Tools/WulaSystem/Presentation/Character/2D/Build Sprite Animator Base Controller 生成 base controller。");
            }

            // 音频生命周期挂在容器 GO 上：GO 销毁时 OnDestroy 调 StopPositionalSFX
            var fire = go.AddComponent<TribeCampfire>();
            fire.Initialize(CampfireAudioPath);

            // 互动：以 Entity 能力形式注册（参 EntityManager.IInteractable / Entity.CanInteract）
            AttachInteractable(
                go,
                instanceId: $"TribeCampfire_Interact_{go.GetHashCode()}",
                radius: 2.5f,
                promptLabel: "[F] 制作",
                onInteract: () =>
                {
                    Demo.Tribe.Interaction.TribeCraftingPanel.Toggle();
                    if (EventProcessor.HasInstance)
                        EventProcessor.Instance.TriggerEventMethod("PlayUISFX", null);
                });
        }

        // ─── 帐篷 ────────────────────────────────────────
        private void BuildTent(Transform parent, TribeBiomeContext ctx, float offsetX, string label)
        {
            var go = new GameObject($"Tent_{(offsetX < 0f ? "L" : "R")}");
            go.transform.SetParent(parent, false);

            var sprite = LoadSpriteViaEvent(TentSpritePath);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = ctx.BaseSortingOrder;

            if (sprite != null)
            {
                // 真实 sprite 路径：底边贴地 + 中心对齐 offsetX。
                // pivot 不可控（取决于 import 设置），用 bounds 反推让脚下 Y = 0。
                sr.sprite = sprite;
                go.transform.localScale = Vector3.one * TentScale;
                // sprite.bounds 已经按 PPU 折算到世界单位（scale=1 时的世界 size）；再乘 TentScale 才是实际可见高度。
                var height = sprite.bounds.size.y * TentScale;
                // 让 sprite 底边落到 parent.y（= ctx.GroundY + YOffset）：把 pivot 抬到 (halfHeight + 底边到 pivot 的偏移)。
                // bounds.center.y 就是 pivot 在世界空间到 sprite 几何中心的有符号距离（已乘 PPU）。
                // 总公式：localPos.y = halfHeight - boundsCenterY * TentScale + TentYOffset
                var boundsCenterY = sprite.bounds.center.y * TentScale;
                go.transform.localPosition = new Vector3(offsetX, height * 0.5f - boundsCenterY + TentYOffset, 0f);
            }
            else
            {
                // 兜底：素材缺失时退回旧的色块 + 文字标签（保留调试可读性）
                go.transform.localPosition = new Vector3(offsetX, TentFallbackSize.y * 0.5f + TentYOffset, 0f);
                go.transform.localScale = new Vector3(TentFallbackSize.x, TentFallbackSize.y, 1f);
                sr.sprite = PlaceholderSpriteCache.GetWhitePixel();
                sr.color = TentColor;

                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, TentFallbackSize.x),
                                                           1f / Mathf.Max(0.01f, TentFallbackSize.y), 1f);
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

            // 注册一段以本 NPC 为主角的对话（不再走 PortraitSpriteId；头像 OpenDialogueUI 后用
            // SetDialoguePortraitSprite 直接拷 Character/Head 当前 SpriteRenderer.sprite，与 Player HUD 同款）。
            var perNpcDialogueId = $"Tribe_Npc_{NpcInstanceId}_Dialogue";
            RegisterNpcDialogue(perNpcDialogueId, NpcDisplayName);

            // 闭包捕获 characterRoot —— 互动时同步把 NPC head 的 sprite 推到对话框
            var capturedCharacterRoot = characterRoot;
            var dialogueId = perNpcDialogueId;
            AttachInteractable(
                go,
                instanceId: $"Npc_{NpcInstanceId}_Interact",
                radius: NpcInteractRadius,
                promptLabel: "[F] 对话",
                onInteract: () =>
                {
                    if (!EventProcessor.HasInstance || string.IsNullOrEmpty(dialogueId)) return;
                    EventProcessor.Instance.TriggerEventMethod(
                        "OpenDialogueUI", new List<object> { dialogueId });
                    PushNpcHeadToDialoguePortrait(capturedCharacterRoot);
                    EventProcessor.Instance.TriggerEventMethod("PlayUISFX", null);
                });

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

        // ─── NPC 对话注册 ────────────────────────────────────
        /// <summary>注册一段以本 NPC 为主角的 demo 对话（不带 PortraitSpriteId）。
        /// 头像在 F 触发 OpenDialogueUI 之后通过 <c>SetDialoguePortraitSprite</c> 事件
        /// 直接把 NPC <c>Character/Head/SpriteRenderer.sprite</c> 引用贴上去，
        /// 与 <see cref="Demo.Tribe.Player.TribePlayerHud.AttachHeadSprite"/> 同款做法。</summary>
        private static void RegisterNpcDialogue(string dialogueId, string speakerName)
        {
            if (!EventProcessor.HasInstance || string.IsNullOrEmpty(dialogueId)) return;

            var line1 = new EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.DialogueLine(
                            "L1", speakerName, $"你好，旅行者。我是{speakerName}。")
                        .WithNextLine("L2");
            var line2 = new EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.DialogueLine(
                            "L2", speakerName, "需要我做点什么？")
                        .AddOption(new EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.DialogueOption("继续探索").WithNextLine("L3"))
                        .AddOption(new EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.DialogueOption("结束对话"));
            var line3 = new EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.DialogueLine(
                            "L3", speakerName, "祝你好运！");

            var dialogue = new EssSystem.Core.Application.SingleManagers.DialogueManager.Dao.Dialogue(dialogueId, $"{speakerName}对话")
                .AddLine(line1)
                .AddLine(line2)
                .AddLine(line3);

            EventProcessor.Instance.TriggerEventMethod(
                "RegisterDialogue", new List<object> { dialogue });
        }

        /// <summary>从 NPC characterRoot 收集 Skin/Eyes/Hair/Head 四层 SpriteRenderer 的当前 sprite，
        /// 按 z-order（背→前）打包成一组 Sprite，走 <c>DialogueManager.EVT_SET_PORTRAIT_SPRITE</c>
        /// 层叠贴到对话框头像位 —— 还原"角色完整脸"（Skin 底面 + Eyes 眼睛 + Hair 头发 + Head 头饰）。</summary>
        private static readonly string[] PortraitPartLayers = { "Skin", "Eyes", "Hair", "Head" };

        private static void PushNpcHeadToDialoguePortrait(Transform characterRoot)
        {
            if (!EventProcessor.HasInstance || characterRoot == null) return;

            var sprites = new List<Sprite>(PortraitPartLayers.Length);
            for (var i = 0; i < PortraitPartLayers.Length; i++)
            {
                var partTr = characterRoot.Find(PortraitPartLayers[i]);
                var renderer = partTr != null ? partTr.GetComponent<SpriteRenderer>() : null;
                if (renderer != null && renderer.sprite != null)
                    sprites.Add(renderer.sprite);
            }
            if (sprites.Count == 0) return;

            EventProcessor.Instance.TriggerEventMethod(
                "SetDialoguePortraitSprite", new List<object> { sprites });
        }

        // ─── 互动能力挂载 ──────────────────────────────────
        /// <summary>把"靠近 + F 互动"作为 <see cref="EssSystem.Core.Application.SingleManagers.EntityManager.Capabilities.IInteractable"/>
        /// 能力挂到 <paramref name="host"/> 上 —— 与 IDamageable / IFlashEffect 等同走 Entity Capability 体系，
        /// 由 EntityService.Tick 自动驱动，无需自挂 MonoBehaviour。</summary>
        private static void AttachInteractable(GameObject host, string instanceId,
            float radius, string promptLabel, System.Action onInteract)
        {
            if (host == null || string.IsNullOrEmpty(instanceId)) return;

            var entity = new Entity
            {
                InstanceId = instanceId,
                ConfigId = "Interactable",
                Kind = EntityKind.Static,
                CharacterRoot = host.transform,
                WorldPosition = host.transform.position,
            };
            entity.CanInteract(radius, promptLabel, onInteract);
            EntityService.AttachEntityHandle(host, entity);
        }

        // ─── 资源加载工具 ─────────────────────────────────
        private static Sprite LoadSpriteViaEvent(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (EventProcessor.HasInstance)
            {
                // 优先走 ResourceManager 通道（命中缓存 + subfolder hints）
                var r = EventProcessor.Instance.TriggerEventMethod(
                    "GetSprite", new List<object> { path });
                if (ResultCode.IsOk(r) && r.Count >= 2 && r[1] is Sprite s) return s;
            }
            // 兜底：直接 Resources.Load
            return TribeResourceProvider.LoadSprite(path);
        }

    }
}
