using UnityEngine;
using EssSystem.Core.Manager;

namespace EssSystem.Core.ResourceManager
{
    /// <summary>
    /// 资源管理器 - 简化版
    /// </summary>
    public class ResourceManager : Manager<ResourceManager>
    {
        private ResourceService _resourceService;

        protected override void Initialize()
        {
            base.Initialize();
            _resourceService = ResourceService.Instance;
        }

        protected override void Start()
        {
            base.Start();
            // DataManager会在Start之前完成数据加载，此时触发预加载
            _resourceService.OnDataLoaded();
        }

        /// <summary>
        /// 获取Prefab
        /// </summary>
        public GameObject GetPrefab(string path)
        {
            return _resourceService.Get<GameObject>(path);
        }

        /// <summary>
        /// 获取Sprite
        /// </summary>
        public Sprite GetSprite(string path)
        {
            return _resourceService.Get<Sprite>(path);
        }

        /// <summary>
        /// 获取AudioClip
        /// </summary>
        public AudioClip GetAudioClip(string path)
        {
            return _resourceService.Get<AudioClip>(path);
        }

        /// <summary>
        /// 获取Texture
        /// </summary>
        public Texture2D GetTexture(string path)
        {
            return _resourceService.Get<Texture2D>(path);
        }

        /// <summary>
        /// 获取外部Sprite
        /// </summary>
        public Sprite GetExternalSprite(string filePath)
        {
            return _resourceService.Get<Sprite>(filePath, true);
        }

    }
}
