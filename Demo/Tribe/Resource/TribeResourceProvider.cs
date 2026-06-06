using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Demo.Tribe.Resource
{
    /// <summary>
    /// Tribe-specific resource access.
    /// Editor uses Assets/FrameworkResources/Tribe directly; player builds use Addressables with the same address.
    /// </summary>
    public static class TribeResourceProvider
    {
        private const string FrameworkRoot = "Assets/FrameworkResources/";

        public static Sprite LoadSprite(string address)
        {
            var sprites = LoadAllSprites(address);
            return sprites.Length > 0 ? sprites[0] : null;
        }

        public static Sprite LoadSpriteVariant(string address, int preferredIndex)
        {
            var sprites = LoadAllSprites(address);
            if (sprites.Length == 0) return null;
            if (preferredIndex >= 0 && preferredIndex < sprites.Length) return sprites[preferredIndex];
            return sprites[0];
        }

        public static Sprite[] LoadAllSprites(string address)
        {
            var totalWatch = System.Diagnostics.Stopwatch.StartNew();
            address = NormalizeAddress(address);
            if (string.IsNullOrEmpty(address)) return Array.Empty<Sprite>();

#if UNITY_EDITOR
            var editorSprites = LoadAllSpritesFromFrameworkResources(address);
            if (editorSprites.Length > 0)
            {
                LogLoadTiming($"Editor.FrameworkResources/{address}", editorSprites.Length, totalWatch);
                return editorSprites;
            }
#endif

            try
            {
                var sample = System.Diagnostics.Stopwatch.StartNew();
                var handle = Addressables.LoadAssetsAsync<Sprite>(address, null);
                var loaded = handle.WaitForCompletion();
                if (loaded != null && loaded.Count > 0)
                {
                    LogLoadTiming($"Addressables.Label/{address}", loaded.Count, sample);
                    LogLoadTiming($"LoadAllSprites/{address}", loaded.Count, totalWatch);
                    return loaded.Where(s => s != null)
                        .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
                LogLoadTiming($"Addressables.Label.Empty/{address}", 0, sample);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TribeResourceProvider] Addressable sprite label load failed: {address}, {ex.Message}");
            }

            try
            {
                var sample = System.Diagnostics.Stopwatch.StartNew();
                var handle = Addressables.LoadAssetAsync<Sprite>(address);
                var sprite = handle.WaitForCompletion();
                var result = sprite != null ? new[] { sprite } : Array.Empty<Sprite>();
                LogLoadTiming($"Addressables.Asset/{address}", result.Length, sample);
                LogLoadTiming($"LoadAllSprites/{address}", result.Length, totalWatch);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TribeResourceProvider] Addressable sprite load failed: {address}, {ex.Message}");
                LogLoadTiming($"LoadAllSprites.Failed/{address}", 0, totalWatch);
                return Array.Empty<Sprite>();
            }
        }

        private static void LogLoadTiming(string label, int count, System.Diagnostics.Stopwatch stopwatch)
        {
            if (stopwatch == null) return;
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            var message = $"[TribeStartup] ResourceProvider.{label}: count={count}, {elapsed} ms";
            if (elapsed >= 16) Debug.LogWarning(message);
            else Debug.Log(message);
        }

        private static string NormalizeAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return string.Empty;
            var normalized = address.Replace('\\', '/').Trim('/');
            const string frameworkPrefix = "Assets/FrameworkResources/";
            if (normalized.StartsWith(frameworkPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(frameworkPrefix.Length);

            var dot = normalized.LastIndexOf('.');
            if (dot >= 0) normalized = normalized.Substring(0, dot);
            return normalized;
        }

#if UNITY_EDITOR
        private static Sprite[] LoadAllSpritesFromFrameworkResources(string address)
        {
            var sample = System.Diagnostics.Stopwatch.StartNew();
            var assetPath = FrameworkRoot + address;
            var sprites = new List<Sprite>();

            if (AssetDatabase.IsValidFolder(assetPath))
            {
                var guids = AssetDatabase.FindAssets("t:Sprite", new[] { assetPath });
                foreach (var guid in guids)
                    AddSpritesAtPath(AssetDatabase.GUIDToAssetPath(guid), sprites);
            }
            else
            {
                var resolvedPath = ResolveAssetPath(assetPath);
                if (!string.IsNullOrEmpty(resolvedPath))
                    AddSpritesAtPath(resolvedPath, sprites);
            }

            var result = sprites
                .Where(s => s != null)
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            LogLoadTiming($"AssetDatabase/{address}", result.Length, sample);
            return result;
        }

        private static string ResolveAssetPath(string pathWithoutExtension)
        {
            if (System.IO.File.Exists(pathWithoutExtension)) return pathWithoutExtension;

            string[] extensions = { ".png", ".jpg", ".jpeg", ".asset", ".prefab" };
            foreach (var ext in extensions)
            {
                var candidate = pathWithoutExtension + ext;
                if (System.IO.File.Exists(candidate)) return candidate;
            }

            return null;
        }

        private static void AddSpritesAtPath(string assetPath, List<Sprite> sprites)
        {
            if (string.IsNullOrEmpty(assetPath)) return;

            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (var asset in allAssets)
            {
                if (asset is Sprite sprite)
                    sprites.Add(sprite);
            }

            if (sprites.Count == 0)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite != null) sprites.Add(sprite);
            }
        }
#endif
    }
}
