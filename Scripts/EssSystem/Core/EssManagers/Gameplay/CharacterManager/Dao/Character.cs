using System.Collections.Generic;

namespace EssSystem.Core.EssManagers.Gameplay.CharacterManager.Dao
{
    /// <summary>
    /// 角色运行时实例（<b>非持久化</b>）—— 保存 <see cref="InstanceId"/>、所属配置、以及部件 View 引用。
    /// 由 <see cref="CharacterManager.CharacterService"/> 在 <c>CreateCharacter</c> 时构建并缓存。
    /// </summary>
    public class Character
    {
        /// <summary>实例唯一 ID（场景内唯一，由调用方指定）。</summary>
        public string InstanceId;

        /// <summary>对应的配置 ID。</summary>
        public string ConfigId;

        /// <summary>原始配置引用（创建时持有，方便后续按 PartId 查 ActionConfig）。</summary>
        public CharacterConfig Config;

        /// <summary>根 View（挂在 GameObject 上的 MonoBehaviour），销毁实例时一起销毁。</summary>
        public Runtime.CharacterView View;

        /// <summary>partId → 运行时部件 View（包含 SpriteRenderer / 动画状态）。</summary>
        public readonly Dictionary<string, Runtime.CharacterPartView> Parts =
            new Dictionary<string, Runtime.CharacterPartView>();
    }
}
