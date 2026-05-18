// §4.1 跨模块走 bare-string 协议，不 using ResourceManager

namespace EssSystem.Core.Presentation.CharacterManager.Dao
{
    /// <summary>
    /// 快速生成 <see cref="CharacterConfig"/> 的工厂 —— **2D Sprite 模式专用**。
    /// <para>给 EntityManager 等上层模块"一行初始化视觉"的入口。</para>
    /// <para>约定：所有生成的 config 默认使用 <see cref="DefaultCharacterConfigs.Actions"/>
    /// 定义的 8 个标准动作（Walk/Idle/Jump/Attack/Defend/Damage/Death/Special），
    /// 并按 <c>{sheetPrefix}_{ActionName}_{frameIndex}</c> 命名规则引用 Sprite。</para>
    /// <para>3D Prefab / FBX 工厂方法在 <see cref="CharacterConfigFactory"/> 的 3D 分部文件中。</para>
    /// </summary>
    public static partial class CharacterConfigFactory
    {
        /// <summary>
        /// 单部件怪物（slime / 小怪 / 简单 NPC）：一个 Dynamic Body + 8 个标准动作。
        /// </summary>
        /// <param name="configId">注册到 CharacterService 的 ID（通常 = entity 类型名）。</param>
        /// <param name="sheetPrefix">Sprite sheet 前缀（Slicer 切出的命名前缀，如 "slime_green"）。</param>
        public static CharacterConfig MakeSimpleMonster(string configId, string sheetPrefix)
        {
            var cfg = new CharacterConfig(configId, configId);
            cfg.WithPart(DefaultCharacterConfigs.MakeAnimatedPart("Body", sheetPrefix, sortingOrder: 0));
            return cfg;
        }

        /// <summary>
        /// 多部件分层角色：按传入的 (partId, sheetPrefix, sortingOrder) 顺序生成 Dynamic 部件。
        /// <para>用法：<c>MakeLayered("goblin", ("Body","goblin_body",0), ("Armor","goblin_armor",1), ("Weapon","goblin_sword",2))</c></para>
        /// </summary>
        public static CharacterConfig MakeLayered(string configId,
            params (string partId, string sheetPrefix, int sortingOrder)[] layers)
        {
            var cfg = new CharacterConfig(configId, configId);
            if (layers == null) return cfg;
            foreach (var (pid, prefix, order) in layers)
            {
                if (string.IsNullOrEmpty(pid)) continue;
                cfg.WithPart(DefaultCharacterConfigs.MakeAnimatedPart(pid, prefix, order));
            }
            return cfg;
        }

        /// <summary>一键注册单部件怪物到 <see cref="CharacterManager.CharacterService"/>。</summary>
        public static CharacterConfig RegisterSimpleMonster(string configId, string sheetPrefix)
        {
            var cfg = MakeSimpleMonster(configId, sheetPrefix);
            if (CharacterService.Instance != null)
                CharacterService.Instance.RegisterConfig(cfg);
            return cfg;
        }

        /// <summary>一键注册分层角色到 <see cref="CharacterManager.CharacterService"/>。</summary>
        public static CharacterConfig RegisterLayered(string configId,
            params (string partId, string sheetPrefix, int sortingOrder)[] layers)
        {
            var cfg = MakeLayered(configId, layers);
            if (CharacterService.Instance != null)
                CharacterService.Instance.RegisterConfig(cfg);
            return cfg;
        }

        // ═══════════════════════════════════════════════════════════════════════
        //  Sheet-Based Creature —— 单部件，Idle + Walk 两动作，4 行 × N 列方向式 sheet
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 生成"sheet 式生物"角色配置 —— 单部件 Body，含 Idle / Walk 两个动作，
        /// 每个动作走 <see cref="UnityEngine.Resources.LoadAll{T}"/> 加载整张 sub-sprite sheet，
        /// 按朝向（左 / 右）挑选不同行的帧序列。
        /// <para>典型场景：Tribe 动物 / 怪物的 4 行 × 4 列 spritesheet（行 = 朝向，列 = 帧）。</para>
        /// </summary>
        /// <param name="configId">注册 ID。</param>
        /// <param name="displayName">显示名。</param>
        /// <param name="idleResourcePath"><c>Resources.LoadAll&lt;Sprite&gt;</c> 路径（Idle sheet，不含扩展名）。</param>
        /// <param name="walkResourcePath">Walk sheet 路径；为空则与 Idle 共用一张 sheet。</param>
        /// <param name="frameRate">每秒帧数（FPS）。</param>
        /// <param name="leftFrameIndices">面朝左时在 sub-sprite 列表中取的帧索引（如 4×4 sheet 的 [4,5,6,7]）。</param>
        /// <param name="rightFrameIndices">面朝右时的帧索引（如 [8,9,10,11]）。</param>
        /// <param name="visualScale">部件 LocalScale 统一倍率（覆盖默认 1）。</param>
        /// <param name="visualYOffset">部件 LocalPosition.y（"BottomCenter pivot 补偿"用，对齐脚下）。</param>
        public static CharacterConfig MakeSheetCreature(string configId, string displayName,
            string idleResourcePath, string walkResourcePath,
            float frameRate = 10f,
            int[] leftFrameIndices = null, int[] rightFrameIndices = null,
            float visualScale = 1f, float visualYOffset = 0f)
        {
            var walkPath = string.IsNullOrEmpty(walkResourcePath) ? idleResourcePath : walkResourcePath;
            var idle = new CharacterActionConfig("Idle")
                .WithSheet(idleResourcePath)
                .WithFrameRate(frameRate)
                .WithLoop(true);
            var walk = new CharacterActionConfig("Walk")
                .WithSheet(walkPath)
                .WithFrameRate(frameRate)
                .WithLoop(true);
            if (leftFrameIndices != null && leftFrameIndices.Length > 0)
            {
                idle.WithDirectionalFrames(-1, leftFrameIndices);
                walk.WithDirectionalFrames(-1, leftFrameIndices);
            }
            if (rightFrameIndices != null && rightFrameIndices.Length > 0)
            {
                idle.WithDirectionalFrames(+1, rightFrameIndices);
                walk.WithDirectionalFrames(+1, rightFrameIndices);
            }

            var bodyPart = new CharacterPartConfig("Body", CharacterPartType.Dynamic)
                .WithLocalScale(UnityEngine.Vector3.one * UnityEngine.Mathf.Max(0.0001f, visualScale))
                .WithLocalPosition(new UnityEngine.Vector3(0f, visualYOffset, 0f))
                .WithSortingOrder(0)
                .WithDynamic("Idle", idle, walk);

            return new CharacterConfig(configId, displayName ?? configId)
                .WithRenderMode(CharacterRenderMode.Sprite2D)
                .WithPart(bodyPart);
        }

        /// <summary>一键注册 sheet 式生物（幂等：同 ID 已注册时直接覆盖）。</summary>
        public static CharacterConfig RegisterSheetCreature(string configId, string displayName,
            string idleResourcePath, string walkResourcePath,
            float frameRate = 10f,
            int[] leftFrameIndices = null, int[] rightFrameIndices = null,
            float visualScale = 1f, float visualYOffset = 0f)
        {
            var cfg = MakeSheetCreature(configId, displayName, idleResourcePath, walkResourcePath,
                frameRate, leftFrameIndices, rightFrameIndices, visualScale, visualYOffset);
            if (CharacterService.Instance != null)
                CharacterService.Instance.RegisterConfig(cfg);
            return cfg;
        }
    }
}
