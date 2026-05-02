namespace EssSystem.EssManager.CharacterManager.Dao
{
    /// <summary>
    /// 快速生成 <see cref="CharacterConfig"/> 的工厂 ——
    /// 给 EntityManager 等上层模块"一行初始化视觉"的入口。
    /// <para>约定：所有生成的 config 默认使用 <see cref="DefaultCharacterConfigs.Actions"/>
    /// 定义的 8 个标准动作（Walk/Idle/Jump/Attack/Defend/Damage/Death/Special），
    /// 并按 <c>{sheetPrefix}_{ActionName}_{frameIndex}</c> 命名规则引用 Sprite。</para>
    /// </summary>
    public static class CharacterConfigFactory
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

        /// <summary>
        /// 一键注册单部件怪物到 <see cref="CharacterManager.CharacterService"/>。
        /// </summary>
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
    }
}
