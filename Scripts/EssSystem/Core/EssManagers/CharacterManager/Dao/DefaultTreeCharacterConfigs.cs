using UnityEngine;

namespace EssSystem.EssManager.CharacterManager.Dao
{
    /// <summary>
    /// 内置 Tree 系列 <see cref="CharacterConfig"/> —— 单 Static 部件 + 根节点放大 10 倍。
    /// <para>
    /// 这些 Config 的 <see cref="CharacterConfig.ConfigId"/> 是**跨模块字符串协议**：
    /// 任何业务模块（EntityManager / 关卡数据 / JSON 配置）只通过 ID 引用，不直接 <c>using</c> 本类型。
    /// </para>
    /// <para>素材规范：<c>Resources/Sprites/Objects/Trees/Tree_small_{1..4}.png</c>、<c>Tree_medium_{1..4}.png</c>。</para>
    /// </summary>
    public static class DefaultTreeCharacterConfigs
    {
        /// <summary>小树 CharacterConfig ID 前缀。完整 ID = <c>SmallTreeChar_{1..4}</c>。</summary>
        public const string SmallTreeCharIdPrefix  = "SmallTreeChar_";
        /// <summary>中型树 CharacterConfig ID 前缀。完整 ID = <c>MediumTreeChar_{1..4}</c>。</summary>
        public const string MediumTreeCharIdPrefix = "MediumTreeChar_";
        /// <summary>每种树的变体数量。</summary>
        public const int    TreeVariantCount       = 4;

        /// <summary>
        /// 把 4 个小树 + 4 个中树共 8 份 CharacterConfig 注册到 <paramref name="charSvc"/>。
        /// <para>由 <see cref="CharacterManager"/> 在 <c>Initialize</c> 时调用 —— 业务模块不应直接调用本方法。</para>
        /// </summary>
        // 素材为 32×32 @ 100 PPU = 0.32 世界单位；pivot 已手动调到树根（底部中心）。
        // RootScale 选取：让视觉宽度 ≈ 期望 tile 数（K = TileWidth / 0.32）。
        private const float SmallTreeRootScale  = 10f;   // 0.32 × 10 ≈ 3.2 tile
        private const float MediumTreeRootScale = 10f;   // 0.32 × 10 ≈ 3.2 tile

        public static void RegisterAll(CharacterService charSvc)
        {
            if (charSvc == null) return;
            for (var i = 1; i <= TreeVariantCount; i++)
            {
                charSvc.RegisterConfig(BuildTree(
                    configId:    SmallTreeCharIdPrefix + i,
                    displayName: $"小树#{i}",
                    spriteId:    "Tree_small_" + i,
                    rootScale:   SmallTreeRootScale));
                charSvc.RegisterConfig(BuildTree(
                    configId:    MediumTreeCharIdPrefix + i,
                    displayName: $"中型树#{i}",
                    spriteId:    "Tree_medium_" + i,
                    rootScale:   MediumTreeRootScale));
            }
        }

        /// <summary>
        /// 构建单棵树的 <see cref="CharacterConfig"/>：单 Static 部件 + 指定根节点缩放 + Body 排序值 50。
        /// pivot 在树根 → 视觉树底 = Character 根 = Entity.WorldPosition + SpawnOffset。
        /// </summary>
        private static CharacterConfig BuildTree(string configId, string displayName, string spriteId, float rootScale)
        {
            return new CharacterConfig(configId, displayName)
                .WithRootScale(Vector3.one * rootScale)
                .WithPart(new CharacterPartConfig("Body", CharacterPartType.Static)
                    .WithStatic(spriteId)
                    .WithSortingOrder(50));           // 高于 Tilemap 默认 0，低于 TestPlayer 的 100
        }
    }
}
