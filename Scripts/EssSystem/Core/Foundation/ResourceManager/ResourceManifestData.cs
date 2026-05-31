using System;
using System.Collections.Generic;

namespace EssSystem.Core.Foundation.ResourceManager
{
    /// <summary>
    /// ResourceManifest 中的单条记录。
    /// 由 Editor 工具自动生成，运行时只读。
    /// </summary>
    [Serializable]
    public class ResourceManifestEntry
    {
        /// <summary>查询键：通常为文件名（不含路径、不含扩展名）。图集子精灵则为 sprite.name。</summary>
        public string key;
        /// <summary>Resources/ 相对路径（不含扩展名），供 Resources.LoadAsync 使用。</summary>
        public string path;
        /// <summary>非空时表示这是一张图集（SpriteSheet）的子精灵，值为 atlas 内该精灵的名称。</summary>
        public string spriteName;
    }

    /// <summary>ResourceManifest JSON 根对象。</summary>
    [Serializable]
    public class ResourceManifestData
    {
        public List<ResourceManifestEntry> entries = new List<ResourceManifestEntry>();
    }
}
