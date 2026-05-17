namespace Demo.Tribe.Enemy
{
    /// <summary>
    /// 生物 Sprite 锚点对齐方式 —— 决定 <see cref="TribeSpriteAnimator"/> 是否补 Y 偏移。
    /// </summary>
    public enum SpritePivot
    {
        /// <summary>Sprite 锚点 = 中心（默认；Unity Sprite Importer 默认行为）。</summary>
        Center = 0,
        /// <summary>Sprite 锚点 = 底部 —— 自动把 sprite 顶起 halfHeight 让脚底贴 GameObject 原点。</summary>
        Bottom = 1,
    }
}
