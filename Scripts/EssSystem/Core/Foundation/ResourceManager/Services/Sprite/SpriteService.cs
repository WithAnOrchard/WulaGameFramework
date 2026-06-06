using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using EssSystem.Core.Base.Event;
using EssSystem.Core.Base.Util;
using EssSystem.Core.Foundation.ResourceManager.Services.Base;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EssSystem.Core.Foundation.ResourceManager.Services.Sprite
{
    /// <summary>Sprite 资源服务 — 管理 Sprite 资源的加载、缓存、卸载、子图兜底。</summary>
    public class SpriteService : ResourceServiceBase<SpriteService>
    {
#if UNITY_EDITOR
        private readonly Dictionary<string, UnityEngine.Sprite> _frameworkSpriteByName =
            new Dictionary<string, UnityEngine.Sprite>();
#endif

        // ============================================================
        // Event 常量
        // ============================================================
        public const string EVT_GET_SPRITE = "GetSprite";
        public const string EVT_GET_SPRITE_ASYNC = "GetSpriteAsync";
        public const string EVT_LOAD_SPRITE_ASYNC = "LoadSpriteAsync";
        public const string EVT_REGISTER_SPRITE_TO_CACHE = "RegisterSpriteToCache";
        
        // ============================================================
        // 同步获取 Sprite（从缓存）
        // ============================================================
        [Event(EVT_GET_SPRITE)]
        public List<object> GetFromCache(List<object> data)
        {
            string path = data[0] as string;
            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Sprite"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

#if UNITY_EDITOR
            if (TryLoadFrameworkSprite(path, out var editorSprite))
                return ResultCode.Ok(editorSprite);
#else
            var addressableSprite = LoadAddressableSpriteSync(path);
            if (addressableSprite != null)
            {
                CacheSpriteAliases(path, addressableSprite);
                return ResultCode.Ok(addressableSprite);
            }
#endif
            return ResultCode.Fail("Sprite 未加载");
        }

        // ============================================================
        // 异步获取 Sprite
        // ============================================================
        [Event(EVT_GET_SPRITE_ASYNC)]
        public List<object> GetAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Sprite"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoadWithFallback(path, typeof(UnityEngine.Sprite), "Sprite");
            return ResultCode.Fail("加载中");
        }

        // ============================================================
        // 异步加载 Sprite
        // ============================================================
        [Event(EVT_LOAD_SPRITE_ASYNC)]
        public List<object> LoadAsync(List<object> data)
        {
            string path = data[0] as string;

            if (string.IsNullOrEmpty(path)) return ResultCode.Fail("路径为空");

            var key = new ResourceKey(path, false, NormalizeTypeTag("Sprite"));
            if (TryGetResource(key, out var cached)) 
                return ResultCode.Ok(cached);

            StartAsyncLoadWithFallback(path, typeof(UnityEngine.Sprite), "Sprite");
            return ResultCode.Fail("加载中");
        }

        // ============================================================
        // 注册 Sprite 到缓存
        // ============================================================
        [Event(EVT_REGISTER_SPRITE_TO_CACHE)]
        public List<object> RegisterSpriteToCache(List<object> data)
        {
            if (data == null || data.Count < 2) return ResultCode.Fail("参数错误");
            
            string spriteId = data[0] as string;
            var sprite = data[1] as UnityEngine.Sprite;
            
            if (string.IsNullOrEmpty(spriteId) || sprite == null)
                return ResultCode.Fail("Sprite ID 或 Sprite 为空");
            
            var key = new ResourceKey(spriteId, false, NormalizeTypeTag("Sprite"));
            CacheResource(key, sprite);
            Log($"注册 Sprite 到缓存: {spriteId}", Color.green);
            return ResultCode.Ok();
        }

        // ============================================================
        // Fallback 异步加载 — 尝试多个候选路径
        // ============================================================
        protected override void StartAsyncLoadWithFallback(string path, System.Type type, string typeTag)
        {
            var key = new ResourceKey(path, false, NormalizeTypeTag(typeTag));
            if (TryGetResource(key, out _)) return;

#if UNITY_EDITOR
            if (TryLoadFrameworkSprite(path, out _)) return;
#else
            StartAddressableSpriteLoad(path);
#endif
            // 先尝试直接路径
            StartAsyncLoad(path, type, typeTag);

            // 最后尝试子图加载
            StartAsyncLoadSpriteByName(path);
        }

        /// <summary>异步加载 Sprite 子图 — 尝试从各个子目录加载。</summary>
        private void StartAsyncLoadSpriteByName(string path)
        {
            var spriteName = Path.GetFileNameWithoutExtension(Path.GetFileName(path));
            if (string.IsNullOrEmpty(spriteName)) spriteName = path;

            // 异步尝试各个子目录
            foreach (var hint in SubfolderHints)
            {
                var candidate = string.IsNullOrEmpty(hint) ? spriteName : $"{hint}/{spriteName}";
                var req = Resources.LoadAsync<UnityEngine.Sprite>(candidate);
                req.completed += _ =>
                {
                    if (req.asset != null)
                    {
                        var spriteKey = new ResourceKey(spriteName, false, NormalizeTypeTag("Sprite"));
                        if (!TryGetResource(spriteKey, out var existing))
                        {
                            CacheResource(spriteKey, req.asset);
                            Log($"异步加载 Sprite 子图成功: {spriteName}", Color.green);
                        }
                    }
                };
            }
        }

        private void CacheSpriteAliases(string path, UnityEngine.Sprite sprite)
        {
            if (sprite == null) return;
            CacheResource(new ResourceKey(path, false, NormalizeTypeTag("Sprite")), sprite);
            if (!string.IsNullOrEmpty(sprite.name))
                CacheResource(new ResourceKey(sprite.name, false, NormalizeTypeTag("Sprite")), sprite);
        }

#if UNITY_EDITOR
        private bool TryLoadFrameworkSprite(string path, out UnityEngine.Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(path)) return false;

            var normalized = path.Replace('\\', '/').Trim('/');
            const string frameworkPrefix = "Assets/FrameworkResources/";
            if (normalized.StartsWith(frameworkPrefix, System.StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(frameworkPrefix.Length);

            var assetPath = ResolveFrameworkSpritePath("Assets/FrameworkResources/" + normalized);
            if (string.IsNullOrEmpty(assetPath))
                return TryLoadFrameworkSpriteByName(path, normalized, out sprite);

            sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(assetPath);
            if (sprite == null)
            {
                var spriteName = Path.GetFileNameWithoutExtension(normalized);
                foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    if (asset is UnityEngine.Sprite candidate && candidate.name == spriteName)
                    {
                        sprite = candidate;
                        break;
                    }
                }
            }

            if (sprite == null) return false;
            CacheSpriteAliases(path, sprite);
            return true;
        }

        private bool TryLoadFrameworkSpriteByName(string originalPath, string spriteName, out UnityEngine.Sprite sprite)
        {
            sprite = null;
            spriteName = Path.GetFileNameWithoutExtension(spriteName);
            if (string.IsNullOrEmpty(spriteName)) return false;

            if (_frameworkSpriteByName.TryGetValue(spriteName, out sprite) && sprite != null)
            {
                CacheSpriteAliases(originalPath, sprite);
                return true;
            }

            var guids = AssetDatabase.FindAssets($"{spriteName} t:Sprite", new[] { "Assets/FrameworkResources" });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;

                sprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(assetPath);
                if (sprite != null && sprite.name == spriteName) break;

                sprite = null;
                foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    if (asset is UnityEngine.Sprite candidate && candidate.name == spriteName)
                    {
                        sprite = candidate;
                        break;
                    }
                }

                if (sprite != null) break;
            }

            if (sprite == null)
                sprite = ScanFrameworkSpriteByName(spriteName);

            if (sprite == null) return false;
            _frameworkSpriteByName[spriteName] = sprite;
            CacheSpriteAliases(originalPath, sprite);
            return true;
        }

        private static UnityEngine.Sprite ScanFrameworkSpriteByName(string spriteName)
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/FrameworkResources" });
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath)) continue;

                var mainSprite = AssetDatabase.LoadAssetAtPath<UnityEngine.Sprite>(assetPath);
                if (mainSprite != null && mainSprite.name == spriteName)
                    return mainSprite;

                foreach (var asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath))
                {
                    if (asset is UnityEngine.Sprite candidate && candidate.name == spriteName)
                        return candidate;
                }
            }

            return null;
        }

        private static string ResolveFrameworkSpritePath(string pathWithoutExtension)
        {
            if (File.Exists(pathWithoutExtension)) return pathWithoutExtension;

            string[] extensions = { ".png", ".jpg", ".jpeg", ".asset" };
            foreach (var ext in extensions)
            {
                var candidate = pathWithoutExtension + ext;
                if (File.Exists(candidate)) return candidate;
            }

            return null;
        }
#endif

#if !UNITY_EDITOR
        private static UnityEngine.Sprite LoadAddressableSpriteSync(string path)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<UnityEngine.Sprite>(path);
                return handle.WaitForCompletion();
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpriteService] Addressable Sprite load failed: {path}, {ex.Message}");
                return null;
            }
        }

        private void StartAddressableSpriteLoad(string path)
        {
            try
            {
                var handle = Addressables.LoadAssetAsync<UnityEngine.Sprite>(path);
                handle.Completed += op =>
                {
                    if (op.Status == AsyncOperationStatus.Succeeded && op.Result != null)
                    {
                        CacheSpriteAliases(path, op.Result);
                    }
                };
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SpriteService] Addressable Sprite async load failed: {path}, {ex.Message}");
            }
        }
#endif
    }
}
